# Runner input preservation and cleanup coverage

Date: 2026-10-20. Development base: main
`8f49a1f3d569db6fa55f5e15c085f93f2fc3dbb8`. This is fresh local OptiPlex
Windows/Ubuntu-24.04 evidence, separate from archived development rounds.

## Changes and regression scope

- The standalone Runner now resolves the `gpiImpl` library before acquiring its
  workspace and rejects aliases of generated artifact paths, including the lease.
  Previously cleanup could delete that input before required-file validation.
- Output equal to or beneath an explicit include directory is rejected before
  cleanup. Generated evidence would otherwise become part of the include tree,
  and cleanup could delete files being supplied as compiler inputs.
- Added 50 named production CLI subprocess regressions: 15 source collisions,
  15 GPI implementation collisions, 15 include/output preservation cases, three
  lease-path cases, one include ancestor and one permitted sibling-prefix case.
  These are table-driven regression scenarios, not 50 features or distinct branches.
- The CLI coverage collection discovers `test_runner_cli*.py`, retaining private
  instrumentation and the production runtime configuration with JSON reflection
  disabled. Production helper orchestration remains C#.
- Evidence schemas are unchanged. Invalid GPI implementation syntax now fails
  before acquiring the workspace, so it produces a diagnostic without a workspace
  failure manifest. Existing files remain untouched on collision rejection.

The first attempted manifest-collision matrix failed because existing output
containment already rejects that arrangement. It was removed, along with a
redundant proposed code change. An interval reviewer initially missed this
ordering and corrected the review after the test result. Source-collision cases
replaced it. The subsequent GPI matrix demonstrated 15 failures before the fix;
the complete focused 50-case suite passed after the fix. Logs remain in ignored
`build/collision-before-fix.log` and `build/collision-focused.log`.

## Local validation

Reused existing SDK 10.0.402, Python 3.12 environments, pinned development cocotb
adapters, native compiler/linker dependencies, Git, Make, Icarus and CMake. No
wholesale reinstall or released cocotb environment was used. Windows and WSL
use independent bin/obj/build outputs, with Linux source under
`/home/jeffd/src/SharpTestBench`. Both C# doctor checks passed.

Full Release solution builds used locked restore. Linux ran `make test`;
Windows ran the equivalent solution build, three executable managed suites and
retained Python contract discovery. Both passed with zero build errors.
Existing XML documentation warnings remain.

| Suite | Windows passed | Windows skipped | Linux passed |
|---|---:|---:|---:|
| Core | 480 | 0 | 480 |
| Generators | 17 | 0 | 17 |
| Helpers | 758 | 9 | 767 |
| Retained contracts, including the 50 new CLI cases | 797 | 0 | 797 |
| Total | 2,052 | 9 | 2,061 |

## Managed coverage

Collection ran serially with builds/tests in each checkout. Six collections
merged all four required production assemblies: library, generator, Tools and
Runner. ReportGenerator source resolution found all 126 source files, including
41 generated files. Generated code remains in the denominator.

| Measurement | Windows | Linux |
|---|---:|---:|
| Merged covered/valid lines | 4,433 / 8,491 (52.2082%) | 4,433 / 8,491 (52.2082%) |
| Merged covered/valid branches | 1,982 / 5,080 (39.0157%) | 1,993 / 5,080 (39.2323%) |
| CLI collection Runner lines | 2,108 / 3,208 (65.7107%) | 2,108 / 3,208 (65.7107%) |
| CLI collection Runner branches | 298 / 672 (44.3452%) | 299 / 672 (44.4940%) |
| CLI Program.cs unique executable lines | 78 / 133 | 78 / 133 |

The private CLI runtime configuration retained
`System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault=false` on both
systems. New tests exercise preflight rejection and input preservation; the
managed CLI collection still does not cover the full success path or runtime
switch matrix. These results do not measure NativeAOT instructions, native host
instructions or HDL coverage. The source denominator changed from the previous
checkpoint, so percentages are not a claim of feature completeness.

## Native and actual simulator qualification

- Linux: real Icarus Counter, DSP and CPU positive parity at seed 71; DSP and CPU
  injected-fault negatives detected by both runners with exit code 1.
- Published self-contained ReadyToRun Tools: Windows x64 DSP positive/negative
  parity at seed 73; Linux x64 DSP positive/negative parity at UInt64.MaxValue.
  Tests included poisoned inherited selection/fault controls. Positive reports
  matched identities/seeds/traces; negative reports matched fault detection.
- Fresh Linux x64 NativeAOT Runner: real Counter success, simulator failure with
  `/bin/false`, missing-source configuration failure, and direct GPI/include
  collision rejection with byte preservation. Failures exited 1 and retained
  the expected evidence where a workspace had been acquired.
- Real compiler-option/selection qualification on both systems passed: include
  path with spaces, define, parameter override, intentional width failure,
  selected-case parity with matching seeds and retained input provenance.
- Worker verification passed. Tools DLLs have ReadyToRun headers (Windows RVA
  5744, Linux RVA 67680, size 196, signature `52545200`); the standalone Linux
  Runner is an x86-64 ELF native executable. Full Tools retains its bundled
  CLR/JIT fallback. Simulator assemblies still require hostfxr/runtime.

No Windows NativeAOT Runner, macOS, ARM64, released-reference cocotb or additional
Verilator execution is certified by this checkpoint. RID plans are not executed
platform qualification.

## Reviews and delivery controls

Peer source reviews occurred at 15, 30 and 45 scenarios, followed by an independent
deep source review at 50 before commit/push. No actionable findings remain.
The requested ancestor and allowed sibling-prefix coverage was added. See
[review details](runner-cleanup-deep-peer-review.md),
[review ledger](runner-cleanup-reviews.jsonl) and
[scenario ledger](runner-cleanup-iterations.jsonl).

Checks compare normalized lexical paths; general filesystem identity protection
for hard links, symbolic-link aliases or Windows alternate names remains outside
this qualification. Before/after hashing is not a filesystem snapshot.

All 12 GitHub workflows were read-only verified `disabled_manually`; none was
dispatched. PR #62 remains open, draft, unmerged and unchanged at
`0318a864d9b570f9ffbc4857e7365ec9ed6a4736`. Unknown local `AGENTS.md` is preserved
and excluded from the commit. Delivery uses a normal push to main after local
validation and the final review; the delivered output report records the exact SHA.

Local logs are retained under ignored `build/collision-coverage-*`, along with
the regenerated `build/code-coverage` reports. No historical report was rewritten.
