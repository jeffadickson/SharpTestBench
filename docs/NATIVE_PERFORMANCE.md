# Native C++20 performance measurement

Latest results are in **Four optimizations (#53)** below: direct VPI vectors,
ordered batches, native clocks and linear text conversion, with paired Icarus and
Verilator measurements. Earlier #50–#52 experiments are retained for comparison.
These are SharpTestBench before/after measurements, not C# versus Python comparisons.

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

## Four optimizations: direct vectors, batches, native clocks and linear text (#53)

The previous inconclusive SIMD increment did not establish a performance ceiling.
This change removes work: direct VPI aval/bval words replace native binary strings;
ordered numeric batches reduce managed/native crossings; native clocks keep per-edge
Timer/ReadWrite callbacks outside the CLR; linear text conversion builds byte planes
without per-bit BigInteger temporaries. C++20, O3, LTO and explicit portable loop/SLP
vectorization remain enabled. No fast-math or host-specific ISA was added.

### Actual before/after

Preserved native and managed binaries from `89881b9d37ef9e00b0984bcbd46f3765054b7810`
are compared with the new implementation. Both use the same pinned adapter binaries,
HDL, SDK and machine. A native-only intermediate configuration is also retained in
the raw data. Each simulator has nine measured rounds in rotating three-variant
order, plus one unmeasured process per configuration. Each micro run checks 1,000
round trips per width/mode, 100,000 timers and 100,000 32-bit transactions. AXI runs
2,048 write/read pairs plus reset cases; every trace, cycle and stall count must match.
The old AXI assembly retains its original scalar/manual-clock implementation; the
new assembly enables snapshots and native clocks. The new standalone clock benchmark
is disabled in these comparisons so the old/new processes perform comparable work.

Times below exclude process startup and model compilation. Speedups are ratios of
medians; parentheses are 95% paired-bootstrap intervals (10,000 resamples). Raw wall
timings include startup and, for Icarus, HDL compilation. Verilator reuses the same
precompiled model in every variant. No completed samples were excluded.

| Simulator | Workload | Previous median | New median | Speedup (95% interval) |
|---|---|---:|---:|---:|
| Icarus | 100,000 timers | 53.957 ms | 54.790 ms | 0.985× (0.885–1.026×) |
| Icarus | 100,000 32-bit transactions | 196.249 ms | 201.666 ms | 0.973× (0.872–1.029×) |
| Icarus | 1,000 packed 256-bit round trips | 3.824 ms | 2.380 ms | 1.606× (1.478–1.786×) |
| Icarus | 1,000 packed 4096-bit round trips | 49.824 ms | 30.644 ms | 1.626× (1.368–1.692×) |
| Icarus | 1,000 string 4096-bit round trips | 3732.359 ms | 128.141 ms | 29.127× (28.275–31.032×) |
| Icarus | AXI workload body | 925.355 ms | 706.560 ms | 1.310× (1.232–1.366×) |
| Icarus | AXI including startup | 1271.129 ms | 1073.559 ms | 1.184× (1.096–1.225×) |
| Verilator | 100,000 timers | 77.418 ms | 75.350 ms | 1.027× (0.994–1.059×) |
| Verilator | 100,000 32-bit transactions | 201.046 ms | 205.106 ms | 0.980× (0.923–1.068×) |
| Verilator | 1,000 packed 256-bit round trips | 2.636 ms | 0.780 ms | 3.380× (3.295–3.902×) |
| Verilator | 1,000 packed 4096-bit round trips | 34.832 ms | 5.632 ms | 6.185× (5.064–6.470×) |
| Verilator | 1,000 string 4096-bit round trips | 3626.823 ms | 108.829 ms | 33.326× (32.539–33.741×) |
| Verilator | AXI workload body | 817.636 ms | 639.032 ms | 1.279× (1.220–1.320×) |
| Verilator | AXI including startup | 1138.934 ms | 996.871 ms | 1.143× (1.080–1.202×) |

The actual before/after AXI body speedups are 1.310× on Icarus and 1.279× on
Verilator (23.6% and 21.8% less elapsed time). Packed 4096-bit round trips improve
1.626× and 6.185× respectively. Timer-only and single 32-bit transaction intervals
include 1 on both simulators; no speedup or regression is established for those.
All 216 AXI executions across these four runs, including warmups, produced the same
trace/cycle/stall hash. This verifies that the speedups did not omit bus work.

### Independent contributions

Twelve measured rounds per simulator rotate six configurations: all four disabled,
each one enabled alone, and all four enabled. These use identical current binaries;
only process-level switches change. Twelve rounds balance each configuration across
all execution positions. The table attributes each change to its relevant workload.
These ratios need not multiply: the features interact and affect overlapping costs.

| Enabled alone | Workload | Icarus speedup (95% interval) | Verilator speedup (95% interval) |
|---|---|---:|---:|
| Direct | Packed 256-bit | 1.472× (1.282–1.770×) | 3.441× (2.264–3.780×) |
| Direct | Packed 4096-bit | 1.715× (1.650–1.801×) | 6.087× (5.678–6.451×) |
| Batch | AXI body | 1.265× (1.228–1.301×) | 1.174× (1.139–1.236×) |
| Clock | 10,000 clock cycles; HDL counter only | 1.733× (1.626–1.774×) | 1.685× (1.649–1.823×) |
| Clock | 10,000 clock cycles; managed edge observer | 2.212× (2.139–2.405×) | 2.288× (2.218–2.415×) |
| Clock | AXI body | 1.071× (1.037–1.127×) | 0.983× (0.959–1.029×) |
| Text | String 4096-bit | 27.755× (26.505–29.245×) | 30.224× (27.657–32.339×) |

Clock measurements check exactly 10,000 HDL counter increments and exact elapsed
simulation steps. They include clock creation, start and cancellation; the first
clock mode can include first-use JIT cost, so they are not pure steady-state native
edge timings. Observed mode retains managed edge-wait overhead. Native clocks remove
per-edge managed driving; shared edge-subscription coalescing is not implemented.

### Managed allocations

These are median cumulative bytes on the simulator thread with all features disabled
versus enabled, not peak memory or native allocations. Small run-to-run bookkeeping
variation is retained in the raw data.

| Simulator | Workload | Disabled | Enabled | Reduction |
|---|---|---:|---:|---:|
| Icarus | 1,000 string 4096-bit round trips | 4,678,365,356 B | 17,776,288 B | 99.620% |
| Icarus | 10,000 unobserved clock cycles | 24,017,088 B | 15,184 B | 99.937% |
| Icarus | 10,000 observed clock cycles | 30,806,848 B | 6,808,536 B | 77.899% |
| Verilator | 1,000 string 4096-bit round trips | 4,678,365,356 B | 17,776,288 B | 99.620% |
| Verilator | 10,000 unobserved clock cycles | 24,017,192 B | 15,184 B | 99.937% |
| Verilator | 10,000 observed clock cycles | 30,806,848 B | 6,808,536 B | 77.899% |

### Reproduction and limits

For attribution, build the current managed projects and install the pinned adapters,
then run the following once for each simulator. Set the tool paths as described above;
Verilator requires the pinned 5.048 source build and its VERILATOR_ROOT/bin on PATH.

```sh
python tools/native_performance.py --ablation --sim verilator \
  --baseline-python /path/to/installed/site-packages \
  --repeat 12 --packed-count 1000 --transactions 2048 \
  --output build/four-ablation-verilator
```

For actual before/after, preserve the prior native host and complete Benchmark and
AxiMemory output directories before rebuilding. Supply `--baseline-host`,
`--baseline-managed` and `--baseline-revision 89881b9d37ef9e00b0984bcbd46f3765054b7810`,
omit `--ablation`, use nine rounds, and set `AXI_BATCH=1 AXI_CLOCK_MODE=native`.
Both adapter paths must reference the same installed pinned libraries for this comparison.

The four controls are STB_DIRECT_PACKED, STB_BATCH_TRANSFERS, STB_NATIVE_CLOCK and
STB_LINEAR_TEXT; value 0 disables a feature before process startup. SimClock also
supports explicit Managed/Native selection. Direct VPI is probed per object on
Icarus/Verilator; other backends retain the string path. Native clocks similarly
retain managed fallback for unverified families or older native hosts.

All timing evidence is Linux x64 on the recorded AMD EPYC 9V74, .NET 10.0.402,
Icarus 12.0 and Verilator 5.048. This is an AXI4-Lite-shaped single-outstanding fixture
and synthetic vector/clock traffic, not a substantial CPU/DSP or full-AXI benchmark.
It measures SharpTestBench plus simulator work, not a general acceleration of
Verilator's HDL evaluation engine. Windows/macOS functional validation is separate
from Linux performance evidence. Intervals characterize these samples, not portable
performance guarantees; an interval including 1 does not establish a speedup.

Raw samples, allocations, paired intervals, compiler flags and binary/source hashes:
[ablation Icarus](evidence/four-optimizations-2026-10-16/ablation-icarus.json),
[ablation Verilator](evidence/four-optimizations-2026-10-16/ablation-verilator.json),
[before/after Icarus](evidence/four-optimizations-2026-10-16/before-after-icarus.json),
[before/after Verilator](evidence/four-optimizations-2026-10-16/before-after-verilator.json).

The Icarus attribution run uses clean revision 5e1b1cd; later Linux measurements use
5eb2df2, which changes only Windows compiler-runtime linkage and check diagnostics.
The subsequent 6b51699 commit adds a packed force/release regression without changing
runtime code. Every evidence file records its exact revision and hashes.
