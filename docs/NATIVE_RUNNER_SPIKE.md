# Native .NET consumer runner — initial spike (issue #57 / #60)

This branch introduces a small standalone .NET 10 command-line runner for a **prebuilt**
SharpTestBench test assembly and native host. It does **not** import Python or run Python
cocotb tests. It requires locally provisioned native libraries; current adapter
binary remains the existing cocotb-derived GPI implementation. This is a
development proof of interface, not the final independent-adapter milestone.

Run:

```sh
dotnet run --project tools/SharpTestBench.Runner -- run path/to/manifest.json
```

A manifest example (relative paths are resolved from the manifest directory):

```json
{
  "schemaVersion": 1,
  "simulator": "icarus",
  "top": "top",
  "sources": ["top.sv"],
  "assembly": "bin/Tests.dll",
  "nativeHost": "native/libsharptestbench_host.so",
  "icarusBootstrap": "native/cocotb_bootstrap.vpi",
  "gpiLibrary": "native/libcocotb.so",
  "dotnetRoot": "/usr/share/dotnet",
  "outputDirectory": "build/verification",
  "seed": 1,
  "gpiEntryPoint": "entry_point"
}
```

**Current limitations:** Icarus-only, prebuilt managed/native binaries, no custom
simulator build options, native bridge bootstrap symbol and GPI library paths
must be supplied, fixed 120-second watchdog. The sample names above are
placeholders, not verified filenames: consult `tools/run.py` and the installed
adapter for the correct bootstrap and GPI locations. Neither full packaging nor
CI validation is claimed. Python scripts remain in the repository for legacy
development workflows while the adapter migration proceeds.

Future increments: extract and test manifest parsing, expose executable build
and dependency provisioning, replace GPI bootstrap ownership, add Verilator,
capability detection and structured failure categories; qualify Windows and macOS.
