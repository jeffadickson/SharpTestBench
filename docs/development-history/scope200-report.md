# Scope and coverage requirement closure — new200-unit batch

Base f203e00 was normally pushed on explicit request on2026-10-22. Previous batches are not recounted. This new batch stays local after that push. Work on main; CI stays disabled and PR62 untouched. Unknown local AGENTS.md is preserved.

Plan:40 managed N-dimensional cross contracts,4 helper discovery-domain failure contracts,85 pinned-source audit decisions covering the remaining102 declarations,71 native simulator contracts. That is200engineering units (115 code/test units and85 audit decisions), not200features. Each declaration overload group is one review unit. Exact changes and executed evidence, including backend limits, will be recorded. Builds every10, reviews every15, full gates/commits every50, independent final deep review before final commit.

Fresh baseline: Windows2754passed/15Unixskips; Linux2769passed/0skips. All3managed suites and951retained contracts passed; Linux used make test. Both Icarus runners passed100requirements,70closure,8integration,100conformance contracts on each OS. Existing SDK10.0.402/Python3.12 environments and C# doctor passed; nothing reinstalled. This establishes a new baseline, not new feature work. See scope200-baseline.json.


## Verified milestone 50 of 200

Added bounded shared-sample N-dimensional crosses with 40 managed acceptance
contracts. Construction snapshots axes/exclusions, bounds the full product,
iterates up to 32 axes, and retains flat SchemaVersion1 identities/counters.
Tests cover classification, exclusions, input failures, culture/Unicode naming,
thread ownership, active sampling, reset, transactional merge and overflow.
Four helper contracts strengthen discovery against each declared suite domain
while allowing valid future tests during partial qualification. Six pinned-source
bridge audit decisions explicitly retain unsupported external-thread gaps.

Locked Release/all three managed suites passed every ten. Full milestone 50
passed Windows 2,798 tests plus 15 Unix skips; Linux 2,813 tests/no skips:
607 core, 17 generator, 1,238 helper and 951 retained contracts. Linux used make
test. Both Icarus runners passed retained requirements100, closure70,
integration8 and conformance100 on both OSes with matched names/effective seeds.
The pinned source audit verifies 206/302 declarations; 96 remain, excluding
aliases/dynamic exports. These counts are not parity percentages.

Reviews 15/30/45 found no actionable issues. A PowerShell 5 wrapper stopped on
expected negative-test stderr at checkpoint40; rerunning the normal PowerShell7
script completed successfully. It was a harness invocation correction, not a
production defect or extra iteration. No new native fixture, managed coverage
percentage, publication or HDL coverage result is claimed at this midpoint.
Only 50/200 units are verified. Final deep review and final commit remain due.


## Verified milestone 100 of 200

Locked Release/all three managed suites passed every10. Full100 gates passed
Windows2,798 tests/15 Unix skips and Linux2,813/no skips, including951 retained
contracts (make test in Linux). Both Icarus runners passed retained requirements100,
closure70,integration8,conformance100 with exact identities/effective seeds on bothOS.
Pinned source reviews verify 273/302 declarations; audit counts are not parity percentages.

This milestone remains local. Final deep review/final200 validation and final commit remain due.


## Verified milestone 150 of 200

Locked Release/all three managed suites passed every10. Full150 gates passed
Windows2,804 tests/15 Unix skips and Linux2,819/no skips, including951 retained
contracts (make test in Linux). Both Icarus runners passed retained requirements100,
closure70,integration8,conformance100 with exact identities/effective seeds on bothOS.
Pinned source reviews verify 302/302 declarations; audit counts are not parity percentages.

New scope fixture selected21 contracts on bothOS/bothIcarus runners:
18 passed and 3 declared backend exclusions (not required CI cases). Native evidence is separate
from managed/retained totals. Verilator string/capability probe passed9/skipped12
at150; this is a single simulator run, not paired-runner Verilator qualification.

This milestone remains local. Final deep review/final200 validation and final commit remain due.


Milestone150 correction: the first native gate exposed unavailable Icarus string
handles, then the strict required-manifest no-skips boundary. String/refusal
contracts now declare Verilator requirements. SelectForIcarus validates the full
identity/seed/domain and simulator metadata before selecting eligible cases;
required Runner/Parity attestation still rejects skips. Six additional corrective
xUnit facts cover metadata, seeds, empty selection and domain failures. These are
additional tests within the batch correction, not six manufactured ledger units:
the numbered plan remains44 managed units,85 audit decisions and71 native units.
Actual new managed fact count is50. Refusal diagnostics distinguish kind from
capability failures. Full150 reruns passed after the correction. Preserved failed
attempt logs document the discovery and required-skip failures.
