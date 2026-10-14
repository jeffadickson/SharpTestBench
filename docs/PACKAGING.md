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

## Native dependency and integrity gates

Package staging removes stale generated files. Native import reports are retained:
Linux uses ldd with only the staged native directory, macOS uses otool, and Windows
uses PE imports from objdump. Missing/non-system dependencies fail the build.
Windows VC++ runtime imports remain explicit OS prerequisites; they are not silently
redistributed. Unix installed execution clears inherited native library search paths.
RID and hash mismatches are tested as failures before starting a simulator.

Directory.Build.props is the version source for managed, generator and native
packages. Each native manifest records the repository revision, dirty-tree flag,
upstream provenance, imports and file hashes. CI packages are candidates; local
packages from a dirty tree cannot be assembled into the release bundle. This does
not yet establish byte-for-byte reproducible native builds across toolchain versions.

## Release preparation and publication

The release-bundle CI job waits for all three native consumers. It requires their
expected counter result, RID/checksum negative checks, consistent clean source
revision and version, and package hashes. It selects exactly five packages: runtime,
generator and linux-x64/win-x64/osx-arm64 native packages. The Linux copies of the
platform-neutral packages are canonical. It writes release-manifest.json and
SHA256SUMS. Seven tooling tests exercise acceptance and rejection paths.

The manual release.yml workflow accepts a successful main-push CI run ID. With
publish=false it produces a reviewable bundle without publishing. With publish=true
it checks that every package version is absent on nuget.org, then uses NuGet trusted
publishing and pushes the exact validated bytes. There is no rebuild at publication
and no skip-duplicate flag. Multi-package NuGet publication is not atomic: a partial
network failure requires inspection and a deliberate recovery/re-version decision.

Before the first public release, the owner must configure a nuget.org trusted
publisher for jeffadickson/SharpTestBench, workflow release.yml, environment
nuget-release, and set NUGET_USER to the NuGet profile name. Configure the GitHub
environment's release access/review rules as appropriate. No account, trusted policy,
or public package ownership is assumed to exist; no public publication was attempted.
See [NuGet's trusted publishing instructions](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).
These account settings are the remaining external publication prerequisite.

A further CI gate builds a consumer from the candidate feed in a digest-pinned
.NET SDK image, then copies only its output and installed native package into a
digest-pinned .NET runtime image. Python and Icarus are OS prerequisites there;
no cocotb Python package or repository is installed/mounted. Simulation runs with
network disabled and a read-only container filesystem apart from /tmp and result
artifacts. This is a separate Linux distribution/consumer environment, not proof
of every Windows/Mac clean image. OS apt packages are recorded by the build log
but are not snapshot-pinned; fully reproducible OS provisioning remains separate.
Run tools/package_container.py --artifacts <downloaded-CI-artifacts> to reproduce.
