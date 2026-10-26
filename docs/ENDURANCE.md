# Reproducible endurance verification

The endurance workflow runs Stryker.NET mutation checks followed by changing-seed
FsCheck properties and randomized malformed-regex inputs. It defaults to **five
hours**. Use an explicit short duration while developing the workflow. Nothing
installs a schedule or starts a detached run unless `-Background` is supplied.

Requirements are maintained in `requirements/endurance.sdoc` (STB-END-001 through
STB-END-009). Active requirements are obligations, not claims of verified closure.

The implementation reuses BCL monotonic timing/process/file APIs and the existing
Git CLI rather than adding a service host or Git binding. FsCheck supplies
replay and shrinking through the repository's existing dependency. Stryker.NET
supplies generated .NET mutants; the private source-linked driver keeps its
instrumentation separate from the full helper assembly. This reduces extra
dependencies while leaving whole-product mutation adequacy and coverage-guided
fuzzing as explicitly unmeasured work.

## Existing checkout

Run PowerShell 7.4 or newer from a trusted checkout. Check the directory first:

```powershell
$checkout = Read-Host 'Existing SharpTestBench checkout'
if (!(Test-Path -LiteralPath $checkout -PathType Container)) { throw 'Checkout missing' }
Set-Location -LiteralPath $checkout
& ./tools/Setup-Endurance.ps1 -Repository $checkout
& ./tools/Endurance.ps1 -Duration 00:03:00 -Seed 20261026 -PropertyCases 100 -FuzzCases 200
```

`make endurance ENDURANCE_ARGS="-Duration 00:03:00 -Seed 20261026"` is the existing
GNU Make frontend. `make endurance` requests the five-hour default. Do not use it
as a quick smoke test. The direct compiled CLI is
`dotnet tools/SharpTestBench.Tools/bin/Release/net10.0/SharpTestBench.Tools.dll endurance --help`.
The PowerShell wrapper incrementally builds that host with locked restore.

Source preparation, dependency discovery and wrapper host compilation precede
the campaign budget. Snapshot campaign-project compilation, local tool restore, Stryker,
supplemental guard builds/tests and all property/fuzz batches share one monotonic
budget. Optional coverage follows it and has separate timing. A short budget can
expire during compilation or mutation before any property batch finishes; that
is **partial**, not a successful campaign. Child cleanup is bounded separately.

`SetupSeconds` measures the C# source snapshot and tool-discovery phase. Earlier
PowerShell dependency provisioning and host bootstrap are outside that field as
well as outside the campaign budget. Baseline verification has its own bounded
execution before the campaign. These fields are not an end-to-end job duration.

Setup reuses the SDK selected by `global.json` (10.0.402), Python 3.12, an existing
StrictDoc 0.30.4 environment and repository-local .NET tools. `-InstallMissing`
allows isolated SDK/StrictDoc installation. SDK URLs and SHA-512 values are pinned
in `tools/Endurance/dependencies.json`; fresh StrictDoc environments use the
hash-locked, wheels-only PyPI inventory alongside it. Existing matching StrictDoc
environments are reused without replacing their transitive packages. NuGet uses
the committed package locks and local tool manifest. Stryker.NET is pinned to
5.0.0; Coverlet remains 10.1.0. No administrator/sudo operation is attempted.

Bootstrap supports Windows x64 and Ubuntu 24.04/Debian 12 x64. Git and PowerShell
7.4+, Python 3.12 and OS .NET runtime prerequisites must already be available.
Installing those system prerequisites, configuring a Git credential manager or
SSH agent, and granting repository push access are explicit operator tasks.
Other architectures and distributions are unavailable rather than qualified by
a RID name. Simulator and native compiler packages are not required by this
scoped managed campaign; it does not qualify native simulation or publication.

## Dedicated machine workflow

Use a reviewed copy of the launcher. The destination must **not exist**, even as
an empty directory. Its parent is checked; no existing checkout is reset.

```powershell
$trusted = Read-Host 'Existing trusted SharpTestBench checkout containing the launcher'
if (!(Test-Path -LiteralPath $trusted -PathType Container)) { throw 'Trusted checkout missing' }
$parent = Read-Host 'Existing parent directory for a dedicated new checkout'
if (!(Test-Path -LiteralPath $parent -PathType Container)) { throw 'Parent missing' }
$workspace = Join-Path $parent ('stb-endurance-' + [Guid]::NewGuid().ToString('N'))
& (Join-Path $trusted 'tools/Remote-Endurance.ps1') -Workspace $workspace `
  -SourceRef main -Duration 00:03:00 -Seed 20261026 -MachineLabel qualification
```

The standalone command clones a separate checkout with LF source bytes, selects
the trusted source commit detached, reuses/bootstraps dependencies, preflights
clean source byte identity and remote access, builds and runs the managed
baseline and StrictDoc export, runs the campaign, attempts scoped coverage, then
publishes a small result branch. Baseline failure/unavailability is preserved as
finished failure/unavailable evidence. A missing baseline report prevents unsafe
publication. A dry-run Git push cannot guarantee acceptance by later server hooks.
No token is accepted in parameters or repository URLs. Authentication prompts are
disabled for the standalone command; configure Git authentication beforehand.

## What is measured

The baseline builds the solution and executes Core, Generators and `Endurance*`
Tools tests with exact discovery/result inventory checks, plus StrictDoc export.
It does not replace the complete repository managed/retained-contract baseline.
Full discovery JSON has a separate16MiB capture bound so the complete inventory
can exceed the128KiB diagnostic prefix without being silently truncated. These
larger baseline discovery files remain local; they are not result-branch payloads.
Campaign coverage measures only production `SharpTestBench.Tools.Endurance*`
types through their unit/integration tests. Reports carry covered/valid line and
branch counts; unavailable measurements remain null. Other production types,
live child campaign instructions, HDL and NativeAOT instructions are unmeasured.
Use `make coverage` separately for the established four-production-assembly
coverage scope described in `CODE_COVERAGE.md`. Do not compare different scopes
as though their percentages were equivalent.

Stryker uses a small project source-linking the **actual production** evidence
rules/models. The selected six guard conditions concern finished lifecycle,
portable paths, checksums, clean source, revision identity and coverage evidence.
The private mutation driver uses xUnit v2/VSTest; existing xUnit v3 suites remain
unchanged. Mutation ranges are UTF-16 character offsets. The raw Stryker report
is retained locally and checksum-bound to its aggregate. Mutants outside the
selected spans, including compilation-invalid ones, are counted separately.
Selected survivors, invalids, timeouts, uncovered or pending mutants prevent
successful completion. Ignored mutants are visible and are not killed mutants.
Supplemental explicit guard-deletion probes are recorded separately; neither
their results nor this small Stryker scope establish whole-project adequacy.

FsCheck checks literal equality, OR algebra and immutable matcher snapshots
against independent string/algebra expectations. It shrinks property failures.
Malformed regex generation uses a deterministic bounded alphabet and lengths;
expected parse errors and match timeouts are counted, not treated as crashes.
This is randomized input testing, **not coverage-guided fuzzing**. Each complete
batch uses a recorded UInt64 seed, then an odd-increment full-period sequence.
Seeds are reproducibility controls, not cryptographic random values. The first
failure is emitted before shrinking; the last observed counterexample is retained
with an exact seed/count replay. An interrupted shrink is not claimed minimal.

```powershell
# After checking/changing into the trusted checkout as above:
& ./tools/Endurance.ps1 -Mode Replay -Seed 20261026 -PropertyCases 100 -FuzzCases 200
```

## Lifecycle, resources and recovery

Each run owns `build/endurance/<GUID>/` with an immutable-input snapshot, local
owner identity, atomic report checkpoints and bounded logs. A checkout lease
rejects overlap. Children run serially; default timeout is 300 seconds through
the wrapper, with 128 KiB retained per stream. A ring retains eight successful
batch captures. Previous run directories are preserved. A 512 MiB artifact scan
requests cancellation at most once per second; this is not a hard disk quota and
can overshoot. The source snapshot has a separate 256 MiB bound. Case counts
include verified complete batches only; partial final work has unknown counts.

States distinguish running, completed-budget, partial, interrupted, failed and
unavailable. Checkpoint replacement retries transient sharing failures six times
with bounded backoff, then propagates the error. A permanently unwritable path
cannot be promised a fresh checkpoint. Abrupt machine termination can lose work
since the last checkpoint. `ActualSecondsIsLowerBound` identifies recovered time.

Use `-Background` for an explicit detached launch; it returns launcher PID, run ID
and local log paths. On Windows its window is hidden. Linux detached launch
requires the existing util-linux `setsid` command to create a separate session;
missing `setsid` is an explicit failure. Stop and recover commands:

```powershell
& ./tools/Endurance.ps1 -Mode Stop -Run '<local-run-GUID-without-hyphens>'
# Only after confirming the campaign owner has exited:
& ./tools/Endurance.ps1 -Mode Recover -Run '<local-run-GUID-without-hyphens>'
```

Recovery rejects a live PID with the recorded creation time, reconciles stale
ring-log hashes, and marks interruption/failure rather than inventing completion.
Retain local snapshots, logs and counterexamples until investigation is complete.

## Safe results transport and review

Publication requires a finished validated report, clean committed source and
exact source hash inventory. Autocrlf/clean-smudge filters can make a Git-clean
working tree byte-different; preflight rejects that mismatch. The dedicated clone
uses LF bytes to avoid this ambiguity. A dirty local run is useful locally but
cannot be published as clean evidence.

The only result tree files are `reports/endurance/<run-id>/summary.json` and
`checksums.json`. Each bounded summary uses allowlisted fields, portable relative
paths and hashes. Raw logs, corpora, binaries, local absolute paths and credentials
remain local. Branch names are unique
`remote-runs/<platform>-<arch>-<label>/<UTC>-<source-prefix>-<nonce>`.
The publisher uses a private Git index and normal non-forced push, never checking
out/merging or modifying main. On upload failure it retains the local result ref,
commit and `publication.json` with an exact retry command. No main-branch fallback.

The development convention to update root `prompt.md` before commits applies to
source development commits. Automated result-only commits deliberately contain
only the two JSON files; adding a prompt would violate their allowlist.

```powershell
& ./tools/Endurance.ps1 -Mode Publish -Run '<local-run-id>' -ExpectedSource '<40-hex-source>'
& ./tools/Endurance.ps1 -Mode Review -Commit '<40-hex-result-commit>' -ExpectedSource '<40-hex-source>'
& ./tools/Endurance.ps1 -Mode Import -Summary '<downloaded-summary.json>' `
  -Checksums '<downloaded-checksums.json>' -ExpectedSource '<40-hex-source>'
```

Fetch a desired result ref normally before Review. Review reads only the exact
allowlisted JSON tree and never executes or checks out its contents. Import
verifies checksums, source identity and lifecycle, retains original evidence, and
labels `Imported=true`, `ImportReexecutedChecks=false`; it does not rerun checks or
turn remote evidence into local execution. Checksums bind bytes, not author
trust: accept results only from machines and repository access you trust.

For example, from the checked trusted checkout, fetch and review without checkout:

```powershell
$branch = Read-Host 'Full remote-runs branch name'
if ($branch -notmatch '^remote-runs/[a-z0-9/-]+$' -or $branch.Contains('..')) { throw 'Unexpected result ref' }
git fetch origin "refs/heads/${branch}:refs/remotes/origin/${branch}"
if ($LASTEXITCODE) { throw 'Fetch failed' }
$resultCommit = git rev-parse --verify "refs/remotes/origin/${branch}^{commit}"
if ($LASTEXITCODE) { throw 'Result commit unavailable' }
& ./tools/Endurance.ps1 -Mode Review -Commit $resultCommit -ExpectedSource '<40-hex-source>'
```

After retaining any evidence needed for diagnosis, stop the run, wait for its
owner to exit, and remove only its selected GUID directory. Do not delete the
checkout lock file while another process might own it. For explicit local cleanup:

```powershell
# $checkout must be the existing trusted checkout checked above.
$runId = Read-Host 'Finished local run GUID without hyphens'
if ($runId -notmatch '^[0-9a-f]{32}$') { throw 'Invalid run ID' }
$runDirectory = Join-Path $checkout ('build/endurance/' + $runId)
if (!(Test-Path -LiteralPath $runDirectory -PathType Container)) { throw 'Run missing' }
. (Join-Path $checkout 'tools/Endurance/Common.ps1')
Assert-EnduranceOwnedPath -Root $checkout -Path $runDirectory
$report = Get-Content -LiteralPath (Join-Path $runDirectory 'report.json') -Raw | ConvertFrom-Json
if ($report.Status -eq 'running') { throw 'Stop/wait/recover before cleanup' }
Remove-Item -LiteralPath $runDirectory -Recurse
```

Deleting local artifacts does not delete the already published result branch.
Keep a failed upload's local Git result ref until its retry succeeds.

GitHub CI remains administratively disabled and PR #62 remains untouched. Local
results transport does not dispatch hosted jobs. This workflow makes no new
native-performance benchmark claim and does not automatically close requirements.

Primary tool references: [Stryker.NET configuration](https://stryker-mutator.io/docs/stryker-net/configuration/),
[Microsoft SDK release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json).
