# Development batch: 200 recorded source/test edits

Base: main `b35382a` (including the newer winner-validation and benchmark work).
Work continues on main, with peer reviews at each multiple of 15 and full local
Release validation/commits at 50, 100, 150 and 200. No push occurs before final
independent deep review. The ledger counts individual source/test edits, including
named regression additions; it does not claim 200 features or count historical work.

## Checkpoint 50

Added seed-attested priority qualification. Every request is checked against the
fixed 453-vector schedule reconstructed from the effective UInt64 seed. This
complements independent winner validation: valid winners for the wrong requests
now fail qualification. The unseeded overload retains archived-report compatibility.

Added 49 named regressions: eight independent golden schedules, 40 semantically
valid but wrong-request traces across schedule boundaries, and one wrong-seed
trace. Golden masks were generated with Python integer arithmetic modulo 2^64,
independently of the production C# generator. Reviews at 15/30/45 found no
actionable defects and requested tail/wrong-seed cases, which were added.

Checkpoint 50 passed: Windows core480, generators17, helpers937 passed plus9
skips, contracts797 (2,231 passed/9 skipped); Linux core480, generators17,
helpers946, contracts797 (2,240 passed). Both C# doctors and actual Icarus
positive/negative priority parity at seed83 passed. Linux Verilator also passed.
Release builds used locked restore; existing XML documentation warnings remain.
No coverage percentage is claimed for this intermediate checkpoint.

Fresh execution evidence and later checkpoints are appended after validation.
Unknown local AGENTS.md remains outside commits. CI remains disabled; PR #62
is not modified. Windows and Linux use separate native checkouts and outputs.

## Checkpoint 100

Added independently targeted valid/index/onehot faults for all twelve priority
encoder configurations, strict negative-only CLI controls, isolated workspaces,
and actual observed-output mismatch markers. The upstream Verilog remains pinned
and unchanged. Added 47 named control/argument regressions and an executable
independent Python golden reference. Review60 exposed the requested-kind echo
risk; edit76 now requires a single observed wrong output and rejects multiple
wrong outputs. Reviews75/90 and the supplemental qualification-driver review
found no actionable defects.

Windows: core480, generators17, helpers984 passed/9 skipped, contracts797:
2,278 passed/9 skipped. Linux: core480, generators17, helpers993, contracts797:
2,287 passed. Locked Release builds, doctors, default actual Icarus positive and
negative parity passed on both; Linux Verilator positive passed. The opt-in
prepared-host matrix passed 36 configuration/kind cases on Linux and nine on
Windows, each exercising both production runner launch paths and C# fault
contracts. It does not execute priority-ci orchestration for every target.
Separate full priority-ci config11/index qualification passed on both systems.
No intermediate coverage percentage is claimed.

## Checkpoint 150

Initial priority/AXIS/LFSR provenance verification now runs inside the owned
case lease, after stale completion artifacts are retired. Invalid source hashes,
license/revision metadata, absent license/inventory files and malformed JSON
produce current structured case-failure evidence without launching children.
Another active owner's evidence remains protected. Post-run verification remains.
Added 49 named regressions: 30 fixture/artifact combinations, 18 corruption-mode
cases and one active-owner case. Reviews105/120/135/150 found no production
defects; the105 review corrected the malformed-JSON assertion to accept parser
exception subclasses before those cases were enabled.

Locked Release validation passed: Windows core480, generators17, helpers1,033
passed/9 skipped, contracts797 (2,327 passed/9 skipped); Linux core480,
generators17, helpers1,042, contracts797 (2,336 passed). Both doctors, actual
Icarus positive/negative priority qualification and Linux Verilator positive
simulation passed. These cloned-fixture failure tests do not claim post-run
mutation execution or HDL coverage. No intermediate coverage percentage claimed.

## Checkpoint 200

Added negative-only `--fault-vector 0..452`. Faults remain off before onset;
qualification predicts the first observable mismatch from the seed and width,
requires exact observed output/configuration/vector identity, and rejects an
unobservable remaining schedule before child launch. Index/onehot outputs remain
unspecified for invalid zero requests. Default negative workspace names remain
compatible. Negative report fields faultVector/detectedVector are additive.

Added 49 named onset regressions including all configurations, zero-request
index/onehot deferral, final-vector/boundary controls, wrong-vector and prefix
rejection, invalid options and seed-dependent unobservable tails. Review165
found stale success on optional-driver setup failures; golden/manifest loading
now occurs after report retirement inside the failed-summary handler. Five
supplemental retained Python regressions cover these failures. Reviews180/195
confirmed the fix. Independent deep source review found no actionable production
defects and requested the inherited PRIORITY_VECTOR README entry, now corrected.

Locked Release builds and all suites passed: Windows core480, generators17,
helpers1,082 passed/9 skipped, contracts802 (2,381 passed/9 skipped); Linux
core480, generators17, helpers1,091, contracts802 (2,390 passed). Doctors and
actual Icarus positive/default-negative qualification passed on both; Linux
Verilator positive simulation passed. Final matrices, coverage and native
publication results are recorded below.

The 200 ledger entries represent four feature implementations, 194 individual
managed regression additions, one independent reference source and one observed
failure-classification hardening edit. Review fixes and five supplemental Python
setup regressions are documented separately; this is not 200 distinct features or
200 independently executed full test cycles. All 199 new regression cases are
included in the final suite totals.

## Final qualification

Final prepared-host matrices passed on the final source: Windows42 paired case
executions/42 distinct configuration-kind-onset settings; Linux68 executions/64
distinct settings. Both include all36 configuration/output combinations at onset0.
Linux adds24 valid-onset executions and8 final-vector index/onehot executions;
four valid-onset0 cases intentionally repeat baseline settings. Windows adds
four late valid and two late index/onehot cases. Every paired case launches both
production runner paths and checks observed kind, exact vector, exit1 and seed83.
Input hashes remain stable throughout each matrix. These are separate from the
managed/contract suite totals and do not exercise full priority-ci for every case.
Full priority-ci default positive/negative and config11/index/vector452 controls
also passed on both systems, including additive metadata validation.

Merged managed coverage requires all four production assemblies and completed
serially after builds/tests on each checkout:

| Platform | Lines | Branches |
| --- | --- | --- |
| Windows x64 |4621/8770 (52.69%)|2229/5394 (41.32%)|
| Linux x64 |4621/8770 (52.69%)|2240/5394 (41.53%)|

All130 represented source files resolve. All41 generated-source documents have
populated HTML sections on each platform. Selected production Runner CLI startup,
preflight and failure paths cover78/133 Program.cs lines in their separate
collection, retaining production-disabled JSON reflection. This does not claim a
complete CLI success/runtime-switch matrix, NativeAOT instruction coverage or HDL
coverage. Generated source remains in the denominator. No historical percentage
is presented as a fresh baseline or attributed as an improvement from these edits.

Publication/verification and actual execution passed for Windows x64 Tools and
Linux x64 Tools plus Runner. Tools DLLs have the RTR managed-native signature
and bundle CLR/JIT fallback; Tools is self-contained ReadyToRun, not NativeAOT.
Linux standalone Runner is a native ELF without an accompanying Runner DLL.
Published Tools passed actual positive and late-negative priority parity while
inheriting poisoned PRIORITY_FAULT/CONFIG/KIND/VECTOR/REPORT values. NativeAOT
Runner passed positive simulation, the expected vector452 index fault, a real
simulator subprocess failure and missing-source failure classification. Its
positive5436-row trace equals both Icarus managed-runner and Verilator traces.
Simulator assemblies still execute using hostfxr/runtime in a separate process.
Only win-x64 Tools and linux-x64 Tools/Runner were executed; six configured RID
plans do not certify six platforms. Optional released-reference cocotb2.1.0 tests
were not run; the existing pinned development adapters were reused without
reinstallation.

## Reproduction and evidence

Use the environment selected by the handoff docs and C# doctor. Build Release
with locked restore, run all three managed executables and retained contracts
(make test in the WSL native checkout). For simulator matrix qualification first
prepare build/priority-83/manifest.json with priority-ci --seed 83, then run:

```text
python tests/Tooling/qualify_priority_faults.py --name qualification-all
python tests/Tooling/qualify_priority_faults.py --name qualification-late-valid --configs 0,1,10,11 --kinds valid --vectors 0,255,260,324,325,452
python tests/Tooling/qualify_priority_faults.py --name qualification-late-outputs --configs 0,1,10,11 --kinds index,onehot --vectors 452
```

CLI syntax uses separate arguments (for example priority-ci --seed 83). Collect
coverage with the documented C# code-coverage command after all builds/tests,
then publish-workers/verify-workers with mode tools/--rid win-x64 or mode all/--rid
linux-x64. Run the published Tools priority-ci positive and config11/index/onset452
negative controls. For standalone Runner, clone the priority manifest with fresh
outputDirectory and run the published native executable: positive env fault0,
negative env fault1/config11/kind1/vector452. Compare positive metrics to Icarus
and Verilator, require the exact late marker for the negative, then repeat with
vvp=/bin/false and an absent source path, requiring exit1 and the documented
failure kinds. Windows and Linux build outputs remain independent throughout.

Fresh per-platform counts, matrix input hashes, managed coverage counters,
generated-source/CLI checks and actual deployment evidence are retained in
[Windows summary](development200-windows-summary.json) and
[Linux summary](development200-linux-summary.json). Native qualification logs,
case results and complete coverage HTML remain in each checkout's ignored build/
under development200-* and code-coverage/. Final source bytes are audited in
[the source inventory](development200-source-hashes.json). The independent
[deep review](development200-deep-review.md) records source and evidence scope.

Checkpoint commits50/100/150: cc288a5, d99d758,5d967d7. The final checkpoint
records the remaining validated source and evidence. A normal main push follows
final independent evidence review and read-only verification that all12 workflows
remain disabled_manually and PR62 remains open/draft/unmerged at
0318a864d9b570f9ffbc4857e7365ec9ed6a4736. The final delivery records the actual
push SHA. Unknown local AGENTS.md is preserved and excluded from commits.
