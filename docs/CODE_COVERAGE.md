# Managed code coverage

Build the Release solution, restore the pinned local tools, then collect:

```sh
dotnet build SharpTestBench.slnx -c Release -p:RestoreLockedMode=true
dotnet tool restore
dotnet run --project tools/SharpTestBench.Tools -c Release --no-build -- code-coverage
```

Open `build/code-coverage/report/index.html`. The command also writes merged
Cobertura, JSON and text summaries. It measures simulator-independent managed
library, generator and managed-helper tests, plus the retained contract suite
through its C# test API. A separate Runner collection executes the retained
`test_*runner*.py` contracts against the production `SharpTestBench.Runner`
assembly referenced by RunnerValidation, rather than linked source copies.
An additional `runner-cli` collection executes the production entry point through
`test_runner_cli.py`, instrumenting a private copy of its DLL and retaining its
production runtime configuration with JSON reflection disabled. It covers help,
usage errors, malformed manifests, missing inputs and a controlled compiler
failure with retained phase evidence. The compile-boundary test deliberately
uses dummy input payloads and a rejecting subprocess; it does not simulate HDL.
This measures selected CLI startup/preflight/failure paths, not a complete CLI
success path or runtime-feature-switch matrix, and does not establish NativeAOT
instruction coverage. RunnerValidation enables JSON reflection for its test protocol;
the separate CLI collection preserves the production-disabled setting.
Native code, HDL and simulator-only behavior require the
existing simulator qualification workflows. Functional coverage bins are a
separate measurement (`Coverage.cs`). No percentage target implies untested
simulator behavior is covered.

The merged report requires all four production assemblies: SharpTestBench,
SharpTestBench.Generators, SharpTestBench.Tools and SharpTestBench.Runner.
Compiler-generated sources (including JSON serialization contexts) remain in
the counts and are emitted under each project's ignored `obj` directory so
ReportGenerator can display their source. They are not handwritten logic;
changes in generated code or assembly scope can change the denominator, so
percentages from different scopes are not directly comparable.

Coverlet wraps the executable xUnit v3 suites without recompiling instrumented
assemblies. Contract tests instrument a private helper copy to avoid modifying
the running coverage controller. Each suite explicitly selects its production
assembly, including SharpTestBench.Tools. Failed tests,
missing assemblies, empty reports and inconsistent counters stop publication of
the completion summary. The deliberate failing-test mutation in
`tools/SharpTestBench.Tools/ManagedTestsCommand.cs` runs separately and is excluded from coverage collection.
The disabled CI workflow retains report-upload steps for failure diagnosis.

Tools: [Coverlet console](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/GlobalTool.md),
[ReportGenerator](https://github.com/danielpalme/ReportGenerator). Versions are
pinned in `.config/dotnet-tools.json`; update them explicitly and revalidate both
Windows and Linux. Coverage collection must run serially with other operations
that build or execute these same assemblies, since instrumentation temporarily
modifies the binaries and restores them afterward.
