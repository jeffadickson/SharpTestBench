# Requirement closure and test coverage — 100 engineering units

Completed 100 distinct acceptance contracts: 30 managed feature contracts and 70
actual simulator contracts. Five helper tests support suite selection. Reruns,
review corrections and earlier campaigns are not additional iterations or features.

Base f02e163 was normally pushed on explicit request. The verified midpoint 739ca18
and this final commit remain local on main; no second push. Separate Windows and
WSL native-filesystem checkouts use independent build outputs. Unknown AGENTS.md
is preserved. All 12 GitHub workflows remain disabled; PR #62 remains open/draft,
unmerged and unchanged at 0318a864d9b570f9ffbc4857e7365ec9ed6a4736.

## Concrete acceptance scope

| Units | Changes and tested behavior | Requirement contributions |
|---|---|---|
| C001–C010 | Point/suite coverage reset; retain goals/predicates, isolate snapshots, thread/reentry refusal, atomic suite preflight | STB-029 |
| C011–C030 | Additive subset snapshot merge; bin identity/goals, overlapping hits, shape/counter validation, checked overflow and transactional rollback | STB-029 |
| R001–R020 | New local depth-three FIFO Verilog plus Queue oracle: wraparound, full/empty refusal, simultaneous transfers, reset, stalls, duplicates and seeded traffic | STB-007/018 |
| R021–R030 | Packed deposits/views, signed/real/integer values, array identity and asymmetric ascending indices checked against HDL aliases | STB-007/010 |
| R031–R040 | Owned/borrowed gather/select, cancellation deadlines, error identity, cleanup joins and input snapshotting | STB-006/044 |
| R041–R050 | Structured task groups, closed/foreign-thread refusal, nested failures, original errors and callback-failure cleanup joins | STB-006/031 |
| R051–R060 | Coverage reset/merge on settled native readback, explicit tuple bins, rollback/recovery, overflow and phase preservation | STB-029 |
| R061–R070 | Actual hierarchy/provenance, conversion/cache failures, thread guards, constant refusal and null-write preservation | STB-007/013 |

These close the selected acceptance gaps, not entire broad GitHub requirements.
Merge is additive: callers establish equivalent predicates/provenance and avoid
unintended duplicate runs. No schema changed. Explicit tuple bins and packed views
do not establish automatic cross generation or inferred multidimensional metadata.
The FIFO qualification is specific to this local depth-three design, not every
depth or the pinned AXIS FIFO. API audit remains 138/302 reviewed, 164 unreviewed.

Two defects were reproduced before fixes on both Icarus runners on both OSes:

- A throwing cancellation callback bypassed task-group joins, leaking asynchronous
  child cleanup. CancelChildren now records the callback error and always reaches
  the join. Additional post-fix coverage checks child-failure-initiated cancellation.
- WriteBits(null) threw NullReferenceException. The new null guard reports
  ArgumentNullException(value) after the existing thread/phase check, preserving
  queued valid deposits.

Pre-fix rows and source hashes are retained in closure100-reproductions.json.

## Fresh validation

Locked Release builds passed every 10 units. Independent reviews at 15/30/45/60/75/90
and final source/evidence deep review completed before final commit. Full local
validation and commit at 50; full validation repeated at 80 after the core task fix and 100.
All three managed suites plus 951 retained contracts: Windows 2,712 passed/15 Unix skips;
Linux 2,727 passed/0 skipped (525 core, 17 generator, 1,234 helper, 951 retained total).
All 70 new and 100 retained native requirement contracts passed both Icarus runners
on Windows and Linux, with exact discovery/seed/input checks. Eight existing
integration regressions passed both runners both OSes. Linux Verilator executed all 70
new contracts. Fresh doctors and pinned dependency/API audits passed.

Full self-contained Tools was published and actually exercised on Windows x64 and
Linux x64, including the new 70 contracts, retained 100, reporting failures and existing
arbiter/frame/drop checks. Tools uses ReadyToRun with bundled CLR/JIT fallback,
not NativeAOT. Linux x64 standalone Runner NativeAOT executed all 70new contracts,
the retained 100 and existing traffic fixtures. Simulator assemblies still execute
in a separate CoreCLR/hostfxr process. No Windows Runner AOT or six-platform claim.

## Fresh managed coverage

| OS | Lines | Branches |
|---|---:|---:|
| Windows | 6568/10315 (63.67426%) | 3527/6958 (50.68985%) |
| Linux | 6569/10315 (63.68396%) | 3548/6958 (50.99166%) |

Serial collection includes all four production assemblies: Core, Generators, Tools
and Runner. Generated sources contribute to the denominator and their HTML pages
and physical source availability were checked. Production Runner collection and
source presentation already existed before this batch; they were validated, not
reimplemented. Selected production CLI startup/preflight/failure paths run with
reflection disabled, not the entire CLI success/runtime-switch matrix. Native
simulation contracts are additional execution evidence, not instrumented managed
coverage; AOT instructions and HDL code coverage are not measured.

## Review findings and remaining limits

Review 60 found the ascending-vector palindrome could hide reversal; asymmetric
payload plus independent endpoint aliases replaced it. Review 75 found cleanup
assertions could pass after a full loser timeout; token cancellation and exact
simulated deadlines now distinguish the behavior. Extra midpoint review clarified point
reset versus suite preflight reentry scope. No actionable final source findings
remain. Paired runners share runtime/native infrastructure; parity alone is not an
independent oracle. Parent-driven cancellation, noncooperative children, arbitrary
backend schedules and full upstream conformance remain separate requirements.

Machine-readable checkpoints, iteration ledger, review ledger and independent deep
review accompany this report. Previous campaign reports remain historical.

The initial Verilator run passed 69/failed 1: its HDL integer is reported as Packed,
width 32. R028 initially assumed a native Integer. The corrected contract selects
from observed metadata and verifies signed negative readback through either native
Integer/Enum or actual 32-bit Packed/LogicArray. No native type guard was weakened.
The corrected 70 cases passed development/published paired runners both OSes, Linux
Verilator and Linux NativeAOT Runner. This fixture-only correction followed the
full 100 gate; relevant native execution was repeated. Managed coverage inputs were
unchanged. Initial failure and metadata probe are retained in reproductions.json.
