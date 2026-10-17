/** @file main.cpp
 * @brief Native C++20 Verilator model-port test without VPI or cocotb.
 *
 * The generated Vcounter model exposes its top ports as C++ members.
 * A low-to-high clock transition is followed by eval() before sampling state.
 */
#include "Vcounter.h"
#include "verilated.h"
#include <chrono>
#include <cstdint>
#include <cstdio>
#include <cstdlib>

int main(int argc, char** argv) {
    Verilated::commandArgs(argc, argv);
    constexpr std::uint64_t cycles = 100000;
    Vcounter dut;
    dut.clk = 0; dut.reset = 1; dut.enable = 1;
    dut.eval();
    dut.clk = 1; dut.eval();
    if (dut.count != 0) { std::fprintf(stderr, "Reset failed\n"); return 1; }
    dut.clk = 0; dut.eval(); dut.reset = 0;
    const auto begin = std::chrono::steady_clock::now();
    for (std::uint64_t i = 0; i < cycles; ++i) {
        dut.clk = 1;
        dut.eval();
        const auto expected = static_cast<std::uint8_t>(i + 1u);
        if (dut.count != expected) {
            std::fprintf(stderr, "Mismatch cycle=%llu expected=%u actual=%u\n",
                         static_cast<unsigned long long>(i + 1), expected,
                         static_cast<unsigned>(dut.count));
            return 2;
        }
        dut.clk = 0;
        dut.eval();
    }
    const auto seconds = std::chrono::duration<double>(
        std::chrono::steady_clock::now() - begin).count();
    dut.final();
    std::printf("STB_VERILATOR_DIRECT_PASS cycles=%llu body_seconds=%.9f final=%u\n",
                static_cast<unsigned long long>(cycles), seconds,
                static_cast<unsigned>(dut.count));
    return 0;
}
