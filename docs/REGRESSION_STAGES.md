# Regression stages

`[SimTest(Stage = -2)]` assigns an integer stage; the default is 0. Stages run in
ascending numeric order. Within a stage, tests sort by fully qualified declaring
type and method name using ordinal comparison; SimCase variants remain together
in attribute declaration order. Each case retains its ordinary cancellation,
timeout, result and cleanup boundary. Stages do not share an implicit fixture.

`tools/run.py --stage -2 --stage 2` selects those stages. Selection intersects
with `--filter`; unselected tests are omitted from reports. Selecting no tests
fails explicitly. The equivalent environment variable is `CS_TEST_STAGES=-2,2`.
Malformed stage values fail regression startup instead of silently selecting all.

`--stage-failure-policy continue` (default) runs every selected stage even after a
failure. `--stage-failure-policy stop-after-stage` finishes all cases in the failing
stage, then records later selected stages as skipped with the failed stage number.
Expected exceptions that pass do not stop later stages; explicit skips are not
failures. A leaked job/callback still aborts the process's regression immediately:
stages never override isolation safeguards. Environment configuration uses
`CS_STAGE_FAILURE_POLICY=continue` or `stop-after-stage`; unknown policies fail.

Tests: `python tools/stage_tests.py` runs six fresh Icarus processes with deliberately
conflicting method/stage order, parameterized cases, injected failure, both failure
policies, multi-stage selection, combined filtering and empty selection. CI runs
these checks and retains JSON/JUnit and logs. This is ordering and selection,
not parallel execution or automatic dependency resolution.
