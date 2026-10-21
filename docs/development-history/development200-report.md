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
