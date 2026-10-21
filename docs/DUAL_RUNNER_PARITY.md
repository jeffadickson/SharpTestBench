# Dual-runner parity CI

The parity workflow runs the source-runner orchestration and standalone manifest
runner in separate Icarus processes. Both launch paths are now implemented in C#,
with separate option handling and process orchestration. Both currently use the same managed runtime,
native host, and pinned GPI adapter. This validates orchestration and result
equivalence; it does not qualify the independent VPI backend under development
in PR #62. The existing runner remains the default for users.

## Local checks

`priority-ci --seed 83` adds the pinned upstream priority encoder to the local
Icarus dual-runner matrix; `--negative-control` checks valid-bit fault detection.
See [the fixture guide](../examples/PriorityEncoder/README.md) for the 12
configurations, 453-vector independent oracle and explicit qualification limits.
These local commands do not enable or dispatch GitHub workflows.

Use the pinned .NET SDK in `global.json`. Build before running tooling tests:

```sh
dotnet build SharpTestBench.slnx -c Release -p:RestoreLockedMode=true
python -m unittest discover -s tests/Tooling -v
dotnet run --project tests/SharpTestBench.Tests -c Release --no-build
dotnet run --project tests/SharpTestBench.Generators.Tests -c Release --no-build
```

The small `tests/RunnerValidation` executable references the production Runner
assembly so tooling tests can exercise its validation/process components without a
simulator. It is test infrastructure, not a public runner command.

On Linux, provision Icarus, a native toolchain, and the pinned adapter as in the
workflow, then run:

```sh
dotnet run --project tools/SharpTestBench.Tools -c Release -- dual-runner-ci --fixture counter --seed 1
dotnet run --project tools/SharpTestBench.Tools -c Release -- dual-runner-ci --fixture dsp --seed 17
dotnet run --project tools/SharpTestBench.Tools -c Release -- dual-runner-ci --fixture cpu --seed 17
dotnet run --project tools/SharpTestBench.Tools -c Release -- dual-runner-ci --fixture dsp --seed 17 --negative-control
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
Negative controls also require matching full-width seeds for each qualified case
across both runners. Detecting the expected failure independently is insufficient
when the runners used different per-case stimuli. Blank fault markers are rejected.

## Evidence and failure behavior

Each case writes to `build/dual-<fixture>-<seed>/`; negative controls append
`-negative`. The directory retains manifests, input plans, expected identities,
driver logs, runner outputs, functional metrics, and the parity/negative-control
report. Errors also produce `case-failure.json`. CI uploads these directories
even when execution fails. GitHub workflows are currently disabled; use the local
commands above for qualification. The workflow definition retains its push/PR
triggers for managed-runtime, generator, SDK, fixture and tooling changes.

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
are separate. `maxLogBytes` defaults to 16 MiB per stream and accepts 1024–67108864;
overflow fails the run. Process reports include observed/retained byte counts,
truncation flags, and the original failure detail. They
retain executable, argument list, exit code, elapsed wall time and timeout state.
Failures are classified by phase rather than executable filename, so custom tool
paths preserve build/simulator failure distinctions. Timeout cleanup kills a
running child process tree and observes output-copy tasks before disposing logs.

## Manifest compilation, selection and input evidence

The optional `hdlStandard` accepts `1995`, `2001`, `2005`, `2005-sv`, or the default
`2012`. `includeDirectories` preserves search order; `defines` and `parameters`
are maps emitted in ordinal key order. Paths resolve relative to the manifest.
A null define value emits a bare define. Parameter values are HDL expressions
passed as single arguments, without shell interpolation. Sources preserve input
order and duplicate canonical source paths are rejected.

Optional `testNames` selects exact qualified cases. Its unique, nonempty identities
must match `expectedNames`; selection and the base seed are passed explicitly to
the child after inherited selection variables are removed. The runtime preserves
discovery-assigned per-case seeds when selection narrows a multi-case assembly.
Parity children explicitly remove inherited selection, fault, metric-path and
timeout controls before applying fixture-specific values. Merely omitting those
variables from process overrides would leave the parent's values inherited.

JSON readers reject duplicate keys, unknown manifest fields, malformed Unicode,
nonfinite numeric values, excessive nesting, and inputs larger than 16 MiB.
Manifest property matching is case insensitive; HDL option-map keys remain case
sensitive. Files use strict UTF-8, with an optional UTF-8 BOM.

Each invocation records an ID and an atomic `input-manifest.json` before compiling.
It hashes the manifest, sources, adjacent managed DLLs/configuration, explicit
native/GPI inputs and hostfxr, plus files beneath explicit include directories.
Include trees reject symbolic links and are limited to 4096 entries per directory
tree. The Runner rejects output directories equal to or beneath explicit include
directories before cleanup. Source, native adapter and GPI implementation paths
are also rejected if they name generated runner artifacts, including the lease.
These checks compare normalized paths; they do not establish general filesystem
identity protection against alternate names or hard-link aliases. The same input set is checked
again before accepting results, detecting observed content changes, additions or
removals. This is before/after attestation, not a filesystem snapshot or a claim
that every transitive native dependency has been qualified.

Success, failure, and process reports are written to temporary files, flushed,
and atomically replaced. The parity orchestrator clears known stale reports before
building, preserves unrelated files, and refuses cleanup of locked child workspaces.

After provisioning the pinned adapter and building the solution, run
`dotnet run --project tools/SharpTestBench.Tools -c Release -- runner-manifest-check` to qualify real include/define/parameter
handling, an intentional parameter-width failure, exact selection from a multi-case
assembly, parity of selected-case seeds, and retained input provenance. The parity
workflow runs this check for its Counter/seed-1 case and uploads the evidence.

This remains a focused qualification step for issues #60 and #61. Full native
shutdown health instrumentation, independent adapter qualification, cancellation
of the runner itself, Verilator parity through this CLI, and wider native-runner
platform qualification remain separate work.

## Explicit expected stimulus seeds

The optional v1 manifest `expectedSeeds` object maps every expected qualified
identity to its exact UInt64 seed. It requires `expectedNames` and exactly the
same identity set. Passed and failed result rows must match their own expected
seed; row order and the base seed do not substitute for this per-case contract.
This permits filtered runs whose case ordinal produces a seed above the base.

Current fixture producers bind expected identities, selection and seed maps
consistently. The parent parity command also checks both result sets against the
map, including negative controls, and adds `expectedSeedMatched` to its report.
Two runners agreeing on the same wrong stimulus cannot qualify through that path.
Older manifests without the field retain numeric seed validation and pairwise
agreement; they do not gain expected-stimulus attestation automatically. Older
Runner binaries reject this new optional field as unknown: rebuild matching
workers before consuming newly emitted manifests. Schema version and existing
evidence fields remain unchanged.

Malformed Unicode property names are configuration errors in both shared strict
JSON parsing and the production Runner CLI, including JSON-reflection-disabled
execution. Valid supplementary Unicode keys/values remain accepted by the generic
JSON parser; manifest unknown-field policy remains separate.

## Reporting failure qualification

Run the local C# `reporting-check` helper after a Release build. It discovers the
selected integration case and binds its actual per-case seed, then invokes both
runners for two negative controls: an exception containing NUL/BEL and valid
supplementary Unicode/XML metacharacters; and a directory blocking final JUnit
replacement. It requires failed child exits without watchdog expiry, preserved
JSON control characters, valid visibly escaped XML, primary-result retirement
and standalone infrastructure-failure evidence as appropriate. Workspaces use
unique local build/reporting-* directories and retain logs and reports.

The runtime pre-renders every report before changing evidence, atomically replaces
each file and requests simulator completion in finally. This is per-file atomicity,
not a four-file transaction. If publication fails after this attempt promoted its
primary result JSON, that primary is retired. Preparation failures preserve prior
completed evidence. Both parent paths independently scan both output streams for
the reserved CS_REPORT_FAILURE diagnostic and reject it even with zero simulator
exit status or parseable passing results. Retirement is best effort; the diagnostic
is authoritative when cleanup also fails. Fault injection controls are removed
from ordinary parity children before fixture-specific overrides.

JSON retains diagnostic control characters through the normal .NET serializer.
XML sanitization does not alter that source error; invalid UTF-16 may still follow
the JSON serializer's replacement policy. This is not a claim that arbitrary
ill-formed UTF-16 has a lossless UTF-8 JSON representation.

## Standalone compiled image evidence

Standalone Runner requires a successful compiler process to leave a nonempty
`top.vvp` before simulation starts. A zero compiler exit without that artifact is
a build failure. It fingerprints the image before simulation and rechecks the
bytes afterward before attesting results. Completed v1 run evidence adds a
`Compiled` object with Role, Path, Bytes and Sha256; prior fields and schemaVersion
remain intact. This is snapshot integrity, not a claim of immutable storage or
validation of arbitrary compiler output semantics. Legacy runner evidence has a
different schema and does not gain this field.

Controlled C# child tests exercise the production managed entry point with JSON
reflection disabled. They establish orchestration/evidence behavior only; actual
HDL simulation, native host loading and NativeAOT publication use separate gates.


## Local legacy invocation safety

The `run` CLI validates canonical input/output ownership before removing old
reports or waveform data. It protects the managed build's `bin` and `obj` trees
and dedicated backend directories (`verilator`, `nvc-work`, `ghdl-work`). GHDL
analysis, elaboration and simulation share `ghdl-work`. Inherited JSON log paths
are made absolute before launch, so backend working directories cannot change
the validated destination. This protects known outputs; it is not a compiler
sandbox. SDK/adapter discovery failures before full ownership verification leave
previous reports intact. After verification, old completion sidecars are retired
before assembly preparation or compilation.

Icarus requires fresh nonempty compiled output. The legacy compile sidecar records
its SHA256 and byte count; the image is checked again before accepting results.
Standalone run evidence also binds the compiled image. These are before/after
snapshots and do not detect a mutation restored between checks.

Legacy completion must be an array of objects with unique valid identities,
unsigned64 numeric seeds and `passed`, `failed` or `skipped` statuses. Ordinary
runs require at least one result and may retain skipped cases. Discovery may
produce an empty result array. The stricter manifest CI attestation still rejects
skips and requires its expected identities/count/seeds. Standalone supplemental
coverage/discovery and trace sidecars are retired alongside primary artifacts;
canonical manifest and runtime-config aliases are rejected before cleanup.

Standalone GPI implementation library paths are resolved against the manifest
directory before passing them to the child. Summary source and assembly hashes
reuse the same verified streaming fingerprint records as input evidence.
