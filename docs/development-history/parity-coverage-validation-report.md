# Dual-runner parity and failure coverage checkpoint — 2026-10-20

Base: main at `ef6eda5`, fetched normally. This is one completed 50-scenario checkpoint on the OptiPlex, with fresh local execution. The earlier round 8 batch is not recounted. Windows and WSL retain independent source checkouts and bin/obj/build outputs; the unknown untracked Windows AGENTS.md was preserved.

## Delivered changes

- Negative controls now require equal unsigned 64-bit seeds for each qualified identity across the two runners. Previously, two independently detected failures could qualify despite different stimuli. Row ordering is irrelevant, and full-width values are not converted through floating point. Blank fault markers are rejected.
- An owned parity case retires old completion reports before attempting child cleanup. A locked child no longer leaves stale matched:true reports alongside a new failure. Locked child files are preserved, and contention for another owner's case lease preserves that owner's evidence.
- Production Runner missing-input diagnostics include the resolved file path in the existing Error string. Exception type, configuration_error classification, exit code and evidence fields are unchanged.
- A separate runner-cli coverage collection instruments a private copy of production Runner DLL/PDB/deps/runtimeconfig files, preserving JSON reflection disabled. It measures selected real entry-point startup/preflight/failure paths, independently of the reflection-enabled RunnerValidation protocol harness.

Production orchestration remains C#. New Python code is a retained subprocess contract test, not a production helper. Upstream pins, public command selection, evidence field names and schema versions are unchanged. Coverage summary's existing suites map gains a runner-cli entry and corresponding Cobertura artifact; the merged production assembly set remains four.

## Scenario and review cadence

The [50-record ledger](parity-coverage-iterations.jsonl) records named regression scenarios, not 50 features or independent HDL designs. Scenarios 1–15 cover negative identities/seeds/status/exit boundaries; 16–30 cover positive qualification, metrics, argv, child environments and logs; 31–45 cover workspace locks, retries, cleanup and failure reports; 46–50 run the production CLI through help, usage, malformed JSON, missing source and controlled compiler failure.

There are 54 added discovered cases: 45 C# scenario facts, five Python CLI tests and four supplemental C# collection-rejection rows. The supplemental rows prevent failed, missing, empty or wrong-assembly CLI coverage from publishing completion. Each CLI test runs in an isolated temporary directory, including spaces in its path. The compiler-failure test uses dummy payloads and a rejecting Python subprocess; it cannot load those payloads or simulate HDL.

Independent read-only peer reviews occurred at 15, 30 and 45; see the [review ledger](parity-coverage-reviews.jsonl). A separate final reviewer examined the full batch and follow-up fixes; see the [deep review](parity-coverage-deep-peer-review.md). No unresolved actionable findings remain. A ledger name overstated atomicity and a follow-up loop shadowed its parameter (CS0136); both were corrected. Reviewers did not execute tests, collect coverage or inspect remote state.

## Regression evidence and corrections

- Focused Windows gates: 15 scenarios passed; the initial 30-case gate exposed two overly exact JsonException assertions, corrected to allow derived JSON parse exceptions. The subsequent 45-case gate passed all 45.
- Temporarily weakening seed comparison to identity-only acceptance caused four of the 45 C# scenarios to fail. The production source was restored to its pre-mutation SHA-256 before final validation. The mutation log is retained separately.
- Initial full Windows/Linux gates both exposed the missing-input diagnostic without its file path. The production message was fixed and the original assertion retained. Pre-fix logs were preserved before rerunning both full gates.
- Whitespace checks pass. The seven changed production/test file Git blob hashes matched between Windows and Linux. Ledger labels were regenerated from actual test names; JSONL uses UTF-8/LF.

## Full local Release checkpoint

Each final gate built the complete solution with RestoreLockedMode=true, disabled build servers and one MSBuild node, then ran all three managed suites and retained contracts. WSL used make test. Existing XML-documentation warnings remain. All final runs have zero failures/errors.

| Platform | Core | Generator | Helper passed/skipped | Contracts | Total passed/skipped |
|---|---:|---:|---:|---:|---:|
| Windows x64 | 480 | 17 | 758/9 | 747 | 2002/9 |
| Linux x64 WSL | 480 | 17 | 767/0 | 747 | 2011/0 |

Windows skips are the nine existing Unix symbolic-link cases. The earlier local ef6eda5 checkpoint had 1,948 Windows passes plus nine skips and 1,957 Linux passes; those are reference results, not additional executions counted in this batch.

The existing SDK 10.0.402, Python environments, pinned cocotb adapters, compilers, linker/zlib and simulators were reused. CS_PYTHON, CS_DOTNET_ROOT and DOTNET_ROOT were selected explicitly using the same locally derived paths recorded in the [OptiPlex environment report](optiplex-validation-report.md). No wholesale reinstall, released-reference cocotb substitution, Dell share, ZIP, imported WSL image or earlier-chat handoff was used.

## Managed coverage

Coverlet 10.1.0 and ReportGenerator 5.5.11 remain pinned. Collection ran serially with builds/tests in each independent checkout. Individual reports, required assembly sets and merged XML/JSON counter agreement passed validation. The controller requires SharpTestBench, SharpTestBench.Generators, SharpTestBench.Tools and SharpTestBench.Runner.

| Final platform | Covered/valid lines | Line coverage | Covered/valid branches | Branch coverage |
|---|---:|---:|---:|---:|
| Windows x64 | 4422/8480 | 52.15% | 1966/5062 | 38.84% |
| Linux x64 WSL | 4422/8480 | 52.15% | 1977/5062 | 39.06% |

The prior local checkpoint measured 3795/8468 lines (44.82%), with branches 1852/5050 Windows and 1862/5050 Linux. This batch adds production code and a new CLI collection, so both scope and denominator differ; the percentage change is not a feature-completeness measure.

The new CLI collection alone measures 2095/3197 Runner assembly lines (65.53%), including generated code. Its branch counts are 279/654 Windows (42.66%) and 280/654 Linux (42.81%). Direct source inspection of that collection's Cobertura confirms hits on 67 of 122 distinct production Runner Program.cs lines on both platforms. Both private runtimeconfig files retain System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault=false. The existing RunnerValidation collection remains separate: its pattern now executes 178 contracts, including the five CLI tests; the dedicated CLI collection separately measures those five against its instrumented copy. Re-executions during collection are not added to discovered test totals.

All 126 merged source files resolve on both platforms, including 41 generated sources. Generated JSON-context HTML pages are retained. Generated code remains in the denominator. The new CLI scope is startup/preflight/selected failure behavior, not complete successful CLI execution or a complete runtime-feature-switch matrix. Managed coverage still does not measure NativeAOT instructions or HDL; actual simulation below is separate functional evidence.

## Real dual-runner and native qualification

- Linux Icarus Counter, DSP and CPU positive parity passed at seed 71. DSP/CPU fault-1 controls passed with both runners exiting 1, expected scoreboard markers/identities and matching per-case seeds. Positive DSP/CPU traces and counters matched.
- Fresh published Windows x64 Tools passed DSP positive and negative parity at seed 73. Fresh published Linux x64 Tools passed the same cases with base seed 18446744073709551615. Parent filter, list-only, early-termination, timeout and fault controls were deliberately poisoned; fixture values/removal overrides isolated the child runs.
- Fresh Linux x64 NativeAOT Runner passed Counter directly, classified /bin/false as simulator_failure with exit 1, and classified a missing source as configuration_error with the source path in Error. Its --help also launched successfully with runtime-root environment variables pointing to a nonexistent location.
- Publication and complete file verification passed for Windows x64 ReadyToRun Tools and Linux x64 ReadyToRun Tools plus NativeAOT Runner. Tools DLLs have nonzero managed-native headers and RTR signatures; Linux Runner was identified as an x86-64 ELF executable.

Full Tools is self-contained ReadyToRun with CLR/JIT fallback, not NativeAOT. Native Runner orchestration still launches managed simulator test assemblies through a separate hostfxr/runtime process. No Windows NativeAOT, macOS, ARM64, commercial backend or independent VPI backend qualification is claimed. This batch's dual-runner CLI is Icarus-only; no new Verilator parity claim is made.

Raw evidence remains in each checkout's ignored build directory under parity-coverage-*, dual-*, code-coverage/ and parity-coverage-workers/. Coverage before this batch was preserved as parity-coverage-baseline-summary.json. Existing historical reports were not rewritten.

## Delivery

The checkpoint is committed only after the full local gates and final source review pass. Normal main push and final remote SHA/workflow/PR checks are recorded in the delivered report copy. GitHub workflows remain disabled and PR #62 remains untouched.
