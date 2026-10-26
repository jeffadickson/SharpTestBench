# Endurance workflow validation — 2026-10-26

## Final outcome

The workflow is implemented and locally qualified on Windows/Linux x64. Clean
standalone source942e603 passed both801-case baselines, StrictDoc, mutation and
randomized campaigns, scoped coverage, data-only local-bare publication, passive
review and checksum-bound import. The final source delta adds complete bounded
PowerShell review output and safe Git outcome diagnostics; its tests/review are
recorded below. No hours-long run or schedule was started, and no hosted CI ran.

| Final measurement | Windows x64 | Linux x64 |
| --- | --- | --- |
| Core / Generators / Tools inventory |669 /17 /1384;12 Tools skips|669 /17 /1384;no skips|
| Retained contracts |948 passed /3 skipped|951 passed|
| Unique baseline total |3006 passed /15 skipped|3021 passed|
| PowerShell boundary cases |10 passed|10 passed;partial Pester OS metadata|
| Merged four-assembly lines |7566/11565 (65.42153047989623%)|7567/11565 (65.43017725897103%)|
| Merged four-assembly branches |4687/8819 (53.14661526250142%)|4711/8819 (53.418754960879916%)|
| Clean480-second run batches / properties / malformed inputs |30 /9000 /6000|51 /15300 /10200|
| Selected Stryker outcomes in successful clean run |43 killed /6 ignored|43 killed /6 ignored|
| Clean-run scoped lines / branches |739/849 /729/1072|739/849 /730/1072|

Repeated coverage/Runner-subset executions are not added to the unique baseline
totals. Merged coverage requires the four production assembly names and includes
generated code plus the documented retained/selected Runner CLI collections.
Scoped Endurance coverage is a different measurement. Neither measures HDL,
NativeAOT instructions, all runtime switches or complete CLI success behavior.
The previous failed/partial attempts below are retained as failed/partial evidence.

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
instruction coverage or HDL coverage. Linux standard collection was pending at that checkpoint; final results are recorded below.
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

## Pending qualification / delivery at the first checkpoint

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

## Standalone findings after checkpoint41bdf3c

Linux standard four-assembly collection completed:7567/11565 lines
(65.43017725897103%) and4711/8819 branches (53.418754960879916%). Counters and
assembly sets were validated by the existing collector. Cross-collection emitted
inventories differ; these values are not a normalized platform comparison.

Clean standalone clones of41bdf3c built the full Release solution and exported
requirements successfully on both OSes, then correctly rejected baseline success
because Core's full discovery JSON exceeded the128KiB diagnostic retention cap.
Both reported baseline-failed with zero completed campaign work. They published
valid failure-only JSON/checksums to temporary **local bare remotes**, not GitHub:

- Windows run36e3d86ed8094ebabfc0ba1928e9ed05,
  result commitf083e392befe40b806dade6490eebecb59a32581.
- Linux run036e4a6b184c4c23a7feff9bd99cd60e,
  result commit9ed8cfa987201ef94f8fc6d1928b0ce09ad6c32d.

Windows result-tree inspection confirmed exactly two allowlisted files and its
temporary remote main remained41bdf3c2b0a0e642ddea63312015d3e47470a283. No success
was inferred from these failed baselines.

The fix gives full structured discovery its own16MiB capture budget while keeping
strict inventory validation. A real-child regression emits1250 valid cases above
128KiB, proves the ordinary prefix truncates, and parses all1250 with the new
bound. It passed on Windows and Linux. The focused inventory is now115 cases.

Linux execution of the nine PowerShell boundary cases also found multiple
`setsid` matches on PATH. Selecting one ApplicationInfo fixes FilePath binding;
the exact-source native fixture rerun passed all9 cases, including actual Linux
session-separated background execution. The existing Pester3.4.0 was reused from
its installed files, with TEMP set to the actual native temp directory. Pester
warned that its OS metadata was partial; no full system-profile claim is made.
Actual terminal-disconnect survival remains untested. A read-only deep source
review accepted both fixes without actionable findings. Positive fresh standalone
reruns are the next qualification step; no GitHub push has occurred yet.

## Later transport and load findings

Passive review and checksum-bound import of the real Windows failure commit
f083e392 succeeded without checkout. The imported report remained failed with
zero batches, `Imported=true` and `ImportReexecutedChecks=false`. Its original
evidence SHA256 was9de973871fb5e1453c175a09a8028186740b0491c401b6c84a3ac0afa30a3a5e.

The PowerShell Review wrapper also needed a larger bounded structured-response
envelope than its diagnostic prefix. It now permits1MiB for the persisted512KiB
report and rejects truncation before printing partial JSON as success. Actual
wrapper review returned185980 complete bytes and parsed the failed report.
The ten-case PowerShell suite passed on Windows and Linux, including a200000-byte
response control. Ordinary diagnostic capture remains128KiB by default.

A concurrent Windows standard-coverage attempt on the corrective source failed
five temporary Git fixture operations. It emitted no accepted completion summary.
An extra validation attempt was stopped before collection to reduce overlapping
work; available physical memory was approximately1.2GiB on this16GiB machine.
This is observed load context, not a proven explanation of the five failures.
Git errors now include exit/timeout/interruption/truncation/settlement flags
without raw stderr. The serial passing rerun and final totals are recorded below.

The first clean Linux corrective-source campaign passed its801-case baseline and
StrictDoc, completed11 randomized batches, but recorded42 selected mutants killed,
6 ignored and1 timed out. It correctly finished failed (stryker-findings), with
valid failure evidence published only to its local bare remote as
399a5f66efe09f0ab34074ae99023ecb3aca8dde. That timeout is not a killed mutant.
A fresh retry uses the same source/settings with less competing validation work;
its result must be recorded rather than assuming success.

## Clean Windows standalone qualification

Corrective source942e6033927c05197bfca4fd2195ace02dd94bec completed the full
standalone command on Windows x64, including clean clone/access preflight,
full Release build,801 selected baseline cases without skips, StrictDoc export,
campaign, scoped coverage and local-bare publication. Run
07c12b1935424420853bb64661b19fb7 requested480seconds and recorded480.115225;
30 verified batches produced9000 property cases and6000 malformed inputs.
Initial seed20261028 advanced to9980051252944286490. Stryker recorded43 killed,
6 ignored and no selected findings; six supplemental deletions were killed.
The raw Stryker SHA256 is
b48d0847086e53f802d023f2206855fbb08c8347ea04c58e449a84d7cdc75608.

The preserved snapshot remained unchanged, logs were not truncated, and coverage
was739/849 lines and729/1072 branches in the stated Endurance scope. Setup was
9.8616951seconds and coverage139.8734335seconds, both outside campaign timing.

Result commitbd634eb4f636853aee90ae90daded294c1fd66f3 has exactly its summary
and checksums; temporary remote main remained942e603. Passive review and import
both passed. The imported copy kept30 completed batches and explicitly recorded
no reexecution. Original summary SHA256:
1fadfbd25420720e3c855783c5c696cac237d6f70cf259a2db17055ff1de953e.
The exact portable files are saved under
[docs/evidence/endurance/windows](../evidence/endurance/windows/summary.json).
Raw logs, paths, binaries and corpora remain local. No result branch was uploaded
to GitHub during this qualification.

## Clean Linux standalone qualification

The quieter retry used the same corrective source942e603 and seed20261028,
without changing mutation thresholds or counting the earlier timeout as killed.
Run9abd3e68b4494cd09f659b422c11d8ea passed the clean-clone/access preflight,
full Release build,801 baseline cases without skips and StrictDoc. It requested
480seconds and recorded480.1141971, with51 verified batches,15300 property cases
and10200 malformed inputs. Next seed9587389500507283667. The unfinished final
batch was excluded from counts.

It recorded43 selected Stryker kills,6 ignored and no selected findings; six
supplemental deletions were killed. Raw Stryker SHA256:
63fd391e3e715b109456db2af3ed642c7e0ff3309667d4bf491d218d3fcb734e.
Source consistency remained true and logs were not truncated. Scoped coverage
was739/849 lines and730/1072 branches. Setup1.8276799seconds and optional
coverage13.8329401seconds were outside campaign timing. These timings are not
a native/managed or cross-platform benchmark.

Result commit9211665cd6fcfd2c7aca5c3fa17e9e8bb55979c8 contains exactly two
allowlisted JSON files and temporary remote main remained942e603. Passive review
and checksum-bound import passed, preserving51 batches with no reexecution.
Summary SHA256:c98b225c6b57717319b84446db82d81c332044a240472df5080b3665acf43845.
Exact portable files are under
[docs/evidence/endurance/linux](../evidence/endurance/linux/summary.json).
SDK10.0.402, Stryker5.0.0, FsCheck3.6.0, Coverlet10.1.0 and StrictDoc0.30.4
were recorded on both OSes; per-report Git/PowerShell/Python versions are retained.

The Windows and Linux clean runs used:

```text
pwsh -NoProfile -File tools/Remote-Endurance.ps1 -Repository <temporary-local-bare> -Workspace <new-dedicated-path> -SourceRef 942e6033927c05197bfca4fd2195ace02dd94bec -Duration 00:08:00 -Seed 20261028 -PropertyCases 100 -FuzzCases 200 -MachineLabel <standalone-windows-or-linux> -Python <verified-existing-python> -StrictdocExecutable <verified-existing-strictdoc>
```

The existing parent directories were checked, destinations rejected if present,
and local authentication/system tools were reused. These are actual local-bare
transport tests, not GitHub-hosted runs or a claim that every fresh-install path
was exercised.

## Final validation and review disposition

The final Windows serial collector exited0 and emitted an accepted four-assembly
completion summary after all1384 Tools cases (1372 passed/12 skipped), the951-case
retained collection and both existing Runner collections passed. Its counters
were7566/11565 lines and4687/8819 branches. Linux's accepted corrective-source
collection executed1384 Tools cases without skips and measured7567/11565 lines
and4711/8819 branches. Earlier different-denominator collections remain historical
snapshots, not replacements for these final totals.

Independent final agent source/evidence review checked both portable checksums,
source/run IDs, counts, seeds,801-case baselines, mutation outcomes and coverage
scope. It found no new actionable source issue and requested removal of stale
pending wording; this final section and leading table resolve that finding.
Review was read-only source/evidence inspection, not external-expert participation
or an independent execution attestation.

Requirements STB-END-001..009 have implementation/test traceability and finite
qualification evidence, while remaining Active obligations. No blanket closure
is claimed for hours-long reliability, unexecuted fresh SDK extraction, fresh
Windows StrictDoc installation, Debian12, ARM64, terminal-disconnect survival,
coverage-guided fuzzing, whole-project mutation strength, HDL or NativeAOT scope.
No new native publication or native/managed benchmark was run. The established
Markdown/XMLDoc/StrictDoc documentation system was extended; no repository-wide
documentation-system migration was performed.

Local source checkpoints are41bdf3c (implementation) and942e603 (discovery/detach
correction). The final commit contains this completed evidence, portable blobs,
review-output fix, diagnostics and updated prompt.md. Delivery is verified by the
subsequent normal push, not by this statement of intent. CI must remain disabled
and PR62 must remain untouched.
