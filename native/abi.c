#include "abi.h"
#include "upstream/gpi.h"
#include "upstream/vpi_user.h"
#include <stdlib.h>
#include "platform.h"
#include <stdio.h>
#include <limits.h>

_Static_assert(sizeof(int) == 4, "GPI int must be 32 bits");
_Static_assert(sizeof(long) <= 8, "GPI long must fit in int64_t");
_Static_assert(GPI_DEPOSIT == 0 && GPI_FORCE == 1 && GPI_RELEASE == 2 && GPI_NO_DELAY == 3, "Adapt GPI write actions");
_Static_assert(GPI_VALUE_CHANGE == 0 && GPI_RISING == 1 && GPI_FALLING == 2, "Adapt GPI edges");
_Static_assert(GPI_AUTO == 0 && GPI_OBJECTS == 1, "Adapt GPI discovery");
_Static_assert(GPI_RANGE_DOWN == -1 && GPI_RANGE_NO_DIR == 0 && GPI_RANGE_UP == 1, "Adapt GPI directions");
_Static_assert(GPI_UNKNOWN == 0 && GPI_MEMORY == 1 && GPI_MODULE == 2 && GPI_ARRAY == 6 &&
    GPI_ENUM == 7 && GPI_STRUCTURE == 8 && GPI_REAL == 9 && GPI_INTEGER == 10 && GPI_STRING == 11 &&
    GPI_FIXED_STRING == 12 && GPI_GENARRAY == 13 && GPI_PACKAGE == 14 && GPI_PACKED == 15 &&
    GPI_LOGIC == 16 && GPI_LOGIC_ARRAY == 17, "Adapt GPI object types");

static __typeof__(gpi_register_start_of_sim_time_callback) *p_gpi_register_start_of_sim_time_callback;
static __typeof__(gpi_register_timed_callback) *p_gpi_register_timed_callback;
static __typeof__(gpi_register_value_change_callback) *p_gpi_register_value_change_callback;
static __typeof__(gpi_register_readonly_callback) *p_gpi_register_readonly_callback;
static __typeof__(gpi_register_readwrite_callback) *p_gpi_register_readwrite_callback;
static __typeof__(gpi_register_nexttime_callback) *p_gpi_register_nexttime_callback;
static __typeof__(gpi_remove_cb) *p_gpi_remove_cb;
static __typeof__(gpi_get_root_handle) *p_gpi_get_root_handle;
static __typeof__(gpi_get_handle_by_name) *p_gpi_get_handle_by_name;
static __typeof__(gpi_get_handle_by_index) *p_gpi_get_handle_by_index;
static __typeof__(gpi_get_num_elems) *p_gpi_get_num_elems;
static __typeof__(gpi_get_range_left) *p_gpi_get_range_left;
static __typeof__(gpi_get_range_right) *p_gpi_get_range_right;
static __typeof__(gpi_get_object_type) *p_gpi_get_object_type;
static __typeof__(gpi_get_signal_name_str) *p_gpi_get_signal_name_str;
static __typeof__(gpi_get_signal_value_binstr) *p_gpi_get_signal_value_binstr;
static __typeof__(gpi_get_signal_value_real) *p_gpi_get_signal_value_real;
static __typeof__(gpi_get_signal_value_str) *p_gpi_get_signal_value_str;
static __typeof__(gpi_set_signal_value_binstr) *p_gpi_set_signal_value_binstr;
static __typeof__(gpi_set_signal_value_real) *p_gpi_set_signal_value_real;
static __typeof__(gpi_set_signal_value_str) *p_gpi_set_signal_value_str;
static __typeof__(gpi_iterate) *p_gpi_iterate;
static __typeof__(gpi_next) *p_gpi_next;
static __typeof__(gpi_get_sim_time) *p_gpi_get_sim_time;
static __typeof__(gpi_get_sim_precision) *p_gpi_get_sim_precision;
static __typeof__(gpi_finish) *p_gpi_finish;
static __typeof__(gpi_is_signed) *p_gpi_is_signed;
static __typeof__(gpi_get_simulator_product) *p_gpi_get_simulator_product;
static __typeof__(gpi_set_signal_value_int) *p_gpi_set_signal_value_int;
static __typeof__(gpi_get_range_dir) *p_gpi_get_range_dir;
static __typeof__(gpi_is_constant) *p_gpi_is_constant;
static __typeof__(gpi_get_signal_value_long) *p_gpi_get_signal_value_long;
static __typeof__(gpi_register_end_of_sim_time_callback) *p_gpi_register_end_of_sim_time_callback;
static __typeof__(gpi_get_signal_fullname_str) *p_gpi_get_signal_fullname_str;
static __typeof__(gpi_get_signal_type_str) *p_gpi_get_signal_type_str;
static __typeof__(gpi_get_definition_name) *p_gpi_get_definition_name;
static __typeof__(gpi_get_definition_file) *p_gpi_get_definition_file;

uint32_t cs_abi_version(void) { return 1; }
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
    p_gpi_register_start_of_sim_time_callback = (__typeof__(p_gpi_register_start_of_sim_time_callback))dlsym(gpi, "gpi_register_start_of_sim_time_callback");
    if (!p_gpi_register_start_of_sim_time_callback) return "missing GPI symbol: gpi_register_start_of_sim_time_callback";
    p_gpi_register_timed_callback = (__typeof__(p_gpi_register_timed_callback))dlsym(gpi, "gpi_register_timed_callback");
    if (!p_gpi_register_timed_callback) return "missing GPI symbol: gpi_register_timed_callback";
    p_gpi_register_value_change_callback = (__typeof__(p_gpi_register_value_change_callback))dlsym(gpi, "gpi_register_value_change_callback");
    if (!p_gpi_register_value_change_callback) return "missing GPI symbol: gpi_register_value_change_callback";
    p_gpi_register_readonly_callback = (__typeof__(p_gpi_register_readonly_callback))dlsym(gpi, "gpi_register_readonly_callback");
    if (!p_gpi_register_readonly_callback) return "missing GPI symbol: gpi_register_readonly_callback";
    p_gpi_register_readwrite_callback = (__typeof__(p_gpi_register_readwrite_callback))dlsym(gpi, "gpi_register_readwrite_callback");
    if (!p_gpi_register_readwrite_callback) return "missing GPI symbol: gpi_register_readwrite_callback";
    p_gpi_register_nexttime_callback = (__typeof__(p_gpi_register_nexttime_callback))dlsym(gpi, "gpi_register_nexttime_callback");
    if (!p_gpi_register_nexttime_callback) return "missing GPI symbol: gpi_register_nexttime_callback";
    p_gpi_remove_cb = (__typeof__(p_gpi_remove_cb))dlsym(gpi, "gpi_remove_cb");
    if (!p_gpi_remove_cb) return "missing GPI symbol: gpi_remove_cb";
    p_gpi_get_root_handle = (__typeof__(p_gpi_get_root_handle))dlsym(gpi, "gpi_get_root_handle");
    if (!p_gpi_get_root_handle) return "missing GPI symbol: gpi_get_root_handle";
    p_gpi_get_handle_by_name = (__typeof__(p_gpi_get_handle_by_name))dlsym(gpi, "gpi_get_handle_by_name");
    if (!p_gpi_get_handle_by_name) return "missing GPI symbol: gpi_get_handle_by_name";
    p_gpi_get_handle_by_index = (__typeof__(p_gpi_get_handle_by_index))dlsym(gpi, "gpi_get_handle_by_index");
    if (!p_gpi_get_handle_by_index) return "missing GPI symbol: gpi_get_handle_by_index";
    p_gpi_get_num_elems = (__typeof__(p_gpi_get_num_elems))dlsym(gpi, "gpi_get_num_elems");
    if (!p_gpi_get_num_elems) return "missing GPI symbol: gpi_get_num_elems";
    p_gpi_get_range_left = (__typeof__(p_gpi_get_range_left))dlsym(gpi, "gpi_get_range_left");
    if (!p_gpi_get_range_left) return "missing GPI symbol: gpi_get_range_left";
    p_gpi_get_range_right = (__typeof__(p_gpi_get_range_right))dlsym(gpi, "gpi_get_range_right");
    if (!p_gpi_get_range_right) return "missing GPI symbol: gpi_get_range_right";
    p_gpi_get_object_type = (__typeof__(p_gpi_get_object_type))dlsym(gpi, "gpi_get_object_type");
    if (!p_gpi_get_object_type) return "missing GPI symbol: gpi_get_object_type";
    p_gpi_get_signal_name_str = (__typeof__(p_gpi_get_signal_name_str))dlsym(gpi, "gpi_get_signal_name_str");
    if (!p_gpi_get_signal_name_str) return "missing GPI symbol: gpi_get_signal_name_str";
    p_gpi_get_signal_value_binstr = (__typeof__(p_gpi_get_signal_value_binstr))dlsym(gpi, "gpi_get_signal_value_binstr");
    if (!p_gpi_get_signal_value_binstr) return "missing GPI symbol: gpi_get_signal_value_binstr";
    p_gpi_get_signal_value_real = (__typeof__(p_gpi_get_signal_value_real))dlsym(gpi, "gpi_get_signal_value_real");
    if (!p_gpi_get_signal_value_real) return "missing GPI symbol: gpi_get_signal_value_real";
    p_gpi_get_signal_value_str = (__typeof__(p_gpi_get_signal_value_str))dlsym(gpi, "gpi_get_signal_value_str");
    if (!p_gpi_get_signal_value_str) return "missing GPI symbol: gpi_get_signal_value_str";
    p_gpi_set_signal_value_binstr = (__typeof__(p_gpi_set_signal_value_binstr))dlsym(gpi, "gpi_set_signal_value_binstr");
    if (!p_gpi_set_signal_value_binstr) return "missing GPI symbol: gpi_set_signal_value_binstr";
    p_gpi_set_signal_value_real = (__typeof__(p_gpi_set_signal_value_real))dlsym(gpi, "gpi_set_signal_value_real");
    if (!p_gpi_set_signal_value_real) return "missing GPI symbol: gpi_set_signal_value_real";
    p_gpi_set_signal_value_str = (__typeof__(p_gpi_set_signal_value_str))dlsym(gpi, "gpi_set_signal_value_str");
    if (!p_gpi_set_signal_value_str) return "missing GPI symbol: gpi_set_signal_value_str";
    p_gpi_iterate = (__typeof__(p_gpi_iterate))dlsym(gpi, "gpi_iterate");
    if (!p_gpi_iterate) return "missing GPI symbol: gpi_iterate";
    p_gpi_next = (__typeof__(p_gpi_next))dlsym(gpi, "gpi_next");
    if (!p_gpi_next) return "missing GPI symbol: gpi_next";
    p_gpi_get_sim_time = (__typeof__(p_gpi_get_sim_time))dlsym(gpi, "gpi_get_sim_time");
    if (!p_gpi_get_sim_time) return "missing GPI symbol: gpi_get_sim_time";
    p_gpi_get_sim_precision = (__typeof__(p_gpi_get_sim_precision))dlsym(gpi, "gpi_get_sim_precision");
    if (!p_gpi_get_sim_precision) return "missing GPI symbol: gpi_get_sim_precision";
    p_gpi_finish = (__typeof__(p_gpi_finish))dlsym(gpi, "gpi_finish");
    if (!p_gpi_finish) return "missing GPI symbol: gpi_finish";
    p_gpi_is_signed = (__typeof__(p_gpi_is_signed))dlsym(gpi, "gpi_is_signed");
    if (!p_gpi_is_signed) return "missing GPI symbol: gpi_is_signed";
    p_gpi_get_simulator_product = (__typeof__(p_gpi_get_simulator_product))dlsym(gpi, "gpi_get_simulator_product");
    if (!p_gpi_get_simulator_product) return "missing GPI symbol: gpi_get_simulator_product";
    p_gpi_set_signal_value_int = (__typeof__(p_gpi_set_signal_value_int))dlsym(gpi, "gpi_set_signal_value_int");
    if (!p_gpi_set_signal_value_int) return "missing GPI symbol: gpi_set_signal_value_int";
    p_gpi_get_range_dir = (__typeof__(p_gpi_get_range_dir))dlsym(gpi, "gpi_get_range_dir");
    if (!p_gpi_get_range_dir) return "missing GPI symbol: gpi_get_range_dir";
    p_gpi_is_constant = (__typeof__(p_gpi_is_constant))dlsym(gpi, "gpi_is_constant");
    if (!p_gpi_is_constant) return "missing GPI symbol: gpi_is_constant";
    p_gpi_get_signal_value_long = (__typeof__(p_gpi_get_signal_value_long))dlsym(gpi, "gpi_get_signal_value_long");
    if (!p_gpi_get_signal_value_long) return "missing GPI symbol: gpi_get_signal_value_long";
    p_gpi_register_end_of_sim_time_callback = (__typeof__(p_gpi_register_end_of_sim_time_callback))dlsym(gpi, "gpi_register_end_of_sim_time_callback");
    if (!p_gpi_register_end_of_sim_time_callback) return "missing GPI symbol: gpi_register_end_of_sim_time_callback";
    p_gpi_get_signal_fullname_str = (__typeof__(p_gpi_get_signal_fullname_str))dlsym(gpi, "gpi_get_signal_fullname_str");
    if (!p_gpi_get_signal_fullname_str) return "missing GPI symbol: gpi_get_signal_fullname_str";
    p_gpi_get_signal_type_str = (__typeof__(p_gpi_get_signal_type_str))dlsym(gpi, "gpi_get_signal_type_str");
    if (!p_gpi_get_signal_type_str) return "missing GPI symbol: gpi_get_signal_type_str";
    p_gpi_get_definition_name = (__typeof__(p_gpi_get_definition_name))dlsym(gpi, "gpi_get_definition_name");
    if (!p_gpi_get_definition_name) return "missing GPI symbol: gpi_get_definition_name";
    p_gpi_get_definition_file = (__typeof__(p_gpi_get_definition_file))dlsym(gpi, "gpi_get_definition_file");
    if (!p_gpi_get_definition_file) return "missing GPI symbol: gpi_get_definition_file";
    bound = 1;
    return NULL;
}

void * cs_v1_gpi_register_start_of_sim_time_callback(cs_callback cb, void * data) { return p_gpi_register_start_of_sim_time_callback(cb, data); }
void * cs_v1_gpi_register_timed_callback(cs_callback cb, void * data, uint64_t delay) { return p_gpi_register_timed_callback(cb, data, delay); }
void * cs_v1_gpi_register_value_change_callback(cs_callback cb, void * data, void * signal, int32_t edge) { return p_gpi_register_value_change_callback(cb, data, signal, edge); }
void * cs_v1_gpi_register_readonly_callback(cs_callback cb, void * data) { return p_gpi_register_readonly_callback(cb, data); }
void * cs_v1_gpi_register_readwrite_callback(cs_callback cb, void * data) { return p_gpi_register_readwrite_callback(cb, data); }
void * cs_v1_gpi_register_nexttime_callback(cs_callback cb, void * data) { return p_gpi_register_nexttime_callback(cb, data); }
int32_t cs_v1_gpi_remove_cb(void * cb) { return p_gpi_remove_cb(cb); }
void * cs_v1_gpi_get_root_handle(const char * name) { return p_gpi_get_root_handle(name); }
void * cs_v1_gpi_get_handle_by_name(void * parent, const char * name, int32_t mode) { return p_gpi_get_handle_by_name(parent, name, mode); }
void * cs_v1_gpi_get_handle_by_index(void * parent, int32_t index) { return p_gpi_get_handle_by_index(parent, index); }
int32_t cs_v1_gpi_get_num_elems(void * signal) { return p_gpi_get_num_elems(signal); }
int32_t cs_v1_gpi_get_range_left(void * signal) { return p_gpi_get_range_left(signal); }
int32_t cs_v1_gpi_get_range_right(void * signal) { return p_gpi_get_range_right(signal); }
int32_t cs_v1_gpi_get_object_type(void * signal) { return p_gpi_get_object_type(signal); }
const char * cs_v1_gpi_get_signal_name_str(void * signal) { return p_gpi_get_signal_name_str(signal); }
const char * cs_v1_gpi_get_signal_value_binstr(void * signal) { return p_gpi_get_signal_value_binstr(signal); }
double cs_v1_gpi_get_signal_value_real(void * signal) { return p_gpi_get_signal_value_real(signal); }
const char * cs_v1_gpi_get_signal_value_str(void * signal) { return p_gpi_get_signal_value_str(signal); }
void cs_v1_gpi_set_signal_value_binstr(void * signal, const char * value, int32_t action) { p_gpi_set_signal_value_binstr(signal, value, action); }
void cs_v1_gpi_set_signal_value_real(void * signal, double value, int32_t action) { p_gpi_set_signal_value_real(signal, value, action); }
void cs_v1_gpi_set_signal_value_str(void * signal, const char * value, int32_t action) { p_gpi_set_signal_value_str(signal, value, action); }
void * cs_v1_gpi_iterate(void * parent, int32_t kind) { return p_gpi_iterate(parent, kind); }
void * cs_v1_gpi_next(void * iterator) { return p_gpi_next(iterator); }
void cs_v1_gpi_get_sim_time(uint32_t * hi, uint32_t * lo) { p_gpi_get_sim_time(hi, lo); }
void cs_v1_gpi_get_sim_precision(int32_t * precision) { p_gpi_get_sim_precision(precision); }
void cs_v1_gpi_finish(void) { p_gpi_finish(); }
int32_t cs_v1_gpi_is_signed(void * signal) { return p_gpi_is_signed(signal); }
const char * cs_v1_gpi_get_simulator_product(void) { return p_gpi_get_simulator_product(); }
void cs_v1_gpi_set_signal_value_int(void * signal, int32_t value, int32_t action) { p_gpi_set_signal_value_int(signal, value, action); }
int32_t cs_v1_gpi_get_range_dir(void * signal) { return p_gpi_get_range_dir(signal); }
int32_t cs_v1_gpi_is_constant(void * signal) { return p_gpi_is_constant(signal); }
int64_t cs_v1_gpi_get_signal_value_long(void * signal) { return p_gpi_get_signal_value_long(signal); }
void * cs_v1_gpi_register_end_of_sim_time_callback(cs_callback cb, void * data) { return p_gpi_register_end_of_sim_time_callback(cb, data); }
const char * cs_v1_gpi_get_signal_fullname_str(void * signal) { return p_gpi_get_signal_fullname_str(signal); }
const char * cs_v1_gpi_get_signal_type_str(void * signal) { return p_gpi_get_signal_type_str(signal); }
const char * cs_v1_gpi_get_definition_name(void * signal) { return p_gpi_get_definition_name(signal); }
const char * cs_v1_gpi_get_definition_file(void * signal) { return p_gpi_get_definition_file(signal); }

/* Packed extension: three little-endian byte planes (value, unknown, high-Z).
   The pinned GPI still uses binary strings; no simulator-private handles used. */
#include <stdlib.h>
#include <string.h>
uint64_t cs_abi_features(void) { return 3; }

/* Optional VPI metadata extension. No GPI C++ object layout assumptions.
 * Call only for a packed handle from a known VPI implementation.
 * Result: 0 success, 1 unavailable, 2 invalid arguments, 3 inconsistent width,
 * 4 insufficient capacity. On failure discard all output except count. */
int32_t cs_v1_vpi_packed_ranges(const char *name, uint32_t bits,
                              int32_t *pairs, uint32_t capacity, uint32_t *count) {
    if (!name || !pairs || !count || !bits || bits > 1048576 || !capacity || capacity > 64) return 2;
    *count = 0;
    __typeof__(&vpi_handle_by_name) by_name = dlsym(RTLD_DEFAULT, "vpi_handle_by_name");
    __typeof__(&vpi_iterate) iterate = dlsym(RTLD_DEFAULT, "vpi_iterate");
    __typeof__(&vpi_scan) scan = dlsym(RTLD_DEFAULT, "vpi_scan");
    __typeof__(&vpi_handle) handle = dlsym(RTLD_DEFAULT, "vpi_handle");
    __typeof__(&vpi_get_value) value = dlsym(RTLD_DEFAULT, "vpi_get_value");
    __typeof__(&vpi_get) get = dlsym(RTLD_DEFAULT, "vpi_get");
    __typeof__(&vpi_free_object) release = dlsym(RTLD_DEFAULT, "vpi_free_object");
    if (!by_name || !iterate || !scan || !handle || !value || !get || !release) return 1;
    vpiHandle object = by_name((PLI_BYTE8 *)name, NULL);
    if (!object) return 1;
    if (get(vpiSize, object) != (int32_t)bits) { release(object); return 3; }
    vpiHandle iterator = iterate(vpiRange, object);
    if (!iterator) { release(object); return 1; }
    uint64_t product = 1;
    int32_t status = 0;
    vpiHandle range;
    while ((range = scan(iterator)) != NULL) {
        vpiHandle left = handle(vpiLeftRange, range), right = handle(vpiRightRange, range);
        if (!left || !right) status = 1;
        else {
            s_vpi_value l = { .format = vpiIntVal }, r = { .format = vpiIntVal };
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
static int32_t plane_size(void *signal, uint32_t bits, const void *buffer, uint32_t length) {
    if (!signal || !buffer || bits == 0 || bits > 1048576) return 1;
    if (length != 3 * ((bits + 7) / 8)) return 1;
    if (p_gpi_get_num_elems(signal) != (int)bits) return 2;
    return 0;
}
int32_t cs_v1_read_planes(void *signal, uint32_t bits, uint8_t *buffer, uint32_t length) {
    int32_t error = plane_size(signal, bits, buffer, length);
    if (error) return error;
    const char *text = p_gpi_get_signal_value_binstr(signal);
    if (!text || strnlen(text, (size_t)bits + 1) != bits) return 5;
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
int32_t cs_v1_write_planes(void *signal, uint32_t bits, const uint8_t *buffer, uint32_t length, int32_t action) {
    int32_t error = plane_size(signal, bits, buffer, length);
    if (error) return error;
    if (action < 0 || action > 3) return 1;
    if (p_gpi_is_constant(signal)) return 6;
    uint32_t n = (bits + 7) / 8;
    for (uint32_t i = 0; i < n; i++) {
        if ((buffer[i] & buffer[n+i]) || (buffer[2*n+i] & ~buffer[n+i])) return 1;
    }
    if (bits % 8) {
        uint8_t unused = (uint8_t)(0xffu << (bits % 8));
        if ((buffer[n-1] | buffer[2*n-1] | buffer[3*n-1]) & unused) return 1;
    }
    char *text = malloc((size_t)bits + 1);
    if (!text) return 4;
    for (uint32_t i = 0; i < bits; i++) {
        uint32_t offset = i / 8;
        uint8_t mask = (uint8_t)(1u << (i % 8));
        text[bits-1-i] = buffer[2*n+offset] & mask ? 'Z' : buffer[n+offset] & mask ? 'X' : buffer[offset] & mask ? '1' : '0';
    }
    text[bits] = 0;
    p_gpi_set_signal_value_binstr(signal, text, action);
    free(text);
    return 0;
}
