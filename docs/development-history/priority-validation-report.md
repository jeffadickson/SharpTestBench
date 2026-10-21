# Pinned Verilog priority encoder and failure coverage

Date: 2026-10-20. Base main: `36b2e1a8f55ea7980877e2b64fd4c0105cceb9f4`.
Results below are fresh local OptiPlex Windows/Ubuntu-24.04 execution, not
archived round 8 results. Separate Windows and WSL native-filesystem checkouts
retain independent bin/obj/build outputs.

## New Verilog and implementation

Added Alex Forencich's MIT-licensed `priority_encoder.v` at existing pinned
verilog-axis revision `48ff7a7e2ef782cf778d47910cf85835c64b1bce`. The source is
unmodified and matches the downloaded upstream SHA-256:
`df28220d95b47df349a72803a9751aad331eb8e1435416e8686d00572a974dd5`.
Source, COPYING and a verified provenance inventory are committed under
`tests/Fixtures/verilog-priority/`; existing fixture inventories are unchanged.

The new `examples/PriorityEncoder` wrapper instantiates widths 2, 3, 5, 8, 17 and
32 with both priority directions. The independent C# scoreboard scans request
bits linearly, rather than mirroring the HDL reduction tree. Each successful
runner checks 453 masks across 12 configurations, producing 5,436 comparisons:
all low-byte patterns, full-width boundary masks, single bits and complements,
plus a specified 64-bit-LCG stream derived from the effective test seed. It checks
valid, index and onehot; unused outputs are normalized when valid is false.

New production C# command `priority-ci` preserves the existing parity evidence
schema, matches identities/seeds and complete traces, and brackets execution
with upstream hash verification. Inherited priority fault/report controls are
explicitly removed before supplying fixture values. `--negative-control` flips
valid in the local wrapper and requires both runners to fail with
`PRIORITY_MISMATCH` and matching seeds. It fails on the first vector/configuration;
fault detection is not independently qualified in every configuration.

Added 50 managed regressions: 30 literal oracle cases, 15 malformed metric-report
cases, and five acceptance/range/order boundaries. These are regression scenarios,
not 50 features or distinct development iterations. The peer review's malformed
trace gap was addressed by extracting a production per-row validator. It rejects
incorrect dimensions/configuration order, inconsistent stimulus, invalid flags,
indexes and onehot masks. Semantic winners still come from the simulator
scoreboard; the helper validator does not independently reconstruct seeded stimuli.

## Fresh full validation

Reused SDK 10.0.402, Python 3.12 environments, pinned development cocotb adapters,
native compilers and existing simulator installations. No wholesale reinstall,
Dell share, ZIP or WSL import was used. C# doctor passed on both systems. A new
fixture project and the helper test project's dependency lock were recorded.

Full Release solution builds used locked restore, followed by all three managed
suites and retained contracts. Linux used `make test`. Zero build errors occurred;
existing/new public XML documentation warnings remain. A preliminary filtered
xUnit invocation used an unsupported filter option; the full successful suites
below are the regression execution evidence.

| Suite | Windows passed | Windows skipped | Linux passed |
|---|---:|---:|---:|
| Core | 480 | 0 | 480 |
| Generators | 17 | 0 | 17 |
| Helpers, including 50 added cases | 808 | 9 | 817 |
| Retained contracts | 797 | 0 | 797 |
| Total | 2,102 | 9 | 2,111 |

## Actual HDL and native qualification

- New priority encoder: Icarus dual-runner positive/negative at seed 83 on
  Windows, and seeds 83 and UInt64.MaxValue on Linux. Positives matched complete
  traces, identities and seeds. Negatives detected the marker in both runners
  with exit codes 1. Parent priority fault/report variables were poisoned.
- Linux Verilator: new priority scoreboard passed at seed 83. Its entire
  5,436-row trace matched Linux Icarus. Compilation emitted width-extension and
  link-time optimization warnings; execution passed. This is the existing helper
  launch path, not standalone Runner support for Verilator.
- Published self-contained ReadyToRun Tools: new priority positive/negative at
  seed 89 passed on Windows x64 and Linux x64. Published DSP positive/negative
  also passed at seed 73 on Windows and UInt64.MaxValue on Linux.
- Fresh Linux x64 NativeAOT Runner: new priority positive/negative at seed 83
  passed with expected exits 0/1 and results passed/failed. Positive metrics matched
  managed Icarus and Verilator. Its Counter success, simulator-failure and
  missing-source paths also passed.
- Existing Linux Icarus Counter/DSP/CPU positives and DSP/CPU fault negatives
  passed at seed 71. Both systems passed real include-with-spaces, define,
  parameter override, width-failure, selected-case seed parity and input provenance
  qualification through `runner-manifest-check`.
- Worker publication/verification passed for Windows x64 Tools and Linux x64
  Tools+Runner. Tools remains ReadyToRun with bundled CLR/JIT fallback; standalone
  Linux Runner is NativeAOT. Simulator test assemblies still use hostfxr/runtime
  in a separate process. No Windows AOT Runner, macOS or ARM64 execution is claimed.

## Managed coverage scope

Coverage ran serially with builds/tests in each checkout, using pinned Coverlet
10.1.0 and ReportGenerator 5.5.11. Six collections merged all four required
production assemblies. The example scoreboard and HDL execution are separate
functional qualification, not additions to those four-assembly coverage counts.

| Measurement | Windows | Linux |
|---|---:|---:|
| Merged lines | 4,459 / 8,540 (52.2131%) | 4,459 / 8,540 (52.2131%) |
| Merged branches | 2,009 / 5,126 (39.1924%) | 2,020 / 5,126 (39.4069%) |
| CLI Runner lines | 2,108 / 3,208 | 2,108 / 3,208 |
| CLI Program.cs unique executable lines | 78 / 133 | 78 / 133 |

All 127 report source files resolve, including 41 generated sources. Generated
code remains in the denominator and source HTML pages remain available. Production
CLI JSON reflection stays disabled in its private instrumentation copy. This
does not establish complete CLI success/runtime-switch coverage, NativeAOT
instruction coverage, native host instruction coverage or HDL coverage percentages.
No released-reference cocotb 2.1 environment was executed. Unknown-state semantics,
width-one behavior, arbiter fairness and FIFO behavior are outside this fixture.

## Reviews and delivery

Peer reviews followed the first 15, 30 and 45 managed regression cases, followed
by a separate final independent deep source review at 50 before pushing. The
malformed-trace coverage gap was addressed; no actionable findings remain.
See [review details](priority-deep-peer-review.md),
[review ledger](priority-reviews.jsonl) and [scenario ledger](priority-iterations.jsonl).

All 12 GitHub workflows remain disabled; no hosted jobs were dispatched. PR #62
remains open/draft/unmerged/unchanged at
`0318a864d9b570f9ffbc4857e7365ec9ed6a4736`. Unknown local `AGENTS.md` is preserved
outside the commit. Delivery uses a normal main push after validation/review;
the delivered output copy records its exact SHA and remote verification.

Local logs remain under ignored `build/priority-*`, with regenerated coverage
under `build/code-coverage`. Prior batch and historical reports are unchanged.
