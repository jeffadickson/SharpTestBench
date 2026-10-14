# Contributing to SharpTestBench

Start with [README](README.md), the [documentation index](docs/README.md), and
[tracked tasks](docs/TASKS_INDEX.md). Choose an issue before expanding scope.

## Repository layout

| Directory | Purpose |
|---|---|
| src/SharpTestBench | Managed types, scheduling, handles, discovery and test runner |
| native | CoreCLR host and versioned C ABI shim |
| tests/SharpTestBench.Tests | Simulator-independent assertions |
| tests/Integration | SystemVerilog integration and Python timing reference |
| tests/VhdlIntegration | VHDL integration fixtures |
| tests/Benchmark | Matched microbenchmarks |
| tools | Build, packaging, compiler checks and diagnostic runners |
| docs | Design notes, compatibility, task index and historical evidence |
| third_party | Pinned upstream source and license/provenance records |

Open SharpTestBench.slnx with a .NET 10-capable IDE. Run `dotnet build
SharpTestBench.slnx -c Release` and `dotnet run --project tests/SharpTestBench.Tests
-c Release`. Native tests require Linux x86-64 and the dependencies in README.

## Change and review rules

Use a focused branch and reference the relevant issue. Include the problem,
behavior change and actual validation results in the PR. Keep generated build
output out of git; retain intentional evidence snapshots under docs/evidence.
Never store credentials, private DUT files or license material in test artifacts.

Simulator calls require the simulator thread. Do not block it waiting for an
unfinished task. Test cancellation, phase rules, unknown values and native ABI
failures when changes affect them. Preserve upstream notices and pin provenance.

Close an issue only when its completion criteria are met. An unavailable backend
is not a passing feature test. Mark installation/license dependencies as blockers.
Historical benchmark figures are not evidence of current whole-project speedup.
CI currently covers Linux unit/build and Icarus; NVC, GHDL, Verilator, commercial
simulators and other OSes require separate recorded validation.
