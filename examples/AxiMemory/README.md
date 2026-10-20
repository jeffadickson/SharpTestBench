# AXI memory verification and comparison

This example uses an AXI4-Lite-shaped, 32-bit, single-outstanding memory slave with
64 words. It is a verification fixture, not production AXI IP or a complete AXI
compliance suite. It has independent AW/W capture, byte enables, stable responses
under backpressure, and SLVERR for unaligned/out-of-range addresses. Reset explicitly
clears RAM, including committed writes; that is this fixture's policy, not an AXI
requirement. Protection attributes, bursts, IDs and multiple outstanding requests
are outside its scope.

The C# test uses Roslyn-generated typed ports. The Python test uses released
cocotb 2.1.0. They load the same deterministic transaction plan, drive identical
seeded stalls, and independently model memory: C# updates masked words, Python
updates individual bytes. Both check response codes, read data, response stability,
request-before-response ordering and bounded completion. Reset cuts cover AW-only,
W-only, pending B and pending R. The harness compares the complete transaction and
cycle trace, not just a checksum or final pass count.

```sh
dotnet build examples/AxiMemory -c Release
# Build native host once with tools/SharpTestBench.Tools/LegacyRunCommand.cs; provision build/reference-python with
# cocotb==2.1.0 and find_libpython==0.5.1 (CI already does this).
dotnet run --project tools/SharpTestBench.Tools -c Release -- axi-workload --transactions 1024 --repeat 5
```

Use the same CS_DOTNET_ROOT/CS_IVERILOG/CS_IVL_ROOT/CS_VVP overrides as tools/SharpTestBench.Tools/LegacyRunCommand.cs.
The count is write/read pairs; reset probes add commands. Results, exact plans,
traces, logs and metrics are in build/axi-workload. One whole workload at a time
must use that parent directory. Each simulator process has a separate workspace.
Fault modes 1/2/3 corrupt reads, ignore byte enables, or drop write responses. Both
languages must fail with the expected scoreboard/timeout diagnostic; six negative
runs prove these faults cannot silently pass. A stale metrics file is removed
before each invocation. Both runners require a real result report.

## Measurement boundaries

There is one unmeasured fresh-process warmup per language, then repeated fresh
processes in alternating order. Warmup warms filesystem/loader caches; it does not
preserve .NET JIT state between processes. Wall time includes runner, HDL compilation
and runtime startup; body time covers stimulus, sampling, scoreboard and trace
accumulation. Do not compare only body time and call it end-to-end speedup.

C# records cumulative managed allocation bytes and GC collection counts. A separate
Python run records tracemalloc peak traced memory and collection counts; that run
is excluded from timing medians because instrumentation changes timing. Cumulative
allocation and peak live traced memory are different measures. Neither measures
native simulator allocations or proves a memory-efficiency advantage.

The committed development run in docs/benchmarks/axi-memory-linux-20261014.json
uses 1,024 pairs, 2,120 total commands, five measured runs per language, equal traces
and six detected faults. Median wall times were about 1.02 s C# and 2.78 s Python;
body times were 0.74 s and 2.33 s. These are observations of this fixture on this
machine, not general claims about cocotb performance. CI repeats a smaller workload
on Linux x64, Windows x64 and Mac ARM64. CPU/DSP, AXI bursts, parallel channels,
larger memories and additional simulator performance remain #18 work.
