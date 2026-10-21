# Upstream AXI Stream register qualification

The unchanged MIT `axis_register.v` is vendored from
[alexforencich/verilog-axis](https://github.com/alexforencich/verilog-axis/tree/48ff7a7e2ef782cf778d47910cf85835c64b1bce).
The source, license, revision, and SHA-256 values are in `tests/Fixtures/verilog-axis`.
No upstream test scripts are executed and no runtime downloads are needed for this fixture.

`dotnet run --project tools/SharpTestBench.Tools -c Release -- axis-ci --mode 2 --seed 17` runs the independent legacy and native
Icarus paths and compares exact test identity, seed, exit status, and normalized cycle traces.
Modes 0/1/2 cover bypass, simple register, and skid buffer. `--disabled-sidebands`
uses mode 2 with keep/last/id/dest/user propagation disabled.

The managed scoreboard maintains a queue of accepted beats and checks every valid
output, including stalled output stability. It checks all sidebands, ordering,
reset flushing, conservation, and complete draining. Stimulus includes idle source
opportunities, deterministic data, sink backpressure, and a midstream reset.
`--negative-control` corrupts the wrapper data bit; both paths must report the
named `AXIS_MISMATCH` scoreboard failure. Crashes and empty results do not qualify.

CI tests all four configurations with seeds 1 and 17 and retains plans, source
fingerprints, child logs, traces, and parity reports under `build/axis-*`.
This qualifies these configurations on Icarus; it does not claim full AXI protocol
coverage or upstream project certification.

The same C# stream scoreboard also drives the separately pinned FIFO fixture. See
[the FIFO qualification guide](../../docs/FIFO_FIXTURE.md) for capacity, pipeline,
sideband, reset and failure scope.
