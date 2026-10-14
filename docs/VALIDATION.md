# Unreleased validation update

Fixture lifecycle: 364 managed xUnit cases and six fresh-process cleanup scenarios.
Normal cleanup runs on all four available backends; errors and noncooperative
callbacks are injected on Icarus. See FIXTURES.md.

Managed test migration: xUnit v3 3.2.2 runs 361 named cases preserving the
previous 1,916 checks. The isolated mutation probe reports exactly one named
failure and 360 passing cases. Run `python tools/managed_tests.py` after building.

Capability checkpoint: 1,916 unit assertions; Icarus 47 passed/1 skipped;
Verilator 46 passed/2 skipped; GHDL 12 passed/6 skipped; NVC 17 passed/1 skipped.
The native fault suite additionally rejects five raw/typed nonbinary and force/release
operations on an unknown product. All four backends export complete capability
catalogues. See CAPABILITIES.md and docs/evidence/capabilities.

Timing/waveform checkpoint: Icarus 46 passed/1 skipped; Verilator 45 passed/2 skipped;
GHDL 11 passed/6 skipped; NVC 16 passed/1 skipped. New force cases run only on Icarus;
other added skips explicitly record that limit. Scheduled-write and equal-deadline
traces exactly match fresh pinned-Python runs on all four backends. VCD signal
values and JSON/JUnit artifact links are checked, including failing tests and
disabled capture. Evidence: docs/evidence/timing-waveforms.

Structured logging: 44 passing integration tests (one deliberate skip) on Icarus
11 and Verilator 5.048. logging_check.py validates identities, source locations,
exceptions, escaped multiline messages and bounded output fields; stage tests verify
failed-test events. The same updated runtime passed NVC (15 tests) and GHDL
(10 tests with five explicit backend skips). Evidence: docs/evidence/structured-logging.

Selection follow-up: tools/stage_tests.py now runs nine simulator processes,
including exact/multiple names and discovery-only mode; filtered case seeds are
checked against complete discovery order. Evidence: docs/evidence/selection.

Task API/stage work: 42 passing integration tests on each of Icarus 11 and
Verilator 5.048, 1,846 managed assertions, six fresh-process stage checks,
compiler checks, native fault tests and Python timing parity. Independent
[GitHub CI](https://github.com/jeffadickson/SharpTestBench/actions/runs/38596631139)
passed on Icarus 12. Evidence is in `docs/evidence/task-stages`.

Functional coverage adds one HDL fixture per backend (43 passed and one deliberate
skip each) and raises managed assertions to 1,860. Exact exported sample counts,
goals and test/seed identity are checked by tools/coverage_check.py. Evidence is
in `docs/evidence/functional-coverage`. The release table below remains historical;
VHDL was rerun for the structured-logging checkpoint above. Package consumer
validation was not rerun in this batch.

# Validation record — SharpTestBench v0.8.0-alpha

Linux x86-64, .NET SDK 10.0.402/runtime 10.0.13, Python 3.12.14. Pinned cocotb:
`5fa952e92c46fd9cb58f55a8655312a4bf02e7de`. Test reports are in `docs/evidence/v0.8`;
older evidence remains under the earlier release directories.

| Check | Result |
|---|---|
| Value, logic truth table, range/array and RNG tests | 1,846 assertions passed |
| Icarus Verilog 11.0 integration | 36 passed; 1 deliberate skip |
| Verilator 5.048 integration | 36 passed; 1 deliberate skip |
| GHDL 2.0/VPI | 10 passed; 5 explicit backend-requirement skips |
| NVC 1.23.0/VHPI | 15 passed; no skips |
| Isolated NuGet consumer with recursive generated bindings | Compiled without source/project reference |
| NuGet-installed managed/native assets on Icarus | Package test passed |
| NuGet-installed managed/native assets on NVC | Package test passed |
| NuGet-installed native layout inference and package discovery on Verilator | Package test passed |
| Wrong widths and signedness | Generated bindings compile; wrong widths/signs and packed-field widths rejected |
| Python cocotb comparison | Edge/NBA/ReadOnly plus 20 multi-NBA chain transitions and coalesced deposits match |
| Repeated Icarus lifecycle probe | 22 passed in v0.6; not rerun in v0.7/v0.8 |

New v0.6 evidence covers explicit packed object views with HDL-side indexing
checks, 20-step three-stage delta chains, all 81 ordered nine-state driver pairs,
ReadWrite field composition, and failed-write registration cleanup. The first
ReadWrite field test failed on NVC and led to the phase-local preview fix described
in PACKED_VIEWS_AND_TIMING.md. All final regression runs below pass.
The v0.5 packed/string benchmark remains historical and was not rerun in v0.6 or v0.7.

All four suites exercise the versioned native ABI. The added object metadata
case passes on each backend. ABI mismatch and missing-symbol failures, all seven
callback registration failures, 100 failed Timer tasks, and a signed native integer
outside int32 are tested in isolated processes (`native-faults.json`).

Development caught and fixed native-host search-path dependence on an empty loader
path entry. NVC also returns uppercase qualified names; the VHDL test now accepts
backend-specific case. Optional definition metadata remains nullable.

The task suite includes typed returns, premature Result access, cancelled
results, nested groups with two cleanup failures, and deduplication when a failing
child is awaited. VHDL tests add records, nested arrays, integer/enum/real/string
round trips, generics and constant guards, native nine-state values, null ranges,
and embedded NUL/Latin-1 strings. Both generated binding trees compile in a separate
NuGet consumer and execute native reads/writes on their respective simulators.

GHDL/VPI does not expose all objects needed for those type tests. Its five skips
are recorded as requirements, not counted as passes. NVC force/deposit testing did
not satisfy the force-hold contract: the final suite verifies rejection. Only
Icarus currently enables force/release. A native signedness return of -1 is now
reported as unavailable instead of being misinterpreted as signed.

The lifecycle script requires the intentionally leaking test to fail even with an
ExpectedException annotation. It also terminates HDL before the selected test can
finish and requires an explicit failing simulator-termination result and CLI exit 1.
No `fatal library error` occurred in the repeated probe. The earlier intermittent
shutdown diagnostic still has no proven root cause. Native host output replacement
is now atomic so a new build does not truncate a library mapped by a running process.

## Historical v0.1 full-runtime performance

These measurements were made before the v0.2–v0.7 changes; performance
against Python has not been remeasured for v0.7. Five processes per language in alternating order. Median seconds within measured
regions, excluding build/startup. Each run warms 10,000 timer waits. .NET Release
build, tiered compilation disabled. Both implementations use deferred deposits.
Every round-trip value is checked and the final checksum is identical.

| Workload | Python cocotb | SharpTestBench | Python / C# |
|---|---:|---:|---:|
| 100,000 timer waits | 0.308491 s | 0.041253 s | 7.48× |
| 100,000 typed uint32 write/wait/read/check cycles | 0.934873 s | 0.158837 s | 5.89× |

These measure the new runtime, not the earlier single-coroutine prototype. They
still use a minimal DUT and a small subset of cocotb features; they are not a
prediction of real project speedup or proof of full semantic parity. No CPU pinning,
independent machines, allocation profiling, or confidence intervals were used.

The first full-runtime typed implementation took approximately 1.32 seconds in the
round-trip benchmark, versus 0.93 seconds for Python. The result exposed avoidable
BigInteger/bit-string conversions. A fixed-width 32-bit native write path and
binary parsing for known <=64-bit reads fixed that bottleneck. Unknown-value reads
still reject conversion; the optimization does not silently turn X/Z into numbers.
`benchmark-before-integer-fastpath.json` preserves the original result.


## Remaining validation gaps

The native Icarus suite now passes on Windows x64 and macOS ARM64 as well as Linux
x64; see ALPHA.md for CI evidence. Cross-platform packaging remains open.
Commercial simulators, Native AOT,
debugger-based shutdown investigation, broader multi-driver/mixed-language cases,
full upstream timing conformance and realistic-project performance remain open.
The local NuGet contains the Linux native host, but not the simulator or pinned
GPI libraries. No package was publicly published. See OPEN_WORK.md for both code
work and external-environment gates; there is no full-parity claim.

## Reproduce

Use README.md commands. `tools/lifecycle.py` follows an Integration build.
`tools/package.py` follows Icarus and NVC runs so both generated binding files
exist. The package-consumer DLL can then be selected using `run.py --assembly`,
with `--host` pointing to its installed runtimes/linux-x64/native host.

## v0.7 discovery checks

See DISCOVERY.md for the observed per-backend matrix. Verilator discovers and
reads a real HDL package member and exposes three packed ranges through the new
optional native VPI extension. Inferred writes match independent HDL aliases,
including negative and ascending indices. Boundary checks reject bad capacity,
missing objects and mismatched widths; 100 repeated scans exercise iterator
release. Bit views are exercised on all four simulators, with GPI fallback on three.

Unavailable or unsupported driver/load metadata is recorded, not counted as
verified connectivity extraction. Packed struct field-name inference remains
open. An initial package-member test assumed GPI Integer; Verilator exposes that
member as a packed vector, so the test now accepts and validates the native kind.

## v0.8 rename, structs and clock

The active namespace, assembly, NuGet, props and host library are SharpTestBench.
All four simulator suites and three isolated package consumers passed with the
new identities. Historical evidence retains the original project name.

Named struct fields match an independent HDL payload alias on Icarus/Verilator
and vector overlays on NVC/GHDL. Unit tests reject invalid schema declarations.
The managed clock test verifies edge times before and after a duty/period change
and checks invalid updates leave timing intact. Its first fixture used the DUT
functional clock and disturbed a later test's initial sampled value; it now uses
a dedicated HDL signal. This was test isolation, not a scheduler timing change.

## Incremental generator and platform packages — 2026-10-14

[CI run 38626020220](https://github.com/jeffadickson/SharpTestBench/actions/runs/38626020220)
passed on Linux x64, Windows x64 and macOS ARM64. Each job ran 364 managed cases,
10 generator cases, native Icarus integration, the generated counter, concurrent
workspace success/failure isolation and occupied-directory rejection, and a fresh
NuGet-cache consumer outside the checkout using packaged host/GPI/Icarus files.
See GENERATOR.md, PACKAGING.md and WORKSPACES.md. Packages remain alpha candidates,
not published releases or proof of other simulator/architecture combinations.

A separate local lifecycle run passed 22/22 including two intentional failure
cases. Debugger attachment remains blocked as documented in DEBUGGING.md; no
debugger success or shutdown root-cause fix is claimed.

## Base-trigger semantic review

Nineteen inventoried definitions reviewed against pinned _base_triggers.py; see
API_REVIEW.md and API_REVIEWS.json. Source hashes and evidence anchors are checked
in CI. PrimitiveEventContract and PrimitiveLockContract pass on local Icarus,
Verilator, GHDL and NVC; Icarus also passes the existing 6,144-job fairness stress. IsLocked
includes reserved handoff state. These are scoped contract tests, not complete
upstream API equivalence.

Timer is also reviewed (20 total decisions; 282 inventory definitions remain).
TimerRoundingMinimum covers positive substep clamping and ties-to-even rounding.

Linux CI job 116071363888 in run 38627816208 passed both DEBUGGER_PASS and
NATIVE_DEBUGGER_PASS: managed source/local/exception checks and separate GDB
startup/finish breakpoints with normal simulator exit. The historical fatal
diagnostic was not reproduced; simultaneous mixed debugging is not claimed.
