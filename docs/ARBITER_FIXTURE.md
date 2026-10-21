# Pinned arbiter fixture

The fixture retains unmodified MIT `arbiter.v` and `priority_encoder.v` from
verilog-axis commit48ff7a7e2ef782cf778d47910cf85835c64b1bce. Exact source and
COPYING hashes live in tests/Fixtures/verilog-arbiter/provenance.json. Source:
https://github.com/alexforencich/verilog-axis/blob/48ff7a7e2ef782cf778d47910cf85835c64b1bce/rtl/arbiter.v.

After the normal Release build and environment selection:

```sh
dotnet tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll arbiter-ci --ports 3 --round-robin --block ack --seed 83
dotnet tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll arbiter-ci --ports 8 --msb --block request --seed 83 --negative-control
```

Ports2/3/4/8, fixed or round-robin arbitration, no/request/ack blocking and LSB/MSB
priority define48 construction configurations. Actual executed configurations are
recorded separately; this is not certification of the full matrix or an AXI
multi-source protocol driver. `--count`1..2048 controls seeded random cycles after
the directed reset, continuous-request, held-grant and release prefix. Initial and
final reset are required. The default count is128 and seed1.

C# owns the generated top, stimulus, builds, both Icarus runners, seed attestation
and evidence. The C# HDL scoreboard and parent share a circular-scan policy model.
A separate data-only Python oracle uses ordered-list rotation and replays every
positive trace independently; it performs no build or simulator orchestration.
The shared C# model alone is not independent proof. The seven-column trace is
cycle/reset/request/acknowledge/grant/valid/encoded; counters are cycles, valid,
idle, resets and grant transitions. Invalid grants normalize to zero with encoded0.

Request blocking holds while the granted request remains asserted regardless of
acknowledgement. Acknowledgement blocking holds even after request withdrawal until
the granted port is acknowledged. Round-robin preference advances on selection,
not on idle cycles or a held grant; reset restarts the configured preference.
No starvation-freedom claim applies when a blocking client never releases.

Positive qualification requires matching traces, requested controls, exact seeds,
policy/counter validation, the independent oracle and unchanged source/plan/config/
wrapper/oracle hashes. A negative control flips grant bit0 only when valid; vendor
RTL stays unchanged. Both runners must fail with ARBITER_MISMATCH and exact seeds.
Negative runs do not claim successful trace/oracle validation. Existing schema1
parity evidence fields remain, with configuration and hash metadata added.
Checkpoint100 executed six configurations, each positive and corrupt-grant, on
Windows/Linux Icarus through both runners. See the campaign report for exact cases.
One three-port round-robin case also passed Linux Verilator and Linux NativeAOT
Runner, each with independent replay. These cases do not certify all48 settings.
The oracle hash covers arbiter_reference.py; its imported fifo_reference.py parser
is outside that hash, so complete transitive dependency attestation is not claimed.
