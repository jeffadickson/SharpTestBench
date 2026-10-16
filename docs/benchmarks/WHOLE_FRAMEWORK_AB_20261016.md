# Whole-framework A/B benchmark — 2026-10-16

These results are from GitHub Actions run [38963393670](https://github.com/jeffadickson/SharpTestBench/actions/runs/38963393670), commit `3d472ca4`. The benchmark compares legacy transport toggles with optimized direct-packed/batch toggles **inside the existing cocotb-derived GPI bootstrap**. Neither is a fully independent VPI backend. It uses the same signed FIR RTL, same C# scoreboard, same deterministic stimulus (1,124 matched cycle-trace records), three runs per mode and explicit trace equivalence.

| Runner | Legacy median test-body | Optimized median test-body | Ratio | Legacy allocated | Optimized allocated |
|---|---:|---:|---:|---:|---:|
| Icarus Ubuntu x64 | 35.4655 ms | 31.5519 ms | 0.8897 | 7,747,312 B | 7,113,568 B |
| Verilator Ubuntu x64 | 36.0991 ms | 32.1310 ms | 0.8901 | 7,747,248 B | 7,113,472 B |
| Verilator Ubuntu ARM64 | 31.7600 ms | 28.2678 ms | 0.8900 | 7,747,096 B | 7,113,272 B |

Body-time reduction on these small shared CI samples: ~11.0%; allocation reduction: ~8.2%. These are preliminary observations, not a general performance guarantee.

**Wall-time caveat:** Icarus medians were ~0.327s per run, whereas Verilator was ~11.8s x64/~10.3s ARM64 because the current runner rebuilds/elaborates the model for each process. Do not compare wall-time figures as runtime-engine speed. Use a prebuilt model for future steady-state measurements.

**CI failures unrelated to the benchmark:** Windows x64/macOS Icarus tests and Windows ARM64 native job failed because the newly added `tests/Integration/top.sv` edge-capture always block referenced `clk` and `perf_b` before declaration. Fixed in subsequent commit `b46ef63a`; requalification remains necessary.

## Remaining acceptance
1. Confirm cross-platform suite success on the declaration-order fix.
2. Add full independent C++20 VPI bootstrap/handles/callbacks and run unchanged C# FIR assembly **without libgpi**. Reject missing adapter support explicitly.
3. Benchmark three configurations: existing legacy GPI, optimized hybrid and independent VPI. Compare exact cycle traces, matched body time, allocation and build-excluded process wall time.
4. Repeat with larger samples and at least seven alternated runs on controlled hardware.
