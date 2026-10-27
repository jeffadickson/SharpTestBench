# WP-01 and WP-02 qualification — 2026-10-26/27

WP-01 and WP-02 passed their bounded Windows x64 / Ubuntu24.04 x64 gates.
This is fresh execution, not a restatement of historical selection200/endurance
results. It does not close STB-041, STB-007, STB-END-004 or other broader Active
StrictDoc obligations. WP-03 through WP-07 remain open.

Execution started on main at c70bc4cca97d8eee884b08516d8fe836faf95beb. Testing then
found and fixed a production process-context defect and documentation-export
issues. The final candidate is identified by the raw source hashes and twelve
Git source-blob IDs in each [qualification record](../evidence/work-packages-01-02/README.md).
Those Git object IDs match between checkouts and are checked against the final
staged source. Raw hashes can differ with working-tree line endings. The commit
containing this report supplies the candidate revision; c70bc4c alone lacks the fix.
No numeric iteration batch was requested or manufactured.

## Changes and failure evidence

1. `EnduranceProcess.Run` is synchronous, while its pipe read/write/flush awaits
   previously captured a caller synchronization context. Two controlled cases
   failed before the fix with2 and3 context posts, then passed with zero posts
   after all three awaits used ConfigureAwait(false). The recording context
   permits progress while counting posts; this is not a real UI-app execution.
   Zero-input output capture and one-MiB backpressured input use real child pipes.
   Deadlines, cancellation, retained-byte limits and strict Git result acceptance
   were preserved. No result/discovery/run-evidence schema changed.
2. Normal xUnit3 regression and the isolated xUnit2/VSTest Stryker driver share
   identical test source. The source-linked Process subject is separate from
   the existing six-guard endurance subject, preserving its scope/parser/schema.
   All dependency versions are reused from the existing pinned driver. The
   initial compile correctly rejected missing xUnit3 test cancellation; the
   shared-source build symbol now passes its TestContext cancellation token,
   while the private xUnit2 driver retains the explicit bounded process deadline.
3. StrictDoc0.30.4 exports had an out-of-export README link and incorrect relative
   source-detail links. The README now points to the repository document. The
   opt-in `Check-RequirementsExport.ps1` checks existing contained targets and
   repairs only two demonstrated generated prefixes, validating the whole tree
   before writes. Unknown missing targets, links outside the export, links through
   symlinks/junctions and oversized pages reject. A probe also exposed matching
   displayed `href` source text; matching is now restricted to generated start
   tags, with a regression. Eleven Pester cases exercise these boundaries.
4. The first Windows coverage attempt failed four Git fixture cases: successful
   Git exits were marked truncated during stream cleanup. That collection has
   no accepted merged completion result. The controlled context test proves a
   real defect; it does not prove the exact cause of every earlier fixture failure.
   The complete post-fix collection passed without weakening any guard. Failed
   logs remain local under the initial WP-01 directory listed below.

## WP-01 execution

SDK10.0.402, existing Python3.12 environments, pinned development cocotb
5fa952e92c46fd9cb58f55a8655312a4bf02e7de and native tools were reused. Doctor
passed on both OSes. No wholesale installation, Dell share, ZIP, WSL import,
shared Windows/Linux bin/obj/build outputs or optional simulator installation
was used. Released cocotb2.1 reference tests were not substituted for development
adapters; no new released-reference Python comparison is claimed here.

Locked restore and Release full-solution builds passed. The initial Windows full
build emitted2161 warnings, primarily existing public XMLDoc debt. Its final
incremental full build emitted0 warnings/0 errors; incremental output does not
establish that the debt disappeared. SDK/xUnit analyzers ran; PowerShell parser,
StrictMode and Pester ran. PSScriptAnalyzer was not installed/available.

| Unique inventory | Windows | Linux |
|---|---:|---:|
| Core | 669 passed | 669 passed |
| Generators | 17 passed | 17 passed |
| Tools | 1374 passed /12 Unix skips | 1386 passed |
| Retained contracts | 948 passed /3 skips | 951 passed |
| Total | **3008 passed /15 skips** | **3023 passed /0 skips** |

Linux executed `make test`. Full executable discovery was reconciled with XML
using class/method/display-name multiplicities: the framework truncates some
parameter display names identically, so a set would lose genuine cases. This
changes neither production HDL identities nor their duplicate-rejection rules.
Coverage repetitions, separate Runner subsets and Stryker controls are not extra
unique inventory. Twenty-one PowerShell cases per OS additionally passed (eleven
new export cases plus ten existing launcher cases), without skips. Checker command
coverage was80/81 (98.77%) on each OS; each misses the opposite OS comparison branch.
This is Pester command coverage, not line/branch coverage of managed assemblies.

| Four production assemblies | Covered/valid lines | Covered/valid branches |
|---|---:|---:|
| Windows | 7566/11565 (65.4215%) | 4688/8819 (53.1580%) |
| Linux | 7567/11565 (65.4302%) | 4710/8819 (53.4074%) |

Both accepted collectors require Core, Generators, Tools and production Runner.
Generated C# contributes to the denominator. Each coverage HTML tree checked266
pages and1102 local file links. Each final StrictDoc tree checked64 pages and3427
local file links, after672 narrow repairs; a second read-only check required zero
repairs. Both original portable endurance-report checksums matched. These checks
validate static double-quoted local file targets, not external URLs or fragments.

On each OS FsCheck3.6.0 passed three1000-case properties, replay91557096206384.
Each malformed-regex sweep used seed20261024 and2000 inputs:295 accepted,
1705 rejected, zero observed match timeouts, no unexpected exceptions. This is
seeded malformed-input testing, not coverage-guided fuzzing. No failed randomized
counterexample was observed, so no minimized counterexample is claimed.

Stryker.NET5.0.0 passed both control cases and killed both selected read/write
ConfigureAwait boolean mutants on each OS. There were no selected survivors,
timeouts, invalids, uncovered or pending outcomes. Separately107 mutants were
ignored and5 compile errors excluded; neither group is counted as killed.
The mutation report source exactly matched the production file on each OS.
FlushAsync can complete synchronously on these pipes and its flag is not covered
by this mutation-sensitivity claim. Other process code and whole-product mutation
adequacy are outside this narrow result. See the [reproduction commands](../../tests/SharpTestBench.Process.Mutation.Tests/README.md).

## WP-02 actual publication, correctness and timing

Windows x64 Tools published as self-contained ReadyToRun with bundled CLR/JIT.
Linux x64 Tools published likewise; Linux x64 Runner published with NativeAOT.
Actual publication logs include native code generation, ELF x86-64 format/linkage
was retained, and complete worker inventories were verified before and after
execution. Full Tools is not NativeAOT. The simulated test assemblies still load
hostfxr/CLR in separate processes even with the AOT orchestration executable.

Published Tools doctor and dual-runner positive counter/DSP/CPU checks passed on
both OSes. DSP and CPU negative controls produced the expected mismatch marker
and exit1 through both front ends, with matching identities and expected seeds.
The Linux AOT runner additionally detected both negatives directly; independent
post-run checks bound exact failed identities, seed17, status, marker, result
hashes and run-manifest hashes. This qualifies actual Icarus orchestration; it
is not the independent VPI backend in PR62.

All nine reports used seed17, one warmup and five measured repetitions per runner.
That is45 measured pairs/90 workload invocations, plus18 excluded warmup workload
invocations. Separate help-proxy processes are not extra workload samples.
Execution order alternated; fixture/host preparation occurred outside timing.
Each report checked identical per-pair inputs before/after, expected passing
identities/seeds and DSP/CPU functional metrics. Every original report passed
`runner-benchmark-check`. Raw samples/distributions, manifests, driver/phase logs,
results and hashes remain under ignored build directories; compact portable
samples and summaries are committed.

All table values are milliseconds. L/R means legacy managed Tools / standalone
Runner. Each deployment has its own campaign; do not collapse differing legacy
samples into one baseline. Help is a separate process round-trip proxy, not
isolated initialization. HDL compile and simulation are child phase walls; Other
is the remainder including startup, adapter queries, validation and reporting.
Fixture/.NET/native-host preparation is excluded, not a measured build speedup.

| Platform | Fixture | Standalone | Legacy total | Standalone total | Standalone min–max | Help proxy L/R | HDL compile L/R | Simulation L/R | Other L/R |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| windows | counter | managed | 1160.088 | 568.135 | 559.269–577.804 | 83.880/85.760 | 109.872/99.644 | 221.042/226.231 | 823.108/240.186 |
| windows | cpu | managed | 1178.234 | 589.199 | 584.218–734.001 | 82.570/85.373 | 106.739/103.929 | 248.819/246.881 | 830.315/241.106 |
| windows | dsp | managed | 1225.913 | 596.145 | 585.949–673.921 | 85.954/84.216 | 114.562/105.800 | 252.424/252.684 | 867.636/256.664 |
| linux | counter | native-aot-artifact | 559.921 | 239.252 | 226.973–317.847 | 30.791/4.552 | 14.443/10.637 | 155.870/178.743 | 392.867/43.479 |
| linux | counter | managed | 552.019 | 389.722 | 344.568–437.312 | 31.935/34.417 | 14.117/17.670 | 158.985/161.004 | 375.391/214.610 |
| linux | cpu | native-aot-artifact | 662.445 | 284.635 | 250.155–344.657 | 30.535/6.623 | 16.343/11.597 | 204.173/231.467 | 421.545/44.810 |
| linux | cpu | managed | 588.210 | 438.151 | 372.754–461.010 | 30.165/36.987 | 15.704/19.193 | 188.897/180.937 | 387.865/240.218 |
| linux | dsp | native-aot-artifact | 573.991 | 264.055 | 245.084–289.406 | 30.149/5.132 | 16.337/10.770 | 183.194/192.984 | 370.971/42.962 |
| linux | dsp | managed | 595.040 | 422.561 | 369.450–431.950 | 30.619/35.709 | 15.572/17.798 | 191.384/181.631 | 392.014/221.376 |

These are finite local observations, not a general speedup, statistical confidence
claim or comparison of HDL engines. Warm filesystem/runtime caches remain;
managed JIT state is not shared between fresh processes. Heavy build/test/coverage/benchmark jobs were serialized. Lightweight read-only
provenance collection occurred while the matrix ran; the machine was not
hermetically isolated. The two runners perform different orchestration/log-validation work.

A retrospective collection check found that subsequent Windows fixture
preparation rebuilt the shared native-host DLL. Earlier Windows report hashes
therefore differ from the current shared file, and earlier binary bytes were
not archived by the existing harness. Each original per-campaign before/after
attestation passed; its timings/results are retained without claiming those
older input binaries still exist. The portable records explicitly identify the
later shared-host rebuilds. All other Windows protected inputs, and all Linux
protected inputs, matched when checked again after the matrix. This is a retained
artifact limitation, not evidence that a completed timed pair used differing
hosts. An exact older Windows binary cannot be reconstructed merely from its hash.

## Reproduction and local evidence

Read [the benchmark guide](../RUNNER_BENCHMARKS.md), [coverage scope](../CODE_COVERAGE.md)
and [requirement export instructions](../requirements/README.md). Select locally
verified SDK/Python/native paths before running; doctor is read-only. From the
appropriate checkout in PowerShell, first check the directory and then run:

```powershell
$ErrorActionPreference='Stop'
$PSNativeCommandUseErrorActionPreference=$true
$repo=(Get-Location).Path
if(!(Test-Path -LiteralPath $repo -PathType Container)){throw 'Checkout missing'}
dotnet restore SharpTestBench.slnx --locked-mode
dotnet build SharpTestBench.slnx -c Release --no-restore --disable-build-servers -m:1
# Check each exit code; the collector executes all three suites and retained contracts.
dotnet tool restore
dotnet tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll code-coverage
# In the native WSL checkout, also execute make test with its selected Python.
```

The actual independent suite commands were `dotnet <suite.dll>` for Core,
Generators and Tools, with `-preEnumerateTheories -list full/json` and `-xml` for
inventory binding, and selected Python `-m unittest discover -s tests/Tooling -q`.
Final post-fix collection repeated them instrumented, including retained Runner
subsets. Pester3.4.0 executed both files under tests/Endurance with command coverage
of tools/Check-RequirementsExport.ps1. Linux reused the installed module read-only
and set TEMP to its native temp path; it warned of partial OS metadata.

Publication commands were `publish-workers --mode tools --output <unique-build-subdir>`
on Windows and `--mode all` on Linux, followed by verify-workers. Each supported
fixture ran `dual-runner-ci --fixture <counter|dsp|cpu> --seed 17`; DSP/CPU repeated
with `--negative-control`. Benchmark commands were
`runner-benchmark --fixture <counter|dsp|cpu> --seed 17 --repeats 5 --warmups 1`,
and separately on Linux added `--runner-directory <verified-AOT-runner-directory>`.
Each emitted report then ran runner-benchmark-check. Direct AOT negatives used
fresh manifests/output directories with the same seed/plan and deliberate fault.

Relative to the corresponding checkout, raw evidence is retained at:

- Initial Windows baseline/rejected coverage: build/wp01-windows-35d50fe896d1461ab5704f4d02afce45.
- Pre/post context reproduction: build/wp01-context-before.{log,xml} and wp01-context-after.{log,xml}.
- Final Windows baseline/coverage/docs: build/wp01-windows-final-9aad51cae3fc4207afcece7d36769613.
- Windows Stryker: build/wp01-process-stryker-70bb107b12d54232afa2d246bf4a55c0.
- Initial Linux baseline: build/wp01-linux-20261027T024229Z.
- Final Linux make/coverage/docs/Stryker: build/wp01-linux-20261027T031442Z.
- Windows publication/parity/benchmark index: build/wp02-windows-a0de4bb6db9b4f428ff4624067ccf130.
- Linux publication/parity/AOT negatives/benchmark index: build/wp02-linux-20261027T032930Z.
- Unique benchmark raw folders are identified in each corresponding fixture/deployment driver log.

Portable records strip machine paths and retain raw-report hashes, source bindings,
input identities/hashes, samples, phase distributions, negative dispositions and
checksums. LF checksum-bound JSON is marked -text for cross-platform Git fidelity.

## Final review and remaining boundaries

Final review is a self-review of source, requirement traces, test/mutation scope,
artifact hashes, counts and measurement interpretation; no independent agent or
external expert execution/review is claimed. Findings above were incorporated
before the local commit. The review also rejected inferring root cause for every
initial fixture failure, counting excluded mutants as kills, counting repeated
coverage runs as extra unique cases, and treating rebuilt Windows inputs as
immutable retained binaries.

No five-hour/terminal-disconnect endurance, new SDK installation, extra OS/RID,
commercial/backend matrix, HDL coverage, NativeAOT instruction coverage, whole-
product mutation score or complete CLI/runtime-switch instrumentation is claimed.
Broader XMLDoc debt, parent requirements, package release and WP03–WP07 remain.
All12 GitHub workflows stayed disabled; PR62 stayed open/draft/unmerged at
0318a864d9b570f9ffbc4857e7365ec9ed6a4736. No jobs were dispatched or result branches
uploaded. This task ends with a verified local commit; no new push is authorized.
## Library and implementation review

The existing .NET BCL remains the appropriate dependency for asynchronous streams
and context control; Microsoft's [ConfigureAwait contract](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.configureawait?view=net-10.0)
explains the context-marshalling choice. No replacement process/async library was
added. The small export adapter uses the existing [bounded Regex constructor](https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex.-ctor?view=net-10.0)
only for the pinned exporter's double-quoted start tags and two demonstrated path
patterns, not arbitrary HTML parsing. This avoids adding a general DOM dependency
while retaining explicit syntax limitations and actual export regressions.
[Stryker's official configuration reference](https://stryker-mutator.io/docs/stryker-net/configuration/)
supports using the established isolated project/config workflow; the execution
claims above come from the pinned5.0.0 runs, not from current documentation alone.
These primary sources were checked during final implementation review. Existing
Coverlet, ReportGenerator, FsCheck and private test-driver packages were reused;
no new library/version was introduced.