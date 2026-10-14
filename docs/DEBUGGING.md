# Debugger validation and remaining limits

The DebugIntegration fixture and tools/debugger_tests.py are an opt-in reproducer
for #27. Managed attachment passes on Linux CI with pinned NetCoreDbg 3.2.0-1092.
The Linux job requires the check to pass and retains its evidence.

Build portable PDBs and run with Icarus and a local NetCoreDbg executable:

```sh
dotnet build tests/DebugIntegration -c Debug
python tools/debugger_tests.py --debugger /absolute/path/to/netcoredbg
```

Use the same CS_DOTNET_ROOT, CS_IVERILOG, CS_IVL_ROOT and CS_VVP overrides as the
normal source runner. Build the native host first with tools/run.py. The harness
waits for the simulator's PID, attaches through DAP, sets a source breakpoint after
an awaited timer, expects inspectedValue == 41, resumes to a deliberate exception,
and checks the failed regression result and simulator exit. It preserves the DAP
transcript and driver log under build/debugger-check. DEBUGGER_PASS is emitted only
after all these checks pass. A failed attachment is a failed check, not a skip.
The fixture opts into Linux sibling ptrace access only in its own process; it does
not modify global ptrace settings. Do not use this fixture as production bootstrap.

## Verified Linux managed result, 2026-10-14

[Linux job 116069596771](https://github.com/jeffadickson/SharpTestBench/actions/runs/38627301397/job/116069596771)
passed with .NET 10.0.13 and NetCoreDbg 3.2.0-1092. It hit the source breakpoint
after an awaited timer, evaluated inspectedValue as 41, resumed, stopped on the
expected InvalidOperationException, resumed again and checked the regression
failure and simulator process exit. The debugger archive is version- and
SHA-256-pinned in CI. This verifies managed debugging inside Icarus on Linux.

The restricted local environment fails configurationDone with 0x80070057 on both
NetCoreDbg 3.1.3-1062 and 3.2.0-1092; its fixture ptrace opt-in is also unavailable.
The successful native-host run rules out a universal framework/.NET 10 attachment
failure. It does not identify the precise cause of the local restriction.
Windows/Mac attachment and simultaneous native/managed debugging remain unverified.

For manual managed attachment, set STB_DEBUG_PID to a writable absolute path and
run the DebugIntegration DLL with tools/run.py, --no-build, --top counter,
--source examples/Counter/counter.sv and --entry-type 'Entry, DebugIntegration'.
Attach to the recorded PID within 45 seconds using the debugger's managed attach
configuration. Set a breakpoint at STB_BREAKPOINT and enable thrown exceptions.

For native investigation, build the host with debug symbols and start or attach a
native debugger to the simulator process. Set pending breakpoints on
sharptestbench_initialize and on the native callback/shutdown paths. This is an
investigation recipe, not tested mixed-debugger support; concurrent debuggers may
compete for process control. Preserve module versions, native and managed stacks,
exit status and result artifacts when reproducing the historical shutdown message.

## Shutdown evidence

A fresh 20-run cancellation/teardown stress plus leak and early-HDL-finish negative
cases passed (22/22) with no `fatal library error` diagnostic. The native callback
fault suite also passed in CI run 38626020220. This is non-reproduction evidence;
#16 remains open because the original diagnostic's root cause is unresolved.
