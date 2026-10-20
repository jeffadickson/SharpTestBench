# Isolated simulator runs

Build managed/native assets once, then run concurrent simulations with separate
`--work-dir` directories and `--no-build`. An explicit workspace requires no-build
so concurrent MSBuild/native writes are not silently introduced. The default host
comes from the prebuilt root build directory; --host can select another library.

All simulator build files, generated bindings, traces, results and ordinary logs
are placed in the selected workspace. Explicit --waves/--log-json paths remain
caller-selected: provide distinct paths for concurrent runs. Labels alone do not
isolate files. Legacy runs use the root build directory and are serialized by the
same exclusive lock. An occupied workspace is rejected; after a process crash,
remove its stale lock only after confirming the process exited.

`tools/SharpTestBench.Tools/StageCommand.cs` starts independent success and deliberate-failure
processes with the same label, verifies isolated results and HDL outputs, and tests
occupied-directory rejection. Existing Python subprocess orchestration is retained:
xUnit is useful for managed/compiler cases, while these native process and artifact
checks have no demonstrated need for a second orchestration framework.
