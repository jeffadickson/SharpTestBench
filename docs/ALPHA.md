# Alpha target and platform gates

The alpha target includes native Windows x64, macOS and Linux execution. Windows
and macOS are required targets, not presumed supported because managed code builds.
The platform CI jobs compile the host and execute the Icarus integration suite,
VCD checks and failure/timeout cleanup scenarios. Check actual job outcomes before
claiming support for a platform or architecture. Linux's wider simulator matrix
remains separate; an Icarus pass does not certify every backend on that OS.

## Native execution evidence

[CI run 38620959862](https://github.com/jeffadickson/SharpTestBench/actions/runs/38620959862)
validates commit `36259572f9f0ffe2b90eb3b5c163f72a1feeaf4f` on Linux x64, Windows x64 and
macOS ARM64. Each passed 364 managed tests, 49 Icarus integration tests (one explicit
skip), waveform checks, the counter example, and six cleanup scenarios.
Intel Macs and Windows ARM64 are not validated.

## Build and try the source example

Install .NET SDK **10.0.402** (global.json pins it), Python 3.12, Icarus Verilog and
an appropriate native compiler. Linux uses GCC; macOS uses Clang from the Xcode
command-line tools; Windows uses MinGW-w64 GCC for this host (GNU typeof is used in
the ABI shim). Building the pinned cocotb adapters on Windows also needs the C++
toolchain selected by CMake, normally Visual Studio Build Tools. Use 64-bit tools
of matching architecture. Windows/macOS CI records the tested installation recipe.

From the repository root, in a Python virtual environment:

```sh
python tools/prepare_upstream.py
python -m pip install ./build/upstream/cocotb-source pytest
python tools/doctor.py
dotnet build SharpTestBench.slnx -c Release -p:RestoreLockedMode=true
python tools/managed_tests.py
dotnet build examples/Counter -c Release
python tools/run.py --sim icarus --assembly examples/Counter/bin/Release/net10.0/Counter.dll --source examples/Counter/counter.sv --top counter --entry-type "Entry, Counter" --label counter --waves build/counter.vcd
```

The example asserts reset and four increments on an actual Verilog counter. It
writes JSON/JUnit results, logs and a VCD under build/. The driver has a wall-clock
timeout as well as the simulation-step test timeout. Doctor is read-only; it reports
missing prerequisites and does not install OS packages. Dependency installation
is not yet a one-command bootstrap; Python build dependencies remain a release
reproducibility gap. Native NuGet candidates now have per-platform build and installed-consumer checks;
see PACKAGING.md for the current supported scope and CI evidence. These source instructions do
not imply a published cross-platform NuGet release.

## Roslyn's role

Roslyn is the compiler platform used to compile C#. The incremental source
generator (#20) consumes an explicit HDL interface manifest and produces
strongly typed DUT bindings at build time, with diagnostics for invalid mappings.
For example, an 8-bit HDL port could appear as Signal<W8>, making width mismatches
visible during C# compilation. It does not simulate HDL, synthesize circuits, or
recover missing simulator metadata by guessing. Existing runtime-generated binding
files remain available alongside this build-time generator; see GENERATOR.md.

## Remaining alpha gates

- Preserve the green Windows x64, macOS ARM64 and Linux x64 native matrix; finish architecture-specific packaging (#24/#25).
- Package native GPI dependencies with provenance and validate a consumer outside the repository (#22/#23).
- Isolate simulator build workspaces before concurrent orchestration (#41).
- Prove managed/native debugger attachment (#27).
- Complete the API audit and investigate the historical shutdown diagnostic (#1/#16).

Native clocks, AOT and optional mocking/HTTP libraries are not required to try the
source alpha. Publication requires the supported platform matrix and installation
limitations to be explicit. No full cocotb-parity claim is made.
