# Priority encoder: independent oracle and dual-runner fixture

This fixture exercises Alex Forencich's MIT-licensed `priority_encoder.v` from
the same immutable verilog-axis revision already used by the AXI register
fixture: `48ff7a7e2ef782cf778d47910cf85835c64b1bce`.
The unmodified upstream module, license and SHA-256 inventory are under
`tests/Fixtures/verilog-priority/`. The local wrapper instantiates widths
2, 3, 5, 8, 17 and 32, with both MSB-first and LSB-first priority.

```sh
dotnet build SharpTestBench.slnx -c Release -p:RestoreLockedMode=true
dotnet tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll priority-ci --seed 83
dotnet tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll priority-ci --seed 83 --negative-control
```

Use the configured pinned development adapter environment described in
`docs/ALPHA.md`. The commands run real Icarus simulations through both the
C# helper's existing runner path and the standalone Runner, without dispatching CI.

The C# scoreboard scans bits linearly, independently of the upstream HDL tree.
It checks valid, encoded index and onehot outputs for nonzero requests. For zero
requests, only valid is specified: unused outputs are normalized in reports.
The 453 masks comprise all 256 low-byte patterns, five full-width boundary masks,
32 single-bit masks and their complements, and 128 seeded full-width masks from
an explicitly specified 64-bit LCG. Every mask visits all 12 configurations;
bits above each instance width are deliberately truncated by the wrapper.
The effective per-test seed is retained in runner results.

Positive parity requires passing results with matching identities/seeds and
identical 5,436-row traces. The helper validates row dimensions, configuration
order, masks, flags, indexes and onehot consistency. This structural validator
does not recompute winners or reconstruct seeded stimulus; functional correctness
comes from the independent scoreboard in each simulator process.

The negative control flips the wrapper's valid outputs and requires both runners
to fail with `PRIORITY_MISMATCH` and matching seeds. Failure occurs on the first
vector/configuration, so this does not independently qualify fault detection in
all 12 configurations. Inherited `PRIORITY_FAULT` and `PRIORITY_REPORT` controls
are explicitly removed before fixture values are supplied. Reports use the
existing parity schema.

This is a functional test, not a throughput benchmark. Unknown-state semantics,
width-one behavior and arbiter fairness remain outside its scope.
Upstream source: https://github.com/alexforencich/verilog-axis/blob/48ff7a7e2ef782cf778d47910cf85835c64b1bce/rtl/priority_encoder.v
