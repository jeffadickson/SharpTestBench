# SharpTestBench — C# HDL verification, v0.8.0-alpha

A working C# verification runtime using cocotb's native GPI simulator adapters.
This is an independent experimental implementation, not an official cocotb release
and not yet a complete port. See `docs/COMPATIBILITY.csv` for the feature status
and `docs/OPEN_WORK.md` for the remaining work.

Version 0.7 adds [HDL discovery and automatic packed layouts](docs/DISCOVERY.md).
Verilator now supplies actual multidimensional packed ranges and package members;
other backends expose a checked bit-range fallback with explicit availability.

Formerly **CocotbSharp**. Version 0.8 renames the public package and namespace,
adds portable named packed-struct views and cycle-boundary clock updates.
See [migration and examples](docs/MIGRATION_0_8.md).

## Project navigation

- [Documentation](docs/README.md)
- [Repository layout and contributing](CONTRIBUTING.md)
- [All GitHub tasks](docs/TASKS_INDEX.md)
- Open `SharpTestBench.slnx` to build the managed projects together.

## Implemented

- Versioned native C interface, startup checks and object definition metadata.
- Compiled simulation coroutine scheduling on the simulator thread.
- Timer, rising/falling/value-change, ReadOnly, ReadWrite, NextTimeStep, First,
  Combine, ClockCycles, and trigger timeouts.
- Cancellable native registrations; deferred deposits; ReadOnly write guards.
- Named test jobs, task-scoped cancellation, structured task groups, and task combinators.
- Background failure propagation, test timeouts, and leak checks that cannot be waived by expected exceptions.
- Manual-reset events, FIFO semaphore/lock, bounded queues, clock driver.
- Checked fixed-width signed/unsigned types, four-state bit vectors, nine-state
  logic operators and immutable logic arrays, HDL range-aware value arrays.
- Strict signed/unsigned signal APIs; unavailable signedness is reported explicitly.
- Recursive generated bindings for scopes, records and nested typed arrays.
- Typed job results and aggregate errors across nested task groups.
- VHDL nine-state transport, null ranges, records, enums, generics and fixed strings
  including Latin-1 and embedded NUL (verified on NVC/VHPI).
- Constant write guards, cached child handles, and early-simulator-termination reports.
- Hierarchy traversal, unpacked memory indexing, real/string bindings,
  immediate writes and force/release with backend capability checks.
- Test discovery, parameter cases, expected exceptions, skips, deterministic RNG,
  per-test seeds, JSON and JUnit output, and a build/run command.

The simulation runtime and tests are C#. A small C host loads .NET through hostfxr;
cocotb's existing C++ adapters connect to the simulator. Python scripts currently
orchestrate builds and comparisons. Python is not embedded in the C# test process.

## Hardware-aware types

```csharp
var payload = ctx.Dut.Child("data").Unsigned<W8>();
payload.Write(new UInt<W8>(201));
await Triggers.ReadOnly();
UInt<W8> actual = payload.Read();
```

Writing a `UInt<W16>` or `SInt<W8>` to that API fails to compile. Constructors reject
numeric overflow. Wraparound must be explicit, e.g. `UInt<W8>.Wrap(256)` or `AddWrap`.
`Signal<W>` is the lower-level bit-pattern API and deliberately accepts signed,
unsigned, and four-state representations. Strict signedness lives in
`UnsignedSignal<W>` / `SignedSignal<W>` and is checked against elaborated metadata.

```csharp
[SimTest(TimeoutSteps = 100_000)]
public static async Task CheckTransfer(TestContext ctx)
{
    var clock = ctx.Dut.Child("clk").Signal<W1>();
    var data = ctx.Dut.Child("data").Unsigned<W8>();
    var sampled = ctx.Dut.Child("sampled").Unsigned<W8>();
    var clockJob = ctx.StartSoon(new SimClock(clock, period: 10).Run);

    await Triggers.FallingEdge(clock);
    data.Write(new UInt<W8>(42));
    await Triggers.RisingEdge(clock);
    await Triggers.ReadOnly(); // Wait for sequential HDL updates to settle.
    if (sampled.Read().Value != 42) throw new Exception("Transfer mismatch");
    clockJob.Cancel();
}
```

See `tests/Integration/Tests.cs` for complete examples, including a randomized
100-transaction stream test with backpressure. Define your own width by implementing
`IWidth`; e.g. `readonly struct W24 : IWidth { public static int Bits => 24; }`.

For Windows and macOS source setup and the counter example, see [ALPHA.md](docs/ALPHA.md).

## Build and run (Linux x86-64)

Prerequisites: .NET 10 SDK, Python 3.12+, GCC/G++, and a supported simulator.
Pin cocotb to commit `5fa952e92c46fd9cb58f55a8655312a4bf02e7de`.
The source snapshot is included in `third_party/cocotb-source.tar.gz`.
Preparation verifies its checksum and sets the recorded package version; native
adapter sources are unchanged. This avoids malformed Git-version labels in archives.

```sh
python tools/prepare_upstream.py
CC=gcc CXX=g++ python -m pip install ./build/upstream/cocotb-source pytest
dotnet run --project tests/SharpTestBench.Tests -c Release -- -xml build/managed-results.xml
python tools/run.py --sim icarus
python tools/run.py --sim verilator
python tools/run.py --sim ghdl
python tools/run.py --sim nvc
python tools/typecheck.py
python tools/parity.py
```

`run.py` builds a test assembly, native host and HDL, then checks the JSON results;
a failed test, absent results, or simulator failure gives a nonzero CLI exit.
It has a 120-second wall-clock watchdog in addition to simulation-time deadlines.
`--filter` selects test names; `--label` preserves separate reports;
`--project Benchmark` runs the matched benchmark assembly. `--no-build` skips
managed/host compilation; simulator build/elaboration is still performed.

Override tool locations with `CS_DOTNET_ROOT`, `CS_IVERILOG`, `CS_IVL_ROOT`,
`CS_VVP`, `CS_GHDL`, `CS_NVC`; use standard `VERILATOR_ROOT`/`GHDL_PREFIX` for extracted installs.
`CS_SEED` defaults to 1. Each result records the effective seed; reproduce a single
case by setting that seed and selecting that case with `--filter`.
`CS_WALL_TIMEOUT` changes the process timeout in seconds.

Generated bindings are written to `build/DutBindings.cs` and `build/VhdlBindings.cs`.
`Bindings.Generate(root, namespaceName: "MyDut")` walks scopes/records and creates
nested `HdlArray<T>` bindings. Constructors validate widths and available signs;
when the backend cannot report signedness, generated properties use `Signal<W>`.
Array access retains declared HDL indices. Generate arrays remain untyped because
instances may have different fields. This is elaboration-driven generation, not a
Roslyn incremental generator. See `docs/HDL_TYPES.md`.

## Installable package and your own tests

`python tools/package.py` builds `build/packages/SharpTestBench.0.8.0-alpha.nupkg` and
an isolated consumer of that package. Run Icarus and NVC suites first so both
binding trees exist. The package contains managed code, the Linux x86-64 native
host, build settings and licenses. The pinned GPI libraries and simulator must
still be installed separately; this is not a self-contained simulator distribution.
The release ZIP includes the package under `packages/`.

Add the package from a local NuGet feed to a .NET 10 test library. The package's
build settings copy runtime dependencies and generate runtimeconfig.json (opt out
with `SharpTestBenchCopyRuntimeAssets=false`). Use the same unmanaged Entry.Initialize
pattern shown in `tests/Integration/Tests.cs`, then:

```sh
python tools/run.py --assembly /path/to/MyTests.dll --source /path/to/top.sv --sim icarus
```

`--source` may be repeated; `--top` selects the HDL top; `--entry-type` overrides
`Entry, MyTests`; `--host` selects a prebuilt native host instead of the local one.
With a compiled host and assembly, `--no-build` avoids SDK/native compilation.
HDL compilation still runs. No NuGet package has been published publicly.

## Semantics and limits

- Use simulation triggers, not Task.Delay, for HDL timing. External .NET task
  continuations can be queued, but no host-thread wakeup/bridge contract is provided.
- All native operations are guarded by simulator thread affinity. No callbacks
  execute HDL access on thread-pool threads.
- A deposit is deferred to ReadWrite. Its effects may require another simulator
  evaluation cycle; use ReadOnly to observe settled values. Immediate is explicit.
- Timer durations must resolve to positive integer simulator steps. Rounding is explicit.
- `Triggers.First` cancels losing trigger registrations. `SimTasks.First` returns
  the winning task and leaves losing tasks running. `SimTasks.Combine` joins all
  tasks before propagating errors; cancelling the wait leaves those tasks running.
- `SimTasks.WithTimeout` uses simulation time and leaves the supplied task running
  on timeout. Its generic overload preserves `Task<T>` results.
- `StartSoon` jobs inherit their parent's cancellation scope. Plain trigger awaits,
  ClockCycles, queue and semaphore waits use that scope automatically. Explicit
  `CancellationToken.None` opts out for bounded cleanup. Cancellation is cooperative.
- `RunGroup` owns jobs started through the supplied group. The first child failure
  cancels its siblings and the group body; scope exit cancels and joins all children.
  The caller can catch a group failure without failing the enclosing test. Ordinary
  `ctx.StartSoon` failures still fail the test. See `docs/TASKS.md` for exact rules.
- Cleanup aborts the regression if tasks/native callbacks remain active, even when
  the test declares an expected exception.
- Task.Delay/Task.Run are not replacements for simulator waits. CPU work can use
  threads, but results need explicit synchronization and native access remains forbidden.
- Verilator cannot preserve X/Z, so unknown writes are rejected. Force/release is
  enabled only for the verified Icarus contract; Verilator and NVC force/deposit
  hold behavior failed that contract, and GHDL force behavior is not established.
- NVC/VHPI exercises the broader VHDL type suite. GHDL/VPI exercises logic, ranges,
  clocks and delta cycles; unavailable type tests are explicitly skipped with reasons.
- Fixed-string NUL and Latin-1 access is verified on NVC/Linux x86-64. Dynamic
  string NUL writes need a length-aware native API and are rejected. HDL strings
  are byte strings; Unicode outside Latin-1 is rejected.
- CoreCLR hosting is implemented for Linux, macOS and Windows. Native Icarus tests
  pass on Linux x64, Windows x64 and macOS ARM64; see [platform evidence](docs/ALPHA.md). Native AOT, commercial validation and mixed-language regression remain open.
- This release has no compatibility promise for its API or native GPI ABI.

## Validation

See `docs/VALIDATION.md`, recorded logs in `docs/evidence`, and the executable tests.
The early single-coroutine prototype's performance ratios do not describe this
framework; a new matched benchmark measures the actual runtime and typed signal API.

## License and upstream

New framework code is MIT licensed, except `NineState.cs`, whose truth tables are
ported from cocotb under BSD-3-Clause. Upstream cocotb source and its license are
preserved unchanged; see `third_party/COCOTB-LICENSE` and `third_party/UPSTREAM.json`.

Version 0.4 adds a [versioned native ABI](docs/NATIVE_ABI.md), startup checks,
registration-failure tests and object metadata. GPI remains pinned and external.

Version 0.5 adds [packed four-state vector transport](docs/PACKED_TRANSPORT.md).
The focused wide-vector microbenchmark improved 22–36× versus the old C# string
path; this is not a whole-simulation or Python-comparison speedup.

Version 0.6 adds [typed packed views and timing checks](docs/PACKED_VIEWS_AND_TIMING.md):
explicit multidimensional indexing, struct-field deposits, phase-aware write
composition, delta-chain parity with Python, and 81 VHDL driver-resolution pairs.
