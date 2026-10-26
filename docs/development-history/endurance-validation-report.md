# Endurance workflow validation — 2026-10-26

This report records actual local work, not another iteration batch. Requirements
STB-END-001 through STB-END-009 remain Active obligations. Implementation and
execution evidence do not establish exhaustive adequacy or broader closure.

## Implementation checkpoint

The new C# endurance commands provide serial monotonic budgeting, immutable input
snapshots, bounded processes/logs, checkout exclusion, atomic checkpoints and
dead-owner recovery. PowerShell supplies existing-tool discovery, pinned isolated
setup, foreground/background launch and a dedicated clean-checkout workflow.
Git transport creates report-only unique refs through a private index; import
and passive review validate checksums and source identity without running branch
contents. See [ENDURANCE.md](../ENDURANCE.md) for commands and limitations.

Stryker.NET5.0.0 is the primary mutation engine. A private xUnit2.9.3/VSTest driver
source-links production evidence rules/models; the existing xUnitv3 suites stay
unchanged. Explicit guard-deletion probes are supplemental, not the primary
mutation score. FsCheck3.6.0 supplies property replay/shrinking; malformed regex
generation is deterministic randomized testing, not coverage-guided fuzzing.

## Actual local validation before the first checkpoint

Both native-filesystem checkouts used SDK10.0.402 with locked restore and separate
Windows/Linux outputs. Windows' system SDK had changed to10.0.403; the working
profile-local10.0.402 was reused. The first doctor attempt lacked existing native
tools on PATH; selecting the existing development environment corrected that.
Working tools were not reinstalled wholesale. Linux installed the hash-locked
isolated StrictDoc0.30.4 environment; its working SDK and Python were reused.
Fresh SDK download/extraction and fresh Windows StrictDoc installation have not
been executed at this checkpoint.

Release builds passed on both OSes. Full managed inventories were Core669,
Generators17 and Tools1379 before the final four review regressions; Windows
Tools had12 Unix skips, Linux none. Retained contracts:951 total, Windows948
passed/3 skipped and Linux951 passed. The reviewed Tools inventory grew to1383;
Linux `make test` passed it, and Windows' standard coverage collection executed
1371 passed/12 skipped. The focused reviewed C# suite114 passed on Windows.
Nine Pester3.4.0 cases passed on Windows/PowerShell7.6.5, including existing-log
preservation, linked Stop targets and rejecting invalid standalone seeds before
clone. StrictDoc0.30.4 export passed with source trace pages.

Commands executed from the checked source directory, after selecting the existing
development environment and pinned SDK/Python, included:

```text
dotnet build SharpTestBench.slnx -c Release -p:RestoreLockedMode=true -p:NoWarn=CS1591 --disable-build-servers -m:1
dotnet build tools/Endurance/Endurance.slnx -c Release -p:RestoreLockedMode=true -p:NoWarn=CS1591 --disable-build-servers -m:1
dotnet tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll doctor
dotnet tests/SharpTestBench.Tests/bin/Release/net10.0/SharpTestBench.Tests.dll
dotnet tests/SharpTestBench.Generators.Tests/bin/Release/net10.0/SharpTestBench.Generators.Tests.dll
dotnet tests/SharpTestBench.Tools.Tests/bin/Release/net10.0/SharpTestBench.Tools.Tests.dll
python -m unittest discover -s tests/Tooling -q
make test PYTHON=python
pwsh -NoProfile -File tools/Endurance.ps1 -Duration 00:10:00 -Seed 20261026 -PropertyCases 100 -FuzzCases 200 -MachineLabel optiplex-windows
pwsh -NoProfile -File tools/Endurance.ps1 -Duration 00:10:00 -Seed 20261026 -PropertyCases 100 -FuzzCases 200 -MachineLabel optiplex-linux
dotnet tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll code-coverage
```

Windows used its explicit existing venv Python executable for retained contracts;
Linux `make test` used the activated existing venv's `python`. Focused Windows
execution added `-class Endurance* -xml <local-output>`; all-suite invocations
also retained XML/log outputs. PowerShell checks used
`Invoke-Pester -Script tests/Endurance/Launcher.Tests.ps1 -PassThru` with failure
counts checked. StrictDoc export used `export docs/requirements --output-dir
build/endurance-strictdoc --no-parallelization` through the existing pinned
StrictDoc executable. Output directories were checked before use.

Local Git integration tests use owned temporary bare repositories, never GitHub.
They cover successful/failed upload, retained retry refs, unchanged main/index,
import binding, malformed/duplicate fields, source/hash/path/lifecycle rejection,
checkout overlap, real sharing contention and dead/live owner recovery.

## Real short campaigns

These runs used dirty development snapshots based on0f37b1e; they are local
execution evidence, not clean-source publishable reports. Their preserved JSON
contains the exact tested source-file hashes and tool identities. The subsequent
clean standalone qualification is recorded separately when completed.

| Platform/run | Requested/actual campaign seconds | Verified batches | Property cases | Malformed inputs | Outcome |
| --- | --- | --- | --- | --- | --- |
| Windows x64 `6f25364d7d974bd395ff142c4903dece` |600 /600.1644051|22|6600|4400|completed-budget|
| Linux x64 `22f8cee662ce4d669180c353cf696ed2` |600 /300.4818064|0|0|0|partial: snapshot build child timeout|
| Linux x64 `c51b4bea86644a2fa03b6e9c049ac83b` |600 /600.5215536|21|6300|4200|completed-budget; unfinished final batch excluded|

All used initial seed20261026 and100 cases per property plus200 malformed inputs
per complete batch. Three properties run per batch. Next seeds were
11008053066906456688 (Windows) and18054082321292809819 (Linux successful retry).
No failing randomized counterexample was observed; no minimization success is
claimed. Verified counts exclude unfinished final work.

Both successful runs recorded43 selected Stryker mutants killed,6 ignored,
zero selected surviving/invalid/timed-out/uncovered/pending mutants. There were
653 excluded mutants, including42 compilation-invalid ones outside selection;
they are not selected kills. All six supplemental guard deletions were killed.
Raw Stryker report SHA256:

- Windows: `b342a34c427483b6b2aabbba64dc792b223246ea273e5dd0f122afb6216ac6f7`
- Linux: `69202fccc39a89d7d7afeed2a0efc863d6e47c0038e938ca8836bb95c9717709`

The first Linux full-solution snapshot build hit its300-second child deadline.
A focused campaign solution now compiles the required Core, Tools test dependency
graph, inherited-pipe fixture and isolated mutation driver. The full-product
baseline remains separate and unchanged in scope. The retry passed snapshot
compilation in the first approximately74 seconds of its campaign. This is a
scope/timing observation, not a claimed comparable performance benchmark.

Optional coverage ran outside the budget: Windows180.0265842seconds;
Linux87.3677859seconds. Both source snapshots remained unchanged and neither
reported truncated logs. The unsuccessful Linux attempt retained unavailable
coverage rather than inventing counters.

## Scoped coverage at these snapshots

| Snapshot | Covered/valid lines | Covered/valid branches |
| --- | --- | --- |
| Successful Windows campaign |623/716|704/1098|
| Successful Linux focused-graph campaign |739/849|728/1070|

Scope is production `SharpTestBench.Tools.Endurance*` through unit/integration
tests. Snapshots and emitted line/branch inventories differ; these percentages
are not used as a platform comparison. Earlier preliminary Linux scope was
600/711 lines and690/1090 branches, before review corrections. None of these is
the merged four-production-assembly report. PowerShell/F# source, live child
campaign instructions, other production types, HDL and NativeAOT instructions
are outside this scoped measurement.

Windows standard four-production-assembly collection completed after the final
C# review fixes:8232/12634 lines (65.15751147696692%) and4752/8987 branches
(52.87637698898409%). It requires Core, Generators, Tools and Runner, including
the existing retained-contract and selected production Runner CLI failure paths.
It does not establish complete CLI success/runtime-switch coverage, NativeAOT
instruction coverage or HDL coverage. Linux standard collection is still pending.
Generated code contributes to this denominator; do not compare it directly with
the narrow Endurance scope or historical denominators.

## Reviews and dispositions

Independent agent source reviews were read-only software reviews, not external
experts or independent execution attestations. Accepted findings were fixed:

- Cancel pending inherited-pipe I/O before closing handles; real fixture test.
- Valid initial tool placeholders and finished setup failure/interruption;
  missing-SDK, malformed-baseline and early-stop regressions.
- SDK symlink discovery, isolated StrictDoc reuse, Linux session separation and
  unique launcher-attempt logs.
- Contained/no-link Stop markers with target-byte preservation tests.
- Coverage startup exceptions and returned cancellation results finalize honest
  lifecycle status while preserving verified counts; injected regressions.

Additional observed fixes cover comparer-independent source inventories,
Windows sharing-denial retry, removing the pre-shrink input protocol prefix and
PowerShell comma-expression grouping for log paths. The final source follow-up
reported no actionable findings. Pester's existing3.4.0 `Should Throw` assertion
needed an explicit expected message; the direct log-preservation control and
the final nine-case suite passed.

Initial Stryker experiments were not qualification: full-helper instrumentation
hit a short child limit, and a private xUnitv3 driver left10 obvious guard
mutants surviving despite passing its35 baseline cases. The isolated source-
linked xUnitv2 driver then killed10 selected one-guard mutants before the later
all-six-guard runs recorded above. This is an observed compatibility choice for
the pinned tool configuration, not a general claim that xUnitv3 is defective.

## Pending qualification / delivery at this checkpoint

The implementation is not yet pushed. Linux standard merged coverage, clean dedicated
standalone runs against local bare remotes, passive review/import checks on that
real evidence, and final evidence review remain to be recorded. Fresh SDK install,
Windows fresh StrictDoc install, Debian12, ARM64, terminal-disconnect survival and
hours-long stability are not executed qualification claims. No hours-long run or
recurring schedule was started. No new native publication, HDL simulation or
native/managed benchmark was run for this workflow change.

Read-only GitHub checks confirmed all12 workflows disabled_manually and PR62
open/draft/unmerged at0318a864d9b570f9ffbc4857e7365ec9ed6a4736. No dispatch or PR
mutation occurred. Source development commits include root prompt.md; automated
data-only result commits contain only the allowlisted JSON/checksum files.
