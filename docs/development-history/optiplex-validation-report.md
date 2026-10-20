# OptiPlex validation and parity environment fixes — 2026-10-20

This is fresh local Windows/Ubuntu WSL evidence, starting from main at `2ec53d3` after an ordinary fetch. It includes `f19e028` and does not reuse the archived round 8 results as current-machine evidence. No Dell share, ZIP, imported WSL image or earlier chat was used.

## Changes

Two production launch defects were corrected in C#:

1. Parity orchestration filtered controls out of its override dictionary, which left those variables inherited by ProcessStartInfo. ChildEnvironment now supplies explicit null removals before fixture-specific overrides. The retained CleanEnvironment contract is unchanged; manifest qualification shares the new helper.
2. The source runner used an ordinal environment dictionary on Windows. Looking up PATH could miss inherited Path, losing dependency directories when building the simulator search path. SimulatorEnvironment now uses Windows case-insensitive lookup and Unix case-sensitive lookup. Linux loader prefixes are applied once.

Three managed regressions cover actual child-process removal of inherited controls, preservation of the existing contract API, and inherited simulator search paths with platform-specific casing. The real process test runs in a nonparallel collection and restores parent values in finally. Evidence schemas, upstream pins and production helper ownership are unchanged.

The dual-runner documentation now describes RunnerValidation's production assembly reference and the disabled hosted workflow state accurately. This is a focused two-defect batch, not a new numbered 200-iteration batch. The 15/50 iteration thresholds were not reached. An independent final read-only deep review examined both fixes, tests, process inheritance, Windows/Unix casing and schema compatibility; it reported no actionable findings. The reviewer did not execute tests; all execution evidence below was produced by the primary agent. The [detailed independent report](optiplex-deep-peer-review.md) preserves its scope and limits.

## Environment

Windows sources/build outputs: `C:\dev\SharpTestBench`. Linux sources/build outputs: `/home/jeffd/src/SharpTestBench` on the WSL native filesystem. No bin/obj/build outputs were shared. The unknown Windows untracked AGENTS.md was preserved.

Both platforms reused .NET SDK 10.0.402 and the existing pinned development cocotb `2.2.0.dev0+r5fa952e9` installation, matching third_party/UPSTREAM.json's commit `5fa952e92c46fd9cb58f55a8655312a4bf02e7de`. Windows Python is 3.12.14 in `.venv-windows`; Linux Python is 3.12.3 in `/home/jeffd/.venvs/sharptestbench`. CS_PYTHON, CS_DOTNET_ROOT and DOTNET_ROOT were set explicitly in the validation shells. Both C# doctor checks passed.

The Windows setup script selected C:\Program Files\dotnet, which contains only SDK 8.0.132. Validation instead selected the existing SDK at `C:\Users\jeffd\Documents\Codex\2026-10-20\resume-plotforge-here-focus-on-how\artifacts\dotnet`. No SDK reinstall or system setting change was made.

Existing native tools were reused: Windows MinGW g++ 16.2.0 and CMake 4.4.4; Linux g++ 13.3.0, clang 18.1.3, linker, zlib1g-dev 1.3, Icarus 12.0, GNU Make 4.3, CMake 3.28.3 and PowerShell 7.6.7. The existing Verilator 5.048 was selected for the corresponding source-runner check. No released-reference cocotb 2.1.0 comparison was run, and no development environment was replaced with that release.

## Managed validation

Both fresh baseline and final gates used a Release solution build with RestoreLockedMode=true, disabled build servers and one MSBuild node; all three managed suites and retained contract tests ran. Linux used make test. Existing XML-documentation warnings remain.

| Gate/platform | Core | Generator | Helper passed/skipped | Contracts | Total passed/skipped |
|---|---:|---:|---:|---:|---:|
| Baseline Windows | 480 | 17 | 706/9 | 742 | 1945/9 |
| Baseline Linux | 480 | 17 | 715/0 | 742 | 1954/0 |
| Final Windows | 480 | 17 | 709/9 | 742 | 1948/9 |
| Final Linux | 480 | 17 | 718/0 | 742 | 1957/0 |

Both final gates passed with zero failures/errors. The nine Windows skips are the explicit Unix symbolic-link tests. Changed production/test files had identical Git blob hashes in both checkouts.

## Coverage

The unchanged Linux baseline independently validated f19e028: 3770/8462 lines (44.55%) and 1842/5046 branches (36.50%). All four required production assemblies were present, and HTML pages for Runner EvidenceJsonContext, Runner ManifestJsonContext and Tools ManifestJsonContext existed.

| Final platform | Covered/valid lines | Line coverage | Covered/valid branches | Branch coverage |
|---|---:|---:|---:|---:|
| Windows x64 | 3795/8468 | 44.82% | 1852/5050 | 36.67% |
| Linux x64 WSL | 3795/8468 | 44.82% | 1862/5050 | 36.87% |

The merged assembly set is SharpTestBench, SharpTestBench.Generators, SharpTestBench.Tools and SharpTestBench.Runner. Production Runner alone measured 1614/3197 lines (50.48%) and 352/654 branches (53.82%) on both platforms. All 126 merged source files resolved on each platform, including 41 generated sources; the representative generated JSON-context HTML pages were present. No missing source path remained in the merged source inventory.

Coverage ran serially with builds/tests in each checkout, using pinned Coverlet 10.1.0 and ReportGenerator 5.5.11. The controller validated individual/merged assembly sets and XML/JSON counter agreement. Runner collection executes 173 retained runner contracts through RunnerValidation against the production Runner assembly. Generated code remains in the denominator and generated-source HTML is retained. This measures managed contracts, not production CLI entry-point/runtime-switch execution, NativeAOT instructions or HDL. The native and simulation checks below are separate functional evidence.

## Actual simulation and publication

Final Linux Icarus dual-runner cases passed at seed 19 for Counter, DSP and CPU, with matching identities/seeds and DSP/CPU functional traces. DSP and CPU fault-1 negative controls both required and observed exit codes 1/1 plus the expected named scoreboard diagnostics. The parent deliberately supplied CS_TEST_FILTER=DOES_NOT_EXIST, CS_LIST_TESTS=1, CS_INJECT_EARLY=1, CS_WALL_TIMEOUT=1 and poisoned DSP/CPU fault values. Explicit fixture settings and removal overrides isolated the children.

runner-manifest-check passed real include/define/parameter handling, an intentional width failure, selected-case parity and input attestation. Linux Counter also passed through the existing Verilator 5.048 at seed 43: one passed, zero failed/skipped. This is source-runner Verilator qualification; the standalone manifest CLI remains Icarus-only.

Fresh Linux x64 publication produced self-contained ReadyToRun Tools and NativeAOT Runner, with complete file verification. Published Tools passed Counter parity at seed 41 under poisoned parent controls. The actual native Runner then passed Counter directly and produced simulator_failure evidence with exit 1 when vvp was replaced by /bin/false. Windows x64 Tools was freshly published/verified in tools mode and passed published Counter parity at seed 43 under the same poisoned filter/list/early/timeout controls.

Both Tools DLLs had nonzero managed-native headers with the RTR signature (Windows RVA 5744/size 196; Linux RVA 67680/size 196). Linux file inspection identified the Runner as an x86-64 ELF executable. Windows/Linux Tools and Linux AOT Runner --help all exited successfully with DOTNET_ROOT and CS_DOTNET_ROOT pointing to nonexistent runtime paths. These startup checks complement actual simulation; simulator processes still use the real runtime paths recorded in their manifests.

The initial Windows published-Tools Counter run at seed 41 failed in the source runner with a GPI DLL dependency loading error; the standalone runner passed. This exposed the Path/PATH defect above. Its logs remain separate from the final corrected run. An initial Windows verify-workers call selected both worker types after only Tools was published; it was corrected to --mode tools. No Windows NativeAOT publication was attempted.

Only Linux x64 ReadyToRun Tools + NativeAOT Runner and Windows x64 ReadyToRun Tools are qualified by this batch. Other RID plans are not executed-platform evidence. Full Tools bundles CLR/JIT fallback and is not NativeAOT. The standalone AOT orchestrator still launches managed simulator tests through a separate hostfxr/runtime process. No independent VPI backend, commercial simulator, macOS or ARM64 qualification is claimed.

Local raw evidence remains under each checkout's ignored build directory: optiplex-baseline-*, optiplex-final-*, optiplex-native-*, optiplex-published-*, optiplex-aot-*, optiplex-verilator.log, dual-*, manifest-check/, code-coverage/ and optiplex-workers/. Archived development-history reports were not rewritten.

## Delivery

All 12 GitHub workflows were read back as disabled_manually. PR #62 was read back open/draft at `0318a864d9b570f9ffbc4857e7365ec9ed6a4736`, and was not changed. No hosted jobs were dispatched.

Commit/push status is recorded with the delivered copy of this report after final gates pass.
