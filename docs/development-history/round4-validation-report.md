# SharpTestBench LFSR/CRC development validation

Pushed main: [cb7a50301ec82a12d73ba7505f091f493ed907f4](https://github.com/jeffadickson/SharpTestBench/commit/cb7a50301ec82a12d73ba7505f091f493ed907f4).

Completed 200 tracked code-edit iterations with Release builds, managed/generator/tooling suites, and local commits at iterations 50, 100, 150, and 200. Added 175 tooling regressions. Final Windows and Linux validation each passed 378 managed, 17 generator, and 668 tooling tests.

Vendored the unchanged MIT LFSR/CRC module from [alexforencich/verilog-lfsr revision c1f86d0](https://github.com/alexforencich/verilog-lfsr/tree/c1f86d036745935f3812ec6ddc7821c70ef4c9de), retaining the source copyright, license, exact hashes, and immutable provenance.

The Python integer model is anchored against standard-library CRC32 and CRC16 and a fixed PRBS7 sequence. An independent managed boolean-register model checks every vector before comparing the upstream parallel XOR-mask RTL. Coverage includes Galois/Fibonacci topology, bit reversal, feed-forward and feedback, 1/7/16/32/58-bit state, up to 64-bit data, zero/all-ones inputs, every state/data basis bit, and deterministic random data.

All 32 real Icarus legacy/native parity cases passed: eight fixture configurations, both LOOP and REDUCTION styles, and seeds 1 and 17. Each runner checked 3,860 vectors. All 32 state/data corruption controls produced named LFSR_MISMATCH failures in both runners. Counter, DSP, CPU, and AXI skid-buffer baseline parity also passed.

Qualification validates exact test identities, seed agreement, exit codes, complete input/output traces, source integrity, and an unchanged exact plan hash. The new eight-job CI matrix runs both styles, both seeds, and both corruption channels. These checks qualify the stated configurations on Icarus, rather than every upstream parameter combination. Builds retain XML-documentation warning debt.

Final validation caught two existing process-test budget failures during concurrent validation. Ordinary child checks now receive 30 seconds, and the outer harness allows startup/cleanup grace. Explicit one-second timeout tests remain unchanged. Log truncation, observed bytes, non-timeout classification, and expected exit codes are checked more strictly. Final gates were rerun after this correction.

PR #62 remains open, draft, and unmerged.

## Commits

- cb7a50301ec82a12d73ba7505f091f493ed907f4 ci: qualify LFSR CRC configurations and harden process test deadlines (#60)
- 588b44816b294cba0e20372135bd7987f7fc0bab test: attest LFSR stimulus and complete dual-runner traces (#60)
- 39fcd13e12f04152842e3691109996eeed401366 test: add independent managed LFSR scoreboard and sixteen RTL configurations (#60)
- 5a626789c07edce3aa2d687ade2737bc7f2208fd test: pin upstream LFSR RTL and verify serial CRC reference (#60)

## GitHub CI

- [Build and simulator tests](https://github.com/jeffadickson/SharpTestBench/actions/runs/39150760347): queued
- [Upstream AXI Stream parity](https://github.com/jeffadickson/SharpTestBench/actions/runs/39150760354): queued
- [Upstream LFSR CRC parity](https://github.com/jeffadickson/SharpTestBench/actions/runs/39150760440): in_progress
- [Dual runner parity (Icarus)](https://github.com/jeffadickson/SharpTestBench/actions/runs/39150760330): in_progress
- [API documentation](https://github.com/jeffadickson/SharpTestBench/actions/runs/39150760415): success
- [Windows ARM64 native validation](https://github.com/jeffadickson/SharpTestBench/actions/runs/39150760360): in_progress

## Evidence

- `round4-development-summary.json`: checkpoint results and test totals.
- `round4-development-evidence.zip`: iteration ledger, committed patch, Windows build/test logs/XML, Linux simulator evidence, and source hash comparison.
- `round4-linux-summary.json`: all 64 simulator qualification reports and source hashes.
