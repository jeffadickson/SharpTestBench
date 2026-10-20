# Timing and waveform evidence

Managed timing traces and fresh pinned-Python reference traces match on Icarus 11,
Verilator 5.048, GHDL 2 and NVC 1.23. Icarus additionally matches force/deposit
ordering. VCD files contain real signal transitions; JSON and JUnit link to them
using paths relative to this directory. tools/SharpTestBench.Tools/ValidationCommands.cs and wave_tests.py
verify content, links, failure retention and disabled capture.

Full managed suites: Icarus 46 pass/1 skip; Verilator 45 pass/2 skips;
GHDL 11 pass/6 skips; NVC 16 pass/1 skip. Force on unvalidated backends is
explicitly skipped. See TIMING_CONFORMANCE.md and WAVEFORMS.md for limits.
