# Local requirement contracts

All 100 distinct behavior contracts cover clocks, waits, jobs, primitives, queues,
handles, writes, callbacks and cleanup. This separate assembly preserves Integration
case discovery/seed order. Explicit failures complement cross-runner agreement.

    dotnet tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll contracts-ci --through 100 --seed 83
    dotnet tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll contracts-ci --integration-regressions

The command verifies discovery producer inputs and exact effective per-case seeds.
Windows/Linux both Icarus runners and Linux Verilator executed100/100. The eight
integration regressions are existing tests, not additional numbered engineering units.
See docs/development-history/requirements100-report.md for fresh managed coverage,
publication evidence and scope limits. This is selected conformance, not complete
cocotb equivalence or native-clock implementation qualification.
