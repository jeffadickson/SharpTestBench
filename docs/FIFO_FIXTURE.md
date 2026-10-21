# Pinned AXI-stream FIFO qualification

The FIFO fixture uses unmodified MIT `axis_fifo.v` from verilog-axis commit
48ff7a7e2ef782cf778d47910cf85835c64b1bce. Source and license hashes are committed
in tests/Fixtures/verilog-fifo/provenance.json. The C# verifier rejects changed
source, licensing metadata or repository/revision identity. The source is at
https://github.com/alexforencich/verilog-axis/blob/48ff7a7e2ef782cf778d47910cf85835c64b1bce/rtl/axis_fifo.v.

After selecting the existing development environment and building Release:

```sh
dotnet tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll fifo-ci --seed 83 --depth 8 --pipeline 1 --count 256 --stall 7 --reset-at 128
dotnet tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll fifo-ci --seed 83 --depth 8 --pipeline 1 --negative-control
```

`--depth` is RAM capacity in stream beats: powers of two from 2 through 64.
`--pipeline` selects 0 through 3 RAM pipeline stages. `--output-fifo` enables the
upstream output FIFO; `--disabled-sidebands` selects its documented keep/last/ID/
destination/user defaults. These options define 96 construction configurations;
actual executed cases are recorded separately in the development report.
Count is 1 through 2048, stall interval is 0 through 32, and `--reset-at none`
disables the mid-run reset. The default reset is halfway through active stimulus.
Frame, pause, oversize/drop and mark-when-full policies are disabled in this fixture.
Full AXI bursts, packet-aware frame dropping and multi-source arbitration remain
separate work. This fixture does not establish the complete upstream FIFO matrix.

C# owns builds, the generated HDL top, both Icarus runner invocations and evidence.
The existing C# stream scoreboard holds pending source data until input acceptance,
checks stalled output and accepted order, and accounts for reset flushing. An
Offer row creates a source beat only when no prior beat is pending. Capacity-based
drain cycles include room for RAM/pipeline/output buffering and a pending source.
The parent independently binds trace payloads and ready/reset controls to the plan,
then calls a data-only Python deque oracle for each positive trace. The Python
oracle performs no builds or simulator orchestration and needs no cocotb install.

The trace retains the existing 11-column AXI schema: cycle, reset, accepted,
output-valid, output-ready, data, keep, last, ID, destination and user. Counters
retain accepted/consumed/flushed/stalls/resets/cycles. Positive runs require
complete draining, source/plan/adapter/oracle hash stability, matching per-case
seeds and matching runner traces. The functional replay validates ordering and
ownership; it is not a fixed pipeline-latency model.

The negative control flips one output payload bit in the generated wrapper;
vendor RTL remains unchanged. Both runners must fail with AXIS_MISMATCH and the
expected seed. Positive trace/oracle success is not claimed for negative runs.
Reports retain schemaVersion1 and existing parity fields, with FIFO configuration
and input hash metadata added. Fresh artifacts live beneath build/fifo-*.
Checkpoint60 executed eight configurations on Windows/Linux Icarus, each with
positive independent-oracle replay and payload-corruption controls in both runners.
Depths2/8/32/64, pipelines0/1/2/3, both output-buffer and sideband settings were
represented; these eight cases do not certify all96 combinations. Linux Verilator
also passed one buffered depth8/pipeline2 case with independent trace replay.
See the requirements campaign report for exact scope.
