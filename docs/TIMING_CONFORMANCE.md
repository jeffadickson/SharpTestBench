# Timing conformance expansion

The pinned cocotb reference is 5fa952e92c46fd9cb58f55a8655312a4bf02e7de.
`tests/Integration/TimingTests.cs` is compiled into both managed Verilog and VHDL
suites. `test_timing_order.py` independently executes equivalent operations in
Python. `tools/SharpTestBench.Tools/ReferenceCommand.cs` runs a fresh Python simulator process and requires
exact trace equality, successful process exit and successful JUnit results.

| Observation | Icarus 11 / Verilator 5.048 | GHDL 2 / NVC 1.23 |
|---|---|---|
| Read immediately after a pending deposit of 20 and Immediate(30), previously 10 | 30 | 10 |
| Read after the next ReadOnly | 20 | 20 |
| Two writes in ReadWrite, then ReadOnly | Last value, 50 | Last value, 50 |
| First of two timers at the same deadline | Two steps | Two steps |
| Combine of two timers at the same deadline | Two steps | Two steps |

VHDL signal writes retain their inertial/delta semantics even when requested as
Immediate. An earlier deferred deposit still takes effect in ReadWrite afterward.
The traces establish ordering on these fixtures, not arbitrary cross-process HDL
race ordering. The same-time First check verifies completion time and callback
cleanup; it does not promise which indistinguishable timer wins. Exact timeout
boundary policy is separate from this test.

Icarus additionally verifies force before a queued deposit flush (77), release
before the next flush (30), force followed by a ReadWrite deposit (88), and a
post-release deposit (60), all against Python. Force is deliberately skipped on
the other backends, whose force/deposit contract has not been established. These
skips are recorded rather than treated as passing force coverage.

Reproduction: first run `tools/SharpTestBench.Tools/LegacyRunCommand.cs --sim SIM --label timing-SIM`, then
`tools/SharpTestBench.Tools/ReferenceCommand.cs --sim SIM --label timing-SIM` with the same simulator paths
and VHDL library environment. The second command uses the HDL elaboration produced
by the first. Local exact traces pass for Icarus, Verilator, GHDL and NVC; CI checks
Icarus 12. Existing edge/NBA/ReadOnly, delta-chain and nine-state resolution tests
remain in place. Full upstream timing-suite parity is not implied.
