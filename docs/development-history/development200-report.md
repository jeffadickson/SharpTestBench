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
