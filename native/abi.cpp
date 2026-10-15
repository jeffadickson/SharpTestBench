/** @file abi.cpp
 * @brief Stable C ABI over the pinned simulator GPI implementation.
 *
 * All entry points run on the simulator thread after cs_bind_gpi succeeds.
 * Opaque object pointers are borrowed; this shim never dereferences GPI C++ objects.
 * Numeric ABI codes are frozen by the assertions below. The small forwarding
 * functions deliberately preserve GPI callback/setter behavior rather than adding
 * a second scheduler. Borrowed text must be copied before the next native call.
 */
#include "abi.h"
#include "upstream/gpi.h"
#include "upstream/vpi_user.h"
#include <stdlib.h>
#include "platform.h"
#include <stdio.h>
#include <limits.h>

static_assert(__cplusplus >= 202002L, "Build the simulator bridge as C++20 or later");
static_assert(sizeof(int) == 4, "GPI int must be 32 bits");
static_assert(sizeof(long) <= 8, "GPI long must fit in int64_t");
static_assert(GPI_DEPOSIT == 0 && GPI_FORCE == 1 && GPI_RELEASE == 2 && GPI_NO_DELAY == 3, "Adapt GPI write actions");
static_assert(GPI_VALUE_CHANGE == 0 && GPI_RISING == 1 && GPI_FALLING == 2, "Adapt GPI edges");
static_assert(GPI_AUTO == 0 && GPI_OBJECTS == 1, "Adapt GPI discovery");
static_assert(GPI_RANGE_DOWN == -1 && GPI_RANGE_NO_DIR == 0 && GPI_RANGE_UP == 1, "Adapt GPI directions");
static_assert(GPI_UNKNOWN == 0 && GPI_MEMORY == 1 && GPI_MODULE == 2 && GPI_ARRAY == 6 &&
    GPI_ENUM == 7 && GPI_STRUCTURE == 8 && GPI_REAL == 9 && GPI_INTEGER == 10 && GPI_STRING == 11 &&
    GPI_FIXED_STRING == 12 && GPI_GENARRAY == 13 && GPI_PACKAGE == 14 && GPI_PACKED == 15 &&
    GPI_LOGIC == 16 && GPI_LOGIC_ARRAY == 17, "Adapt GPI object types");

// Exact upstream function types avoid signature drift, including native long.
static decltype(&gpi_register_start_of_sim_time_callback) p_gpi_register_start_of_sim_time_callback;
static decltype(&gpi_register_timed_callback) p_gpi_register_timed_callback;
static decltype(&gpi_register_value_change_callback) p_gpi_register_value_change_callback;
static decltype(&gpi_register_readonly_callback) p_gpi_register_readonly_callback;
static decltype(&gpi_register_readwrite_callback) p_gpi_register_readwrite_callback;
static decltype(&gpi_register_nexttime_callback) p_gpi_register_nexttime_callback;
static decltype(&gpi_remove_cb) p_gpi_remove_cb;
static decltype(&gpi_get_root_handle) p_gpi_get_root_handle;
static decltype(&gpi_get_handle_by_name) p_gpi_get_handle_by_name;
static decltype(&gpi_get_handle_by_index) p_gpi_get_handle_by_index;
static decltype(&gpi_get_num_elems) p_gpi_get_num_elems;
static decltype(&gpi_get_range_left) p_gpi_get_range_left;
static decltype(&gpi_get_range_right) p_gpi_get_range_right;
static decltype(&gpi_get_object_type) p_gpi_get_object_type;
static decltype(&gpi_get_signal_name_str) p_gpi_get_signal_name_str;
static decltype(&gpi_get_signal_value_binstr) p_gpi_get_signal_value_binstr;
static decltype(&gpi_get_signal_value_real) p_gpi_get_signal_value_real;
static decltype(&gpi_get_signal_value_str) p_gpi_get_signal_value_str;
static decltype(&gpi_set_signal_value_binstr) p_gpi_set_signal_value_binstr;
static decltype(&gpi_set_signal_value_real) p_gpi_set_signal_value_real;
static decltype(&gpi_set_signal_value_str) p_gpi_set_signal_value_str;
static decltype(&gpi_iterate) p_gpi_iterate;
static decltype(&gpi_next) p_gpi_next;
static decltype(&gpi_get_sim_time) p_gpi_get_sim_time;
static decltype(&gpi_get_sim_precision) p_gpi_get_sim_precision;
static decltype(&gpi_finish) p_gpi_finish;
static decltype(&gpi_is_signed) p_gpi_is_signed;
static decltype(&gpi_get_simulator_product) p_gpi_get_simulator_product;
static decltype(&gpi_set_signal_value_int) p_gpi_set_signal_value_int;
static decltype(&gpi_get_range_dir) p_gpi_get_range_dir;
static decltype(&gpi_is_constant) p_gpi_is_constant;
static decltype(&gpi_get_signal_value_long) p_gpi_get_signal_value_long;
static decltype(&gpi_register_end_of_sim_time_callback) p_gpi_register_end_of_sim_time_callback;
static decltype(&gpi_get_signal_fullname_str) p_gpi_get_signal_fullname_str;
static decltype(&gpi_get_signal_type_str) p_gpi_get_signal_type_str;
static decltype(&gpi_get_definition_name) p_gpi_get_definition_name;
static decltype(&gpi_get_definition_file) p_gpi_get_definition_file;

/** @brief Return the fixed-width public ABI generation (not the upstream GPI version). */
uint32_t cs_abi_version(void) { return 1; }
/** @brief Bind every required GPI export; return null or a borrowed diagnostic.
 * @details Retry is permitted after failure; callers must not use partial bindings.
 * The module is deliberately never unloaded because simulator callbacks and managed
 * delegates may outlive individual tests. Successful binding is simulator-thread-only
 * and idempotent; this static state is not a concurrent initialization primitive.
 */
const char *cs_bind_gpi(void) {
    static void *gpi;
    static int bound;
    static char error[512];
    if (bound) return NULL;
    const char *gpi_path = getenv("CS_GPI_LIBRARY");
    if (!gpi_path) gpi_path = "libgpi.so";
#ifdef _WIN32
    const wchar_t *wide_gpi = _wgetenv(L"CS_GPI_LIBRARY");
    if (!gpi) gpi = (void *)LoadLibraryW(wide_gpi ? wide_gpi : L"gpi.dll");
    (void)gpi_path;
#else
    if (!gpi) gpi = dlopen(gpi_path, RTLD_NOW | RTLD_LOCAL);
#endif
    if (!gpi) { snprintf(error, sizeof error, "libgpi.so: %s", dlerror()); return error; }
    p_gpi_register_start_of_sim_time_callback = reinterpret_cast<decltype(p_gpi_register_start_of_sim_time_callback)>(dlsym(gpi, "gpi_register_start_of_sim_time_callback"));
    if (!p_gpi_register_start_of_sim_time_callback) return "missing GPI symbol: gpi_register_start_of_sim_time_callback";
    p_gpi_register_timed_callback = reinterpret_cast<decltype(p_gpi_register_timed_callback)>(dlsym(gpi, "gpi_register_timed_callback"));
    if (!p_gpi_register_timed_callback) return "missing GPI symbol: gpi_register_timed_callback";
    p_gpi_register_value_change_callback = reinterpret_cast<decltype(p_gpi_register_value_change_callback)>(dlsym(gpi, "gpi_register_value_change_callback"));
    if (!p_gpi_register_value_change_callback) return "missing GPI symbol: gpi_register_value_change_callback";
    p_gpi_register_readonly_callback = reinterpret_cast<decltype(p_gpi_register_readonly_callback)>(dlsym(gpi, "gpi_register_readonly_callback"));
    if (!p_gpi_register_readonly_callback) return "missing GPI symbol: gpi_register_readonly_callback";
    p_gpi_register_readwrite_callback = reinterpret_cast<decltype(p_gpi_register_readwrite_callback)>(dlsym(gpi, "gpi_register_readwrite_callback"));
    if (!p_gpi_register_readwrite_callback) return "missing GPI symbol: gpi_register_readwrite_callback";
    p_gpi_register_nexttime_callback = reinterpret_cast<decltype(p_gpi_register_nexttime_callback)>(dlsym(gpi, "gpi_register_nexttime_callback"));
    if (!p_gpi_register_nexttime_callback) return "missing GPI symbol: gpi_register_nexttime_callback";
    p_gpi_remove_cb = reinterpret_cast<decltype(p_gpi_remove_cb)>(dlsym(gpi, "gpi_remove_cb"));
    if (!p_gpi_remove_cb) return "missing GPI symbol: gpi_remove_cb";
    p_gpi_get_root_handle = reinterpret_cast<decltype(p_gpi_get_root_handle)>(dlsym(gpi, "gpi_get_root_handle"));
    if (!p_gpi_get_root_handle) return "missing GPI symbol: gpi_get_root_handle";
    p_gpi_get_handle_by_name = reinterpret_cast<decltype(p_gpi_get_handle_by_name)>(dlsym(gpi, "gpi_get_handle_by_name"));
    if (!p_gpi_get_handle_by_name) return "missing GPI symbol: gpi_get_handle_by_name";
    p_gpi_get_handle_by_index = reinterpret_cast<decltype(p_gpi_get_handle_by_index)>(dlsym(gpi, "gpi_get_handle_by_index"));
    if (!p_gpi_get_handle_by_index) return "missing GPI symbol: gpi_get_handle_by_index";
    p_gpi_get_num_elems = reinterpret_cast<decltype(p_gpi_get_num_elems)>(dlsym(gpi, "gpi_get_num_elems"));
    if (!p_gpi_get_num_elems) return "missing GPI symbol: gpi_get_num_elems";
    p_gpi_get_range_left = reinterpret_cast<decltype(p_gpi_get_range_left)>(dlsym(gpi, "gpi_get_range_left"));
    if (!p_gpi_get_range_left) return "missing GPI symbol: gpi_get_range_left";
    p_gpi_get_range_right = reinterpret_cast<decltype(p_gpi_get_range_right)>(dlsym(gpi, "gpi_get_range_right"));
    if (!p_gpi_get_range_right) return "missing GPI symbol: gpi_get_range_right";
    p_gpi_get_object_type = reinterpret_cast<decltype(p_gpi_get_object_type)>(dlsym(gpi, "gpi_get_object_type"));
    if (!p_gpi_get_object_type) return "missing GPI symbol: gpi_get_object_type";
    p_gpi_get_signal_name_str = reinterpret_cast<decltype(p_gpi_get_signal_name_str)>(dlsym(gpi, "gpi_get_signal_name_str"));
    if (!p_gpi_get_signal_name_str) return "missing GPI symbol: gpi_get_signal_name_str";
    p_gpi_get_signal_value_binstr = reinterpret_cast<decltype(p_gpi_get_signal_value_binstr)>(dlsym(gpi, "gpi_get_signal_value_binstr"));
    if (!p_gpi_get_signal_value_binstr) return "missing GPI symbol: gpi_get_signal_value_binstr";
    p_gpi_get_signal_value_real = reinterpret_cast<decltype(p_gpi_get_signal_value_real)>(dlsym(gpi, "gpi_get_signal_value_real"));
    if (!p_gpi_get_signal_value_real) return "missing GPI symbol: gpi_get_signal_value_real";
    p_gpi_get_signal_value_str = reinterpret_cast<decltype(p_gpi_get_signal_value_str)>(dlsym(gpi, "gpi_get_signal_value_str"));
    if (!p_gpi_get_signal_value_str) return "missing GPI symbol: gpi_get_signal_value_str";
    p_gpi_set_signal_value_binstr = reinterpret_cast<decltype(p_gpi_set_signal_value_binstr)>(dlsym(gpi, "gpi_set_signal_value_binstr"));
    if (!p_gpi_set_signal_value_binstr) return "missing GPI symbol: gpi_set_signal_value_binstr";
    p_gpi_set_signal_value_real = reinterpret_cast<decltype(p_gpi_set_signal_value_real)>(dlsym(gpi, "gpi_set_signal_value_real"));
    if (!p_gpi_set_signal_value_real) return "missing GPI symbol: gpi_set_signal_value_real";
    p_gpi_set_signal_value_str = reinterpret_cast<decltype(p_gpi_set_signal_value_str)>(dlsym(gpi, "gpi_set_signal_value_str"));
    if (!p_gpi_set_signal_value_str) return "missing GPI symbol: gpi_set_signal_value_str";
    p_gpi_iterate = reinterpret_cast<decltype(p_gpi_iterate)>(dlsym(gpi, "gpi_iterate"));
    if (!p_gpi_iterate) return "missing GPI symbol: gpi_iterate";
    p_gpi_next = reinterpret_cast<decltype(p_gpi_next)>(dlsym(gpi, "gpi_next"));
    if (!p_gpi_next) return "missing GPI symbol: gpi_next";
    p_gpi_get_sim_time = reinterpret_cast<decltype(p_gpi_get_sim_time)>(dlsym(gpi, "gpi_get_sim_time"));
    if (!p_gpi_get_sim_time) return "missing GPI symbol: gpi_get_sim_time";
    p_gpi_get_sim_precision = reinterpret_cast<decltype(p_gpi_get_sim_precision)>(dlsym(gpi, "gpi_get_sim_precision"));
    if (!p_gpi_get_sim_precision) return "missing GPI symbol: gpi_get_sim_precision";
    p_gpi_finish = reinterpret_cast<decltype(p_gpi_finish)>(dlsym(gpi, "gpi_finish"));
    if (!p_gpi_finish) return "missing GPI symbol: gpi_finish";
    p_gpi_is_signed = reinterpret_cast<decltype(p_gpi_is_signed)>(dlsym(gpi, "gpi_is_signed"));
    if (!p_gpi_is_signed) return "missing GPI symbol: gpi_is_signed";
    p_gpi_get_simulator_product = reinterpret_cast<decltype(p_gpi_get_simulator_product)>(dlsym(gpi, "gpi_get_simulator_product"));
    if (!p_gpi_get_simulator_product) return "missing GPI symbol: gpi_get_simulator_product";
    p_gpi_set_signal_value_int = reinterpret_cast<decltype(p_gpi_set_signal_value_int)>(dlsym(gpi, "gpi_set_signal_value_int"));
    if (!p_gpi_set_signal_value_int) return "missing GPI symbol: gpi_set_signal_value_int";
    p_gpi_get_range_dir = reinterpret_cast<decltype(p_gpi_get_range_dir)>(dlsym(gpi, "gpi_get_range_dir"));
    if (!p_gpi_get_range_dir) return "missing GPI symbol: gpi_get_range_dir";
    p_gpi_is_constant = reinterpret_cast<decltype(p_gpi_is_constant)>(dlsym(gpi, "gpi_is_constant"));
    if (!p_gpi_is_constant) return "missing GPI symbol: gpi_is_constant";
    p_gpi_get_signal_value_long = reinterpret_cast<decltype(p_gpi_get_signal_value_long)>(dlsym(gpi, "gpi_get_signal_value_long"));
    if (!p_gpi_get_signal_value_long) return "missing GPI symbol: gpi_get_signal_value_long";
    p_gpi_register_end_of_sim_time_callback = reinterpret_cast<decltype(p_gpi_register_end_of_sim_time_callback)>(dlsym(gpi, "gpi_register_end_of_sim_time_callback"));
    if (!p_gpi_register_end_of_sim_time_callback) return "missing GPI symbol: gpi_register_end_of_sim_time_callback";
    p_gpi_get_signal_fullname_str = reinterpret_cast<decltype(p_gpi_get_signal_fullname_str)>(dlsym(gpi, "gpi_get_signal_fullname_str"));
    if (!p_gpi_get_signal_fullname_str) return "missing GPI symbol: gpi_get_signal_fullname_str";
    p_gpi_get_signal_type_str = reinterpret_cast<decltype(p_gpi_get_signal_type_str)>(dlsym(gpi, "gpi_get_signal_type_str"));
    if (!p_gpi_get_signal_type_str) return "missing GPI symbol: gpi_get_signal_type_str";
    p_gpi_get_definition_name = reinterpret_cast<decltype(p_gpi_get_definition_name)>(dlsym(gpi, "gpi_get_definition_name"));
    if (!p_gpi_get_definition_name) return "missing GPI symbol: gpi_get_definition_name";
    p_gpi_get_definition_file = reinterpret_cast<decltype(p_gpi_get_definition_file)>(dlsym(gpi, "gpi_get_definition_file"));
    if (!p_gpi_get_definition_file) return "missing GPI symbol: gpi_get_definition_file";
    bound = 1;
    return NULL;
}

/** @brief Register a callback for simulation startup; return an opaque callback handle or null on failure. Caller retains callback/data lifetime. */
void * cs_v1_gpi_register_start_of_sim_time_callback(cs_callback cb, void * data) { return p_gpi_register_start_of_sim_time_callback(cb, data); }
/** @brief Register a callback for delay simulator steps; return an opaque callback handle or null on failure. Caller retains callback/data lifetime. */
void * cs_v1_gpi_register_timed_callback(cs_callback cb, void * data, uint64_t delay) { return p_gpi_register_timed_callback(cb, data, delay); }
/** @brief Register a callback for the requested edge on signal; return an opaque callback handle or null on failure. Caller retains callback/data lifetime. */
void * cs_v1_gpi_register_value_change_callback(cs_callback cb, void * data, void * signal, int32_t edge) { return p_gpi_register_value_change_callback(cb, data, static_cast<gpi_sim_hdl>(signal), static_cast<gpi_edge>(edge)); }
/** @brief Register a callback for the read-only phase; return an opaque callback handle or null on failure. Caller retains callback/data lifetime. */
void * cs_v1_gpi_register_readonly_callback(cs_callback cb, void * data) { return p_gpi_register_readonly_callback(cb, data); }
/** @brief Register a callback for the read/write phase; return an opaque callback handle or null on failure. Caller retains callback/data lifetime. */
void * cs_v1_gpi_register_readwrite_callback(cs_callback cb, void * data) { return p_gpi_register_readwrite_callback(cb, data); }
/** @brief Register a callback for the next time step; return an opaque callback handle or null on failure. Caller retains callback/data lifetime. */
void * cs_v1_gpi_register_nexttime_callback(cs_callback cb, void * data) { return p_gpi_register_nexttime_callback(cb, data); }
/** @brief Remove a registered callback using upstream lifetime rules; forward GPI status without freeing caller data. */
int32_t cs_v1_gpi_remove_cb(void * cb) { return p_gpi_remove_cb(static_cast<gpi_cb_hdl>(cb)); }
/** @brief Find a root by name; return a borrowed handle or null. */
void * cs_v1_gpi_get_root_handle(const char * name) { return p_gpi_get_root_handle(name); }
/** @brief Find a named child with the frozen discovery mode; return a borrowed handle or null. */
void * cs_v1_gpi_get_handle_by_name(void * parent, const char * name, int32_t mode) { return p_gpi_get_handle_by_name(static_cast<gpi_sim_hdl>(parent), name, static_cast<gpi_discovery>(mode)); }
/** @brief Find an indexed child by its HDL index; return a borrowed handle or null. */
void * cs_v1_gpi_get_handle_by_index(void * parent, int32_t index) { return p_gpi_get_handle_by_index(static_cast<gpi_sim_hdl>(parent), index); }
/** @brief Return the backend element count of a live object. */
int32_t cs_v1_gpi_get_num_elems(void * signal) { return p_gpi_get_num_elems(static_cast<gpi_sim_hdl>(signal)); }
/** @brief Return the declared left bound of a live indexed object. */
int32_t cs_v1_gpi_get_range_left(void * signal) { return p_gpi_get_range_left(static_cast<gpi_sim_hdl>(signal)); }
/** @brief Return the declared inclusive right bound of a live indexed object. */
int32_t cs_v1_gpi_get_range_right(void * signal) { return p_gpi_get_range_right(static_cast<gpi_sim_hdl>(signal)); }
/** @brief Return the frozen GPI object-kind code for a live object. */
int32_t cs_v1_gpi_get_object_type(void * signal) { return p_gpi_get_object_type(static_cast<gpi_sim_hdl>(signal)); }
/** @brief Return the borrowed local object name; copy before another native call. */
const char * cs_v1_gpi_get_signal_name_str(void * signal) { return p_gpi_get_signal_name_str(static_cast<gpi_sim_hdl>(signal)); }
/** @brief Return borrowed displayed MSB-first logic text; preserves backend logic symbols. */
const char * cs_v1_gpi_get_signal_value_binstr(void * signal) { return p_gpi_get_signal_value_binstr(static_cast<gpi_sim_hdl>(signal)); }
/** @brief Read a backend real value without fast-math or integer conversion. */
double cs_v1_gpi_get_signal_value_real(void * signal) { return p_gpi_get_signal_value_real(static_cast<gpi_sim_hdl>(signal)); }
/** @brief Return borrowed backend string bytes; managed code applies the object-specific encoding contract. */
const char * cs_v1_gpi_get_signal_value_str(void * signal) { return p_gpi_get_signal_value_str(static_cast<gpi_sim_hdl>(signal)); }
/** @brief Submit NUL-terminated logic text using the frozen write-action code; GPI consumes the input during this call. */
void cs_v1_gpi_set_signal_value_binstr(void * signal, const char * value, int32_t action) { p_gpi_set_signal_value_binstr(static_cast<gpi_sim_hdl>(signal), value, static_cast<gpi_set_action>(action)); }
/** @brief Submit a real value using the frozen write-action code. */
void cs_v1_gpi_set_signal_value_real(void * signal, double value, int32_t action) { p_gpi_set_signal_value_real(static_cast<gpi_sim_hdl>(signal), value, static_cast<gpi_set_action>(action)); }
/** @brief Submit NUL-terminated string bytes using the frozen write-action code. */
void cs_v1_gpi_set_signal_value_str(void * signal, const char * value, int32_t action) { p_gpi_set_signal_value_str(static_cast<gpi_sim_hdl>(signal), value, static_cast<gpi_set_action>(action)); }
/** @brief Start backend child/connectivity iteration; null means unsupported. An iterator may be empty. */
void * cs_v1_gpi_iterate(void * parent, int32_t kind) { return p_gpi_iterate(static_cast<gpi_sim_hdl>(parent), static_cast<gpi_iterator_sel>(kind)); }
/** @brief Advance an iterator until null; exhaustion follows GPI ownership rules, not a managed free. */
void * cs_v1_gpi_next(void * iterator) { return p_gpi_next(static_cast<gpi_iterator_hdl>(iterator)); }
/** @brief Write high/low halves of the unsigned simulator-step time into nonnull outputs. */
void cs_v1_gpi_get_sim_time(uint32_t * hi, uint32_t * lo) { p_gpi_get_sim_time(hi, lo); }
/** @brief Write the base-10 seconds-per-step exponent into a nonnull output. */
void cs_v1_gpi_get_sim_precision(int32_t * precision) { p_gpi_get_sim_precision(precision); }
/** @brief Request simulator termination; must be called on the simulator thread. */
void cs_v1_gpi_finish(void) { p_gpi_finish(); }
/** @brief Return the backend signedness flag; no value conversion occurs. */
int32_t cs_v1_gpi_is_signed(void * signal) { return p_gpi_is_signed(static_cast<gpi_sim_hdl>(signal)); }
/** @brief Return the borrowed backend product name used by capability policy. */
const char * cs_v1_gpi_get_simulator_product(void) { return p_gpi_get_simulator_product(); }
/** @brief Submit a signed 32-bit value using the frozen write-action code. */
void cs_v1_gpi_set_signal_value_int(void * signal, int32_t value, int32_t action) { p_gpi_set_signal_value_int(static_cast<gpi_sim_hdl>(signal), value, static_cast<gpi_set_action>(action)); }
/** @brief Return frozen direction code: -1 descending, 0 none, 1 ascending. */
int32_t cs_v1_gpi_get_range_dir(void * signal) { return p_gpi_get_range_dir(static_cast<gpi_sim_hdl>(signal)); }
/** @brief Return the backend constant flag used by write guards. */
int32_t cs_v1_gpi_is_constant(void * signal) { return p_gpi_is_constant(static_cast<gpi_sim_hdl>(signal)); }
/** @brief Widen the platform-native GPI long to signed int64_t, preserving negative values. */
int64_t cs_v1_gpi_get_signal_value_long(void * signal) { return p_gpi_get_signal_value_long(static_cast<gpi_sim_hdl>(signal)); }
/** @brief Register a callback for simulation shutdown; return an opaque callback handle or null on failure. Caller retains callback/data lifetime. */
void * cs_v1_gpi_register_end_of_sim_time_callback(cs_callback cb, void * data) { return p_gpi_register_end_of_sim_time_callback(cb, data); }
/** @brief Return the borrowed simulator-qualified object name; separators are backend-specific. */
const char * cs_v1_gpi_get_signal_fullname_str(void * signal) { return p_gpi_get_signal_fullname_str(static_cast<gpi_sim_hdl>(signal)); }
/** @brief Return the borrowed backend type description, not a portable type identifier. */
const char * cs_v1_gpi_get_signal_type_str(void * signal) { return p_gpi_get_signal_type_str(static_cast<gpi_sim_hdl>(signal)); }
/** @brief Return a borrowed HDL definition name, or null when unavailable. */
const char * cs_v1_gpi_get_definition_name(void * signal) { return p_gpi_get_definition_name(static_cast<gpi_sim_hdl>(signal)); }
/** @brief Return a borrowed source filename, or null when unavailable. */
const char * cs_v1_gpi_get_definition_file(void * signal) { return p_gpi_get_definition_file(static_cast<gpi_sim_hdl>(signal)); }

/* Packed extension: three little-endian byte planes (value, unknown, high-Z).
   The pinned GPI still uses binary strings; no simulator-private handles used. */
#include <stdlib.h>
#include <string.h>
/** @brief Advertise packed planes (bit 0) and optional VPI packed metadata (bit 1). */
uint64_t cs_abi_features(void) { return 3; }

/** @brief Discover optional VPI packed-range metadata. No GPI C++ object layout assumptions.
 * Call only for a packed handle from a known VPI implementation.
 * Result: 0 success, 1 unavailable, 2 invalid arguments, 3 inconsistent width,
 * 4 insufficient capacity. On failure discard pairs; count is defined only after argument validation. */
int32_t cs_v1_vpi_packed_ranges(const char *name, uint32_t bits,
                              int32_t *pairs, uint32_t capacity, uint32_t *count) {
    if (!name || !pairs || !count || !bits || bits > 1048576 || !capacity || capacity > 64) return 2;
    *count = 0;
    auto by_name = reinterpret_cast<decltype(&vpi_handle_by_name)>(dlsym(RTLD_DEFAULT, "vpi_handle_by_name"));
    auto iterate = reinterpret_cast<decltype(&vpi_iterate)>(dlsym(RTLD_DEFAULT, "vpi_iterate"));
    auto scan = reinterpret_cast<decltype(&vpi_scan)>(dlsym(RTLD_DEFAULT, "vpi_scan"));
    auto handle = reinterpret_cast<decltype(&vpi_handle)>(dlsym(RTLD_DEFAULT, "vpi_handle"));
    auto value = reinterpret_cast<decltype(&vpi_get_value)>(dlsym(RTLD_DEFAULT, "vpi_get_value"));
    auto get = reinterpret_cast<decltype(&vpi_get)>(dlsym(RTLD_DEFAULT, "vpi_get"));
    auto release = reinterpret_cast<decltype(&vpi_free_object)>(dlsym(RTLD_DEFAULT, "vpi_free_object"));
    if (!by_name || !iterate || !scan || !handle || !value || !get || !release) return 1;
    vpiHandle object = by_name((PLI_BYTE8 *)name, NULL);
    if (!object) return 1;
    if (get(vpiSize, object) != (int32_t)bits) { release(object); return 3; }
    vpiHandle iterator = iterate(vpiRange, object);
    if (!iterator) { release(object); return 1; }
    uint64_t product = 1;
    int32_t status = 0;
    vpiHandle range;
    // Exhaust scan even after an error: VPI releases an exhausted iterator itself.
    while ((range = scan(iterator)) != NULL) {
        vpiHandle left = handle(vpiLeftRange, range), right = handle(vpiRightRange, range);
        if (!left || !right) status = 1;
        else {
            s_vpi_value l{}, r{};
            l.format = r.format = vpiIntVal;
            value(left, &l); value(right, &r);
            int64_t span = (int64_t)l.value.integer - r.value.integer;
            uint64_t length = (uint64_t)(span < 0 ? -span : span) + 1;
            if (length > bits || product > bits / length) status = 3;
            else product *= length;
            if (*count < capacity) {
                pairs[2 * *count] = l.value.integer;
                pairs[2 * *count + 1] = r.value.integer;
            } else status = 4;
        }
        // Release only VPI-owned metadata handles, never borrowed GPI handles.
        if (left) release(left);
        if (right) release(right);
        release(range);
        ++*count;
    }
    release(object);
    if (!*count) return 1;
    if (status) return status;
    return product == bits ? 0 : 3;
}
/** @brief Validate a caller-owned three-plane buffer and its simulator width.
 * @return 0 valid, 1 invalid arguments/length, 2 simulator width mismatch.
 * @details Width is capped before byte-count arithmetic; buffer capacity must be
 * exactly three ceil(bits/8) planes, and signal must be a live borrowed GPI handle.
 */
static int32_t plane_size(void *signal, uint32_t bits, const void *buffer, uint32_t length) {
    if (!signal || !buffer || bits == 0 || bits > 1048576) return 1;
    if (length != 3 * ((bits + 7) / 8)) return 1;
    if (p_gpi_get_num_elems(static_cast<gpi_sim_hdl>(signal)) != (int)bits) return 2;
    return 0;
}
/** @brief Encode GPI text as LSB-first value/unknown/high-Z byte planes.
 * @return 0 success, 1 invalid buffer, 2 width mismatch, 3 unsupported state,
 * 5 null/malformed GPI text. Discard the entire output on any failure.
 * @details A read may partly fill the caller's buffer before encountering a
 * nine-state value not representable in four-state planes; no simulator state changes.
 */
int32_t cs_v1_read_planes(void *signal, uint32_t bits, uint8_t *buffer, uint32_t length) {
    int32_t error = plane_size(signal, bits, buffer, length);
    if (error) return error;
    const char *text = p_gpi_get_signal_value_binstr(static_cast<gpi_sim_hdl>(signal));
    if (!text) return 5;
    // A bounded scan rejects truncated/oversized transport without unbounded strlen.
    size_t text_length = 0;
    while (text_length <= bits && text[text_length]) ++text_length;
    if (text_length != bits) return 5;
    memset(buffer, 0, length);
    uint32_t n = (bits + 7) / 8;
    for (uint32_t i = 0; i < bits; i++) {
        uint32_t offset = i / 8;
        uint8_t mask = (uint8_t)(1u << (i % 8));
        switch (text[bits - 1 - i]) {
            case '0': break;
            case '1': buffer[offset] |= mask; break;
            case 'x': case 'X': buffer[n + offset] |= mask; break;
            case 'z': case 'Z': buffer[n + offset] |= mask; buffer[2*n + offset] |= mask; break;
            default: return 3;
        }
    }
    return 0;
}
/** @brief Validate planes completely, then issue one GPI string write.
 * @return 0 submitted, 1 invalid planes/action, 2 width mismatch, 4 allocation
 * failure, 6 constant object. GPI setters cannot report backend errors.
 * @details Unknown/high-Z bits cannot carry known value bits. Padding must be zero.
 * The temporary string is owned here and freed after GPI consumes it synchronously.
 */
int32_t cs_v1_write_planes(void *signal, uint32_t bits, const uint8_t *buffer, uint32_t length, int32_t action) {
    int32_t error = plane_size(signal, bits, buffer, length);
    if (error) return error;
    if (action < 0 || action > 3) return 1;
    if (p_gpi_is_constant(static_cast<gpi_sim_hdl>(signal))) return 6;
    uint32_t n = (bits + 7) / 8;
    // Reject noncanonical planes before allocation or any simulator side effect.
    for (uint32_t i = 0; i < n; i++) {
        if ((buffer[i] & buffer[n+i]) || (buffer[2*n+i] & ~buffer[n+i])) return 1;
    }
    if (bits % 8) {
        uint8_t unused = (uint8_t)(0xffu << (bits % 8));
        if ((buffer[n-1] | buffer[2*n-1] | buffer[3*n-1]) & unused) return 1;
    }
    char *text = static_cast<char *>(malloc((size_t)bits + 1));
    if (!text) return 4;
    for (uint32_t i = 0; i < bits; i++) {
        uint32_t offset = i / 8;
        uint8_t mask = (uint8_t)(1u << (i % 8));
        text[bits-1-i] = buffer[2*n+offset] & mask ? 'Z' : buffer[n+offset] & mask ? 'X' : buffer[offset] & mask ? '1' : '0';
    }
    text[bits] = 0;
    p_gpi_set_signal_value_binstr(static_cast<gpi_sim_hdl>(signal), text, static_cast<gpi_set_action>(action));
    free(text);
    return 0;
}
