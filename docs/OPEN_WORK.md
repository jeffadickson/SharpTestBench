# Remaining work toward full cocotb parity

All identified open areas are enumerated as [GitHub tasks](TASKS_INDEX.md).
Issue state is authoritative for execution status; this page preserves scope and context.

The user requested a complete C# implementation. Version 0.8 expands the runnable
core; this list is unfinished work, not optional polish. No claim of full parity
is made. Native simulator adapters are reused, not reimplemented in C#.

## P0 — semantic and lifecycle completeness

- [ ] Audit the public surface item by item against the pinned upstream source.
      `UPSTREAM_API_INVENTORY.csv` lists 302 public definitions, including definitions
      from internal modules. Aliases, dynamic attributes and some exported imports
      require additional review; the inventory is not a completeness percentage.
- [x] Define and test child-scoped cancellation and suppression, task status/exception
      access, named jobs, task combinators and structured task groups (see TASKS.md).
- [x] Typed StartSoon results and aggregate multi-failure reporting, including
      nested groups, cancellation cleanup and duplicate exception suppression.
- [x] Complete upstream task API semantic mapping, typed job-local storage and
      mixed completion/trigger waits. C# cooperative cancellation is
      not an exact substitute for cocotb kill/cancel internals; see TASK_API_MAP.md.
- [x] Expand same-time callback cancellation/reentrancy, native registration errors,
      simulator early termination, multiple failure races, leaked tasks and teardown.
      Covered: 100 cancellation races/run, 256 seeded four-failure teardown groups,
      6,144 synchronization contention jobs, nested multi-failure cleanup, expected-
      exception leak injection, and premature HDL termination. Native registration
      error injection now covers all seven kinds plus 100 failed Timer tasks;
      debugger-based shutdown investigation remains.
- [x] Expand timing comparisons with force/deposit ordering on Icarus and scheduled
      writes/same-time timers against Python on all four backends. Full upstream
      timing-suite parity remains part of the API/conformance audit.
- [x] NVC/VHPI nine-state transport, null and directed ranges, records, nested arrays,
      generics, integers, enums, real and fixed strings (including NUL/Latin-1).
- [x] Two-driver std_logic resolution for all 81 nine-state pairs on NVC and GHDL.
- [x] Twenty three-stage delta transitions per HDL backend; Icarus C# trace matches Python.
- [ ] Broader VHDL constructs, force/deposit ordering and mixed-language regressions;
      GHDL/VPI cannot expose all objects needed by the NVC suite.
- [x] Constant write guards, native integer/enum access, cached named/indexed handles,
      fixed-string NUL support and recursive scope/record/array bindings.
- [x] Explicit multidimensional packed array and struct-field views, direction/index
      checks, sibling write composition and ReadWrite lost-update regression.
- [x] Portable named packed-struct schemas with computed offsets and checked widths.
- [ ] Finish object-model coverage: package/struct simulator matrices, drivers/loads,
      packed struct fields, inferred layouts beyond Verilator, dynamic-string NUL
      and mixed-language traversal. v0.7 adds status-bearing discovery queries,
      native Verilator array dimensions and verified Verilator package access;
      driver/load extraction remains unavailable on the tested backends.
- [ ] Determine simulator-specific capability flags comprehensively. Current known
      restrictions reject X/Z on Verilator and enable force/release only on Icarus;
      NVC failed the force/deposit hold contract. Do not infer
      commercial simulator support merely from the inherited GPI adapter.
- [ ] Test native shutdown and runtime teardown under debugger and fault injection.
      One early local Icarus run printed `fatal library error, lookup self` after its
      result summary despite exit 0. Later clean runs did not reproduce it; root
      cause is unresolved. Fresh v0.2/v0.3 process stress did not reproduce it; this is
      evidence of non-reproduction, not a root-cause fix.

## P1 — package, performance and developer experience

- [x] Versioned C ABI shim, fixed-width integer transport, startup compatibility checks
      and isolated native registration-failure tests (see NATIVE_ABI.md).
- [x] Packed value/X/Z transfer across the C# boundary, buffer validation and
      measured wide-vector conversion improvement (see PACKED_TRANSPORT.md).
- [ ] Direct packed access inside GPI/simulator adapters; the shim still converts
      to/from native binary strings.
- [ ] Real CPU/DSP/AXI model benchmarks and allocation/GC profiling. The current
      benchmark uses a minimal DUT and cannot establish full-project speedup.
- [x] Managed clock period/duty changes at full-cycle boundaries, with edge-time tests.
- [ ] Native clock option; the current driver is managed.
- [x] Recursive elaborated hierarchy generation with nested typed arrays, native
      compilation and execution of generated Verilog/VHDL bindings.
- [ ] Roslyn incremental generator and HDL direction/interface-role annotations.
- [x] Local NuGet package with managed library/native host; isolated package consumer
      compiles and runs on Icarus and NVC. GPI dependencies remain externally installed.
- [ ] Self-contained native dependency distribution and public package publication.
- [ ] Windows/macOS and simulator architecture support; current host is Linux x86-64.
- [ ] Commercial Questa/Xcelium/VCS/Riviera checks when installations/licenses exist.
- [ ] Debugger attachment instructions and actual debugger integration validation.
- [x] Regression stages, explicit stage selection and continue/stop-after-stage policies.
- [x] Typed sampled functional coverage with per-test JSON export; code coverage remains
      outside this initial functional API scope (see COVERAGE.md).
- [x] Exact-name/stage selection, discovery listing, stable selected-case seeds and
      qualified case metadata in JSON reports.
- [x] Structured simulation logs with task/test identity, managed caller locations and
      exception details; native/HDL source mapping is not implied.
- [x] Run-wide VCD capture and linked JSON/JUnit artifacts on all four local backends.
- [ ] Pytest-like fixtures.
- [ ] Native AOT feasibility, reflection trimming and process lifecycle tests.

## P2 — Python-specific interoperability and ecosystem

- [ ] Define whether/how to support Python test reuse. C# is not source-compatible.
- [ ] Port equivalents of bridge/external-thread helpers with deterministic wakeups.
- [ ] Decide equivalents for Python-only conveniences: IPython embedding, assertion
      rewriting, decorators/previews, Python object protocols and environment options.
- [ ] Keep ecosystem libraries (e.g. protocol drivers) separate from core parity; they
      are not automatically ported by implementing the cocotb runtime.

## Completed milestones

- [x] Native host + unmodified GPI integration.
- [x] Multiple tasks, trigger cancellation, phase guards and deferred deposits.
- [x] Typed hardware values and strict signed/unsigned signal APIs.
- [x] Generated bindings; positive/negative compiler checks.
- [x] Icarus and Verilator functional suites; GHDL smoke suite.
- [x] Python timing trace comparison and upstream nine-state truth-table comparison.
- [x] Negative test verifies failed assertions produce CLI failure.
- [x] Benchmark exposed slow bit-string conversion; 32-bit integer path implemented.
