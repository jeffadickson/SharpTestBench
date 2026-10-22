# Conformance and requirement coverage — new 200-unit campaign

Base 4856001ca523bddec2deaa7ed4a9a383c5afed29 was pushed on2026-10-22 at the user's request. Prior closure100/requirements100/requirements200 work is not recounted. This campaign remains local. Builds every10, independent reviews every15, final deep review before final commit. CI stays disabled and PR62 untouched. UnknownAGENTS.md is preserved.

Initial scope: bounded automatic cross-bin generation (#29), hostile width validation, selected simulator lifecycle/failure and HDL acceptance gaps. Units are distinct tested acceptance behaviors, not parameter rows or separate feature claims. Broad issues remain open unless their full criteria are established.


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
