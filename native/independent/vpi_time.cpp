/** @file vpi_time.cpp
 * @brief Independent direct-VPI timing implementation for the C++20 host bridge.
 * @details Resolves standard IEEE VPI exports from the current simulator process.
 * Calls are simulator-thread-only. No upstream GPI library is used here.
 */
#include "vpi_time.h"
#include "../platform.h"
#include <vpi_user.h>

bool stb_vpi_read_time(std::uint32_t *hi, std::uint32_t *lo) noexcept {
    if (!hi || !lo) return false;
    using read_t = void (*)(vpiHandle, p_vpi_time);
    // Symbol resolution is process-stable once the simulator has loaded this module.
    // Avoid a dynamic-loader lookup on every simulator time query.
    static const auto read = reinterpret_cast<read_t>(dlsym(RTLD_DEFAULT, "vpi_get_time"));
    if (!read) return false;
    s_vpi_time value{};
    value.type = vpiSimTime;
    read(nullptr, &value);
    *hi = static_cast<std::uint32_t>(value.high);
    *lo = static_cast<std::uint32_t>(value.low);
    return true;
}

bool stb_vpi_read_precision(std::int32_t *precision) noexcept {
    if (!precision) return false;
    using get_t = PLI_INT32 (*)(PLI_INT32, vpiHandle);
    static const auto get = reinterpret_cast<get_t>(dlsym(RTLD_DEFAULT, "vpi_get"));
    if (!get) return false;
    *precision = static_cast<std::int32_t>(get(vpiTimePrecision, nullptr));
    return true;
}
