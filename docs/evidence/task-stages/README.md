# Task API, stress and stage evidence

Local Linux x64 validation on Icarus 11 and Verilator 5.048: 42 passed,
zero failed and one deliberate skip per backend. Unit assertions: 1,846.
Stage tests: six fresh Icarus processes; intentional failure and no-match
scenarios are asserted as expected failures by tools/stage_tests.py.
Native fault tests and pinned Python timing parity pass.

The job-local, completion, cancellation, fairness and teardown fixtures are
AL through AP in tests/Integration/Tests.cs. Reproduce using tools/run.py;
CS_SEED selects the base seed and regression JSON retains each case seed.
Native failures are covered by tools/native_faults.py.

GitHub CI additionally validates the same source on Ubuntu/Icarus 12.
Consult the commit's Actions run for its independent result.
