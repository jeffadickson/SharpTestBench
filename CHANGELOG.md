# Unreleased

- Preserved case seeds across selection; added exact-name selection, discovery-only listing and richer result identity (#33).

- Added typed functional coverage bins, goals and per-test JSON export with HDL fixture checks (#29).

- Added reusable completion triggers, typed job-local state and current-job introspection (#4, #5).
- Documented and tested cooperative cancellation/kill boundaries (#2).
- Expanded seeded scheduler teardown and synchronization fairness stress (#6, #40).
- Added regression stages, selection and failure policies with fresh-process CI tests (#32).

- Published repository structure, solution, CI, contribution guidance and GitHub task index.
- Added simulator-thread-checked SimJob.SetName for typed and untyped jobs (#3).

# v0.8.0-alpha

- Renamed CocotbSharp to SharpTestBench, including namespace, assembly, NuGet and native host.
- Added named packed-struct schemas with automatic offset calculation and checked field widths.
- Added managed clock period/duty updates at cycle boundaries.
- Preserved original release evidence; see docs/MIGRATION_0_8.md for breaking identifiers.

# v0.8.0-alpha

- Native Verilator packed-range inference and typed inferred array views.
- HDL roots/packages/drivers/loads queries with explicit discovery status.
- Automatic bit views preserve simulator-visible index direction.
- Real package member and independent HDL packed-selection tests; native metadata boundary/lifetime tests.
- Original packed struct fields and unsupported connectivity remain open; see docs/DISCOVERY.md.

# Changelog

## 0.6.0-alpha

- Add explicit typed packed array/struct-field views with checked layout and indices.
- Compose sibling field deposits with queued whole-vector writes; preserve untouched X/Z.
- Fix the ReadWrite lost-update case found on NVC; retain phase-local submitted values.
- Make write-registration failure leave no pending writes or preview state.
- Verify HDL-side array indexes/member aliases and compiler field-width rejection.
- Add 20-step three-stage delta regressions and exact Python/Icarus trace comparison.
- Verify all 81 ordered std_logic driver-resolution pairs on NVC and GHDL.


## 0.5.0-alpha

- Add packed value/unknown/high-Z transfers across the managed/native boundary,
  eliminating managed binary-string conversion for typed vector reads/writes.
- Negotiate the new ABI feature and reject older native hosts with an upgrade message.
- Validate native buffer lengths, widths, masks, padding, actions and constants.
- Verify independent string/packed comparisons at 73/256/4096 bits, deferred writes,
  nine-state rejection, 18 native boundary checks and installed package X/Z transfers.
- Measure 22.2×/36.4× improvement for targeted 256/4096-bit immediate C# round trips;
  native GPI still uses strings and full-project performance remains unmeasured.


## 0.4.0-alpha

- Route all managed simulator calls through a versioned C ABI; validate the ABI and
  required GPI symbols before scheduling tests. Normalize native integers to int64.
- Expose qualified object names, native type names and optional definition metadata.
- Fix native-host discovery with nonempty simulator library paths; set an ELF SONAME.
- Add isolated failure tests for mismatched ABI, missing GPI exports, all callback
  registration kinds, failed Timer task cleanup and signed 64-bit transport.
- Package the native interface, pinned headers and updated NuGet consumer.


## 0.3.0-alpha

- Typed SimJob<T> returns with nonblocking Result access and aggregate nested-group errors.
- Native signedness now distinguishes unavailable metadata from signed values.
- Explicit/null HDL ranges; native integer/enum adapters; nine-state and fixed-string
  byte/NUL transport; constant write guards and cached child/array handles.
- Recursive generated scope/record/array bindings and a four-simulator test matrix.
- NVC VHPI runner and explicit simulator requirements/skipped-test reasons.
- Early HDL termination produces failing JSON/JUnit; native host validates its environment
  and uses atomic output replacement during builds.
- Local NuGet package includes the Linux native host; isolated consumers execute on
  Icarus and NVC. Runner accepts custom test DLLs, HDL sources, tops and entry types.
- Force/release restricted to the verified Icarus contract after the NVC probe failed.
- Full portability, upstream conformance and native ABI improvements remain open.

## 0.2.0-alpha

- Fix child cancellation: implicit trigger waits now use an AsyncLocal task scope,
  and nested jobs link to the caller's token as well as the test token.
- Make omitted cancellation and explicit CancellationToken.None distinguishable
  in AsTask, ClockCycles, semaphore, lock and queue APIs. These signature changes
  require recompilation of consumers; alpha releases do not promise binary stability.
- Add names, completion status and exception access to SimJob.
- Add non-owning SimTasks.First, Combine and simulation-time WithTimeout, including
  a generic timeout overload retaining Task<T> results.
- Add TestContext.RunGroup with sibling cancellation and joined scope cleanup.
  A handled group exception does not fail its enclosing test. Publish child failure
  before task completion so cleanup exceptions cannot escape group supervision.
- Prevent ExpectedException from masking leaked tasks/native callbacks.
- Expand Icarus/Verilator suites to 26 passing cases plus one deliberate skip;
  retain three GHDL smoke cases and 1,826 value assertions.
- Add a repeatable 20-process cancellation/shutdown probe and negative leak check.
  No recurrence of the earlier native diagnostic; root cause remains open.
- Retain v0.1 benchmark results as historical measurements, not v0.2 claims.

## 0.1.0-alpha

Initial runnable managed runtime, typed values/signals, simulator triggers,
primitives, regression runner, generated bindings, pinned native GPI host,
three-simulator checks, Python timing parity probe and performance experiments.
