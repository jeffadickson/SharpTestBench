# Development history

These are the saved development reports and evidence summaries from rounds 1–8, previously stored outside Git. They are now available with a normal clone or pull; no chat synchronization, ZIP, or Dell share is required. Git itself retains the source changes and commit history.

This is a curated development record, not a complete chat transcript. Reports describe historical runs on the Dell Windows/WSL machine. Old absolute paths identify that machine and must not be reused as current-machine configuration. A new computer must run its own dependency checks and baseline tests. Report commit IDs identify the source actually tested; this documentation import adds no runtime changes and does not rerun those tests.

## Chronological reports

| Round | Validation report | Structured summary |
|---|---|---|
| 1 | [Validation](validation-report.md) | [Summary](development-summary.json) |
| 2 | [Validation](round2-validation-report.md) | [Summary](round2-development-summary.json) |
| 3 | [Validation](round3-validation-report.md) | [Summary](round3-development-summary.json) |
| 4 | [Validation](round4-validation-report.md) | [Summary](round4-development-summary.json) |
| 5 | [Validation](round5-validation-report.md) | [Summary](round5-development-summary.json) |
| 6 | [Validation](round6-validation-report.md) | [Summary](round6-development-summary.json) |
| 7 | [Validation](round7-validation-report.md) | [Summary](round7-development-summary.json) |
| 8 | [Validation](round8-validation-report.md) | [Summary](round8-development-summary.json) |

## Latest completed development state

- Code handoff commit: `6de9bb705df190f7e15a2341cd979f6af3e66d5a` on `main`, already pushed before this history import.
- Completed round 8: 200 numbered source/test edits, four checkpoint commits, 171 added helper cases, 13 interval reviews and three final deep reviews.
- Historical tests: Windows 1,941 passed plus nine Unix-specific skips; WSL Linux 1,950 passed. These are not fresh results on the OptiPlex.
- [Full final deep reviews](round8-deep-peer-review.md), [review ledger](round8-reviews.jsonl), [iteration ledger](round8-iterations.jsonl), [iteration CSV](round8-iterations.csv), [delivery audit](round8-delivery.json), [migration audit](round8-migration-audit.json) and [source-hash audit](round8-source-hash-audit.json).
- [Windows dependency reference](round8-windows-inventory.json) and [Linux dependency reference](round8-linux-inventory.json). Use versions as reference and discover paths on the current machine.
- Preserve disabled GitHub CI and leave PR #62 open/unmerged/unchanged.

Start a new machine/session with [the self-contained handoff](../handoff/README.md) and its [restart prompt](../handoff/RESTART-PROMPT.txt).

## Updates after the archived round

While this handoff was being prepared, remote main gained commit `f19e02884f3359be4d3f5bd58c7bd5625b7a7d28` (Collect production Runner coverage and retain generated source pages). This handoff was rebased onto that work, preserving it. The current coverage command now collects all four production assemblies and emits generated source pages. Runner collection exercises production assembly contracts through RunnerValidation; production CLI entry-point/runtime switches, native instructions and HDL still require separate evidence. See [current coverage documentation](../CODE_COVERAGE.md).

The archived round 8 reports remain unchanged historical snapshots of `6de9bb7`. Their coverage limitations and suggested Runner/source-page follow-ups describe that older snapshot; they are not assertions about the newer implementation. The documentation import validates records/links/formatting, not the runtime changes in the newer commit. Obtain fresh test/coverage results on the current machine.

## Evidence scope

The ongoing [200-edit development batch](development200-report.md) records
[individual source/test edits](development200-iterations.jsonl) and
[15-edit reviews](development200-reviews.jsonl). It builds on newer main
benchmark work and does not recount archived iterations or claim 200 features.

Fresh OptiPlex validation and the subsequent focused parity environment fixes are
recorded separately in [the OptiPlex report](optiplex-validation-report.md) and
[its independent deep review](optiplex-deep-peer-review.md). They do not replace or
relabel the archived round 8 machine results.

The follow-on [parity/failure coverage checkpoint](parity-coverage-validation-report.md)
records 50 named regression scenarios, four supplemental collection guards,
[interval reviews](parity-coverage-reviews.jsonl), the [scenario ledger](parity-coverage-iterations.jsonl)
and a [final independent deep review](parity-coverage-deep-peer-review.md). Its CLI
coverage scope extends the earlier managed-contract-only measurement.

The subsequent [Runner cleanup coverage checkpoint](runner-cleanup-validation-report.md)
adds 50 production CLI regression scenarios for input preservation and records
its [scenario ledger](runner-cleanup-iterations.jsonl),
[interval reviews](runner-cleanup-reviews.jsonl) and
[independent deep review](runner-cleanup-deep-peer-review.md).

The [priority-encoder checkpoint](priority-validation-report.md) adds pinned
third-party Verilog, an independent C# scoreboard and 50 managed regressions.
Its [review record](priority-deep-peer-review.md),
[scenario ledger](priority-iterations.jsonl) and [review ledger](priority-reviews.jsonl)
separate new local execution from historical qualification.

Saved Markdown, JSON, JSONL and CSV records are included below. Large binary archives, compiled workers, virtual environments, caches, and generated coverage HTML are not required for the handoff and are not imported into Git. The recorded reports preserve their results and limitations; regenerate local outputs with the C# helpers and Make targets when needed. Some historical reports name old ZIP/HTML artifacts; those filenames are archival references, not required downloads. Historical round 8 coverage excluded a separately collected Runner; the newer coverage scope is documented above. Managed coverage does not measure HDL or native instructions.

## Included records

- [development-summary.json](development-summary.json)
- [linux-summary.json](linux-summary.json)
- [round2-delivery.json](round2-delivery.json)
- [round2-development-summary.json](round2-development-summary.json)
- [round2-iterations.jsonl](round2-iterations.jsonl)
- [round2-linux-summary.json](round2-linux-summary.json)
- [round2-validation-report.md](round2-validation-report.md)
- [round3-delivery.json](round3-delivery.json)
- [round3-development-summary.json](round3-development-summary.json)
- [round3-iterations.jsonl](round3-iterations.jsonl)
- [round3-linux-summary.json](round3-linux-summary.json)
- [round3-validation-report.md](round3-validation-report.md)
- [round4-delivery.json](round4-delivery.json)
- [round4-development-summary.json](round4-development-summary.json)
- [round4-iterations.jsonl](round4-iterations.jsonl)
- [round4-linux-summary.json](round4-linux-summary.json)
- [round4-validation-report.md](round4-validation-report.md)
- [round5-delivery.json](round5-delivery.json)
- [round5-development-summary.json](round5-development-summary.json)
- [round5-iterations.jsonl](round5-iterations.jsonl)
- [round5-linux-summary.json](round5-linux-summary.json)
- [round5-validation-report.md](round5-validation-report.md)
- [round6-delivery.json](round6-delivery.json)
- [round6-development-summary.json](round6-development-summary.json)
- [round6-iterations.jsonl](round6-iterations.jsonl)
- [round6-linux-summary.json](round6-linux-summary.json)
- [round6-validation-report.md](round6-validation-report.md)
- [round7-delivery.json](round7-delivery.json)
- [round7-development-summary.json](round7-development-summary.json)
- [round7-iterations.jsonl](round7-iterations.jsonl)
- [round7-linux-summary.json](round7-linux-summary.json)
- [round7-validation-report.md](round7-validation-report.md)
- [round8-deep-peer-review.md](round8-deep-peer-review.md)
- [round8-delivery.json](round8-delivery.json)
- [round8-development-summary.json](round8-development-summary.json)
- [round8-iterations.csv](round8-iterations.csv)
- [round8-iterations.jsonl](round8-iterations.jsonl)
- [round8-linux-inventory.json](round8-linux-inventory.json)
- [round8-linux-summary.json](round8-linux-summary.json)
- [round8-migration-audit.json](round8-migration-audit.json)
- [round8-reviews.jsonl](round8-reviews.jsonl)
- [round8-source-hash-audit.json](round8-source-hash-audit.json)
- [round8-validation-report.md](round8-validation-report.md)
- [round8-windows-inventory.json](round8-windows-inventory.json)
- [validation-report.md](validation-report.md)
