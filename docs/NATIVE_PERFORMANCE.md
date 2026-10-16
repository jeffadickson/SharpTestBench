# Native C++20 performance measurement

This compares the native modernization in issue #50, using the same managed
assemblies and HDL in every configuration. It is not a C# versus Python comparison.

## Method

- Baseline: owned C host/ABI and build script from `2ff0ac644602197f64fb7839d52ff9d848d75387`, plus the pinned adapters built with their original CMake settings.
- Bridge only: current optimized C++20 host, with the baseline adapter binaries.
- Full: current optimized C++20 host and C++20 adapter build overlay.
- The baseline owned bridge has no optimization flag (GCC default); the baseline
  adapters **already use Release `-O3` and LTO**. The new adapter profile additionally
  selects C++20, requires IPO and retains debugging information.
- One unmeasured fresh-process warmup per configuration, then nine measured rounds
  in rotating configuration order. Each process is pinned to the same available
  logical CPU. .NET tiered compilation is disabled for all configurations to avoid
  comparing different JIT tiers. These are controlled measurements, not default
  tiered-JIT startup measurements.
- Each round measures 100,000 timer waits; 100,000 checked 32-bit read/write
  transactions; 1,000 checked round trips for each width/mode of the packed/string
  benchmark; and 2,048 write/read pairs plus resets/extra reads in the AXI fixture.
- Packed/string execution order alternates by round. AXI cycle counts, stall counts
  and full transaction traces must match across every configuration and round.
- Body timings exclude simulator startup and HDL compilation; wall timings include
  them. The string controls are retained in the raw data but do not represent native
  packed-transfer performance.
- Speedup is baseline median divided by candidate median. A 95% paired bootstrap
  interval uses 10,000 resamples of the nine rounds. An interval spanning 1 is treated
  as inconclusive. These intervals describe this run; they do not establish a
  portable performance guarantee or eliminate shared-host interference.

## Reproduction (Linux)

Use the pinned .NET SDK from `global.json`, GCC/G++, Python and Icarus. Set
`CS_DOTNET_ROOT`, `CS_IVERILOG`, `CS_IVL_ROOT` and `CS_VVP` if installed outside PATH.
Start from a full Git clone so the historical source revision is available.

```sh
python tools/prepare_upstream.py
CC=gcc CXX=g++ python -m pip install ./build/upstream/cocotb-source
CC=gcc CXX=g++ python tools/native_performance_prepare.py
dotnet build tests/Benchmark -c Release -p:RestoreLockedMode=true
dotnet build examples/AxiMemory -c Release -p:RestoreLockedMode=true
python tools/native_performance.py \
  --baseline-python build/native-performance/baseline-python \
  --repeat 9 --packed-count 1000 --transactions 2048
```

The output directory contains raw samples, full subprocess logs, checked workload
traces, binary hashes and the optimized host's compiler manifest. The isolated
baseline installation does not replace the active cocotb installation. All build
and installation commands must finish before starting measurements.

The initial environment warmup encountered a truncated simulator executable. Its
results were discarded. The measured run uses a fresh extraction of Ubuntu's Icarus
12.0 package verified against SHA-256
`f19394cee05ad526e0eb3873bc6c17a5828f56df17fbe2db40cb66513a2d4667`;
the extracted vvp binary was also checked against the archive contents.

## Results — 2026-10-16

Linux x64, AMD EPYC 9V74, Icarus 12.0, .NET SDK 10.0.402. Nine samples per configuration.

| Workload | Baseline median | Full optimized median | Speedup | 95% paired interval |
|---|---:|---:|---:|---:|
| 100,000 timer waits | 55.757 ms | 52.700 ms | 1.058× | 0.987–1.103× |
| 100,000 32-bit transactions | 196.560 ms | 190.467 ms | 1.032× | 0.976–1.065× |
| 1,000 256-bit packed round trips | 4.620 ms | 3.895 ms | 1.186× | 1.118–1.277× |
| 1,000 4096-bit packed round trips | 66.342 ms | 53.021 ms | 1.251× | 1.205–1.319× |
| AXI workload body | 921.826 ms | 918.798 ms | 1.003× | 0.962–1.085× |
| AXI workload including startup/compile | 1260.859 ms | 1247.959 ms | 1.010× | 0.954–1.067× |

Only the packed-transfer speedup is resolved by these intervals. Full-stack packed
256-bit throughput improves 18.6% (15.7% less elapsed time); 4096-bit throughput
improves 25.1% (20.1% less elapsed time). Timer, 32-bit and AXI changes remain
inconclusive. The AXI body median changes by only about 0.3%, with its interval
spanning both slowdown and improvement. This is not evidence for a broad 25%
testbench speedup.

The bridge-only comparison gives 1.209× for 256-bit packed transfers and 1.277×
for 4096-bit transfers. Rebuilding the adapters adds no consistently resolved
benefit on these workloads; the old adapter build was already optimized.

All microbenchmark values were checked, and all 30 AXI runs (three warmups plus
27 measured runs) matched transaction traces, cycles and stall counts. No samples
from the completed run were excluded. Raw samples, intervals, hashes and build
settings: [measurement evidence](evidence/native-performance-2026-10-16.json).

Scope remains Linux/Icarus and these workloads. Windows/macOS performance, other
simulators, and substantial CPU/DSP workloads still need separate measurements.
