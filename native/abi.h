// SharpTestBench ABI v1. See docs/NATIVE_ABI.md.
#ifndef CS_ABI_H
#define CS_ABI_H
#include <stdint.h>

int32_t cs_v1_vpi_packed_ranges(const char *name, uint32_t bits,
                              int32_t *pairs, uint32_t capacity, uint32_t *count);
typedef int32_t (*cs_callback)(void *);
uint32_t cs_abi_version(void);
const char *cs_bind_gpi(void);
void * cs_v1_gpi_register_start_of_sim_time_callback(cs_callback cb, void * data);
void * cs_v1_gpi_register_timed_callback(cs_callback cb, void * data, uint64_t delay);
void * cs_v1_gpi_register_value_change_callback(cs_callback cb, void * data, void * signal, int32_t edge);
void * cs_v1_gpi_register_readonly_callback(cs_callback cb, void * data);
void * cs_v1_gpi_register_readwrite_callback(cs_callback cb, void * data);
void * cs_v1_gpi_register_nexttime_callback(cs_callback cb, void * data);
int32_t cs_v1_gpi_remove_cb(void * cb);
void * cs_v1_gpi_get_root_handle(const char * name);
void * cs_v1_gpi_get_handle_by_name(void * parent, const char * name, int32_t mode);
void * cs_v1_gpi_get_handle_by_index(void * parent, int32_t index);
int32_t cs_v1_gpi_get_num_elems(void * signal);
int32_t cs_v1_gpi_get_range_left(void * signal);
int32_t cs_v1_gpi_get_range_right(void * signal);
int32_t cs_v1_gpi_get_object_type(void * signal);
const char * cs_v1_gpi_get_signal_name_str(void * signal);
const char * cs_v1_gpi_get_signal_value_binstr(void * signal);
double cs_v1_gpi_get_signal_value_real(void * signal);
const char * cs_v1_gpi_get_signal_value_str(void * signal);
void cs_v1_gpi_set_signal_value_binstr(void * signal, const char * value, int32_t action);
void cs_v1_gpi_set_signal_value_real(void * signal, double value, int32_t action);
void cs_v1_gpi_set_signal_value_str(void * signal, const char * value, int32_t action);
void * cs_v1_gpi_iterate(void * parent, int32_t kind);
void * cs_v1_gpi_next(void * iterator);
void cs_v1_gpi_get_sim_time(uint32_t * hi, uint32_t * lo);
void cs_v1_gpi_get_sim_precision(int32_t * precision);
void cs_v1_gpi_finish(void);
int32_t cs_v1_gpi_is_signed(void * signal);
const char * cs_v1_gpi_get_simulator_product(void);
void cs_v1_gpi_set_signal_value_int(void * signal, int32_t value, int32_t action);
int32_t cs_v1_gpi_get_range_dir(void * signal);
int32_t cs_v1_gpi_is_constant(void * signal);
int64_t cs_v1_gpi_get_signal_value_long(void * signal);
void * cs_v1_gpi_register_end_of_sim_time_callback(cs_callback cb, void * data);
const char * cs_v1_gpi_get_signal_fullname_str(void * signal);
const char * cs_v1_gpi_get_signal_type_str(void * signal);
const char * cs_v1_gpi_get_definition_name(void * signal);
const char * cs_v1_gpi_get_definition_file(void * signal);
uint64_t cs_abi_features(void);
int32_t cs_v1_read_planes(void *signal, uint32_t bits, uint8_t *buffer, uint32_t length);
int32_t cs_v1_write_planes(void *signal, uint32_t bits, const uint8_t *buffer, uint32_t length, int32_t action);
#endif
