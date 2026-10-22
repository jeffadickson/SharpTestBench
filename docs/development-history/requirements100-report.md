# Requirements and coverage campaign 100

All 100 distinct engineering units are implemented and locally verified. Each is
an explicit simulator behavior contract, with fixes/features where evidence exposed
a gap. Parameter rows, reruns, review corrections and prior campaign work are not
counted as additional iterations or features.

The base is ca0d4ef94305b70539a229095a24d9076027816b. That completed prior 200-unit
campaign was normally pushed at the user's explicit request. The new batch has a
verified local midpoint commit 0c788f9 and a final local commit containing this
report. No second push was made. Windows main and the separate native-filesystem
WSL checkout use independent build outputs. Unknown Windows AGENTS.md is preserved.

## Concrete changes and requirement scope

- R001–R010: selectable rising/falling/any ClockCycles, zero-count validation,
  cancellation detachment and original overload/default-literal compatibility.
- R011–R030: borrowed-task joins/selects, deadline races, original exception and
  result identity, duplicate-subscription accounting and timeout cleanup.
- R031–R040: managed-clock restart, update timing, single-owner refusal, phase and
  thread guards, and absence of transitions after cancellation.
- R041–R050: job identity, cancellation/result contracts and task-local isolation.
  Invalid initial names now fail before scheduling or consuming an automatic name.
- R051–R070: event delivery and semaphore/lock/queue ownership. Reproduced a real
  cancellation-after-grant race; Acquire now returns an unconsumed reservation.
  Successor and anti-stealing tests cover permits, lock leases and queue slots/items.
  Priority-comparer faults/reentry roll back capacity; FIFO/LIFO/tied priorities hold.
- R071–R080: handle identity/cache recovery, constant/vector/phase guards, invalid
  writes preserving queued deposits, and settled combinational HDL output.
- R081–R090: thread-checked clock inspection, factory validation and callback
  lifecycle. Reproduced null-factory NREs/null timeout acceptance and acceptance of
  undefined time policies/foreign-thread Step conversion; guards now reject them.
- R091–R100: cleanup LIFO, continued cleanup after cancellation/failure, exception
  identity, asynchronous disposal, shared/reentrant completion and thread ownership.

The C# contracts-ci command binds exact numbered selections and effective seeds to
full discovery and producer-input attestation. Its integration-regressions mode runs
eight existing fairness, teardown, task, cancellation and external-trigger cases.
Production helper orchestration remains C#; independent Python oracles/RPC tests and
pinned narrow native adapters remain intentional. Existing evidence schemas/pins
and Integration discovery ordering are preserved. The standalone fixture includes
clock, vector, constant, memory and combinational Verilog constructs. An unused
memory declaration initially disappeared from Icarus discovery; fixture initialization
corrected the test setup. That was not reported as a production defect.

The ledger maps selected acceptance behaviors to repository requirement IDs
STB-001–007,019,040,044 and traversal013 as applicable. This does not close those
entire GitHub issues. Additional pinned-source decisions cover 25 awaitable/task
definitions: 138/302 definitions reviewed, 164 unreviewed. Abrupt Task.kill remains
an explicit gap; cooperative cancellation and other documented .NET differences
remain. Review counts are not a parity percentage; aliases/dynamic exports are
outside the inventory. No remote issue or PR state was changed.

## Validation and independent review

Locked Release builds and all three managed suites passed at every ten-unit
checkpoint. Full local validation and commits occurred at 50 and 100; the reproduced
semaphore fix also received an extra full retained run at 60. Linux full gates used
make test. Final fresh totals:

| Local platform | Managed suites and retained contracts | Separate actual simulator evidence |
|---|---|---|
| Windows x64 | 2,677 passed, 15 Unix skips | 100 new contracts + 8 existing integration regressions, both Icarus runners |
| Linux x64 | 2,692 passed, no skips | Same paired Icarus sets; 100/100 Verilator contracts, no skips |

Managed totals are 495 core, 17 generator, 1,229 helper tests, plus 951 retained
contracts. The 100 new simulator contracts are separate from those totals. Each
contract has an explicit state/timing/ownership/error assertion; cross-runner status,
identity and full-discovery-derived seeds must also match. Raw traces are not
compared for the100 requirement contracts. Fresh C# doctors and pinned-source audits passed on both systems.

Reviews occurred at 15, 30, 45, 60, 75, 90, with an additional midpoint review. Findings
resolved an ambiguous default-literal overload, strengthened duplicate detach counts
and replaced a final-level-only clock assertion with a no-transition window. The
60 review suggested direct successor handoff coverage, supplied in R061. Independent
final deep source/test reviews found no actionable defects. Final executed-evidence
and documentation review was completed before the final commit; see the review
ledger for findings and resolutions. Review fixes/reruns are not new iterations.

## Fresh coverage and publication evidence

Merged managed coverage includes all four production assemblies: SharpTestBench,
SharpTestBench.Generators, SharpTestBench.Tools and SharpTestBench.Runner.

| Platform | Covered lines | Covered branches |
|---|---|---|
| Windows | 6,516/10,252 (63.55833%) | 3,490/6,916 (50.46270%) |
| Linux | 6,517/10,252 (63.56808%) | 3,511/6,916 (50.76634%) |

Generated code contributes to these denominators. All recorded physical sources
and generated HTML pages were checked; exact inventories and production CLI scope
are in checkpoint100-validation.json. The separate production Runner collection
and selected CLI entry-point/preflight/failure collection are present; the latter
retains production-disabled JSON reflection. This is not complete CLI success-path
or runtime-switch coverage. The new simulator-only contracts are not instrumented
by this managed collection. Their behavioral coverage does not imply an increase
in the managed percentage, NativeAOT instruction coverage or HDL coverage.

Fresh self-contained ReadyToRun Tools publications on Windows x64 and Linux x64
executed all 100 paired Icarus contracts and reporting/arbiter/frame/drop regressions.
Images were inspected for ReadyToRun and bundled CLR/JIT fallback. Linux x64
standalone NativeAOT Runner executed all 100 contracts with exact names/seeds and
compiled artifact attestation, plus arbiter/frame/drop cases checked by independent
oracles. Simulator test assemblies still use a separate CoreCLR/hostfxr process.
No Windows Runner NativeAOT or six-platform execution is claimed. Clock tests here
primarily select Managed; native-clock cleanup and every backend schedule remain
outside this campaign's claims.

Fresh read-only GitHub inspection verified all 12 workflows disabled_manually and
PR62 open/draft/unmerged at0318a864d9b570f9ffbc4857e7365ec9ed6a4736. No hosted job
was dispatched, PR modified or new campaign commit pushed.

Evidence: [iteration ledger](requirements100-iterations.jsonl),
[reviews](requirements100-reviews.jsonl), [independent deep review](requirements100-deep-review.md),
[pre-fix failures](requirements100-reproductions.json),
[final validation](requirements100-checkpoint100-validation.json),
[final paired results](requirements100-checkpoint100-parity.json). Coverage HTML is
retained in this task's outputs/requirements100/checkpoint100/coverage-windows and
coverage-linux directories. Historical requirements200 evidence remains historical.
