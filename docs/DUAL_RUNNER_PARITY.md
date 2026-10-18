# Dual-runner parity CI

The parity workflow runs the existing Python orchestration and standalone .NET
runner in separate Icarus processes. Both currently use the same managed runtime,
native host, and pinned GPI adapter. This validates orchestration and result
equivalence; it does not qualify the independent VPI backend under development
in PR #62. The existing runner remains the default for users.

## Local checks

Use the pinned .NET SDK in `global.json`. Build before running tooling tests:

```sh
dotnet build SharpTestBench.slnx -c Release -p:RestoreLockedMode=true
python -m unittest discover -s tests/Tooling -v
dotnet run --project tests/SharpTestBench.Tests -c Release --no-build
dotnet run --project tests/SharpTestBench.Generators.Tests -c Release --no-build
```

The small `tests/RunnerValidation` executable links the production runner's
validation/process components so tooling tests can exercise them without a
simulator. It is test infrastructure, not a public runner command.

On Linux, provision Icarus, a native toolchain, and the pinned adapter as in the
workflow, then run:

```sh
python tools/dual_runner_ci.py --fixture counter --seed 1
python tools/dual_runner_ci.py --fixture dsp --seed 17
python tools/dual_runner_ci.py --fixture cpu --seed 17
python tools/dual_runner_ci.py --fixture dsp --seed 17 --negative-control
```

CI covers all three fixtures at seeds 1 and 17. DSP uses a shared deterministic
signed FIR stimulus with stalls and reset cuts; CPU uses the existing checksum
program and independent scoreboard. Each runner receives its own metric output.
The comparator checks exact expected qualified case identities, passing statuses,
and per-case seeds. DSP and CPU also compare functional traces and counters,
excluding elapsed time and allocation measurements.

Negative controls inject fault 1 into DSP/CPU and require both runners to exit 1
with the expected named scoreboard failure and diagnostic marker. A crash,
missing result, or successful child exit cannot qualify as detected corruption.

## Evidence and failure behavior

Each case writes to `build/dual-<fixture>-<seed>/`; negative controls append
`-negative`. The directory retains manifests, input plans, expected identities,
driver logs, runner outputs, functional metrics, and the parity/negative-control
report. Errors also produce `case-failure.json`. CI uploads these directories
even when execution fails. Every main push and PR triggers the workflow, including
managed-runtime, generator, SDK, fixture and tooling changes.

The standalone manifest requires a positive expected count. When `expectedNames`
is provided it must be a unique, nonempty list of that size. CI always supplies
qualified identities. Blank identities, duplicates, malformed result rows,
invalid seeds, unknown statuses, skips, missing cases and unexpected cases fail.
A complete all-failed regression produces a failed run manifest and exit 1.

Output directories must be below the manifest directory and must not traverse
symbolic links. A workspace lock prevents concurrent native writers; known old
results, manifests and logs are cleared before execution. The CI orchestrator
also locks each case directory. Remove a stale lock only after confirming its
owner has exited. Run separate checkouts for concurrent complete CI invocations,
because native build assets are shared within one checkout.

`timeoutSeconds` defaults to 120 and accepts 1–3600. Compiler and simulator logs
are separate, capped at 16 MiB per stream; overflow fails the run. Process reports
retain executable, argument list, exit code, elapsed wall time and timeout state.
Failures are classified by phase rather than executable filename, so custom tool
paths preserve build/simulator failure distinctions. Timeout cleanup kills a
running child process tree and observes output-copy tasks before disposing logs.

This remains a focused qualification step for issues #60 and #61. Full native
shutdown health instrumentation, independent adapter qualification, cancellation
of the runner itself, Verilator parity through this CLI, and wider native-runner
platform qualification remain separate work.
