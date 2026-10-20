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
through its C# test API. Native code, HDL and simulator-only behavior require the
existing simulator qualification workflows. Functional coverage bins are a
separate measurement (`Coverage.cs`). No percentage target implies untested
simulator behavior is covered.

Coverlet wraps the executable xUnit v3 suites without recompiling instrumented
assemblies. Contract tests instrument a private helper copy to avoid modifying
the running coverage controller. Each suite explicitly selects its production
assembly, including SharpTestBench.Tools. Failed tests,
missing assemblies, empty reports and inconsistent counters stop publication of
the completion summary. The deliberate failing-test mutation in
`tools/SharpTestBench.Tools/ManagedTestsCommand.cs` runs separately and is excluded from coverage collection.
CI uploads the report even when collection fails, for diagnosis.

Tools: [Coverlet console](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/GlobalTool.md),
[ReportGenerator](https://github.com/danielpalme/ReportGenerator). Versions are
pinned in `.config/dotnet-tools.json`; update them explicitly and revalidate both
Windows and Linux. Coverage collection must run serially with other operations
that build or execute these same assemblies, since instrumentation temporarily
modifies the binaries and restores them afterward.
