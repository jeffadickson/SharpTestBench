/** @file abi.h
 * @brief Public, unmangled C ABI v1 for managed-to-simulator communication.
 *
 * All functions require the simulator thread. Call cs_bind_gpi successfully before
 * cs_v1_* operations. GPI object handles and strings are borrowed, never freed by
 * managed code; copy returned text before the next native call. Callback data is
 * caller-owned and must remain alive until callback completion/removal; callbacks
 * must not throw across the C ABI. Callback handles obey upstream one-shot/removal
 * lifetime rules. Setters submit to GPI and cannot report backend failures.
 * Buffers use explicit byte lengths; caller owns all input/output memory.
 * See docs/NATIVE_ABI.md and docs/PACKED_TRANSPORT.md for status and lifetime details.
 */
#ifndef CS_ABI_H
#define CS_ABI_H
#include <stdint.h>
#if defined(_WIN32)
# if defined(STB_HOST_BUILD)
#  define CS_EXPORT __declspec(dllexport)
# else
#  define CS_EXPORT __declspec(dllimport)
# endif
#else
# define CS_EXPORT __attribute__((visibility("default")))
#endif
#ifdef __cplusplus
extern "C" {
#endif

/** @brief Query optional VPI packed bounds into 2*capacity endpoints. Status 0 success, 1 unavailable, 2 arguments, 3 width, 4 capacity; discard pairs on failure. */
CS_EXPORT int32_t cs_v1_vpi_packed_ranges(const char *name, uint32_t bits,
                              int32_t *pairs, uint32_t capacity, uint32_t *count);
/** @brief C-calling-convention callback receiving unchanged caller-owned data. */
typedef int32_t (*cs_callback)(void *);
/** @brief Return public ABI generation 1; safe before binding. */
CS_EXPORT uint32_t cs_abi_version(void);
/** @brief Bind every required pinned GPI symbol; return null or a borrowed diagnostic. Retains the module for process lifetime. */
CS_EXPORT const char *cs_bind_gpi(void);
/** @brief Register a callback for simulation startup; return an opaque callback handle or null on failure. Caller retains callback/data lifetime. */
CS_EXPORT void * cs_v1_gpi_register_start_of_sim_time_callback(cs_callback cb, void * data);
/** @brief Register a callback for delay simulator steps; return an opaque callback handle or null on failure. Caller retains callback/data lifetime. */
CS_EXPORT void * cs_v1_gpi_register_timed_callback(cs_callback cb, void * data, uint64_t delay);
/** @brief Register a callback for the requested edge on signal; return an opaque callback handle or null on failure. Caller retains callback/data lifetime. */
CS_EXPORT void * cs_v1_gpi_register_value_change_callback(cs_callback cb, void * data, void * signal, int32_t edge);
/** @brief Register a callback for the read-only phase; return an opaque callback handle or null on failure. Caller retains callback/data lifetime. */
CS_EXPORT void * cs_v1_gpi_register_readonly_callback(cs_callback cb, void * data);
/** @brief Register a callback for the read/write phase; return an opaque callback handle or null on failure. Caller retains callback/data lifetime. */
CS_EXPORT void * cs_v1_gpi_register_readwrite_callback(cs_callback cb, void * data);
/** @brief Register a callback for the next time step; return an opaque callback handle or null on failure. Caller retains callback/data lifetime. */
CS_EXPORT void * cs_v1_gpi_register_nexttime_callback(cs_callback cb, void * data);
/** @brief Remove a registered callback using upstream lifetime rules; forward GPI status without freeing caller data. */
CS_EXPORT int32_t cs_v1_gpi_remove_cb(void * cb);
/** @brief Find a root by name; return a borrowed handle or null. */
CS_EXPORT void * cs_v1_gpi_get_root_handle(const char * name);
/** @brief Find a named child with the frozen discovery mode; return a borrowed handle or null. */
CS_EXPORT void * cs_v1_gpi_get_handle_by_name(void * parent, const char * name, int32_t mode);
/** @brief Find an indexed child by its HDL index; return a borrowed handle or null. */
CS_EXPORT void * cs_v1_gpi_get_handle_by_index(void * parent, int32_t index);
/** @brief Return the backend element count of a live object. */
CS_EXPORT int32_t cs_v1_gpi_get_num_elems(void * signal);
/** @brief Return the declared left bound of a live indexed object. */
CS_EXPORT int32_t cs_v1_gpi_get_range_left(void * signal);
/** @brief Return the declared inclusive right bound of a live indexed object. */
CS_EXPORT int32_t cs_v1_gpi_get_range_right(void * signal);
/** @brief Return the frozen GPI object-kind code for a live object. */
CS_EXPORT int32_t cs_v1_gpi_get_object_type(void * signal);
/** @brief Return the borrowed local object name; copy before another native call. */
CS_EXPORT const char * cs_v1_gpi_get_signal_name_str(void * signal);
/** @brief Return borrowed displayed MSB-first logic text; preserves backend logic symbols. */
CS_EXPORT const char * cs_v1_gpi_get_signal_value_binstr(void * signal);
/** @brief Read a backend real value without fast-math or integer conversion. */
CS_EXPORT double cs_v1_gpi_get_signal_value_real(void * signal);
/** @brief Return borrowed backend string bytes; managed code applies the object-specific encoding contract. */
CS_EXPORT const char * cs_v1_gpi_get_signal_value_str(void * signal);
/** @brief Submit NUL-terminated logic text using the frozen write-action code; GPI consumes the input during this call. */
CS_EXPORT void cs_v1_gpi_set_signal_value_binstr(void * signal, const char * value, int32_t action);
/** @brief Submit a real value using the frozen write-action code. */
CS_EXPORT void cs_v1_gpi_set_signal_value_real(void * signal, double value, int32_t action);
/** @brief Submit NUL-terminated string bytes using the frozen write-action code. */
CS_EXPORT void cs_v1_gpi_set_signal_value_str(void * signal, const char * value, int32_t action);
/** @brief Start backend child/connectivity iteration; null means unsupported. An iterator may be empty. */
CS_EXPORT void * cs_v1_gpi_iterate(void * parent, int32_t kind);
/** @brief Advance an iterator until null; exhaustion follows GPI ownership rules, not a managed free. */
CS_EXPORT void * cs_v1_gpi_next(void * iterator);
/** @brief Write high/low halves of the unsigned simulator-step time into nonnull outputs. */
CS_EXPORT void cs_v1_gpi_get_sim_time(uint32_t * hi, uint32_t * lo);
/** @brief Write the base-10 seconds-per-step exponent into a nonnull output. */
CS_EXPORT void cs_v1_gpi_get_sim_precision(int32_t * precision);
/** @brief Request simulator termination; must be called on the simulator thread. */
CS_EXPORT void cs_v1_gpi_finish(void);
/** @brief Return the backend signedness flag; no value conversion occurs. */
CS_EXPORT int32_t cs_v1_gpi_is_signed(void * signal);
/** @brief Return the borrowed backend product name used by capability policy. */
CS_EXPORT const char * cs_v1_gpi_get_simulator_product(void);
/** @brief Submit a signed 32-bit value using the frozen write-action code. */
CS_EXPORT void cs_v1_gpi_set_signal_value_int(void * signal, int32_t value, int32_t action);
/** @brief Return frozen direction code: -1 descending, 0 none, 1 ascending. */
CS_EXPORT int32_t cs_v1_gpi_get_range_dir(void * signal);
/** @brief Return the backend constant flag used by write guards. */
CS_EXPORT int32_t cs_v1_gpi_is_constant(void * signal);
/** @brief Widen the platform-native GPI long to signed int64_t, preserving negative values. */
CS_EXPORT int64_t cs_v1_gpi_get_signal_value_long(void * signal);
/** @brief Register a callback for simulation shutdown; return an opaque callback handle or null on failure. Caller retains callback/data lifetime. */
CS_EXPORT void * cs_v1_gpi_register_end_of_sim_time_callback(cs_callback cb, void * data);
/** @brief Return the borrowed simulator-qualified object name; separators are backend-specific. */
CS_EXPORT const char * cs_v1_gpi_get_signal_fullname_str(void * signal);
/** @brief Return the borrowed backend type description, not a portable type identifier. */
CS_EXPORT const char * cs_v1_gpi_get_signal_type_str(void * signal);
/** @brief Return a borrowed HDL definition name, or null when unavailable. */
CS_EXPORT const char * cs_v1_gpi_get_definition_name(void * signal);
/** @brief Return a borrowed source filename, or null when unavailable. */
CS_EXPORT const char * cs_v1_gpi_get_definition_file(void * signal);
/** @brief Return feature bits: 0 packed planes, 1 VPI metadata, 2 direct VPI probe, 3 numeric batches, 4 native clocks; safe before binding. */
CS_EXPORT uint64_t cs_abi_features(void);
/** @brief Read value/unknown/high-Z planes into length bytes. Status 0 success, 1 arguments, 2 width, 3 unsupported state, 5 malformed text; discard buffer on failure. */
CS_EXPORT int32_t cs_v1_read_planes(void *signal, uint32_t bits, uint8_t *buffer, uint32_t length);
/** @brief Validate planes then submit a write. Status 0 submitted, 1 arguments, 2 width, 4 allocation, 6 constant; no write occurs on validation failure. */
CS_EXPORT int32_t cs_v1_write_planes(void *signal, uint32_t bits, const uint8_t *buffer, uint32_t length, int32_t action);
/** @brief ABI numeric write descriptor: pointer plus fixed-width value/action (sequential layout). */
typedef struct cs_u32_write { void *signal; uint32_t value; int32_t action; } cs_u32_write;
/** @brief Enable checked direct VPI vector access for a known signal; 0 enabled, 1 unavailable, 2 invalid, 4 allocation. */
CS_EXPORT int32_t cs_v1_enable_direct(void *signal);
/** @brief Release owned direct VPI cache handles before simulator shutdown. */
CS_EXPORT void cs_v1_clear_direct(void);
/** @brief Validate then write ordered numeric entries; statuses match packed transport. */
CS_EXPORT int32_t cs_v1_write_u32_batch(const cs_u32_write *items, uint32_t count);
/** @brief Read ordered known unsigned values; discard all output on nonzero status. */
CS_EXPORT int32_t cs_v1_read_u32_batch(void *const *signals, const uint32_t *widths, uint32_t *values, uint32_t count);
/** @brief Start a native clock; caller owns failure callback/data until successful stop. Status 8 means callback registration/removal failure. */
CS_EXPORT void *cs_v1_clock_start(void *signal, uint64_t high, uint64_t low, cs_callback failure, void *data, int32_t *status);
/** @brief Update timing at the next full-cycle boundary. */
CS_EXPORT int32_t cs_v1_clock_update(void *handle, uint64_t high, uint64_t low);
/** @brief Stop pending edges; 0 consumes the handle, nonzero retains it for safe retry. */
CS_EXPORT int32_t cs_v1_clock_stop(void *handle);
/** @brief Query VPI port direction by exact qualified name: 0 unknown, 1 input, 2 output, 3 inout.
 * Feature bit 6; simulator-thread-only. Borrows name and retains no handle. */
CS_EXPORT int32_t cs_v1_port_direction(const char *name);
#ifdef __cplusplus
} // extern "C"
#endif
#endif
