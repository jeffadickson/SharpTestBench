# SharpTestBench round 8: C# workers, native deployment and local coverage

Repository: [jeffadickson/SharpTestBench](https://github.com/jeffadickson/SharpTestBench). Branch main. Pushed and remotely verified head `6de9bb705df190f7e15a2341cd979f6af3e66d5a`. Remote delivery is recorded separately in `round8-delivery.json`.

Completed 200 separately numbered source/test edits, with local full solution Release build, three managed suites, 742 retained contract tests, whitespace checks and a commit at each 50-edit checkpoint. Most later edits add parameterized test cases; these are not 200 distinct features. Eleven supplemental regression cases and review corrections accompany the numbered edits. Added 171 helper cases overall, from 540 to 711 discovered. Full iteration ledger, hashes and CSV are exported alongside this report.

## What the repository provides

SharpTestBench is an experimental C# HDL verification runtime, currently 0.8.0-alpha, using native cocotb GPI adapters. It remains an incomplete cocotb-compatible implementation. Existing capabilities include simulator-thread coroutine scheduling; timers, edge triggers and trigger combinators; cancellation, task groups and structured failures; queues/events/semaphores/locks; fixed-width signed/unsigned and four-state types; typed generated hierarchy/packed layouts; discovery, deterministic seeds, expected exceptions, JSON/JUnit evidence and native simulator hosting. Backend-specific VHDL transport, discovery, debugger, benchmark and upstream fixture proofs have their own documented status. See the repository compatibility matrix and OPEN_WORK rather than assuming uniform backend support.

This round strengthens deployment and validation. It does not add a new Verilog fixture or expand the functional HDL scenario set. Existing pinned AXI Stream/LFSR and independent oracle assets remain available. A real counter simulation was exercised through the new published workers.

## C# ownership and remaining Python

There are **0 tracked Python helper implementations under tools/** and **55 tracked C# source files under tools/**. Repository production helper orchestration is C#. The audit retains 45 Python files outside tools for independent cocotb scoreboards/oracles, benchmark references, contract/RPC tests and Sphinx configuration. C# still invokes narrow embedded Python adapters for upstream builder/configuration/oracle/AST APIs. This is not a Python-free repository. The original helper-source backup from earlier migration rounds remains preserved in outputs.

## New deployment/build capabilities

| Component | Delivered behavior | Executed platform evidence |
|---|---|---|
| Full SharpTestBench.Tools | Self-contained directory deployment with native ReadyToRun code, bundled CLR and JIT fallback; exact apphost child invocation | Windows x64 publish, help and full-file verification; Linux x64 publish, child invocation and simulation parity |
| Standalone SharpTestBench.Runner | NativeAOT orchestration using generated manifest/evidence JSON metadata | Linux x64 actual native executable, successful Icarus counter simulation and classified simulator failure |
| Publish/verify commands | Host-matched RID, output confined below build/, pre-publish link checks, complete SHA-256 file map and identity/file-set validation | Positive publication plus unit/contract boundary cases and real DLL tampering rejection/restoration |
| Make frontends | Root Makefile and tools/Makefile expose build/test/coverage/native/verify/direct execution | GNU Make exercised locally in WSL |

The standalone AOT executable can launch without an installed .NET runtime. Managed simulation test assemblies still need hostfxr/runtime in the separate simulator process. Full Tools is **not NativeAOT**, trimmed or single-file. Build/simulation operations retain SDK, simulator, compiler/linker, native bridge and upstream adapter requirements as documented.

Build plans support matching Windows/Linux/macOS x64/arm64 hosts. Only Linux x64 ReadyToRun+NativeAOT and Windows x64 ReadyToRun were executed here; macOS, ARM64 and Windows NativeAOT were not qualified. Windows NativeAOT requires Visual C++ build tools. Cross-host publishing is rejected.

Native outputs remain in the checkout's build/workers/win-x64/tools and WSL cache build/workers/linux-x64/{tools,runner}. The WSL source copy is isolated from Windows bin/obj outputs. All 989 tracked source files have identical SHA-256 hashes in the two copies.

The published Linux Tools launched its child commands and ran dual-runner counter parity. The actual AOT Runner then ran the generated manifest directly. A deliberate /bin/false simulator produced simulator_failure evidence. PE header inspection confirmed ReadyToRun code; both Linux workers launched with runtime environment paths intentionally unavailable. A published Tools DLL was altered, rejected by the verifier, restored and successfully reverified. Integrity manifests establish file consistency, not signed authenticity.

## Local validation

| Platform | Core | Generator | Helper passed/skipped | Contract | Total passed/skipped |
|---|---:|---:|---:|---:|---:|
| Windows on this machine |480|17|702/9|742|1941/9|
| Ubuntu 24.04 WSL on this machine |480|17|711/0|742|1950/0|

Windows skips are the nine explicitly Unix-specific symbolic-link cases. No failures or test errors remain. Full solution builds pass with existing XML documentation warnings. No GitHub runner was used for these gates.

New case families cover six explicit native deployment profiles, cross-host/invalid plans, five file-mutation behaviors across artifact names, malformed maps/unknown fields/duplicate keys, actual platform apphost names and directory boundaries, publish/artifact links, and generated JSON round trips including 2^53+1,2^63 and UInt64 maximum. File/seed variants count as test cases, not separate runtime capabilities.

## Coverage

Coverlet 10.1.0 and ReportGenerator 5.5.11 were already pinned and are reused through the C# coverage command, now exposed with make coverage. Fresh reports ran serially with builds/tests on each platform; individual and merged Cobertura counters and ReportGenerator JSON totals were validated.

| Platform | Covered/valid managed lines | Line coverage | Covered/valid branches | Branch coverage |
|---|---:|---:|---:|---:|
|Windows|2136/5262|40.59%|1471/4394|33.48%|
|Linux WSL|2136/5262|40.59%|1480/4394|33.68%|

Default merged coverage includes SharpTestBench, SharpTestBench.Generators and SharpTestBench.Tools. It includes compiler-generated JSON context code; ReportGenerator cannot display virtual generated-source pages that the compiler did not persist. Counter validation still passed. Standalone Runner is not separately collected by this report. Coverage does not measure HDL or native machine instructions. These percentages are the scope of this local managed suite, not end-to-end feature completeness.

## Peer review

Thirteen scheduled reviews occurred at 15,30,...,195, followed by three independent detailed final reviews at 200. Final reviewers inspected source read-only; execution evidence was produced by the primary agent. No unresolved P0/P1/P2 correctness findings remain in those reports.

| Earlier finding | Resolution |
|---|---|
| BOM source literal corruption | Explicit Unicode escape and regression validation |
| Runner help returned error | Help exits successfully for publish smoke |
| Missed child helper invocation | Shared exact apphost/directory selection |
| Publish source/output escape and nested links | Build-directory containment and pre-publish canonical path checks |
| Make omitted contract tests | All four local suites included |
| Parallel native prerequisites raced | One ordered all-mode publish recipe |
| Debug test target used stale Release harness | Test/coverage force fresh Release builds |
| Missing direct malformed-map regressions | Seven supplemental negative cases |

Full peer reports are in round8-peer-reviews/. Follow-up opportunities: independently collect Runner managed coverage, persist generated sources or refine generated-code report presentation, and execute remaining target platforms. Existing unsigned checksum metadata is not a distribution signature.

## Checkpoint commits

| Iteration | Commit | Helper discovered/passed/skipped |
|---:|---|---:|
|50|`c0aa8efa71bcdb97e2a1816ffa25a997580a7d0f`|550/550/0|
|100|`73641c912f989face76a9ac4957af7e468fd473b`|600/600/0|
|150|`5e964c37c304b49da93798b938a87a9292c460a3`|650/650/0|
|200|`6de9bb705df190f7e15a2341cd979f6af3e66d5a`|711/702/9|

## Use locally

```text
make build
make test
make coverage
make native
make verify-native
make run-native-tools ARGS="--help"
make run-native-runner ARGS="--help"
```

Use make native-tools when only the full ReadyToRun helpers are desired. NativeAOT needs the documented host prerequisites. Coverage instrumentation should run serially with builds/tests of the same assemblies.

GitHub Actions remains administratively disabled (all 12 workflows). PR#62 remains open, unmerged and at its original head 0318a864d9b570f9ffbc4857e7365ec9ed6a4736. No workflow was re-enabled. The final normal push and remote verification are recorded in round8-delivery.json.
