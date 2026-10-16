/** @file vpi_time.h
 * @brief Optional simulator-direct VPI time reads for the existing ABI v1 bridge.
 * No cocotb implementation, GPI object handles, or managed callbacks are used.
 */
#ifndef STB_INDEPENDENT_VPI_TIME_H
#define STB_INDEPENDENT_VPI_TIME_H
#include <cstdint>
/** @brief Read the current simulation tick count. False means no VPI symbol. */
bool stb_vpi_read_time(std::uint32_t *hi, std::uint32_t *lo) noexcept;
/** @brief Read the simulator time precision exponent. False means unavailable. */
bool stb_vpi_read_precision(std::int32_t *precision) noexcept;
#endif
