# SharpTestBench upstream RTL development validation

Pushed main: [0d3922c016a636d5807dac2cd686bb6d4a19223f](https://github.com/jeffadickson/SharpTestBench/commit/0d3922c016a636d5807dac2cd686bb6d4a19223f).

Completed 200 tracked code-edit iterations in four batches. Release builds, the managed and generator suites, the complete tooling suite, and diff checks passed at iterations 50, 100, 150, and 200. Four local commits were created; final validation corrections were included by amending the unpublished fourth commit.

Added 178 tooling regressions, a standalone managed AXI Stream queue scoreboard, and an eight-job CI matrix. The final Windows and Linux suites each passed 378 managed, 17 generator, and 493 tooling tests.

The unchanged MIT AXI Stream register is pinned to [alexforencich/verilog-axis revision 48ff7a7](https://github.com/alexforencich/verilog-axis/tree/48ff7a7e2ef782cf778d47910cf85835c64b1bce), with its copyright, license, exact source hashes, and provenance retained. Tests cover bypass, simple register, skid buffer, and disabled sidebands with seeds 1 and 17. All eight real Icarus legacy/native parity cases passed. All eight corruption controls were detected as named scoreboard failures in both paths. Existing counter, DSP, and CPU seed-17 parity also passed.

The scoreboard checks data and sideband ordering, valid output stability under backpressure, reset flushing, accepted/consumed conservation, and final draining. Durable evidence includes exact test identities, seeds, source fingerprints, exit codes, and normalized cycle traces. This qualifies the stated Icarus configurations, rather than full AXI protocol certification.

Validation found and corrected Windows newline conversion of vendored source bytes and a scoreboard assumption about valid-but-unaccepted bypass data. Three regressions were added for the bypass correction. The prior macOS CI failure was corrected by testing file identity and absolute paths without assuming Python and .NET resolve `/var` symlinks identically. Builds retain existing XML-documentation warning debt.

PR #62 remains open, draft, and unmerged at 0318a864d9b570f9ffbc4857e7365ec9ed6a4736.

## Commits

- 0d3922c016a636d5807dac2cd686bb6d4a19223f ci: qualify pinned upstream stream RTL and reject corrupt evidence (#60)
- 0a244b18c9314671df292241e81f111aa49587c7 test: attest upstream stream traces through independent runner paths (#60)
- b18bfe465acfe0470872bf490e14fe9ded8068fe test: exercise upstream AXI register configurations with managed scoreboard (#60)
- 819c45da48bbcf18a2827f479b7048306c0e5f0c test: pin upstream AXI stream fixture and validate deterministic plans (#60)

## GitHub CI

- [Build and simulator tests](https://github.com/jeffadickson/SharpTestBench/actions/runs/39142261762): in_progress
- [Windows ARM64 native validation](https://github.com/jeffadickson/SharpTestBench/actions/runs/39142261763): in_progress
- [API documentation](https://github.com/jeffadickson/SharpTestBench/actions/runs/39142261666): success
- [Dual runner parity (Icarus)](https://github.com/jeffadickson/SharpTestBench/actions/runs/39142261749): success
- [Upstream AXI Stream parity](https://github.com/jeffadickson/SharpTestBench/actions/runs/39142261816): success

## Evidence

- `round3-development-summary.json`: checkpoint gates and counts.
- `round3-development-evidence.zip`: iteration ledger, patch, Windows logs/XML, Linux evidence archive, and source hash comparison.
- `round3-linux-summary.json`: all 16 real AXI qualification reports and source hashes.
