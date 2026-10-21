# Requirements200 final independent deep review

Source scope: the complete campaign through200, with final reviewers inspecting
uncommitted161..200 against checkpoint160(c0b4178), earlier interval reviews retained
in requirements200-reviews.jsonl. Independent reviewers worked read-only; execution
and coverage are separate validation evidence. No remote mutations or push.

## Deep source reviewer

One actionable P2: diagnostic publication could replace an already-known nonzero
simulator failure. LegacyRunCommand now catches recording errors only for a nonzero
simulator exit, writes a bounded secondary warning, then throws the simulator error.
Zero-exit publication errors still propagate. Controlled production Legacy CLI
regressions obstruct the combined log and capture metadata and assert primary
message/type, phase, exit code and warning. Follow-up source review found the P2
corrected and no actionable findings remain. The fix is not another counted unit.

The reviewer checked bounded concurrent stream capture, final overflow-race checks,
partial evidence and bounded local cleanup; compiler publication precedence;
reporting discovery-input binding; pinned RTL/drop mask and following-cycle status
pulses; independent drop/order/sideband/stability/conservation replays; strict oracle
JSON budgets/duplicates/nonfinite/scalars/nesting; bounded diagnostic Unicode and
schema preservation. No other actionable source defect was identified.

## Independent coverage and requirements reviewer

No actionable source defect identified. Controlled FrameOrchestration tests execute
production parent failure logic with mocked commands and a recorded frame payload;
they do not establish fresh native execution. FrameDrop checks last-beat masked
classification, dropped-frame exclusion, good-frame ordering, stalled payload,
completion pulses, complete drain and demanded mixed-traffic/hidden witnesses.
Actual-child capture tests exercise independent limits, immediate exit races,
simultaneous streams, deadlines, Unicode and partial diagnostics. All four oracle
CLIs share strict data loading while retaining independent Python replay logic.

## Limits retained by both reviews

Bounded inherited-pipe cleanup is not guaranteed orphan-descendant termination.
Before/after fingerprints are not continuous protection. FrameDrop does not close
oversize/full-drop, pause, reset interruption or full AXI requirements. Construction
combinations are not executed platform/configuration certification. Managed coverage
requires four production assemblies and generated-source denominators; it does not
measure HDL or NativeAOT instructions or complete CLI/runtime-switch execution.
Review completion is not a substitute for the fresh final200 test/coverage/native
qualification recorded separately in requirements200-report.md and checkpoint JSON.
