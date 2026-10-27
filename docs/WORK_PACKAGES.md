# Remaining work packages and testing gates

This is the execution plan after delivered source `bdb865e`. It consolidates
remaining obligations from OPEN_WORK.md, ALPHA_BACKLOG.md, the selection200
requirement assessment, RUNNER_BENCHMARKS.md and the endurance validation report.
Those reports remain historical evidence. No package is closed by this plan.
GitHub issues retain their own completion state; no issue or PR is changed here.

## Common acceptance and development rules

Work on main; preserve unknown files and newer commits. Keep CI disabled and
PR #62 untouched. Use independent Windows and WSL-native checkouts and outputs.
Reuse pinned SDK 10.0.402, development adapters and separate released-reference
cocotb environment. Production orchestration stays C#; use PowerShell, F#,
FsCheck and the existing independent Python references where applicable.

For each package record source commit plus changed-file hashes, tool versions,
commands and exit codes, discovered/executed/pass/fail/skip counts, retained log
locations, seeds and counterexample/replay paths. A failed, timed-out, partial,
unavailable or skipped required case cannot satisfy its gate. Preserve failed
attempts alongside successful retries. Use unique evidence directories; do not
replace checksum-bound portable reports or historical records.

For code changes collect all-four-production-assembly line and branch coverage
serially with builds/tests. Report numerators, denominators, generated-source
contribution and unmeasured paths; no arbitrary percentage proves closure.
Execute focused Stryker.NET, malformed/negative tests, FsCheck and seeded fuzzing
where applicable. Mutation exclusions, invalids, timeouts and survivors require
separate dispositions; supplemental manual mutations remain separate. Research
current primary sources before selecting new dependencies, then justify any
addition against the existing libraries. Keep XMLDoc, examples, StrictDoc and
the established Sphinx documentation workflow current.

Count substantive changes only: build and all three managed suites every ten
engineering units; review every fifteen; full local gates and coherent local
commits at fifty, or earlier for a smaller complete package. Perform a final deep
source/evidence review and resolve accepted findings before a final commit.
Update prompt.md before every source commit. No new numeric iteration quota is
implied. No new push or external result upload is authorized by this plan.

## Package map

| Package | Scope and stable requirement trace | Prerequisite | Exit gate | Current disposition |
|---|---|---|---|---|
| WP-01 | Fresh baseline and evidence integrity; STB-041, STB-VER-001..003 | Actual checkout and reusable dependencies inspected | G1 | In progress; old results do not satisfy it |
| WP-02 | Managed/NativeAOT runner performance and parity; STB-007, STB-VER-003 | G1, doctor, current native publication | G2 | Harness exists; fresh matched measurements outstanding |
| WP-03 | Failure lifecycle and endurance qualification; STB-END-001..009 | G1, isolated run directories | G3 | Short Windows/Linux runs exist; hours and terminal disconnect unverified |
| WP-04 | Remaining upstream surface and selection semantics; STB-001, STB-033, STB-EXP-001..002, STB-SEL-001..006 | G1, pinned source inventories | G4 | Fourteen literal trigger exports and dynamic/other exposures remain |
| WP-05 | RTL boundary, object-model and backend qualification; STB-007, STB-PRO-001..004 | G1, G4 decisions where semantics change | G5 | Bounded existing fixtures pass historically; broader matrix open |
| WP-06 | Distribution, setup and debugger qualification; STB-END-006, STB-041 | G2/G3/G5 for each advertised target | G6 | Fresh SDK extraction, Windows StrictDoc install and other platforms open |
| WP-07 | Consolidated acceptance and requirement dispositions; STB-041 | Relevant G1..G6 | G7 | Pending; bounded closure only |

Packages may proceed independently after their prerequisites. On this 16 GiB
machine serialize heavy baseline, coverage, Stryker, publication and benchmark
jobs. A package may finish for one explicit target while other targets remain
blocked; that never grants a whole-product acceptance label.

## G1: Fresh baseline and trustworthy evidence

1. Inspect status/branch/remotes/history; fetch normally. Verify SDK and selected
   Python/adapter environment locally. Run the C# doctor before simulation.
2. Locked restore and Release build of SharpTestBench.slnx on both OSes. Execute
   the complete Core, Generators and Tools suites, then retained contracts
   (`make test` in the WSL-native checkout). Reconcile discovery with reports;
   separate repeated coverage invocations from unique case counts.
3. Run real StrictDoc export, validate links/source traces and check evidence
   checksums. Capture build warnings as well as failures. Track the existing public XMLDoc
   warning debt and static-analyzer findings; a passing build is not a warning-free
   API-documentation gate.
4. For code changes, run the four-assembly coverage collector and relevant
   negative/property/fuzz/Stryker checks. For documentation-only changes validate
   requirements and links; report that no fresh code coverage was collected.

The endurance standalone baseline deliberately selects Endurance Tools cases;
its historical 801 cases are not the complete full-product gate. G1 remains
partial if only that command is run. Gate state must identify each OS separately.

## G2: Correctness-qualified managed/native comparison

Publish current-source Linux x64 NativeAOT Runner and Windows/Linux ReadyToRun
Tools with existing publish-workers tooling. Keep the managed Runner comparison
separate. Use runner-benchmark for counter, DSP and CPU with identical fixture,
seed, native host, adapters and source per pair; at least one warmup and five
measured repeats. Retain alternating-order raw samples and distributions; run
runner-benchmark-check on each report. Run independent scoreboards and existing
negative fixtures before interpreting timing. Compare compile, simulation,
startup-proxy and total timing separately. A result failing functional parity
cannot enter the performance summary. No cross-platform speedup or NativeAOT
instruction-coverage claim follows from these runs. Full Tools is ReadyToRun
with CLR/JIT fallback; simulated assemblies still require hostfxr/runtime.

## G3: Lifecycle, replay and longer endurance

First qualify explicit short campaigns, stop/cancellation, dead-owner recovery,
artifact-pressure stop, process-tree cleanup and inherited pipes. Verify exact
completed counts and seed progression after interruptions. Exercise real shell
session disconnection with a dedicated owned child; prove survival by reconnecting
and checking progress, then bounded stop/cleanup. Unit detached-launch tests do
not establish disconnect survival.

Then run a default five-hour campaign per advertised Windows/Linux target,
serially, keeping memory/disk observations and valid intermediate/final atomic
reports. Review all selected Stryker outcomes. Inject a controlled failing
property to demonstrate retained counterexample, shrinking/replay and identical
failure on replay; distinguish this test from a discovered production bug.
Exercise publication/retry/passive review/import only against local bare remotes
unless a new task explicitly authorizes an external upload. Ensure source/main
and index unchanged. Existing short reports are finite evidence, not this gate.

## G4: Upstream and selection obligations

Inventory the fourteen remaining literal trigger exports against the pinned
source. For each classify implemented, intentionally unsupported, or concrete
semantic gap with exact source hashes and requirement/test anchors. Audit
aliases/imports/dynamic exposures separately without turning reviewed counts
into parity percentages. Choose behavioral gaps by user-visible risk, add
independent expected-result cases and failure controls, and run both front ends.
Required-pass manifests must still reject skipped cases and bind run identity,
seed, status and inputs. Preserve result/discovery/evidence schemas. Include
adversarial regex planning/timeout behavior and selected Stryker mutants where
code changes warrant them. Broader parent requirements remain open until their
own completion criteria have execution evidence.

## G5: Real RTL boundaries and backend matrix

Start with the existing CRC, arbiter and byte-enabled bank plus current pinned
Verilog examples; avoid adding HDL merely to inflate case counts. Add CRC
sixteen-bit packet-length wrap boundaries (65535/65536/65537 accepted bytes),
reset/backpressure interactions and independent polynomial/length expectations.
Run a deliberate RTL fault through both front ends to demonstrate scoreboard
sensitivity. Retain actual HDL coverage only if a supported instrumented backend
was executed; managed line coverage does not measure HDL.

Expand package/struct/packed-layout and VHDL/mixed-language fixtures with an
explicit case-by-backend capability matrix. Run Icarus and corresponding
Verilator checks for simulator changes; broader GHDL/NVC cases require their
actual installed environments. Driver/load metadata, dynamic-string NUL and
external-thread wakeup remain explicit design/implementation gaps. Debug native
shutdown under fault injection before claiming its unresolved intermittent
historical symptom fixed. Commercial tools require available licenses; absence
is blocked, never passed.

## G6: Setup, platform and distribution qualification

Use new dedicated environments to exercise checksum-verified SDK download and
extraction, fresh Windows hashlocked StrictDoc install and reuse paths. Include
malformed/truncated download rejection, install interruption and unchanged
existing installations. Test Debian12/ARM64 only on actual available targets;
six RID plans are not six executed platforms. Validate packaged fresh consumers
and example commands per advertised OS/backend, AOT trimming/process lifecycle,
Windows/macOS debugger attachment and simultaneous mixed debugging separately.
Public package publication and licensed/remote infrastructure changes require
explicit task authorization; local package validation can proceed first.

## G7: Consolidated acceptance

Rerun full G1 on the final source for every qualified target, focused affected
simulator/parity gates and serial four-assembly coverage. Retain current seeded
property/fuzz/Stryker evidence with unresolved findings and scope. Recheck docs,
examples, schemas and source hashes. Reconcile StrictDoc requirement-to-code-to-
test-to-run evidence and record each bounded verified obligation, partial area
and blocker without closing broader parent requirements automatically. Complete
final deep source/evidence review before the local final commit. Report commit
state and remaining packages. A later push needs current task authorization.

## Execution record for this planning slice

Source inspected: bdb865ee19006ca2d890bcf66b77bf7a2f677d7b on main;
normal fetch found no newer origin/main commit. This slice changes Markdown only.
Canonical user rules and the local untracked AGENTS.md were read; the latter was
preserved. The user's native WSL checkout rule supersedes its older shared-path
suggestion. No workflow, PR, dependency, executable source or evidence schema
was changed. No new coverage or Stryker result is claimed for this doc-only slice.

Fresh Windows commands: profile-derived .dotnet selected SDK10.0.402;
`dotnet restore SharpTestBench.slnx --locked-mode`, then
`dotnet build SharpTestBench.slnx -c Release --no-restore -m:2` succeeded
(2161 warnings, zero errors). Executable Core and Generators suites passed
669 and17 cases respectively. Tools completed1384 cases:1372 passed,12 Unix
skips,zero failures. StrictDoc0.30.4 export passed, including source pages, at
build/work-packages-strictdoc/html. Windows managed total:2058 passed/12 skips. Full WSL and retained-contract reruns
have not been performed in this slice; G1 is not passed. No new benchmark,
long-running campaign, native publication, commercial/platform test, independent
review is claimed. The user subsequently authorized a normal main push of this
documentation slice; final local read-through checked scope and evidence claims.
Actual delivery must be verified against origin/main after the commit.
Referenced backlog files and all eleven explicitly named
stable requirement IDs and the nine endurance IDs were checked locally; `git diff --check` passed.