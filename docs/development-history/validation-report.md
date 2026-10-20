# SharpTestBench development validation

Completed 200 distinct regression-driven iterations on `main`, with a local build,
full managed/generator/tooling test pass and local commit at each multiple of 25.
The eight local commits start at `eb3275f83f7472be23903d5d7f5676ece7671ac9` and end at `e96c8ae14664b1b06e5e032f29acaa635faa0180`.

## Changes

- Exact parity selection, seed and result validation; durable diagnostic reports.
- Counter, signed FIR and CPU parity at two seeds, with matching functional traces.
- Real scoreboard fault controls and fail-closed process/manifest/result checks.
- Bounded process logs, phase-specific failures, timeout evidence, workspace locks,
  stale-output cleanup and a Windows path-casing regression fix.
- CI triggers include every main push and PR; the managed solution builds before
  tooling tests exercise the production C# components.

## Validation

| Check | Windows | Ubuntu WSL |
|---|---:|---:|
| Managed tests | 378 passed | 378 passed |
| Generator tests | 17 passed | 17 passed |
| Tooling tests | 217 passed | 217 passed |
| Real Icarus parity | — | 6 passed |
| Real scoreboard negative controls | — | 4 passed |
| Native CLI fault injections | — | 10 detected |

Managed failure isolation also detected one intentionally corrupted assertion and
continued through the remaining 377 cases. All required checkpoint gates passed.
The existing Windows solution XML-documentation warnings remain; no build errors.

Both runners still share the existing managed runtime, native host and pinned GPI
adapter. These results qualify runner orchestration and the listed Icarus fixtures;
they do not qualify the independent adapter from PR #62 or close all of issue #61.
PR #62 was not modified or merged.

The evidence archive contains all 200 named iteration results, eight checkpoint
logs/reports, the Linux parity/fault reports and final-source rechecks.

## Delivery

Pushed all eight commits to `main`; remote head verified as
`e96c8ae14664b1b06e5e032f29acaa635faa0180`. The working tree is clean. All four GitHub workflows
were in progress when delivery was verified. PR #62 remains open as a draft.

[Final commit](https://github.com/jeffadickson/SharpTestBench/commit/e96c8ae14664b1b06e5e032f29acaa635faa0180)

- [Windows ARM64 native validation](https://github.com/jeffadickson/SharpTestBench/actions/runs/39102879125): in progress at verification
- [API documentation](https://github.com/jeffadickson/SharpTestBench/actions/runs/39102879128): in progress at verification
- [Dual runner parity (Icarus)](https://github.com/jeffadickson/SharpTestBench/actions/runs/39102879144): in progress at verification
- [Build and simulator tests](https://github.com/jeffadickson/SharpTestBench/actions/runs/39102879155): in progress at verification
