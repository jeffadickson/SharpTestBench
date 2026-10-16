/** @file icarus_probe.cpp
 * @brief Cocotb-free C++20 VPI proof of direct signal handles and callbacks.
 * @details A standalone transition fixture, not yet the complete cs_v1 ABI adapter.
 * This file uses only simulator-installed IEEE VPI headers and libraries.
 */
#include <vpi_user.h>
#include <cstdint>
#include <cstdio>
#include <cstring>

namespace {
int transitions = 0;
bool saw_reset = false;
bool saw_count = false;

PLI_INT32 value_changed(p_cb_data callback) {
    s_vpi_value value{};
    value.format = vpiIntVal;
    vpi_get_value(callback->obj, &value);
    ++transitions;
    if (value.value.integer == 0) saw_reset = true;
    if (value.value.integer >= 3) saw_count = true;
    return 0;
}
PLI_INT32 finish(p_cb_data) {
    if (saw_reset && saw_count && transitions >= 3) {
        std::printf("STB_VPI_PASS transitions=%d\n", transitions);
    } else {
        std::printf("STB_VPI_FAIL transitions=%d reset=%d count=%d\n",
            transitions, saw_reset, saw_count);
    }
    std::fflush(stdout);
    return 0;
}
PLI_INT32 startup(p_cb_data) {
    vpiHandle counter = vpi_handle_by_name(const_cast<PLI_BYTE8*>("top.dut.count"), nullptr);
    if (!counter) {
        std::printf("STB_VPI_FAIL missing top.dut.count\n");
        return 0;
    }
    static s_cb_data change{};
    change.reason = cbValueChange;
    change.cb_rtn = value_changed;
    change.obj = counter;
    if (!vpi_register_cb(&change)) std::printf("STB_VPI_FAIL change registration\n");
    static s_cb_data end{};
    end.reason = cbEndOfSimulation;
    end.cb_rtn = finish;
    if (!vpi_register_cb(&end)) std::printf("STB_VPI_FAIL end registration\n");
    return 0;
}
void register_probe() {
    static s_cb_data begin{};
    begin.reason = cbStartOfSimulation;
    begin.cb_rtn = startup;
    vpi_register_cb(&begin);
}
}
extern "C" {
void (*vlog_startup_routines[])() = {register_probe, nullptr};
}
