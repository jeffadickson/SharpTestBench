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

Tests: `python tools/stage_tests.py` runs nine fresh Icarus processes with deliberately
conflicting method/stage order, parameterized cases, injected failure, both failure
policies, multi-stage selection, combined filtering and empty selection. CI runs
these checks and retains JSON/JUnit and logs. This is ordering and selection,
not parallel execution or automatic dependency resolution.

## Exact selection, discovery and reproducible seeds

`--exact StageTests.Z_First[1]` selects a qualified case name; a short name such as
`--exact Z_First[1]` is also accepted and may match multiple classes. Repeat --exact
to select a union of names. Exact names, stage selection and substring --filter
intersect. `CS_TEST_NAMES` is the equivalent JSON array of exact strings.

`--list` (`CS_LIST_TESTS=1`) elaborates the simulator and lists selected metadata
without invoking test bodies, creating TestContext objects or scheduling test timers.
It emits `*-results.discovery.json` with schema version, names, qualified identities,
stages, case indices, seeds, timeouts, explicit skip flags and simulator requirements.
The runner prints that document; ordinary test result JSON/JUnit is empty in this
mode. Elaboration still runs the HDL's normal initialization; this is not a static
HDL-free reflection tool. Empty selection remains an error.

`--seed 100` sets the base seed (CS_SEED). Case seeds are base plus ordinal in the
complete sorted assembly discovery, before any selection. Therefore running one
case with a filter or stage selection preserves its seed from the full suite.
Changing the test assembly or stage order can change ordinals: preserve assembly
revision and base seed when reproducing a run. JSON results now also include
QualifiedName, Stage and CaseIndex; runtime-level failures use null metadata.
Coverage reports include qualified identity and stage as well.

The nine-process fixture verifies exact short/qualified selection, multiple exact
names, discovery without executing an injected failure, and identical per-case
seeds under full, stage, substring and exact selection.
