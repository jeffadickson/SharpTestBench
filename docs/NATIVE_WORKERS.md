# C# workers and native deployment

Repository-owned production helper orchestration lives in `tools/SharpTestBench.Tools`; the standalone runner lives in `tools/SharpTestBench.Runner`. No Python helper implementations remain under `tools`. Python remains for independent cocotb scoreboards, contract-test RPC bindings, Sphinx configuration and narrow upstream APIs (including Verilator build and upstream oracle access). This is C# ownership, not a Python-free project.

## Local builds

Install .NET SDK 10.0.402 and GNU Make. Run `make build`, `make test`, or `make coverage`. Coverlet 10.1.0 and ReportGenerator 5.5.11 are pinned in `.config/dotnet-tools.json`; the C# coverage command collects core, generator, helper and contract evidence and validates merged Cobertura/JSON counts. The default merged report covers SharpTestBench, SharpTestBench.Generators, SharpTestBench.Tools and SharpTestBench.Runner. Runner contracts exercise the production assembly through RunnerValidation; the standalone CLI entry point and native machine instructions require separate execution evidence. Coverage describes managed assemblies, not HDL. See [coverage scope and generated sources](CODE_COVERAGE.md).

## Native workers

`make native` publishes both workers for the current host. `make native-tools` publishes the full helper suite as a directory-based, self-contained ReadyToRun executable. It contains native precompiled code and a bundled .NET runtime, with JIT fallback; it is **not NativeAOT**. Reflection-based JSON and all helper commands remain supported. Do not publish the full suite as a trimmed/single-file/AOT binary.

`make native-runner` publishes the standalone Icarus runner with **NativeAOT** and generated JSON metadata. Its orchestration executable needs no installed .NET runtime. Simulation test assemblies still execute through the native host and installed .NET hostfxr/runtime in the separate simulator process. The SDK, Icarus, native bridge and GPI adapter remain required for the corresponding build/simulation operations.

Outputs are `build/workers/<rid>/tools` and `build/workers/<rid>/runner`. `WORKER_OUTPUT` may select another directory below the checkout's `build/` directory. Outputs include a complete SHA-256 file manifest. `make verify-native` validates file sets and hashes; `make run-native-tools ARGS="--help"` and `make run-native-runner ARGS="--help"` execute the produced apphosts directly. Run either executable directly from its output directory for ordinary use. Source-dependent helper commands require a checkout as the current directory or an ancestor of the published executable.

Publishing is restricted to matching OS/architecture: linux-x64, linux-arm64, win-x64, win-arm64, osx-x64 and osx-arm64. This is a supported build-plan matrix, not a claim every host was tested. NativeAOT requires the platform compiler/linker prerequisites: clang and zlib development files on Linux, Xcode command-line tools on macOS, and the Visual C++ build tools on Windows. Build on the target host; cross-OS compilation is rejected. Local validation evidence identifies the platforms actually exercised. See Microsoft's [NativeAOT deployment requirements](https://learn.microsoft.com/dotnet/core/deploying/native-aot/).

GitHub CI remains administratively disabled. These Make targets run locally and do not enable workflows.
