# Alpha packages and installed consumer validation

`python tools/native_package.py` builds three packages in `build/packages-RID`:

- SharpTestBench: managed runtime, ABI source/headers and licensing information.
- SharpTestBench.Generators: Roslyn analyzer assembly, with no runtime Roslyn dependency.
- SharpTestBench.Native.RID: native host, pinned GPI core, bootstrap and Icarus adapter,
  licenses, source revision and SHA-256 manifest, plus a standalone Python runner.

The native package targets the machine that builds it: linux-x64, win-x64 or
osx-arm64 in CI. Other architectures and simulator adapters are not advertised by
these alpha packages. The source runner still supports the wider Linux matrix.
System runtime libraries remain OS prerequisites; these are not fully static binaries.

The consumer check creates a temporary directory outside the checkout, restores
only from the local feed into a fresh NuGet cache, compiles the generated counter
bindings, and runs the installed native host and adapters. Its Python process uses
isolated mode and the standalone runner imports only the standard library: no
installed cocotb Python package, repository project reference or source-built native
host is needed at runtime. .NET 10, Python 3.12 and Icarus of matching architecture
remain prerequisites. The test machine itself is a CI machine, not proof of every
possible clean-machine OS image.

Invoke the installed native package's tools/installed_run.py with --native-dir
pointing to its runtimes/RID/native directory, --assembly, --source, --top,
--entry-type and --work-dir. The runner verifies packaged file hashes, uses explicit
library paths, writes JSON/JUnit/logs and rejects an occupied workspace.

## Release policy

0.8.0-alpha packages are local/CI candidates, not publicly published NuGet releases.
Never replace an already published version. A public release needs a new immutable
version, a passing native matrix, installed-consumer evidence, checksums, source
revision, licenses and explicit supported OS/architecture/backend limitations.
CI artifacts contain the nupkg candidates and evidence. Public publication remains
tracked in #23; no credentials or publication endpoint is configured here.
