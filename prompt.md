# Resume SharpTestBench development

Read this file at the start of every new chat, Codex session, or VS Code agent session. Before EVERY commit, update this file with the actual current work, validation, remaining tasks and delivery state. Commit the updated prompt with the work. Do not merely change a timestamp or copy old results as fresh validation.

## Start here

Continue on main. Preserve unknown local changes; fetch normally and inspect newer commits. Never hard-reset, clean unknown files, or force-push. Use this checkout as the complete handoff: no earlier chat, Dell share, ZIP or WSL import is needed.

Read README.md, CONTRIBUTING.md, docs/README.md, docs/handoff/README.md and docs/development-history/README.md. Then read docs/development-history/selection200-report.md, its final independent source/evidence reviews, and the checkpoint200 records linked there. Older round8 reports are historical; f19e028 already implemented production Runner contract coverage and persisted generated source pages.

In VS Code, open this checkout folder and SharpTestBench.slnx. Give the coding agent this file as its starting prompt. Use PowerShell for Windows work, or open the WSL-native checkout in a WSL window for Linux work. Keep Windows and Linux bin/obj/build outputs independent. Derive paths on the current machine rather than assuming historical paths work.

## Current state and delivery

- Last completed code batch: selection200, ending at e42a93257a03844e2b8c4a169a60ebd524955d4f on main. Checkpoint commits are fc72b30, d58b0ac, 348b178 and e42a932. The commit containing this prompt is the subsequent handoff/process update, not another engineering iteration.
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

## Next work

1. Establish a fresh locked Release baseline for the actual source/session and reuse the correct environments. The results above describe the completed batch, not fresh results for later edits.
2. Continue bounded StrictDoc requirements and dual-runner parity/failure coverage; use docs/OPEN_WORK.md and the final requirement assessment to choose concrete gaps. Preserve evidence schemas and required-pass rejection of skipped cases. Keep the three locally authored CRC/arbiter/register-bank Verilog fixtures and independent oracles meaningful.
3. The benchmark harness and its validation tests are already implemented and included in testing. The user asked how benchmarking of the new native implementation is coming. Functional native qualification passed, but selection200 did NOT collect a fresh timed native-versus-managed performance comparison. Do not claim a speedup from publication, AOT, code coverage or total validation duration. Run the existing C# runner-benchmark commands documented in docs/RUNNER_BENCHMARKS.md; do not treat the harness as missing. Use matched workloads, identical seeds/inputs and tool versions, separate build/startup/simulation timing, warmup/repeats and retained distributions. Label Linux NativeAOT Runner, managed Runner and ReadyToRun Tools accurately. Fresh measurements are a next priority, not a completed result or permission to change CI/PR state. The documented managed command is runner-benchmark --fixture counter --seed 17 --repeats 5 --warmups 1; add --runner-directory pointing to the verified published Linux NativeAOT Runner directory for a separate AOT report. Counter/DSP/CPU fixtures are supported.
4. Before the next commit, replace this section with actual changes, tests, review findings, remaining work and push state. Keep the handoff sufficient for another chat or VS Code session.
