# Resume SharpTestBench development

Read this file at the start of every new chat, Codex session, or VS Code agent session. Before EVERY commit, update this file with the actual current work, validation, remaining tasks and delivery state. Commit the updated prompt with the work. Do not merely change a timestamp or copy old results as fresh validation.

## Completed WP-01 / WP-02 qualification (2026-10-26/27)

Read docs/WORK_PACKAGES.md, docs/development-history/work-packages-01-02-validation.md
and docs/evidence/work-packages-01-02/README.md. The user requested completion of
the first two packages; their stated Windows/Linux x64 gates passed. WP03–WP07
remain open, as do broader Active StrictDoc obligations. No200-iteration batch
was restarted. The source commit containing this prompt follows tested base
c70bc4c plus the recorded candidate source hashes/Git blob IDs. Inspect actual
Git history/status for its SHA and delivery; this task makes a verified LOCAL
commit only. The earlier Push request covered c70bc4c, not this new task.

Production fix: EnduranceProcess pipe read/write/flush awaits now use
ConfigureAwait(false), avoiding a dependency on a caller synchronization context
blocked by the synchronous facade. Two pre-fix cases failed with2/3 context posts;
both pass after. Normal xUnit3 and isolated xUnit2/VSTest Process mutation driver
share the same test source and exact production Process code. Stryker5.0.0 killed
both selected read/write boolean mutants on each OS;107 ignored and5 excluded
compile errors are not kills. Flush sensitivity and broader process mutation
coverage are not claimed. Existing six-guard endurance schemas/scope are unchanged.

StrictDoc0.30.4 source-detail relative links need the new opt-in
 tools/Check-RequirementsExport.ps1 after export (see requirements README).
Unknown/missing/escaping/linked targets reject before repair writes. Eleven new
Pester cases plus ten existing launcher cases passed on both OSes. Checker command
coverage80/81 (98.77%) each is not managed line/branch coverage. Final requirement
exports checked64 pages/3427 local file links after672 repairs; second checks
needed zero repairs. External URLs/fragments are outside that check. Both original
portable endurance hashes also matched. The established documentation system
and XMLDoc/StrictDoc source/test traces were preserved.

Fresh final inventories: Core669,Generators17,Tools1386,retained951.
Windows3008 passed/15 documented skips; Linux3023 passed/no skips. Coverage/subset
repetitions and Stryker controls are not additional unique cases. Accepted four-
assembly coverage: Windows7566/11565 lines,4688/8819 branches; Linux7567/11565,
4710/8819. Each coverage HTML tree checked266 pages/1102 local file links.
Generated C# stays in the denominator. Both OSes passed FsCheck3.6.0 three1000-case
properties (replay91557096206384) and2000 malformed regex inputs (seed20261024,
295 accepted/1705 rejected,zero observed timeouts). No failing randomized input
was observed or minimized. SDK10.0.402/pinned dependencies were reused; no wholesale
install. Initial2161 XMLDoc-heavy warnings remain debt despite a zero-warning
incremental final build. SDK/xUnit analyzers and PowerShell parser/StrictMode ran;
PSScriptAnalyzer was unavailable. See the report for exact commands and failures.

Actual publication: Windows/Linux x64 Tools are self-contained ReadyToRun with
CLR/JIT fallback, NOT NativeAOT. Linux x64 Runner is actual NativeAOT (native
compilation/ELF/linkage retained). Inventories verified before and after execution.
Published Tools doctor plus positive counter/DSP/CPU and DSP/CPU fault negatives
passed both runners. Linux AOT directly detected both faults; post-run checks bind
identities,seed17,status,markers and hashes. Simulation assemblies still require
hostfxr/CLR separately; this does not qualify PR62's independent backend.

Nine checked benchmark campaigns: seed17,one warmup,five repetitions,alternating
order,45 measured pairs/90 workload invocations plus18 warmup invocations. Linux
standalone total medians (counter/DSP/CPU) were389.722/422.561/438.151ms managed
and239.252/264.055/284.635ms AOT, in separate campaigns with their own legacy
comparators. Raw ranges/phases are in the portable JSON/report. Help is a startup
proxy; fixture/native builds are excluded; no general speedup or HDL/AOT instruction
coverage claim. Later Windows fixture preparation replaced earlier shared-host
binaries: original per-campaign hash checks passed but older bytes are not archived.
Portable records explicitly retain that limitation; Linux protected inputs still
matched at collection. Never call earlier inputs immutable or infer their bytes
from hashes. The first Windows coverage attempt failed four Git fixture cases and
was rejected; the controlled context bug is proven, but not every failure's exact
root cause. All final post-fix gates passed.

Windows checkout C:/dev/SharpTestBench and independent native Linux checkout
/home/jeffd/src/SharpTestBench keep outputs separate. Both candidate source blob
inventories matched across platforms before commit. Native checkout synchronization
must use audited owned-patch reversal and normal local fetch/fast-forward, preserving
unknown work and ignored outputs; verify actual HEAD/status. Unknown untracked
Windows AGENTS.md remains untouched/uncommitted. All12 CI workflows stay disabled;
PR62 stays open/draft/unmerged at0318a864d9b570f9ffbc4857e7365ec9ed6a4736. No hosted
job, result-branch upload, public package publication or new push was performed.

The latest replacement personal rules use C#/F# by default, PowerShell orchestration,
ripgrep for routine searches, real StrictDoc, scoped coverage, Stryker.NET, FsCheck,
seeded fuzz/negative checks, meaningful comments/XMLDoc and existing Sphinx. Do not
change text-tool installations/aliases silently. Preserve unknown work and source
pins. Final source/evidence/measurement review was a self-review; no independent
agent or external expert review/execution is claimed. All accepted findings are
recorded in the report. Update this prompt before every future source commit.

## Historical work-package planning follow-up (2026-10-26)

The current user requested remaining tasks grouped into meaningful full-testing
work packages and checked against the canonical development rules. Read
`docs/WORK_PACKAGES.md`: WP-01 baseline/evidence, WP-02 native comparisons,
WP-03 endurance lifecycle, WP-04 upstream/selection, WP-05 RTL/backend matrix,
WP-06 distribution/debugging, WP-07 consolidated acceptance. Each has explicit
prerequisites and a test gate; none is closed by planning. Existing stable
StrictDoc requirements remain Active, with bounded evidence distinguished from
parent closure. No new iteration quota or external results upload is authorized.
The user subsequently authorized committing and pushing this documentation slice.

Main and origin/main were inspected/fetched at bdb865e; unknown untracked
AGENTS.md is preserved. Its older shared-WSL-path suggestion is superseded by
the user's explicit separate native-checkout rule. Current guidance is the
user's canonical Development Rules supplied on 2026-10-26 plus applicable repo
guidance; use C#/F#/PowerShell, real StrictDoc, scoped coverage, Stryker.NET,
FsCheck, seeded fuzz/negative checks, current dependency research for additions,
XMLDoc and existing Sphinx, isolated outputs, honest source/run provenance.
Keep CI disabled and PR62 untouched. No new dependency was selected or added.

Fresh Windows SDK10.0.402 locked restore and Release full-solution build passed
with 2161 warnings and zero errors. Existing XMLDoc warning debt is explicitly
tracked by G1 rather than concealed. Core669 and Generators17 passed in fresh
execution. Full Tools suite completed:1384 total,1372 passed,12 Unix skips,zero failures.
StrictDoc0.30.4 export passed, including source pages, at
build/work-packages-strictdoc/html. Windows managed total:2058 passed/12 skips. This follow-up changes documentation only;
no fresh code coverage/mutation result is claimed, and old coverage remains
historical. Full dual-platform G1, fresh timings and longer-run/platform gates
are outstanding until their own execution records establish them.
Final read-through of all four changed documents and evidence claims completed;
no independent review is claimed. This commit contains the work-package plan,
navigation links and updated handoff only. Normal main push is authorized; verify
its outcome against origin/main after committing. Unknown AGENTS.md stays untracked.

## Historical endurance workflow handoff (2026-10-26)

The current task implemented and qualified the reproducible endurance workflow. Read docs/ENDURANCE.md, docs/development-history/endurance-validation-report.md and docs/evidence/endurance/README.md. Requirements STB-END-001..009 remain Active obligations with source/test/finite-evidence traceability, not blanket hours-long closure. Do not restart completed historical200-iteration batches. No hours-long run or recurring schedule was started.

Source milestones:41bdf3c2b0a0e642ddea63312015d3e47470a283 added the C#/PowerShell workflow;942e6033927c05197bfca4fd2195ace02dd94bec fixed full discovery capture and Linux detached command selection. The commit containing this prompt adds final portable evidence, complete bounded Review output and Git outcome diagnostics. All were committed on main. Delivery at commit creation is locally verified; normal push is the next authorized step for this completed task. Check git status/log and fetch origin to verify actual delivery of this commit; do not infer a successful push from authorization. Future tasks do not inherit blanket push permission.

Actual final baseline: Core669, Generators17, Tools1384 and951 retained contracts. Windows3006 passed/15 skips (12 Tools,3 retained); Linux3021 passed/no skips. Additional baseline/coverage repetitions are not extra unique tests. Final four-assembly coverage: Windows7566/11565 lines,4687/8819 branches; Linux7567/11565 lines,4711/8819 branches. Both completion summaries were validated. Generated code contributes to the denominator. These are not HDL, NativeAOT, complete CLI success/runtime-switch or whole-project mutation measurements.

Clean standalone source942e603 was qualified on Windows/Linux x64 using separate new checkouts and temporary local bare Git remotes. Both passed801 selected baseline cases plus StrictDoc, then explicit480-second campaigns with seed20261028,100 cases/property and200 malformed inputs/batch. Windows07c12b1935424420853bb64661b19fb7:30 batches/9000 property cases/6000 malformed inputs,480.115225seconds. Linux9abd3e68b4494cd09f659b422c11d8ea:51/15300/10200,480.1141971seconds. Both killed43 selected Stryker mutants,6 ignored;653 excluded include42 invalid outside selection. Six supplemental deletions were killed. No failing randomized counterexample was observed. Scoped Endurance coverage:739/849 lines on both,729/1072 Windows branches and730/1072 Linux. Full source snapshots stayed unchanged; logs were not truncated.

Result commits to local test remotes: Windowsbd634eb4f636853aee90ae90daded294c1fd66f3; Linux9211665cd6fcfd2c7aca5c3fa17e9e8bb55979c8. Each tree has exactly summary.json/checksums.json; source/main stayed unchanged. Passive review and import passed, with Imported=true and ImportReexecutedChecks=false on imported copies. Exact portable original blobs are committed under docs/evidence/endurance/{windows,linux}; never reformat them without updating provenance. Raw logs/binaries/corpora remain in ignored local build directories. No GitHub result branch was uploaded; final normal main push is separate.

Failed/partial attempts are retained honestly in the report: initial Linux full-solution snapshot child timeout (fixed focused campaign build graph), clean baseline discovery truncation (fixed separate16MiB structured cap), one Linux mutant timeout under concurrent load (failed result retained; quieter same-source retry passed), and five Windows Git fixture failures during overlapping validation (serial1384-case rerun passed). Do not count these as successful runs or claim their exact root cause was proven. Serialize heavy coverage/baseline qualification on this16GiB machine. Campaign orchestration itself executes one child at a time and uses a checkout lease.

Ten Pester cases passed on Windows and Linux, including real detached launch, linked Stop protection, preserved logs and large responses. Existing Pester3.4.0 on Linux needed TEMP set to the native temp path and warned of partial OS metadata. Actual full Review wrapper returned185980 complete bytes. Peer/deep agent source/evidence reviews found and prompted fixes; final review found no source discrepancy and requested stale pending wording be updated, which this handoff/report does. Reviews are source/evidence inspection, not external experts or independent execution attestations.

Dependencies: SDK10.0.402 remains pinned. Windows system SDK is now10.0.403; reuse the profile-derived .dotnet installation containing402, setting DOTNET_ROOT/CS_DOTNET_ROOT/PATH. Setup discovers symlinked Linux SDKs, existing Python3.12 and StrictDoc0.30.4, and pins Stryker.NET5.0.0/FsCheck3.6.0/Coverlet10.1.0. ReportGenerator5.5.11 remains pinned. Linux fresh hash-locked isolated StrictDoc installation passed; working SDK/Python were reused. Fresh SDK download/extraction, fresh Windows StrictDoc install, Debian12, ARM64, terminal-disconnect survival and hours-long stability remain unexecuted paths. No new native publication/simulation/benchmark was performed. Full Tools remains ReadyToRun with bundled CLR/JIT, not NativeAOT; standalone Runner distinctions remain unchanged.

Keep all12 workflows disabled and PR62 open/draft/unmerged/untouched at0318a864d9b570f9ffbc4857e7365ec9ed6a4736. Preserve unknown untracked Windows AGENTS.md. Linux main was safely fast-forwarded to942e603 after reversing only this task's audited temporary patches; no reset or unknown work deletion occurred. After final main push, fetch normally and fast-forward it to the delivered commit. Windows/Linux bin/obj/build remain separate. Derive paths locally; no Dell share, ZIP or WSL import is needed.

Before every source-development commit, update this prompt and include it. Automated data-only result commits are the explicit exception and contain only their allowlisted JSON/checksums. For a local run, check the checkout directory then invoke tools/Endurance.ps1 with an explicit short Duration during development; default is05:00:00. For one-command dedicated execution use tools/Remote-Endurance.ps1 with a non-existing Workspace under a checked existing parent. See docs/ENDURANCE.md for complete commands, setup/auth prerequisites, replay, stop/recovery, passive fetch/review/import and checked cleanup.

## Start here

Portable evidence JSON is marked `-text` in .gitattributes so Git must preserve
the exact checksum-bound Windows/Linux newlines. Do not normalize those blobs.

Continue on main. Preserve unknown local changes; fetch normally and inspect newer commits. Never hard-reset, clean unknown files, or force-push. Use this checkout as the complete handoff: no earlier chat, Dell share, ZIP or WSL import is needed.

Read README.md, CONTRIBUTING.md, docs/README.md, docs/handoff/README.md and docs/development-history/README.md. Then read docs/development-history/selection200-report.md, its final independent source/evidence reviews, and the checkpoint200 records linked there. Older round8 reports are historical; f19e028 already implemented production Runner contract coverage and persisted generated source pages.

In VS Code, open this checkout folder and SharpTestBench.slnx. Give the coding agent this file as its starting prompt. Use PowerShell for Windows work, or open the WSL-native checkout in a WSL window for Linux work. Keep Windows and Linux bin/obj/build outputs independent. Derive paths on the current machine rather than assuming historical paths work.

## Current state and delivery

- Historical completed code batch: selection200, ending at e42a93257a03844e2b8c4a169a60ebd524955d4f on main. Checkpoint commits are fc72b30, d58b0ac, 348b178 and e42a932. The commit containing this prompt is the subsequent handoff/process update, not another engineering iteration.
- Completed units: 70 managed acceptance contracts, 30 pinned export decisions, 70 native protocol contracts and 30 actual paired selection/failure recipes. Seventeen corrective facts are outside that count. Do not recount this or earlier 200-unit batches as new work or call them 200 features.
- The user authorized a normal GitHub push of the completed batch and this handoff. Verify git status, log and origin/main to determine actual delivery; authorization alone is not evidence a push succeeded.
- On this OptiPlex, the Windows checkout is C:/dev/SharpTestBench and the independent Ubuntu-24.04 checkout is /home/jeffd/src/SharpTestBench. These are local reference paths, not portable configuration. The untracked Windows AGENTS.md belongs to the user: preserve it; do not stage, delete or copy it without instruction.
- All twelve GitHub workflows must remain administratively disabled. Do not dispatch hosted jobs. PR #62 must stay open/draft/unmerged/untouched at head 0318a864d9b570f9ffbc4857e7365ec9ed6a4736.

## Validated results — not a new-session baseline

Final selection200 validation on 2026-10-24 used the recorded post-review sources preceding e42a932. Windows: 2,891 passed plus 15 Unix-only skips; Linux: 2,906 passed without skips. Managed inventories were Core669, Generators17 and Tools1269, plus951 retained contracts; Linux used make test. Five retained actual Icarus suites and the new70 protocol contracts/30 recipes passed through both front ends on each OS. The cadence record explicitly retains a Linux checkpoint30 short-regex-budget failure corrected before checkpoint40.

Four-production-assembly coverage: Windows6943/10848 lines (64.0026%),3995/7737 branches (51.6350%); Linux6944/10848 lines (64.0118%),4016/7737 branches (51.9064%). Generated code contributes to the denominator. Both reports checked169 persisted source files,43 generated files,241 HTML pages and1002 local links. Existing Runner coverage/generated-source presentation was validated, not reimplemented. HDL line/branch coverage, NativeAOT instructions, complete CLI/runtime-switch execution and instrumented execution of the new selection-ci orchestration remain unmeasured; pure evidence helpers have unit coverage.

Both OS runs passed three1000-case FsCheck properties (replay91557096206384),2000 malformed regex inputs (seed20261024;295 accepted/1705 rejected;zero observed permitted timeouts), two unchanged production controls and twelve targeted C# mutants. A separate RTL bubble mutant was caught by the exact throughput assertion through both runners; its unchanged controls passed. No failing randomized counterexample exists to minimize. See the adversarial record for scope and hashes.

Published Windows x64 ReadyToRun Tools and Linux x64 ReadyToRun Tools passed70 protocol contracts and30 recipes. Published Linux x64 NativeAOT Runner passed30 recipes. Full Tools uses bundled CLR/JIT fallback, NOT NativeAOT. Simulator test assemblies still load hostfxr/CLR in a separate process. Six supported RID plans do not certify six executed platforms.

Final source review found a premature-completion-evidence qualification gap. It was fixed with RunId-bound rejection/failed-run checks and three regression facts; rereview and actual reruns passed. Final independent evidence review found no actionable inconsistencies. Four broader StrictDoc parent obligations remain open, as do14 literal exports, other/dynamic exposures and the explicitly unmeasured scope.

## Environment and workflow

Reuse working dependencies before installing anything. global.json selects .NET SDK10.0.402; Python3.12, Git, Make, C++20, CMake, PowerShell, Icarus and Linux AOT prerequisites were working. Derive CS_PYTHON, CS_DOTNET_ROOT and DOTNET_ROOT locally; read docs/ALPHA.md and run the C# doctor after selecting the environment. Do not reinstall tools wholesale. Check optional simulator/debugger prerequisites only when needed.

Development adapters are pinned by third_party/UPSTREAM.json to cocotb5fa952e92c46fd9cb58f55a8655312a4bf02e7de. Released-reference tests require a SEPARATE cocotb2.1.0 environment. Never replace the development environment with that release. Production helper orchestration remains C#; independent Python oracles/RPC tests and narrow upstream adapters intentionally remain.

Use C# or F# for automation by default, PowerShell/native cmdlets for shell/file operations, meaningful comments, and stable StrictDoc IDs with implementation/verification traceability. Collect line and branch coverage for code changes, identify unmeasured scope, and retain relevant negative/malformed, seeded property/fuzz and mutation evidence. Coverage alone is not proof of correctness.

For a newly authorized engineering batch, retain the cadence: build/all three managed suites every10 units; independent review every15; full local validation and verified local commits every50; final independent deep review before the final commit. Run coverage serially with builds/tests in each checkout. Do not manufacture iteration claims. Push only within the user's current authorization. Update this prompt before each commit, including milestone and final commits.

## Historical next-work list (superseded by the completed packages above)

1. Establish a fresh locked Release baseline for the actual source/session and reuse the correct environments. The results above describe the completed batch, not fresh results for later edits.
2. Continue bounded StrictDoc requirements and dual-runner parity/failure coverage; use docs/OPEN_WORK.md and the final requirement assessment to choose concrete gaps. Preserve evidence schemas and required-pass rejection of skipped cases. Keep the three locally authored CRC/arbiter/register-bank Verilog fixtures and independent oracles meaningful.
3. The benchmark harness and its validation tests are already implemented and included in testing. The user asked how benchmarking of the new native implementation is coming. Functional native qualification passed, but selection200 did NOT collect a fresh timed native-versus-managed performance comparison. Do not claim a speedup from publication, AOT, code coverage or total validation duration. Run the existing C# runner-benchmark commands documented in docs/RUNNER_BENCHMARKS.md; do not treat the harness as missing. Use matched workloads, identical seeds/inputs and tool versions, separate build/startup/simulation timing, warmup/repeats and retained distributions. Label Linux NativeAOT Runner, managed Runner and ReadyToRun Tools accurately. Fresh measurements are a next priority, not a completed result or permission to change CI/PR state. The documented managed command is runner-benchmark --fixture counter --seed 17 --repeats 5 --warmups 1; add --runner-directory pointing to the verified published Linux NativeAOT Runner directory for a separate AOT report. Counter/DSP/CPU fixtures are supported.
4. Before the next commit, replace this section with actual changes, tests, review findings, remaining work and push state. Keep the handoff sufficient for another chat or VS Code session.
