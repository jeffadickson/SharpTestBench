# Runner wall-time measurements

Build the Release solution with locked restore and select the local pinned adapter
environment (`DOTNET_ROOT`, `CS_DOTNET_ROOT`, Python venv and Icarus), then run:

```sh
dotnet tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll runner-benchmark --fixture counter --seed 17 --repeats 5 --warmups 1
```

`counter`, `dsp` and `cpu` use the same existing prebuilt fixture, native host,
adapter, source and seed for each runner. DSP/CPU use a shared deterministic plan
and compare functional metrics as well as test identities, passing statuses and
the expected seed. Builds and native-host preparation occur before timing.
Filter/fault/fixture controls are sanitized as in parity qualification.
`CS_IVL_ROOT` is removed for both children: both use the selected Icarus
executable's built-in library root, since the standalone manifest has no `-B`
equivalent. Overrides and executable hashes/versions are recorded.

Each invocation creates a new `build/runner-benchmark-<fixture>-<id>` directory.
Every round starts fresh child workspaces. Execution order alternates. Warmups
are retained but excluded from min/median/max summaries; outliers are not removed.
Every invocation is a fresh process; managed JIT state is not shared between runs.
Raw samples, driver logs, manifests, results, phase evidence and hashes are retained.
Failed runs write `failure.json` and never publish `summary.json`. `samples.json`
is incremental, potentially unqualified diagnostic data, not a completion report.
Timeout/start failures retain an outer error log; partial captured child output
may be unavailable. Standalone phase logs are retained where they were produced.

All timings are monotonic wall milliseconds, not CPU time:

- `HelpRoundTripMilliseconds`: a separate `--help` process round trip, a startup
  proxy with warm filesystem/runtime caches. It is not isolated initialization
  time and is not subtracted from a workload invocation.
- `TotalMilliseconds`: full child invocation through exit and output draining.
- `CompileMilliseconds` / `SimulationMilliseconds`: observed child phase walls.
  The source runner's opt-in `run --no-build --sim icarus --timings` writes a
  `<label>-timings.json` sidecar after success. The standalone runner uses its
  existing process evidence, including launch, draining and cleanup before evidence
  serialization. The runners retain their different logging and validation work.
- `OtherMilliseconds`: total minus those two phases, including adapter queries,
  input hashing, validation, startup, report writing and exit. It is not a startup
  measurement and may explain real orchestration differences.

The default standalone deployment is managed. To measure an already published
NativeAOT Runner separately, pass `--runner-directory /absolute/path/to/runner`.
Its host-matched worker inventory is verified before and after measurement, and
the executable/inventory hashes are retained. The inventory's deployment label
is not independent proof of binary format or source revision; publish current
source with `publish-workers --mode runner` before making current-source AOT claims.
Managed and AOT runs have separate reports; neither is inferred from the other.
The legacy command/deployment is recorded and remains the same for both comparisons.

Compare medians within one fixture/platform/deployment, inspect the raw range,
and repeat before drawing conclusions. No speed threshold or automatic winner is
reported. Parent runtime settings are recorded for common .NET switches; this is
not hermetic machine isolation. Run on an otherwise idle machine, serially with
builds, tests, coverage and other benchmarks. Managed simulator test assemblies
still require hostfxr/runtime even when the orchestration executable is NativeAOT.
These measurements do not compare different HDL runtimes or GPI backends.

Use `runner-benchmark-check --report /path/to/summary.json` to check a saved report
without modifying it or requiring its original binaries. New reports pass this
same check before completion is published. The checker verifies complete ordered
pairs, warmup classification, finite nonnegative phases/remainders and recomputed
measured-only summary counts/min/median/max, with a one-nanosecond rounding tolerance.
It checks internal consistency, not authenticity, input hashes against today's
filesystem, correctness of the original simulation, or performance repeatability.

The subsequent 50-edit hardening batch has a [hashed edit ledger](development-history/optiplex-benchmark-50-iterations.jsonl).
Those entries are source/test/documentation edits, including individual regression
cases and review corrections; they are not 50 features or 50 separate benchmarks.

The [WP-01/WP-02 measurements](development-history/work-packages-01-02-validation.md)
provide fresh Windows/Linux counter/DSP/CPU results, including separate Linux
NativeAOT reports and actual fault controls. Earlier Windows shared-host binaries
were rebuilt by later fixture preparation; hashes and per-campaign attestation
remain, but those older bytes are not archived. See that report's precise scope.
