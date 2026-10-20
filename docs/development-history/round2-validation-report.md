# SharpTestBench: second development round

Completed 200 tracked code-edit iterations on `main`. Each ledger entry names a
concrete change and records before/after source hashes. Builds and test execution
were deferred to checkpoints 50, 100, 150 and 200, with a local commit after each
successful checkpoint. The initial Unicode transport regression was repaired and
checkpoint 50 repeated. Final executable fixture corrections are recorded separately.

## Delivered behavior

- Strict, bounded JSON parsing rejects ambiguous duplicate keys, unknown manifest
  fields, invalid Unicode, numeric overflow and excessive nesting.
- Native manifests support ordered includes, deterministic defines, top parameters,
  explicit HDL standards and attested exact test selection.
- Before/after input fingerprints cover sources, explicit headers, adjacent managed
  DLLs/configuration, direct native/GPI inputs and hostfxr. Evidence reports are atomic
  and carry invocation identity and platform metadata.
- Process logs have configurable bounds, observed/retained byte counts, truncation
  flags and durable failure detail. Stale case cleanup respects child workspace locks.
- CI includes real compile-option, wrong-width negative and selected-case parity checks.

## Validation

| Check | Windows | Ubuntu WSL |
|---|---:|---:|
| Managed cases | 378 passed | 378 passed |
| Generator cases | 17 passed | 17 passed |
| Tooling cases | 315 passed | 315 passed |
| Icarus parity / scoreboard negatives | — | 6 / 4 passed |
| Compile options / width negative / selection parity | — | passed |
| Native CLI faults rejected | — | 10 |
| Changed or added inputs rejected | — | 5 |

The tooling suite includes 98 new cases. Final production C# code passed all suites;
the corrected executable fixture was exercised against real Icarus. Four local
commits extend `e96c8ae14664b1b06e5e032f29acaa635faa0180` through `62308bff93e04ea8c765661a93db809147685069`. Existing XML-documentation
warnings remain; no build errors.

Both runners still share the existing GPI adapter. Input attestation is a before/after
check, not an immutable snapshot or complete transitive dependency qualification.
This does not close every acceptance item in issues #57/#61 or qualify PR #62's
independent adapter. PR #62 has not been changed or merged.

The evidence archive contains the 200-entry edit ledger, verification repairs,
four checkpoint logs/reports and Linux simulator/fault evidence.

## Delivery

Pushed the four local commits to `main` and verified remote head
`62308bff93e04ea8c765661a93db809147685069`. The working tree is clean. PR #62 remains open as a draft,
unmerged and at its original head. GitHub CI was queued/running at verification.

[Final commit](https://github.com/jeffadickson/SharpTestBench/commit/62308bff93e04ea8c765661a93db809147685069)

- [Build and simulator tests](https://github.com/jeffadickson/SharpTestBench/actions/runs/39133809717): queued at verification
- [Dual runner parity (Icarus)](https://github.com/jeffadickson/SharpTestBench/actions/runs/39133809651): queued at verification
- [API documentation](https://github.com/jeffadickson/SharpTestBench/actions/runs/39133809716): in_progress at verification
- [Windows ARM64 native validation](https://github.com/jeffadickson/SharpTestBench/actions/runs/39133809731): in_progress at verification
