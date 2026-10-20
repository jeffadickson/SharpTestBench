# Independent deep review: parity and failure coverage checkpoint

Review date: 2026-10-20. Base: ef6eda5. The final reviewer was independent of the implementation author and the separate interval reviewer. Review was read-only: no builds, tests, simulations, coverage collection, file edits or remote actions were performed by the reviewer.

## Scope and conclusion

Reviewed the full working diff: 45 named C# parity orchestration scenarios, five production Runner CLI subprocess scenarios, four supplemental coverage-rejection cases, CompareFaults and fault-marker validation, ParityCiCommand workspace/report behavior, the private-copy CLI coverage collection, the Runner missing-input diagnostic, documentation and the 50-record iteration ledger. No unresolved actionable findings remain after the corrections below.

## Negative controls and schemas

Negative qualification validates expected identities, failed statuses, diagnostic markers, exit code 1 and unsigned seeds before comparing seeds by qualified identity. It does not convert seeds through floating point, and row ordering does not affect case pairing. Blank markers fail closed. Existing evidence field names and schema versions remain unchanged; individually detected faults cannot produce matched=true when per-case seeds differ.

## Workspace failure evidence

Completion reports are retired only after obtaining the case lease, before attempting child cleanup. An early child-lock refusal therefore produces current failure evidence without leaving old matched:true completion reports. Child evidence is preserved. Contention for the case lease occurs before cleanup and preserves the active owner's files. The extended tests exercise both boundaries, plus retries, stale-report cleanup, unrelated files, failed metrics and missing result files.

## Test fidelity and limits

The C# fixture calls production comparison and workspace/report-writing code through controlled ICommandRunner results. It checks ordered argv, exact selection, full-width seed arguments, production Runner path, separate metric destinations, removal overrides, both log streams, child exceptions, metrics, locks and report outcomes. This is comparison/orchestration coverage, not a simulator implementation. Fake output generation follows call order and fixed fixture file locations; explicit command/environment assertions and separate real simulation gates remain necessary.

The stale-success replacement test checks the final report and absence of temporary leftovers; it does not prove atomic visibility during interruption. The ledger/test name was corrected accordingly. The environment and lock cases remain distinct from cross-process stress qualification.

## Production CLI and instrumentation

CLI tests execute the production entry point for help, usage, malformed JSON, missing source and controlled compiler failure. Dummy assembly/native payloads cannot be loaded: a rejecting subprocess ends the compile stage before simulation. The test verifies phase evidence, matching run IDs, lock release and absence of success evidence. The missing-input diagnostic now includes the resolved input path in FileNotFoundException.Message, feeding stderr and FailureEvidence.Error without changing exception type, configuration_error classification, exit code, evidence fields or cleanup.

CLI instrumentation uses a separate DLL/PDB/deps/runtimeconfig copy. It preserves the production JSON-reflection-disabled runtime configuration rather than the RPC harness's enabled setting. Failed collection, missing/empty reports and wrong assemblies prevent merging/completion. The controller still requires the same four production assemblies and validates merged counters. This extends selected managed CLI startup/preflight/failure measurement; it does not measure a complete CLI success path, a complete runtime-switch matrix, NativeAOT instructions or HDL.

## Findings resolved before the final gate

- Ledger records 38 and 45 were regenerated to match their actual method names and avoid an unsupported atomicity implication.
- A follow-up loop initially shadowed InWorkspace's name parameter, a CS0136 build blocker. Renaming it to reportName resolved the issue before the final Release gate.
- The missing-source CLI test exposed an error string without its file path. The one-line production diagnostic fix and unchanged test assertion were reviewed with no additional finding.

The primary agent's build/test, mutation, coverage, native-publication and real-simulation results belong to the separate validation report. None are independently executed evidence from this reviewer.
