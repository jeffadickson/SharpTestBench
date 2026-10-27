# Library opportunities and assigned evaluation tasks

Assessment date: 2026-10-26/27. Source inspected: main5f798ef, including
Core.cs, BoundedCapture.cs, EnduranceProcess.cs, EnduranceCampaign.cs,
EnduranceGit.cs, Runner/ProcessExecution.cs and ManifestValidation.cs,
FixtureContracts.cs, NativeChecks.cs, package/benchmark commands, the export
checker and protocol CRC model. Primary documentation below was consulted.

This is a source/design assessment, not a completed integration or benchmark of
these candidates. No package/version was added, installed or upgraded. Existing
FsCheck/Stryker/Coverlet/ReportGenerator/Roslyn dependencies remain pinned. Candidate
.NET10, NativeAOT, licensing and platform compatibility are unverified in this
repository until their task gates run. Documentation infobox versions, especially
prereleases, are not dependency pin recommendations.

The LIB IDs below are stable backlog task IDs, not new software requirements.
They refine work under the existing StrictDoc IDs; acceptance/status of those
requirements is unchanged. Completed WP-01/WP-02 evidence is not reopened or
relabeled by these proposed evaluations.

Existing reuse baseline: retain System.Text.Json (including generated Runner
serialization), LINQ-to-XML/BCL filesystem/cryptography facilities, FsCheck3.6.0,
Stryker.NET5.0.0, Coverlet10.1.0, ReportGenerator5.5.11 and the generator's pinned
Microsoft.CodeAnalysis.CSharp4.14.0. No evidence here justifies replacing those
with another serializer/test framework or upgrading Roslyn to parse broader
language syntax without a compatibility decision.

## Assigned backlog

| Task | Owning package | Priority/disposition | Existing requirement trace |
|---|---|---|---|
| LIB-01 | WP-03 | High: evaluate CliWrap against current process contracts | STB-END-004, STB-041 |
| LIB-02 | WP-03 | High: prefer BCL TimeProvider; test-only fake-clock pilot | STB-END-001, STB-END-005 |
| LIB-03 | WP-03 | High: evaluate SharpFuzz for managed coverage-guided input campaigns | STB-END-003, STB-VER-002 |
| LIB-04 | WP-04; WP-05 consumes its generators | High: expand existing FsCheck rather than custom random loops | STB-033, STB-007, STB-VER-002 |
| LIB-05 | WP-03 | Medium: pilot JsonSchema.Net for structural evidence validation | STB-END-005, STB-END-008 |
| LIB-06 | WP-04 | Medium: evaluate System.CommandLine for growing helper CLI | STB-033, STB-VER-003 |
| LIB-07 | WP-07 | Medium: evaluate AngleSharp for broader documentation validation | STB-041, STB-VER-001 |
| LIB-08 | WP-06 | High: evaluate NuGet.Packaging/Versioning for package semantics | STB-041 |
| LIB-09 | WP-06 | Medium: prefer BCL PEReader for supported PE metadata checks | STB-041 |
| LIB-10 | WP-07 | Medium: evaluate BenchmarkDotNet for isolated managed microbenchmarks | STB-041 |
| LIB-11 | WP-07 | Conditional: Arrow .NET/DuckDB only if campaign scale warrants it | STB-041 |
| LIB-12 | WP-03 and WP-05 | Retain specialized Git/RNG/oracle/simulator semantics; review reuse boundaries | STB-END-007, STB-007, STB-VER-003 |

## LIB-01

**Process execution: CliWrap pilot, not an immediate replacement.**
Already-written Core.CommandRunner/BoundedCapture, EnduranceProcess and Runner
ProcessExecution repeat process/stream lifecycle plumbing. [CliWrap](https://github.com/Tyrrrz/CliWrap)
provides command execution, pipes and cancellation; reducing that plumbing is
worth a contained prototype in ordinary Tools execution. Keep domain evidence
records, shell-free argument boundaries and explicit environment removal.

First correctness subtask: Core.CommandRunner synchronously waits on
BoundedCapture.Run, whose async awaits merit the same caller-context scrutiny as
the previously fixed EnduranceProcess. Add controlled context probes to these
other paths before a library migration. This is a source-inspection risk, not a
new reproduced failure or a claim that WP-01 tested every possible caller context.
Fix any confirmed issue in the current implementation before comparing libraries.

Gate: run existing cancellation, context-post, inherited-pipe, partial-output,
log-cap, dead-child, stdin-backpressure and process-tree tests through both
implementations on Windows/Linux. Compare error/exit/timing classifications,
stream byte counts and retained prefixes. Do not replace bounded streaming with
unbounded buffered output. A library cancellation API does not establish detached
session survival. The SDK-free PowerShell bootstrap compiles the BCL-only Process
source before SDK setup: it must remain usable without a new package dependency.
Retain current code if the library cannot preserve those boundaries; assess the
maintenance cost of two implementations before adopting either split.

## LIB-02

**Lifecycle time: BCL TimeProvider plus test-only fake time.**
EnduranceCampaign budgets use Stopwatch; lifecycle/storage reports also contain
wall-time evidence. [TimeProvider and FakeTimeProvider](https://learn.microsoft.com/en-us/dotnet/standard/datetime/timeprovider-overview)
provide an existing clock abstraction and controllable test clock. Prefer these
over inventing a new general clock library. The fake implementation comes from
Microsoft.Extensions.TimeProvider.Testing and would be a test dependency.

Gate: a small injected-clock pilot must preserve monotonic elapsed budgets,
separate UTC provenance, duration lower bounds on recovery and cancellation races.
Advance time deterministically through checkpoint/stop/recovery transitions.
Keep real OS child watchdogs and actual five-hour qualification; fake elapsed time
cannot qualify process cleanup or hours-long stability. Never substitute wall
clock for HDL simulation steps or replace the simulator scheduler's time model.

## LIB-03

**Coverage-guided fuzzing: SharpFuzz as an isolated development harness.**
The current seeded malformed-regex sweeps and FsCheck campaigns are not coverage-
guided fuzzing. [SharpFuzz](https://github.com/Metalnem/sharpfuzz) is a .NET fuzzing
integration candidate for JsonInput, manifest/evidence parsing and export/audit
input validation. Use an established supported engine, with Linux as the first
qualification target; do not claim Windows support merely from compilation.

Gate: pin the chosen tool/engine, instrument isolated managed copies, retain corpus
seeds, crash inputs, engine settings and minimized replay cases, and prove known
bad inputs are detected. Bound runtime, memory, diagnostics and corpus retention.
The parser harness must not launch simulators, execute Git, load untrusted code
or fetch remote schemas. Replay retained failures through the production parser.
This measures managed instrumentation, not NativeAOT instructions or HDL. Keep
Stryker.NET as the mutation engine; fuzzing does not replace mutation testing.

## LIB-04

**Property/model testing: expand the existing pinned FsCheck integration.**
Reuse FsCheck3.6.0 for lifecycle commands, selection combinations and protocol
reset/stall/ack/byte-mask sequences instead of new ad hoc random engines. The
[FsCheck model-testing documentation](https://fscheck.github.io/FsCheck/StatefulTesting.html)
marks that API experimental: inspect the pinned version's actual API before
choosing it. Stable generators and explicit command-sequence properties may be
preferable to depending on experimental types.

Gate: independent state models, bounded legal command generation, invalid-command
properties, reproducible seeds and meaningful shrinking that preserves protocol
preconditions. First demonstrate shrinking/replay with a deliberate known fault;
retain actual production counterexamples only when observed. Feed reduced traffic
into both runners in WP-05. Do not derive expected values by calling the same
production transition function or count finite randomized sweeps as proofs.

## LIB-05

**JSON structure: JsonSchema.Net can complement, not replace, strict parsing.**
Core.StrictJson/JsonInput and Runner/ManifestValidation mix structural checks with
cross-field/domain checks. [JsonSchema.Net](https://docs.json-everything.net/schema/basics/)
can express common object/array/enum/range structure and diagnostics. Pilot it in
Tools-only offline validation first. Existing System.Text.Json DOM and generated
Runner serializers remain useful; a source-generated serializer is not a schema
validator, and a schema validator is not an evidence authenticity check.

Gate: differential tests against current accepted/rejected fixtures, duplicate
keys, UTF-8/Unicode failures, integer bounds, depth/size limits and unknown fields.
Keep strict byte parsing before schema evaluation, and preserve source/hash/run-ID,
seed/status and path-containment semantics afterward. Bundle approved schemas and
disable unapproved remote reference resolution; prove offline operation. Test actual
.NET10 publication, allocation/resource limits and NativeAOT separately before any
Runner adoption. Do not infer full validator AOT compatibility from documentation
about serialization support. No evidence schema/version change is implied.

## LIB-06

**CLI parsing: System.CommandLine for growing Tools, conditional for Runner.**
Core.Options hand-parses repeated options and flags; Program dispatches many
commands. [System.CommandLine](https://learn.microsoft.com/en-us/dotnet/standard/commandline/)
provides parser/help functionality and is documented as trim-friendly. That is a
reason to evaluate it, not evidence it already passes this repository's AOT gates.
Keep Runner's tiny positional interface unless a concrete expansion justifies cost.

Gate: preserve existing option spelling/case, repeated-value/duplicate policy,
negative numeric values, missing values, help/usage output, exit codes, quoting and
selection-filter forwarding. Explicitly decide response-file behavior; do not
silently enable new file expansion. Run both CLI failure and real simulator parity
checks, then publish on target hosts. Keep semantic validation in domain code.

## LIB-07

**HTML validation: AngleSharp when expanding beyond pinned export syntax.**
The new Check-RequirementsExport.ps1 deliberately recognizes only double-quoted
start-tag attributes and two StrictDoc0.30.4 path defects. [AngleSharp](https://github.com/AngleSharp/AngleSharp)
offers an HTML DOM parser and would reduce custom tokenization if the checker
expands to other quoting, base URLs, malformed markup or fragment validation.
Prefer a docs-only C# tool prototype over adding dependencies to the SDK bootstrap
or simulation runtime. The bounded current checker remains in place meanwhile.

Gate: compare the actual saved exports, existing eleven regressions, displayed
source/escaped markup, single quotes, entities, base elements, URI fragments and
missing targets. Disable loaders/scripts/networking and keep filesystem limits,
no-link/containment rules and the narrow repair policy outside the DOM library.
A parser is not a filesystem sandbox. Verify idempotence and byte preservation
for untouched files; avoid whole-document reserialization changing source anchors
or checksum-bound evidence. Measure dependency/startup cost before adoption.

## LIB-08

**Package semantics: NuGet.Packaging and NuGet.Versioning.**
PackageCommand generates package projects; PackageContainerCommand uses lexical
version checks. The [NuGet Client SDK](https://learn.microsoft.com/en-us/nuget/reference/nuget-client-sdk)
provides package/nuspec stream readers and NuGet's version semantics. Prefer a
pilot using those facilities over growing a custom NuGet-format/version parser.
Do not add NuGet.Protocol or feed authentication merely for local archive checks.

Gate: package/version compatibility vectors, malformed/duplicate/truncated ZIP
entries, prerelease/build metadata, dependency/RID inventories and fresh consumer
runs. Preserve explicit product acceptance policies even where NuGet's parser
accepts more syntax. Archive parsing does not make extraction safe: retain entry
count/size/path/link boundaries. NuGet documents API stability/support caveats;
pin an evaluated supported version and test transitive dependencies. No public
package publication or credential access is authorized by this evaluation.

## LIB-09

**PE metadata: use the existing .NET metadata reader where it fits.**
NativeChecks parses external dependency-tool output. [PEReader](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.portableexecutable.pereader?view=net-10.0)
from System.Reflection.Metadata is a candidate for PE headers/architecture and
managed metadata inspection without loading inspected assemblies. Inspect the
pinned framework reference first rather than adding an unnecessary package.

Gate: corrupt/truncated files, incorrect machine type, managed/native distinctions
and cross-checks against the existing GNU/LLVM tools on known artifacts. PEReader
is not an ELF reader or a complete ready-made import/export policy engine. Retain
external reference tools and domain checks where they remain necessary. Do not
load untrusted assemblies just to enumerate their public surface. Apply compiled
C# metadata inspection to WP-04 only when useful; pinned upstream Python exposure
analysis still needs its existing Python AST adapter, not Roslyn.

## LIB-10

**Microbenchmarking: BenchmarkDotNet, alongside existing process timings.**
RunnerBenchmarkCommand correctly measures fresh external CLI processes and HDL
phases; retain that contract and completed WP-02 reports. [BenchmarkDotNet](https://benchmarkdotnet.org/articles/guides/how-it-works.html)
is a candidate for isolated bit-vector conversion, hashing, parser and allocation
microbenchmarks with controlled jobs/warmups. It must not silently redefine the
existing process-wall measurements as in-process iterations.

Gate: deterministic correctness controls, matched runtime/toolchain settings,
allocation/GC evidence and retained distributions. Validate target .NET10/AOT jobs
explicitly. Report which setup and overhead treatment applies; no startup-proxy
subtraction or default overhead correction may be presented as the old wall-time
metric. No performance improvement is established by choosing the library.

## LIB-11

**Columnar campaign analysis: conditional Arrow .NET / DuckDB evaluation.**
Current nine benchmark reports are small; JSON plus BCL processing is sufficient.
At materially larger campaign volume, evaluate [Apache Arrow .NET](https://github.com/apache/arrow-dotnet)
for typed columnar results and [DuckDB.NET](https://github.com/Giorgi/DuckDB.NET)
(or an isolated DuckDB CLI) for derived analytical queries. The .NET binding has
its own native packaging and debugger considerations; do not assume first-party
or NativeAOT support from the database's general capabilities.

Gate: demonstrate a measured ingestion/query benefit and bounded memory on
representative data. Explicitly choose IPC or Parquet and evaluate the required
writer; Arrow .NET is not being assumed to supply a Parquet writer. Preserve
units, null/unknown values, source/run IDs, seeds and sample-versus-summary scope.
Keep canonical checksummed JSON untouched; derived columnar files stay outside
allowlisted JSON-only result commits. Reject this dependency if scale does not
justify the footprint or deployed native assets cannot be qualified.

## LIB-12

**Deliberate retention decisions for existing specialized code.**

- EnduranceGit's installed Git CLI and private-index/result-branch rules remain.
  [LibGit2Sharp](https://github.com/libgit2/libgit2sharp) is an alternative binding,
  but not a preferred swap: it introduces native assets and requires new auth,
  config/environment, index-isolation, atomic-ref and no-force/main-preservation
  qualification. A wrapper library does not eliminate those domain rules.
- FixtureContracts.PythonRandom is a compatibility implementation. Replacing it
  with System.Random or a generic MT19937 library is unsafe without exact CPython
  integer seeding, random/getrandbits/range-consumption golden vectors. Extend
  independent Python vectors in WP-05; a shared generator algorithm name does
  not establish sequence compatibility.
- Protocol Entry.Crc is an independent whole-message polynomial-division oracle
  for CRC-8 polynomial0x107/0x07, zero initial/final XOR, no reflection. The
  [primary pycrc model](https://pycrc.org/models.html) defines the0xf4 check value.
  A generic CRC-32 facility is not a replacement. For wrap-boundary workloads,
  benchmark this oracle's scaling and consider a separately pinned test-only
  pycrc vector reference with identical conventions, preserving independent
  small-message checks. Required Python reference tools remain allowed.
- Preserve simulator-thread scheduling, delta/phase semantics, four-state logic
  and task ownership. Generic async queues, Channels, Rx or wall-clock schedulers
  are not assumed behaviorally equivalent. Apply general libraries to host-side
  plumbing only after testing boundaries, not by replacing HDL semantics wholesale.

## Shared adoption gate and sequence

Start WP-03 with LIB-01/02/03 and reuse LIB-04 generators; evaluate LIB-05 when
shape checks actually duplicate. WP-04 owns CLI/model follow-ups; WP-05 owns the
oracle/traffic consumers. WP-06 owns packaging/metadata; WP-07 owns documentation
and analytical follow-ups. LIB-10 is supplemental measurement work, not an edit
to the accepted WP-02 timing evidence. LIB-11 is explicitly conditional.

Each task first records the installed/pinned baseline, candidate stable version,
license/notice/transitive/native-asset review and a small isolated prototype.
No blind latest-version upgrade or .NET target change. Accept adoption only after
locked builds, relevant positive/negative/property/fuzz/Stryker checks, line and
branch coverage where supported, Windows/Linux execution and required actual
publication. Keep failed prototypes and findings; retaining the built-in/current
implementation is a valid completed evaluation. Preserve schemas and StrictDoc
IDs; proposed new product obligations remain inferred/draft until approved with
the required evidence. Update examples/API docs/handoff before adoption commits.

Validation of this assessment is source inspection, primary-document review,
package/task mapping and local Markdown link checks. No candidate integration,
new coverage, package license audit, performance result or requirement closure
is claimed by this documentation-only change. CI stays disabled and PR62 untouched.