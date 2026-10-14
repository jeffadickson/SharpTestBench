# Waveform capture

`python tools/run.py --sim icarus --waves build/debug.vcd` enables a run-wide VCD
at a chosen path. Omit --waves to disable capture. JSON test results gain an Artifacts
entry; JUnit testcases gain a waveform property; a sibling `LABEL-artifacts.json`
manifest records the simulator, result filename, relative waveform path and file
size. Test StartStep/EndStep locate each case within the shared run-wide trace.
Artifact paths are relative to the results directory. CI uploads VCD and manifests.

| Backend | Mechanism | Scope/limits |
|---|---|---|
| Icarus | Generated helper root with $dumpfile/$dumpvars | Requires a simple top-level identifier; no user HDL edits |
| Verilator | Trace-enabled build and --trace-file | Rebuilds with trace support; retains Verilator's two-state behavior |
| GHDL | --vcd | Simulator's VCD type limitations apply |
| NVC | --wave plus --format=vcd | Simulator's VCD type limitations apply |

Only VCD is currently accepted. Capture covers the full run; dynamic on/off windows,
FST output, viewers and commercial backends are not implemented. VHDL records,
strings, enums and unpacked arrays may be omitted or represented differently by
simulator VCD exporters. A complete object/value API does not imply complete VCD
representation. The checks verify declared data and observed values, not every HDL
type's dump format.

The selected output path is overwritten for each enabled run. Paths with spaces
are supported. Discovery-only listing cannot be combined with capture. A missing
or empty requested waveform fails the runner; when a test fails, an available VCD
is still linked before the runner returns failure. Native crashes can leave partial
VCDs. Disabling capture removes stale manifest/result associations but does not
delete previously saved waveform files. The Icarus helper is gated by a managed-run
plusarg so subsequent Python reference simulations cannot overwrite the saved VCD.

`tools/wave_check.py --label LABEL` checks actual VCD declarations, timestamps and
known data transitions plus JSON/JUnit references. `tools/wave_tests.py` checks
failure retention, a path with spaces and disabled capture without stale links.
These controls are runner features; direct invocation of a simulator requires
configuring its native waveform flags separately.
