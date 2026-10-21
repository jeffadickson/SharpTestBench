# Controlled Runner CLI child

This test-only executable stands in for compiler and simulator subprocesses when
testing production Runner entry-point control flow. It never loads a native host,
executes HDL, or certifies a simulator. The parent production binary retains its
normal reflection-disabled JSON configuration. Actual Icarus/Verilator and native
publication qualification are separate gates.

`test_runner_cli_completion.py` creates isolated temporary inputs and behavior JSON.
The fixture writes a placeholder compiler output, then controlled result bytes,
environment observations or failure diagnostics. This reaches production success,
functional failure, result attestation, input-integrity and process-failure paths
without adding Python production orchestration. It has no NuGet dependencies.
