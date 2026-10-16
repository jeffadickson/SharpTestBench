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

## Buffer/SIMD follow-up (#51)

This second run compares against the already-optimized C++20 release at
`870b21e64a6c5eff98504b8351cf86ba219a7c3f`, not the original unoptimized C bridge.
Both configurations use identical adapter binaries. Preserved managed assemblies
also allow a native-only intermediate configuration. The same nine-round method,
workload sizes and correctness checks apply.

| Metric | Previous optimized build | New buffers/SIMD | Finding |
|---|---:|---:|---|
| 1,000 packed 256-bit round trips | 3.723 ms | 3.619 ms | 1.029×; 95% interval 0.931–1.118× |
| 1,000 packed 4096-bit round trips | 51.880 ms | 50.970 ms | 1.018×; 95% interval 0.944–1.130× |
| AXI body | 909.055 ms | 890.139 ms | 1.021×; 95% interval 0.966–1.072× |
| 256-bit managed bytes / 1,000 round trips | 448,040 | 320,040 | 28.6% reduction |
| 4096-bit managed bytes / 1,000 round trips | 4,408,040 | 1,280,040 | 71.0% reduction |

Timing intervals include 1, so no additional speedup is established. Allocation
reductions are measured cumulative bytes allocated on the simulator thread, not
peak memory, total process memory, or native allocation counts. All 30 AXI runs
and all checked microbenchmark values matched. Native-only allocation counts are
unchanged, confirming that the managed buffer changes account for this reduction.

GCC's actual build diagnostic reports 16-byte and 8-byte vectorization of the
canonical-plane validation reduction. It does not claim the branch-heavy string
conversion is SIMD. Compiler switches and diagnostics are retained in the
[raw evidence](evidence/bridge-buffers-2026-10-16.json).

To reproduce this before/after comparison, preserve the prior host and complete
Benchmark/AxiMemory output directories before rebuilding. Pass them as
`--baseline-host <library>` and `--baseline-managed <directory>` (containing
`Benchmark/` and `AxiMemory/`), and set `--baseline-python` to the current adapter
installation's site-packages directory so both variants use the same adapters.
Direct backend vector access remains #17; GPI binary-string conversion is still
present in this implementation.

## Verilator 5.048 follow-up (#52)

Measured code: `307cfca81e78326d4b1ff85eee189b4893241ba9`, clean working tree.
Baseline: `870b21e64a6c5eff98504b8351cf86ba219a7c3f`, the **already optimized**
pre-#51 host and managed binaries preserved for the preceding Icarus comparison.
This measures the buffer/SIMD increment, not the original C-to-C++ modernization
and not C# versus Python/cocotb.

The Linux x64/AMD EPYC 9V74/.NET 10.0.402 experiment uses one CPU, disabled tiered
compilation, one fresh-process warmup per variant and nine interleaved measured
rounds. Each sample runs 100,000 timers, 100,000 32-bit round trips, 1,000 string
and packed round trips at each width, and 2,048 AXI write/read pairs plus reset
cases. All 27 measured samples were retained. The three configurations are the
preserved baseline, new native bridge with preserved managed assemblies, and full
new implementation. Adapter binaries are identical across these configurations.

Each HDL model was compiled **once**, with portable C++20, effective `-O3`, LTO,
and explicit loop/SLP vectorization, then reused unchanged in all variants.
`-O3` is passed in user CFLAGS, after Verilator's default `-Os`; the Python runner
invokes make itself and does not apply Verilator's `-MAKEFLAGS`. Actual compiler
commands and model executable hashes are retained. No host-specific ISA or
fast-math is enabled. Wall times include runner/runtime startup but exclude model
compilation; body times exclude both. This differs from the Icarus wall metric,
which includes its HDL compilation, so those wall times should not be compared
as simulator speed ratios.

| Workload | Before median | After median | Throughput ratio | Paired 95% interval |
|---|---:|---:|---:|---:|
| 100,000 timers | 75.773 ms | 76.467 ms | 0.991× | 0.938–1.096× |
| 100,000 32-bit round trips | 197.561 ms | 201.404 ms | 0.981× | 0.943–1.053× |
| 1,000 packed 256-bit round trips | 2.724 ms | 2.671 ms | 1.020× | 0.949–1.063× |
| 1,000 packed 4,096-bit round trips | 35.720 ms | 33.924 ms | 1.053× | 0.99996–1.35485× |
| AXI test body | 819.714 ms | 833.926 ms | 0.983× | 0.913–1.039× |
| AXI process wall | 1,137.306 ms | 1,173.680 ms | 0.969× | 0.902–1.037× |

Every interval includes no change. The 4,096-bit lower bound is 0.9999574×,
so even that borderline result does **not** establish an additional speedup.
Intervals use 10,000 paired bootstrap resamples; they describe this run, not a
machine-to-machine guarantee. The native-only configuration also shows no
resolved speed change.

| Managed allocation per 1,000 packed round trips | Before | After | Reduction |
|---|---:|---:|---:|
| 256 bits | 448,040 bytes | 320,040 bytes | 28.6% |
| 4,096 bits | 4,408,040 bytes | 1,280,040 bytes | 71.0% |

These are cumulative simulator-thread managed bytes, not peak memory or native
allocation counts. Native-only allocation figures equal the baseline, confirming
that the reduction comes from the managed buffer change. Known-bit round trips
passed; all 30 AXI runs (including warmups) matched complete traces, cycles and
stall counts. This is a single-outstanding AXI4-Lite-shaped fixture, not full AXI
compliance or a CPU/DSP workload.

The optimized full regression passed **70 tests, 0 failures, 4 explicit skips**:
one deliberate skip and the existing simulator restrictions for force ordering,
real callbacks and string callbacks. The 8,193-bit pooled-buffer case passed,
as did 32 queue reference rows and waveform/JUnit-link validation. Four-state
writes and force-hold remain unsupported by our Verilator capability policy.

Evidence:
- [Raw performance samples, hashes, flags and intervals](evidence/verilator-2026-10-16/performance.json)
- [Regression results, skips and build metadata](evidence/verilator-2026-10-16/regression.json)
- [Actual model compiler commands](evidence/verilator-2026-10-16/compiler-commands.txt)

Reproduce after building Verilator 5.048 and the current solution/AxiMemory, with
its `bin` on PATH and VERILATOR_ROOT set to its source/build directory:

```sh
CS_DOTNET_ROOT=/path/to/dotnet python tools/native_performance.py \
  --sim verilator \
  --baseline-revision 870b21e64a6c5eff98504b8351cf86ba219a7c3f \
  --baseline-python /path/to/current/site-packages \
  --baseline-host build/bridge-before/libsharptestbench_host.so \
  --baseline-managed build/bridge-before/managed \
  --output build/verilator-performance --repeat 9 \
  --packed-count 1000 --transactions 2048
```

Preserve the baseline host and full Benchmark/AxiMemory managed output directories
before rebuilding the current revision, as in the buffer follow-up above. The
harness records both sets of hashes. Fresh-process execution of a prebuilt model
is also available through `tools/run.py --sim verilator --no-build
--sim-executable /absolute/path/to/model`; callers must supply a model built for
the requested HDL/top and wave mode. The permanent CI job builds pinned 5.048,
runs the full regression, queue/wave checks and benchmark correctness smoke;
shared-runner timing is not a pass/fail gate.

CI acceptance: all six jobs passed in
[run 38849578307](https://github.com/jeffadickson/SharpTestBench/actions/runs/38849578307)
for code `307cfca81e78326d4b1ff85eee189b4893241ba9`, including the new Verilator
job. Windows/macOS Verilator and broader CPU/DSP benchmarks remain unverified.
