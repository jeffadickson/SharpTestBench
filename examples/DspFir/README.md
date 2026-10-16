# Streaming signed FIR example

This example exercises a four-tap FIR, `y[n] = -3*x[n] + 7*x[n-1] +
11*x[n-2] - 2*x[n-3]`, with signed 16-bit samples and exact signed 32-bit
results. Its one-entry elastic output stage supports one accepted sample per
clock when unstalled. This measures simulator-driven verification, not synthesized
FPGA DSP frequency or a general DSP-library benchmark.

The C# testbench holds input samples until accepted. It checks readiness, output
validity and exact signed results every cycle, including output stalls and reset
while input/output work is pending. The software reference uses wider integer
arithmetic and advances its history only on accepted samples. A separate Python
scoreboard uses an arbitrary-precision dot product under released cocotb 2.1.0.
Both must produce the same complete cycle trace and counters.

The deterministic plan includes negative/positive extrema, zero, long stalls,
continuous traffic, random gaps and repeated reset cuts. Reset discards pending
work; accepted and consumed totals need not match across resets. A final drain
must empty both the testbench input and DUT output.

Three deliberately faulty HDL modes must fail in both languages:

1. Flip the result's low bit.
2. Treat negative input samples as unsigned during the current-tap multiply.
3. Drop a valid result while the consumer is stalled.

Build and run from the repository root after provisioning the usual native host,
Icarus and pinned oracle environment described in the validation documentation:

```sh
dotnet build examples/DspFir -c Release
python tools/dsp_workload.py --cycles 16384 --repeat 5
```

The harness warms each language in a fresh process, alternates measured process
order, compares every trace and then requires six fault detections. `--cycles`
controls random cycles; directed scenarios and the final drain add 101 cycles.
Logs, full traces and `summary.json` are retained under `build/dsp-workload`.
Wall time includes HDL compilation and runtime startup; body time includes driving,
checking and trace collection. C# allocation/GC metrics are recorded, but no
cross-language memory-efficiency claim is made.

Linux, Windows and macOS CI run a shorter Icarus comparison and retain all results.
Verilator and larger/more diverse DSP designs remain future benchmark scope.

## Initial Linux measurement

Icarus 12, .NET 10.0.402, cocotb 2.1.0; five alternating measured processes after
one fresh-process warmup per language, 16,485 cycles per execution. Median body
time: C# **0.2973 s**, Python **1.0157 s** (3.42x ratio). Median process wall time:
C# **0.6679 s**, Python **1.5287 s** (2.29x ratio). All full traces match; all six
fault checks pass. This is one development-host workload, not a universal speed
claim or an incremental speedup from changing the bridge. Raw samples, source
hashes, native build manifest and test counters are in
[the evidence file](../../docs/benchmarks/dsp-fir-linux-20261016.json).
