# Development testing tools

Assessment: 2026-10-14. Decision: adopt xUnit for managed development tests,
use NSubstitute selectively for managed interaction contracts, pilot Testcontainers
for reproducible simulator processes, and defer WireMock.Net until an HTTP client
exists. These are development dependencies, not dependencies of the shipped runtime.
This assessment does not claim package installation or container validation.

## Repository evidence

`tests/SharpTestBench.Tests/Program.cs` is a standalone program whose Check/Throws
helpers abort on the first failed assertion. The current successful run executes
1,916 assertions; that number is not a count of independently discovered test cases.
Named cases would improve failure localization, selection and reporting.

`SimRuntime` is sealed and tied to the simulator thread; `Native.cs` exposes static
P/Invoke. `tools/native_faults.py` supplies fake native libraries in fresh processes
and validates real ABI resolution and registration failures. The integration tests
exercise actual simulator scheduling and HDL transport. These remain necessary.

`tools/run.py` uses shared build files such as top.vvp. Unique labels alone do not
make concurrent builds safe. The present CI installs Icarus and builds pinned cocotb
adapters. No HTTP client or database dependency was found in the runtime. Neither
a Docker executable in PATH nor /var/run/docker.sock was found in this workspace;
this is an environment observation, not proof that every remote Docker host is unavailable.

## Recommended roles

| Tool | SharpTestBench use | Boundary |
|---|---|---|
| xUnit v3 | Facts/theories for values, ranges, packed layouts, coverage and capability policy; later external process orchestration | Keep SimTest and the simulator-owned synchronization context for HDL bodies. |
| NSubstitute + analyzers | Verify cleanup callbacks and future observer/sink contracts; deliberately throw from a dependency | Cannot intercept static P/Invoke or establish HDL timing correctness. Use real value objects. |
| Testcontainers for .NET | Launch real, pinned simulator toolchains with isolated inputs and captured outputs | Requires a usable Docker-compatible environment; container success is not simulation success. |
| WireMock.Net | Future result-upload or remote-runner HTTP client tests | No present HTTP boundary to test; do not invent a network feature to justify the package. |

These recommendations follow from the repository architecture. The libraries'
documented capabilities are linked below; they do not demonstrate compatibility
with SharpTestBench until the adoption tasks pass.

## Execution and cleanup design

Run xUnit outside the simulator. Pure managed cases can use xUnit parallelism when
they have no shared state. External simulator cases must initially be serialized;
later each receives a separate process, working directory and artifact directory.
Do not run multiple runtimes in one process or install xUnit's synchronization
context inside the simulator. Wall-clock process deadlines and simulation-step
deadlines solve different problems and both remain required.

For #31, xUnit fixtures manage host resources; SharpTestBench cleanup scopes manage
HDL-test resources on the simulator thread. One cannot replace the other. Target
tests: reverse acquisition order, exactly-once cleanup, continued cleanup after one
callback fails, timeout/cancellation, and preservation of both the original failure
and cleanup errors. NSubstitute can verify callback contracts through an interface
or delegate. Avoid adding a virtual abstraction to every signal access merely to
make it mockable; preserve the hot path and native fault tests.

The container pilot should begin with Icarus. Pin the image digest, simulator build,
.NET runtime and cocotb source revision; archive provenance with results. Prefer
copied inputs over host-specific bind mounts. For a container whose main process
is the test command, configure one-shot completion semantics instead of waiting
for a server port. Independently verify exit status, expected nonempty test names,
case status and required artifacts. Save stdout/stderr, JSON/JUnit and waveforms
before disposing the container, including on failure. Bound startup and execution,
and clean up after cancellation. Test missing reports and a nonzero process exit.
Missing Docker must be explicitly reported; the designated container CI gate must
fail rather than silently skip. Existing local process workflows remain usable.
Linux containers do not validate Windows/macOS native loading or commercial licenses.

If a real HTTP integration is introduced, use a local WireMock.Net server on an
allocated port with the production HTTP client. Check methods, paths, bodies,
headers and request counts; simulate 429/Retry-After, 5xx, malformed responses,
timeouts and cancellation. Retries must preserve the endpoint's idempotency
contract. NSubstitute then tests whether a caller requested an upload; WireMock
tests the actual HTTP serialization and response handling. Neither proves the
remote service's behavior. No WireMock implementation task is opened now because
there is no approved HTTP feature; revisit this decision when one exists.

## Delivery sequence and acceptance

1. [#41](https://github.com/jeffadickson/SharpTestBench/issues/41): migrate managed
   tests incrementally, preserving assertion coverage and reference data. Pin a
   stable compatible xUnit/runner combination, choose Microsoft Testing Platform
   or VSTest deliberately, and verify discovery and CI output. Avoid a single Fact
   that wraps the entire legacy executable: it retains the diagnostic problem.
2. [#42](https://github.com/jeffadickson/SharpTestBench/issues/42), alongside
   [#31](https://github.com/jeffadickson/SharpTestBench/issues/31): add meaningful
   lifecycle interaction tests with NSubstitute and its analyzers. Keep a real
   simulator test for timeout cleanup and phase/thread constraints.
3. [#43](https://github.com/jeffadickson/SharpTestBench/issues/43): validate the
   Icarus Testcontainers pilot on Docker-enabled CI before expanding the backend
   matrix. Record startup/build costs and compare with the process runner.

Do not remove native fault injection, compiler-negative type checks, Python timing
comparisons, or real four-backend integration tests during this migration. Package
restore, analyzer configuration, lock files and license/dependency review belong
to implementation; this investigation did not restore or benchmark these tools.

## Primary sources consulted

- [xUnit v3 getting started](https://xunit.net/docs/getting-started/v3/getting-started): facts, theories, executable tests and runner choices. Example prerelease versions are not a dependency recommendation.
- [xUnit shared context](https://xunit.net/docs/shared-context): host fixture lifetime and asynchronous cleanup.
- [xUnit parallel execution](https://xunit.net/docs/running-tests-in-parallel): explicit parallelism controls.
- [NSubstitute creation and limitations](https://nsubstitute.github.io/help/creating-a-substitute/): interfaces and overridable members.
- [NSubstitute analyzers](https://nsubstitute.github.io/help/nsubstitute-analysers/): detect invalid substitution patterns.
- [Testcontainers](https://dotnet.testcontainers.org/): disposable containers and custom images.
- [Testcontainers wait strategies](https://dotnet.testcontainers.org/api/wait_strategies/): readiness, cancellation and one-shot jobs.
- [Testcontainers best practices](https://dotnet.testcontainers.org/api/best_practices/): resource isolation and input mapping.
- [WireMock.Net stubbing](https://wiremock.org/dotnet/stubbing/): local servers, matching, verification and delays.
- [WireMock.Net faults](https://wiremock.org/dotnet/faults/): response fault injection.
