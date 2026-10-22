# Conformance and requirement coverage — new 200-unit campaign

Base 4856001ca523bddec2deaa7ed4a9a383c5afed29 was pushed on2026-10-22 at the user's request. Prior closure100/requirements100/requirements200 work is not recounted. This campaign remains local. Builds every10, independent reviews every15, final deep review before final commit. CI stays disabled and PR62 untouched. UnknownAGENTS.md is preserved.

Initial scope: bounded automatic cross-bin generation (#29), hostile width validation, selected simulator lifecycle/failure and HDL acceptance gaps. Units comprise distinct acceptance/regression contracts and separately labeled pinned-source semantic audit decisions, not parameter rows or separate feature claims. Broad issues remain open unless their full criteria are established.


## Verified milestone 50 of 200

Implemented40 automatic two-axis cross acceptance contracts and2 width-provider
regressions. The width defect was reproduced before correction on both OSes:
repeated getter evaluation could cache a different width or accept an initially
oversized value. Validation now caches exactly the single value it checks.
Eight additional pinned-source API decisions classify ANSI and selected clock
interfaces; these are review work, not implemented feature or parity claims.
Clock set_action remains an explicit gap.146/302 declarations are reviewed,
156 remain; inventory aliases/dynamic exports are outside these counts.

Locked Release builds/all3managed passed every10; full50 gates passed Windows
2,754 tests plus15Unixskips and Linux2,769 tests/no skips. Totals:567core,
17generator,1,234helpers and951retained contracts. Linux used make test.
The100 retained requirement and70closure contracts, plus8existing integration
regressions passed both Icarus runners on both OSes with exact effective seeds,
identities and producer-input validation. These are retained tests, not new
engineering units. Baseline/doctors selected existingSDK10.0.402/Python3.12 and
working compilers/adapters; nothing was reinstalled. No fresh managed coverage
percentage, new HDL run or NativeAOT publication is claimed at this midpoint.

Reviews15/30/45 found no actionable source defects. Build30 caught a target-typed
params constructor ambiguity in new tests; explicit constructors corrected it,
and both full managed gates passed. Corrections/reruns are not extra units.

Planned accounting is42managed implementation/test units,58pinned-source audit
decisions covering62 declarations (typing overloads grouped with implementation),
and100new actual simulator contracts:200distinct engineering units, not200features
or implemented API equivalents. Only50units are currently verified;150remain.
The next hardware fixture drafts are not execution evidence. Final deep review
and final commit remain required. This batch remains local after the initial
push through4856001; CI stays disabled and PR62 untouched.


## Verified midpoint 100 of 200

The first100 units comprise42 managed acceptance/regression contracts and58
pinned-source semantic audit decisions covering62 declarations. The audit now
reviews200/302 declarations, leaving102 unreviewed. This is not200 implemented
APIs, full cocotb parity, or a coverage percentage. Generic Freeze, clock action
selection, aggregate array assignment and native integer width/sign/wide-write
gaps remain explicit. Logging, strings and time conversions retain their narrower
C# contracts. No broad requirement is closed solely by these classifications.

Checkpoint60/70/80/90 each passed locked Release and all three managed suites on
both OSes. Checkpoint100 additionally passed951 retained contracts,100 retained
requirement,70 closure and8 integration simulations under both Icarus runners
on both OSes. Windows2754passed/15Unixskips; Linux2769passed/0skips. Counts are
unchanged from50 because51..100 are audit decisions, not new executable tests.

Reviews60/75/90 found no actionable issues; an additional independent review
covered91..100. Idle-reviewer message dispatch delayed75/90 results until after
application through90; both returned before new implementation work. Future
reviews trigger an explicit review turn. Evidence-anchor validation caught a
nonexistent TimeNs reference in unapplied drafts; Now provides UInt64 steps,
and no physical-unit query is claimed. After applying corrected91..100, the
pinned audit was rerun on both OSes; source code and full test inputs were unchanged.

New HDL fixture drafts remain outside the repository and are not execution
evidence.100units remain, followed by final coverage/publication qualification
and independent deep review before final commit. This milestone remains local;
CI stays disabled and PR62 untouched. No fresh coverage percentage is claimed.


## Verified milestone 150 of 200

Added a separate ConformanceContracts project and fixed conformance suite route,
leaving prior fixture discovery and evidence schemas intact. Fifty new native
contracts exercise locally authored SystemVerilog: two-stage elastic transport,
byte-enabled register bank, signed saturating accumulator, and owned group
cancellation. Pipeline transport has an independent handshake-driven queue for
ordering and conservation, plus a separately described two-register timing model.
Review105 required that oracle separation; follow-up confirmed the fix. Reviews
120/135/150 found no actionable issues. Compilation150 caught a local catch-name
collision in a new test; corrected before the full gate was rerun.

R041 reproduced a production defect with the old source under both runners on
both OSes: a throwing linked child cancellation callback escaped parent.Cancel
and was absent from the joined group result. SimTaskGroup now owns its source
and routes parent/context cancellation through its existing error capture.
Registrations remain alive until children join, then dispose; identical tokens
avoid duplicate registration. R041..050 cover original error identity, cleanup,
nesting, unrelated jobs, completed typed results, multiple failures and detachment.
This establishes cooperative owner-thread behavior, not noncooperative termination
or arbitrary foreign-thread cancellation. Retained group tests also passed.

Every10 checkpoint through150 passed locked Release/all3managed on both OSes.
Fresh full150: Windows2754passes/15Unixskips, Linux2769passes/0skips, including951
retained contracts. Separately, both Icarus runners passed retained100requirement,
70closure,8integration and50new conformance tests on both OSes. New fixture names
were independently derived from source and compared with produced identities,
statuses and discovery-seeded results. All150 units are verified;50remain.
No fresh coverage percentage or native publication is claimed at this milestone.

This milestone stays local. Initial authorized push ended at4856001; CI remains
disabled, PR62 untouched, final deep review and final200commit remain pending.


## Completed 200-unit batch

Exactly200 new engineering units:42 managed acceptance/regression contracts,
58 pinned-source audit decisions covering62 declarations, and100 new simulator
contracts. Audit decisions are not implemented features or executed parity.
The four checkpoint commits at50/100/150/200 remain local after the initial
normal push through4856001. No old campaign units are recounted.

Selected requirement outcomes:

- #29: implemented bounded two-axis automatic crosses, exclusions, independent
  goals, deterministic lossless in-memory names, full-product budgets, and existing
  atomic snapshot/reset/merge semantics.40 managed and21 native cross contracts
  cover accepted behavior. Arbitrary multidimensional crosses remain outside this API.
- Width validation: fixed repeated evaluation of user-supplied width getters;
  two before-fix failures reproduced on each OS now pass with one validated value.
- #7/#1: fixed parent-linked cancellation callback errors escaping cancellation
  requests instead of appearing in joined group results. Four pre-fix runner/OS
  reproductions are saved. Twenty new parent-cancellation/resource contracts and
  retained group suites establish cooperative simulator-thread semantics.
- #1:200/302 pinned declarations are reviewed, leaving102. Freeze, clock action
  selection, aggregate array assignment and full native integer-domain gaps remain.
  No GitHub issue is closed merely from this selected evidence.

New SystemVerilog is locally authored: elastic two-stage transport, a byte-enabled
register bank, and a signed saturating accumulator. Test oracles include independent
transaction queues, integer arithmetic, byte stores, asymmetric native aliases and
explicit failure identities. This is not external-design or protocol certification.

Final fresh locked Release/full retained gates passed on both Windows and WSL
environments:567 Core,17 Generators,1234 helper tests and951 retained contracts.
Windows2754passed/15Unixskips; Linux2769passed/0skips. Separately, both Icarus runners
passed100retained requirement,70closure,8integration and100new conformance contracts
on each OS with exact source-derived names, statuses and discovery seeds verified.
Builds/tests ran every10 units. Full local gates and commits occurred at50-unit
milestones; review cadence, fixes and the75/90 dispatch delay are recorded above.

Verilator exposed five incorrect test portability assumptions: its selected HDL
integer appears as a packed object. Native Integer view rejection is preserved;
R070/R087..090 are explicitly Icarus-only. The saved before-run had95passes/5failures;
the corrected run has95passes/5metadata skips, zero failures. Both Icarus runners
on both OSes were rebuilt and rerun after those annotations. Full managed/retained
logs precede this test-only metadata correction; production code was unchanged.

Fresh windows four-assembly managed coverage: 6602/10354 lines (63.76%), 3574/7010 branches (50.98%).

Fresh linux four-assembly managed coverage: 6603/10354 lines (63.77%), 3595/7010 branches (51.28%).

Coverage includes all four required production assemblies and generated code.
Every counted source resolves, and every generated source has an actual HTML/code
page. Runner production contract collection and selected reflection-disabled CLI
startup/preflight/failure paths are collected; this does not establish all successful
CLI entry/runtime-switch paths. Simulator contracts, NativeAOT instructions and HDL
line/branch coverage are not instrumented. No percentage comparison to round8 is made.

Fresh publications executed actual100-test conformance simulation: Windows x64
Tools and Linux x64 Tools use self-contained ReadyToRun with bundled CLR/JIT;
Linux x64 standalone Runner uses NativeAOT. Its100pass result matches expected
names/seeds and compiled-image hash. Simulator assemblies still run in a separate
CoreCLR/hostfxr process. No Windows NativeAOT Runner or six-platform qualification
is claimed. Linux Verilator95/5 is separate from Icarus100/0.

Peer reviews found and resolved two P2 issues: add an independent pipeline queue
instead of relying on mirrored timing equations; execute the full replacement that
R068's name promised. Compilers caught new-test constructor ambiguity/local-name
collision; those were fixed and rerun without extra units. Final independent source
review and portability follow-up found no actionable production defect. Final
independent evidence review also completed with no actionable findings before
this batch's final commit, reconciling current source/raw-log hashes, counts,
actual executions and all stated scope limits.

See conformance200-checkpoint200-full-validation.json for exact counters, source/
log hashes, actual paired/published results and coverage scope. Raw coverage HTML
was retained under the task outputs/conformance200/checkpoint200 directory.
All12GitHub workflows remain disabled. PR62 remains open/draft/unmerged at
0318a864d9b570f9ffbc4857e7365ec9ed6a4736. Unknown local AGENTS.md is preserved.
