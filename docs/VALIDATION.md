# Unreleased validation update

Task API/stage work: 42 passing integration tests on each of Icarus 11 and
Verilator 5.048, 1,846 managed assertions, six fresh-process stage checks,
compiler checks, native fault tests and Python timing parity. Independent
[GitHub CI](https://github.com/jeffadickson/SharpTestBench/actions/runs/38596631139)
passed on Icarus 12. Evidence is in `docs/evidence/task-stages`.

Functional coverage adds one HDL fixture per backend (43 passed and one deliberate
skip each) and raises managed assertions to 1,860. Exact exported sample counts,
goals and test/seed identity are checked by tools/coverage_check.py. Evidence is
in `docs/evidence/functional-coverage`. VHDL and package validation below remains
historical release evidence, not a claim of rerunning those configurations here.

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

Commercial simulators, Windows/macOS, Native AOT,
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
