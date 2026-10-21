using System.Text.Json;
using SharpTestBench.Tools;
using Xunit;

public sealed class ParityFailureCoverageTests
{
    static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);
    static JsonElement Rows(string status = "failed", ulong seed = 17, string error = "DSP_MISMATCH injected", string name = "Entry.Scoreboard") =>
        JsonSerializer.SerializeToElement(new[] { new { QualifiedName = name, Name = "Scoreboard", Status = status, Seed = seed, Error = error } });

    sealed class Fixture : IDisposable, ICommandRunner
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "stb-parity-coverage-" + Guid.NewGuid().ToString("N"));
        public string Work => Path.Combine(Root, "build/case");
        public JsonElement Left { get; set; } = Rows();
        public JsonElement Right { get; set; } = Rows();
        public int LeftExit { get; set; } = 1;
        public int RightExit { get; set; } = 1;
        public string? LeftText { get; set; }
        public string? RightText { get; set; }
        public bool WriteLeft { get; set; } = true;
        public bool WriteRight { get; set; } = true;
        public string? LeftMetrics { get; set; }
        public string? RightMetrics { get; set; }
        public Exception? LeftException { get; set; }
        public Exception? RightException { get; set; }
        public Action? AfterLegacy { get; set; }
        public Action? AfterNative { get; set; }
        public List<string[]> Commands { get; } = [];
        public List<Dictionary<string, string?>> Environments { get; } = [];
        public ToolContext Context => new(Root, this);
        public Dictionary<string, object> Manifest { get; } = new() {
            ["schemaVersion"]=1,["simulator"]="icarus",["nativeHost"]="host",["icarusBootstrap"]="bootstrap",["gpiLibrary"]="gpi",["gpiEntryPoint"]="entry",["gpiImpl"]="impl:entry",["dotnetRoot"]="sdk",["outputDirectory"]="native",["seed"]=17UL,
            ["assembly"] = "fixture assembly.dll", ["top"] = "top", ["entryType"] = "Entry, Fixture",
            ["sources"] = new[] { "source one.sv", "source two.sv" }, ["expectedNames"] = new[] { "Entry.Scoreboard" }
        };
        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Work,"legacy"));Directory.CreateDirectory(Path.Combine(Work,"native"));
            foreach (var name in new[] { "source one.sv","source two.sv","fixture assembly.dll","fixture assembly.runtimeconfig.json","host","bootstrap","gpi","impl" })
                File.WriteAllText(Path.Combine(Work,name),"controlled input; not executable HDL or native code");
            var fxr=Path.Combine(Work,"sdk/host/fxr/10.0.0");Directory.CreateDirectory(fxr);File.WriteAllText(Path.Combine(fxr,NativeBuildCommand.FxrName),"controlled hostfxr input");
        }
        public CommandResult Run(IReadOnlyList<string> arguments, string directory, IReadOnlyDictionary<string, string?>? environment = null, bool capture = false, bool check = true, TimeSpan? timeout = null)
        {
            if (arguments.Count>1 && arguments[1]=="-c") return new(0,JsonSerializer.Serialize(new CocotbAdapter("fixture","python","libs","bootstrap","gpi,entry","impl:entry",null,null)),"");
            bool left = Commands.Count == 0;
            Commands.Add(arguments.ToArray()); Environments.Add(new(environment!));
            Assert.True(capture); Assert.False(check); Assert.Equal(TimeSpan.FromSeconds(300), timeout);
            var error = left ? LeftException : RightException; if (error is not null) throw error;
            if (left ? WriteLeft : WriteRight)
                File.WriteAllText(Path.Combine(Work, left ? "legacy/legacy-results.json" : "native/icarus-results.json"), (left ? LeftText : RightText) ?? (left ? Left : Right).GetRawText());
            var metrics = left ? LeftMetrics : RightMetrics;
            if (metrics is not null) File.WriteAllText(Path.Combine(Work, left ? "legacy/metrics.json" : "native/metrics.json"), metrics);
            if (left) AfterLegacy?.Invoke();else AfterNative?.Invoke();
            return new(left ? LeftExit : RightExit, left ? "legacy stdout\n" : "native stdout\n", left ? "legacy stderr" : "native stderr");
        }
        public Dictionary<string, object> Compare(string? marker = "DSP_MISMATCH", string? metricVariable = null, Action<JsonElement, JsonElement>? metrics = null, string? exact = null, Dictionary<string, string?>? environment = null, ulong seed = 17)
        {
            Manifest["seed"]=seed;
            return ParityCiCommand.Compare(Context, Work, Manifest, seed, environment ?? ParityContracts.ChildEnvironment(new Dictionary<string, string?>()), exact, metricVariable, marker, metrics);
        }
        public void Passing() { Left = Rows("passed"); Right = Rows("passed"); LeftExit = RightExit = 0; }
        public JsonElement Read(string name) => Json(File.ReadAllText(Path.Combine(Work, name)));
        public void Dispose()
        {
            var full = Path.GetFullPath(Root);
            if (Path.GetDirectoryName(full) != Path.TrimEndingDirectorySeparator(Path.GetTempPath())) throw new InvalidOperationException("Temporary path escaped its parent");
            Directory.Delete(full, true);
        }
    }

    // Iterations 1–15: negative-control identity, seed and failure boundaries.
    [Fact] public void NegativeControlRejectsDifferentSeeds()
    {
        using var f = new Fixture { Right = Rows(seed: 18) }; var report = f.Compare();
        Assert.True((bool)report["legacyDetected"]); Assert.True((bool)report["nativeDetected"]); Assert.False((bool)report["matched"]);
    }
    [Fact] public void NegativeControlMatchesCasesByIdentityRatherThanRowOrder()
    {
        using var f = new Fixture(); f.Manifest["expectedNames"] = new[] { "a", "b" };
        f.Left = Json("""[{"Name":"a","Status":"failed","Seed":1,"Error":"DSP_MISMATCH"},{"Name":"b","Status":"failed","Seed":2,"Error":"DSP_MISMATCH"}]""");
        f.Right = Json("""[{"Name":"b","Status":"failed","Seed":2,"Error":"DSP_MISMATCH"},{"Name":"a","Status":"failed","Seed":1,"Error":"DSP_MISMATCH"}]""");
        Assert.True((bool)f.Compare()["matched"]);
    }
    [Fact] public void NegativeControlRejectsSeedsSwappedBetweenCases()
    {
        using var f = new Fixture(); f.Manifest["expectedNames"] = new[] { "a", "b" };
        f.Left = Json("""[{"Name":"a","Status":"failed","Seed":1,"Error":"DSP_MISMATCH"},{"Name":"b","Status":"failed","Seed":2,"Error":"DSP_MISMATCH"}]""");
        f.Right = Json("""[{"Name":"a","Status":"failed","Seed":2,"Error":"DSP_MISMATCH"},{"Name":"b","Status":"failed","Seed":1,"Error":"DSP_MISMATCH"}]""");
        Assert.False((bool)f.Compare()["matched"]);
    }
    [Fact] public void NegativeControlPreservesFullWidthSeeds()
    {
        using var f = new Fixture { Left = Rows(seed: ulong.MaxValue), Right = Rows(seed: ulong.MaxValue) };
        Assert.True((bool)f.Compare()["matched"]);
    }
    [Fact] public void NegativeControlRejectsMismatchAboveDoublePrecision()
    {
        using var f = new Fixture { Left = Rows(seed: 9007199254740992), Right = Rows(seed: 9007199254740993) };
        Assert.False((bool)f.Compare()["matched"]);
    }
    [Fact] public void NegativeControlRejectsSuccessfulLegacyExit()
    {
        using var f = new Fixture { LeftExit = 0 }; var report = f.Compare();
        Assert.False((bool)report["legacyDetected"]); Assert.True((bool)report["nativeDetected"]); Assert.False((bool)report["matched"]);
    }
    [Fact] public void NegativeControlRejectsNativeCrash()
    {
        using var f = new Fixture { RightExit = 139 }; var report = f.Compare();
        Assert.True((bool)report["legacyDetected"]); Assert.False((bool)report["nativeDetected"]); Assert.False((bool)report["matched"]);
    }
    [Fact] public void NegativeControlRejectsBlankMarker()
    {
        using var f = new Fixture(); Assert.False((bool)f.Compare(marker: "")["matched"]);
    }
    [Fact] public void NegativeControlRejectsWhitespaceMarker()
    {
        using var f = new Fixture { Left = Rows(error: "unrelated failure "), Right = Rows(error: "unrelated failure ") };
        Assert.False((bool)f.Compare(marker: " ")["matched"]);
    }
    [Fact] public void NegativeControlRejectsUnrelatedNativeError()
    {
        using var f = new Fixture { Right = Rows(error: "simulator crashed") }; Assert.False((bool)f.Compare()["matched"]);
    }
    [Fact] public void NegativeControlRejectsQualifiedIdentitySpoofing()
    {
        using var f = new Fixture { Right = Rows(name: "Other.Scoreboard") }; Assert.False((bool)f.Compare()["matched"]);
    }
    [Fact] public void NegativeControlRejectsSkippedNativeCase()
    {
        using var f = new Fixture { Right = Rows("skipped") }; Assert.False((bool)f.Compare()["matched"]);
    }
    [Fact] public void NegativeControlRejectsMalformedSeedWithoutThrowing()
    {
        using var f = new Fixture { Right = Json("""[{"Name":"Entry.Scoreboard","Status":"failed","Seed":true,"Error":"DSP_MISMATCH"}]""") };
        Assert.False((bool)f.Compare()["matched"]);
    }
    [Fact] public void NegativeControlDoesNotRequireSuccessMetrics()
    {
        using var f = new Fixture(); bool called = false;
        Assert.True((bool)f.Compare(metricVariable: "DSP_REPORT", metrics: (_, _) => called = true)["matched"]); Assert.False(called);
    }
    [Fact] public void NegativeControlPreservesEvidenceSchemaAndExitCodes()
    {
        using var f = new Fixture(); var report = JsonSerializer.SerializeToElement(f.Compare());
        Assert.Equal(new[] { "exitCodes", "legacyDetected", "matched", "nativeDetected", "negativeControl", "schemaVersion" }, report.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(1, report.GetProperty("exitCodes").GetProperty("legacy").GetInt32()); Assert.Equal(1, report.GetProperty("exitCodes").GetProperty("native").GetInt32());
    }

    const string DspMetrics = """{"trace":[[1,2,3]],"cycles":2,"accepted":1,"consumed":1,"stalls":0}""";
    static void EqualDspMetrics(JsonElement left, JsonElement right)
    {
        if (!JsonElement.DeepEquals(ParityContracts.Metrics("dsp", left), ParityContracts.Metrics("dsp", right))) throw new InvalidDataException("Workload traces differ");
    }

    // Iterations 16–30: positive qualification, metrics and the process boundary.
    [Fact] public void PositiveControlRequiresMatchingPassedCasesAndZeroExits()
    {
        using var f = new Fixture(); f.Passing(); var report = JsonSerializer.SerializeToElement(f.Compare(marker: null));
        Assert.True(report.GetProperty("matched").GetBoolean()); Assert.Empty(report.GetProperty("seedMismatches").EnumerateArray());
        Assert.Equal("Entry.Scoreboard", report.GetProperty("expected")[0].GetString());
    }
    [Fact] public void PositiveControlRejectsLegacyFailureEvenWithPassingResults()
    {
        using var f = new Fixture(); f.Passing(); f.LeftExit = 7; Assert.False((bool)f.Compare(marker: null)["matched"]);
    }
    [Fact] public void PositiveControlRejectsNativeFailureEvenWithPassingResults()
    {
        using var f = new Fixture(); f.Passing(); f.RightExit = 7; Assert.False((bool)f.Compare(marker: null)["matched"]);
    }
    [Fact] public void MissingLegacyResultsCannotQualify()
    {
        using var f = new Fixture { WriteLeft = false }; Assert.Throws<FileNotFoundException>(() => f.Compare());
    }
    [Fact] public void MalformedNativeResultsCannotQualify()
    {
        using var f = new Fixture { RightText = "{" }; Assert.ThrowsAny<JsonException>(() => f.Compare());
    }
    [Fact] public void PositiveMetricsAreComparedBeforeTraceQualification()
    {
        using var f = new Fixture { LeftMetrics = DspMetrics, RightMetrics = DspMetrics }; f.Passing();
        var report = f.Compare(marker: null, metricVariable: "DSP_REPORT", metrics: EqualDspMetrics);
        Assert.True((bool)report["matched"]); Assert.True((bool)report["traceMatched"]);
    }
    [Fact] public void DivergentFunctionalMetricsCannotQualify()
    {
        using var f = new Fixture { LeftMetrics = DspMetrics, RightMetrics = DspMetrics.Replace("[1,2,3]", "[1,2,4]", StringComparison.Ordinal) }; f.Passing();
        Assert.Equal("Workload traces differ", Assert.Throws<InvalidDataException>(() => f.Compare(marker: null, metricVariable: "DSP_REPORT", metrics: EqualDspMetrics)).Message);
    }
    [Fact] public void MissingNativeMetricsCannotQualify()
    {
        using var f = new Fixture { LeftMetrics = DspMetrics }; f.Passing();
        Assert.Throws<FileNotFoundException>(() => f.Compare(marker: null, metricVariable: "DSP_REPORT", metrics: EqualDspMetrics));
    }
    [Fact] public void MalformedLegacyMetricsCannotQualify()
    {
        using var f = new Fixture { LeftMetrics = "{", RightMetrics = DspMetrics }; f.Passing();
        Assert.ThrowsAny<JsonException>(() => f.Compare(marker: null, metricVariable: "DSP_REPORT", metrics: EqualDspMetrics));
    }
    [Fact] public void LegacyLaunchPreservesOrderedSourcesExactSelectionAndFullWidthSeed()
    {
        using var f = new Fixture(); f.Compare(exact: "Entry.Scoreboard", seed: ulong.MaxValue);
        var command = f.Commands[0]; var sourcePositions = command.Select((value, index) => (value, index)).Where(pair => pair.value == "--source").Select(pair => pair.index).ToArray();
        Assert.Equal(new[] { Path.GetFullPath("source one.sv",f.Work), Path.GetFullPath("source two.sv",f.Work) }, sourcePositions.Select(index => command[index + 1]));
        Assert.Equal("18446744073709551615", command[Array.IndexOf(command, "--seed") + 1]);
        Assert.Equal("Entry.Scoreboard", command[Array.IndexOf(command, "--exact") + 1]);
        Assert.Equal(Path.GetFullPath("fixture assembly.dll",f.Work), command[Array.IndexOf(command, "--assembly") + 1]);
        Assert.Contains("--no-build", command);
    }
    [Fact] public void NativeLaunchUsesProductionRunnerAndExactManifestPath()
    {
        using var f = new Fixture(); f.Compare();
        Assert.Equal(new[] { "dotnet", f.Context.At("tools/SharpTestBench.Runner/bin/Release/net10.0/SharpTestBench.Runner.dll"), "run", Path.Combine(f.Work, "manifest.json") }, f.Commands[1]);
    }
    [Fact] public void MetricDestinationsAreIsolatedWithoutMutatingCallerEnvironment()
    {
        using var f = new Fixture(); var original = new Dictionary<string, string?> { ["DSP_REPORT"] = "parent-path", ["UNRELATED"] = "keep" };
        f.Compare(metricVariable: "DSP_REPORT", environment: original);
        Assert.Equal(Path.Combine(f.Work, "legacy/metrics.json"), f.Environments[0]["DSP_REPORT"]);
        Assert.Equal(Path.Combine(f.Work, "native/metrics.json"), f.Environments[1]["DSP_REPORT"]);
        Assert.Equal("parent-path", original["DSP_REPORT"]); Assert.All(f.Environments, env => Assert.Equal("keep", env["UNRELATED"]));
    }
    [Fact] public void BothLaunchesRetainExplicitInheritedControlRemovals()
    {
        using var f = new Fixture(); f.Compare();
        Assert.Equal(2, f.Environments.Count);
        Assert.All(f.Environments, env => { foreach (var key in ParityContracts.SelectionVariables) { Assert.True(env.ContainsKey(key), key); Assert.Null(env[key]); } });
    }
    [Fact] public void DriverLogsRetainBothStreamsForEachRunner()
    {
        using var f = new Fixture(); f.Compare();
        Assert.Equal("legacy stdout\nlegacy stderr", File.ReadAllText(Path.Combine(f.Work, "legacy.log")));
        Assert.Equal("native stdout\nnative stderr", File.ReadAllText(Path.Combine(f.Work, "native.log")));
    }
    [Fact] public void LegacyLaunchExceptionStopsNativeLaunch()
    {
        using var f = new Fixture { LeftException = new TimeoutException("legacy watchdog") };
        Assert.Equal("legacy watchdog", Assert.Throws<TimeoutException>(() => f.Compare()).Message); Assert.Single(f.Commands);
        Assert.False(File.Exists(Path.Combine(f.Work, "native.log")));
    }

    // Iterations 31–45: workspace failures, retries, locks and CLI evidence.
    [Fact] public void NativeLaunchExceptionRetainsCompletedLegacyLog()
    {
        using var f = new Fixture { RightException = new IOException("native launch failed") };
        Assert.Equal("native launch failed", Assert.Throws<IOException>(() => f.Compare()).Message); Assert.Equal(2, f.Commands.Count);
        Assert.Equal("legacy stdout\nlegacy stderr", File.ReadAllText(Path.Combine(f.Work, "legacy.log")));
        Assert.False(File.Exists(Path.Combine(f.Work, "native.log")));
    }
    [Fact] public void WorkspaceSuccessWritesParityAndReleasesLease()
    {
        using var f = new Fixture(); f.Passing();
        ParityCiCommand.InWorkspace(f.Context, "case", (_, env) => f.Compare(marker: null, environment: env));
        Assert.True(f.Read("parity.json").GetProperty("matched").GetBoolean());
        Assert.False(File.Exists(Path.Combine(f.Work, ".parity.lock"))); Assert.False(File.Exists(Path.Combine(f.Work, "case-failure.json")));
    }
    [Fact] public void NegativeWorkspaceWritesOnlyNegativeCompletionReport()
    {
        using var f = new Fixture(); ParityCiCommand.InWorkspace(f.Context, "case", (_, env) => f.Compare(environment: env));
        Assert.True(f.Read("negative-control.json").GetProperty("matched").GetBoolean()); Assert.False(File.Exists(Path.Combine(f.Work, "parity.json")));
    }
    [Fact] public void UnmatchedWorkspaceRetainsDiagnosticReportAndRecordsFailure()
    {
        using var f = new Fixture { Right = Rows(seed: 18) };
        Assert.Throws<InvalidDataException>(() => ParityCiCommand.InWorkspace(f.Context, "case", (_, env) => f.Compare(environment: env)));
        Assert.False(f.Read("negative-control.json").GetProperty("matched").GetBoolean());
        Assert.Equal("InvalidDataException", f.Read("case-failure.json").GetProperty("kind").GetString()); Assert.False(File.Exists(Path.Combine(f.Work, ".parity.lock")));
    }
    [Fact] public void WorkspaceCallbackFailureRetainsOriginalExceptionKind()
    {
        using var f = new Fixture();
        Assert.Throws<TimeoutException>(() => ParityCiCommand.InWorkspace(f.Context, "case", (_, _) => throw new TimeoutException("watchdog detail")));
        var failure = f.Read("case-failure.json"); Assert.Equal("TimeoutException", failure.GetProperty("kind").GetString()); Assert.Equal("watchdog detail", failure.GetProperty("error").GetString());
        Assert.False(File.Exists(Path.Combine(f.Work, "parity.json"))); Assert.False(File.Exists(Path.Combine(f.Work, ".parity.lock")));
    }
    [Fact] public void SuccessfulRetryRemovesPriorCaseFailure()
    {
        using var f = new Fixture();
        Assert.Throws<IOException>(() => ParityCiCommand.InWorkspace(f.Context, "case", (_, _) => throw new IOException("first attempt")));
        f.Passing(); ParityCiCommand.InWorkspace(f.Context, "case", (_, env) => f.Compare(marker: null, environment: env));
        Assert.True(f.Read("parity.json").GetProperty("matched").GetBoolean()); Assert.False(File.Exists(Path.Combine(f.Work, "case-failure.json")));
    }
    [Fact] public void WorkspaceContentionPreservesOwnerAndDoesNotExecuteCallback()
    {
        using var f = new Fixture(); using var owner = new WorkspaceLock(f.Work, ".parity.lock"); bool called = false;
        File.WriteAllText(Path.Combine(f.Work, "parity.json"), "active owner evidence");
        string ReadLock() { using var input = new FileStream(Path.Combine(f.Work, ".parity.lock"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using var reader = new StreamReader(input); return reader.ReadToEnd(); }
        var original = ReadLock();
        Assert.Throws<IOException>(() => ParityCiCommand.InWorkspace(f.Context, "case", (_, _) => { called = true; return new(); }));
        Assert.False(called); Assert.Equal(original, ReadLock()); Assert.False(File.Exists(Path.Combine(f.Work, "case-failure.json")));
        Assert.Equal("active owner evidence", File.ReadAllText(Path.Combine(f.Work, "parity.json")));
    }
    [Fact] public void LockedChildPreservesItsEvidenceAndRetiresStaleCompletionReports()
    {
        using var f = new Fixture(); File.WriteAllText(Path.Combine(f.Work, "native/.sharptestbench-run.lock"), "owned");
        File.WriteAllText(Path.Combine(f.Work, "native/icarus-results.json"), "owned results");
        foreach (var name in new[] { "parity.json", "negative-control.json" }) File.WriteAllText(Path.Combine(f.Work, name), "{\"matched\":true}");
        Assert.Throws<IOException>(() => ParityCiCommand.InWorkspace(f.Context, "case", (_, env) => f.Compare(environment: env)));
        Assert.Equal("owned results", File.ReadAllText(Path.Combine(f.Work, "native/icarus-results.json"))); Assert.Empty(f.Commands);
        Assert.Equal("IOException", f.Read("case-failure.json").GetProperty("kind").GetString());
        Assert.False(File.Exists(Path.Combine(f.Work, "parity.json"))); Assert.False(File.Exists(Path.Combine(f.Work, "negative-control.json")));
    }
    [Fact] public void WorkspaceRemovesStaleCompletionReportsBeforeSuccess()
    {
        using var f = new Fixture(); foreach (var name in new[] { "parity.json", "negative-control.json", "case-failure.json" }) File.WriteAllText(Path.Combine(f.Work, name), "stale");
        f.Passing(); ParityCiCommand.InWorkspace(f.Context, "case", (_, env) => f.Compare(marker: null, environment: env));
        Assert.Equal(1, f.Read("parity.json").GetProperty("schemaVersion").GetInt32()); Assert.False(File.Exists(Path.Combine(f.Work, "negative-control.json"))); Assert.False(File.Exists(Path.Combine(f.Work, "case-failure.json")));
    }
    [Fact] public void WorkspacePreservesUnrelatedCaseAndChildFiles()
    {
        using var f = new Fixture(); var paths = new[] { "keep.txt", "legacy/keep.dat", "native/keep.bin" };
        foreach (var path in paths) File.WriteAllText(Path.Combine(f.Work, path), "keep");
        ParityCiCommand.InWorkspace(f.Context, "case", (_, env) => f.Compare(environment: env));
        Assert.All(paths, path => Assert.Equal("keep", File.ReadAllText(Path.Combine(f.Work, path))));
    }
    [Fact] public void WorkspaceRemovesControlsBeforeCallbackAndEnablesNativeTime()
    {
        using var f = new Fixture();
        ParityCiCommand.InWorkspace(f.Context, "case", (_, env) => {
            foreach (var key in ParityContracts.SelectionVariables) { Assert.True(env.ContainsKey(key), key); Assert.Null(env[key]); }
            Assert.Equal("1", env["STB_NATIVE_VPI_TIME"]); return f.Compare(environment: env);
        });
        Assert.All(f.Environments, env => Assert.Equal("1", env["STB_NATIVE_VPI_TIME"]));
    }
    [Fact] public void WorkspaceMetricsFailureCannotLeaveCompletionReport()
    {
        using var f = new Fixture { LeftMetrics = DspMetrics, RightMetrics = DspMetrics.Replace("[1,2,3]", "[9,2,3]", StringComparison.Ordinal) }; f.Passing();
        Assert.Throws<InvalidDataException>(() => ParityCiCommand.InWorkspace(f.Context, "case", (_, env) => f.Compare(marker: null, metricVariable: "DSP_REPORT", metrics: EqualDspMetrics, environment: env)));
        Assert.False(File.Exists(Path.Combine(f.Work, "parity.json"))); Assert.Equal("Workload traces differ", f.Read("case-failure.json").GetProperty("error").GetString());
        Assert.True(File.Exists(Path.Combine(f.Work, "legacy/metrics.json"))); Assert.True(File.Exists(Path.Combine(f.Work, "native/metrics.json")));
    }
    [Fact] public void WorkspaceMissingResultsWritesFailureAndReleasesLease()
    {
        using var f = new Fixture { WriteRight = false };
        Assert.Throws<FileNotFoundException>(() => ParityCiCommand.InWorkspace(f.Context, "case", (_, env) => f.Compare(environment: env)));
        Assert.Equal("FileNotFoundException", f.Read("case-failure.json").GetProperty("kind").GetString()); Assert.False(File.Exists(Path.Combine(f.Work, ".parity.lock")));
    }
    static string[] ParityArguments(Fixture f, string report) => ["--legacy", Path.Combine(f.Work, "left.json"), "--native", Path.Combine(f.Work, "right.json"), "--expected", Path.Combine(f.Work, "expected.json"), "--report", Path.Combine(f.Work, report)];
    [Fact] public void ParityCliCannotOverwriteItsResultInput()
    {
        using var f = new Fixture(); var left = Path.Combine(f.Work, "left.json"); File.WriteAllText(left, "original input");
        Assert.Throws<InvalidDataException>(() => ParityContracts.Execute(ParityArguments(f, "left.json"))); Assert.Equal("original input", File.ReadAllText(left));
    }
    [Fact] public void ParityCliMalformedEvidenceReplacesStaleSuccessWithoutTemporaryFiles()
    {
        using var f = new Fixture(); File.WriteAllText(Path.Combine(f.Work, "left.json"), "{"); File.WriteAllText(Path.Combine(f.Work, "right.json"), Rows("passed").GetRawText());
        File.WriteAllText(Path.Combine(f.Work, "expected.json"), "[\"Entry.Scoreboard\"]"); File.WriteAllText(Path.Combine(f.Work, "report.json"), "{\"matched\":true}");
        Assert.Throws<InvalidDataException>(() => ParityContracts.Execute(ParityArguments(f, "report.json")));
        var report = f.Read("report.json"); Assert.False(report.GetProperty("matched").GetBoolean()); Assert.True(report.TryGetProperty("error", out _)); Assert.Equal(1, report.GetProperty("schemaVersion").GetInt32());
        Assert.Empty(Directory.EnumerateFiles(f.Work, "*.tmp"));
    }

    [Fact] public void NegativeControlRejectsBothRunnersUsingWrongRequestedSeed()
    {
        using var f = new Fixture { Left = Rows(seed:18), Right = Rows(seed:18) };
        f.Manifest["expectedSeeds"] = new Dictionary<string, ulong> { ["Entry.Scoreboard"] = 17 };
        var report = f.Compare();
        Assert.True((bool)report["legacyDetected"]); Assert.True((bool)report["nativeDetected"]);
        Assert.False((bool)report["expectedSeedMatched"]); Assert.False((bool)report["matched"]);
    }
    [Fact] public void PositiveParityRejectsBothRunnersUsingWrongRequestedSeed()
    {
        using var f = new Fixture(); f.Passing();
        f.Left = Rows("passed",18); f.Right = Rows("passed",18);
        f.Manifest["expectedSeeds"] = new Dictionary<string, ulong> { ["Entry.Scoreboard"] = 17 };
        var report = f.Compare(marker:null);
        Assert.False((bool)report["expectedSeedMatched"]); Assert.False((bool)report["matched"]);
    }
    [Fact] public void NestedCompilerCaseCannotRetainAnEarlierPassingReportAfterFailure()
    {
        using var f=new Fixture();const string name="manifest-check/compiler-parity";
        ParityCiCommand.InWorkspace(f.Context,name,(_,_)=>new Dictionary<string,object> { ["matched"]=true });
        var pair=f.Context.At("build/"+name);Assert.True(File.Exists(Path.Combine(pair,"parity.json")));
        File.WriteAllText(Path.Combine(pair,"pair-input-manifest.json"),"old verified inputs");
        File.WriteAllText(Path.Combine(pair,"oracle-input-manifest.json"),"old verified oracle");
        Assert.Throws<IOException>(()=>ParityCiCommand.InWorkspace(f.Context,name,(_,_)=>throw new IOException("controlled rerun failure")));
        Assert.False(File.Exists(Path.Combine(pair,"parity.json")));
        Assert.False(File.Exists(Path.Combine(pair,"pair-input-manifest.json")));
        Assert.False(File.Exists(Path.Combine(pair,"oracle-input-manifest.json")));
        Assert.True(File.Exists(Path.Combine(pair,"case-failure.json")));
        Assert.False(File.Exists(Path.Combine(pair,".parity.lock")));
    }
    [Fact] public void BothRunnerCommandsUseIdenticalEffectiveAmbientCompilerAndSimulator()
    {
        using var f=new Fixture();
        var environment=new Dictionary<string,string?> { ["CS_IVERILOG"]="tools/compiler with spaces",["CS_VVP"]="tools/simulator with spaces",["CS_IVL_ROOT"]="other library root" };
        f.Compare(environment:environment);
        var manifest=f.Read("manifest.json");
        Assert.Equal(Path.GetFullPath("tools/compiler with spaces",f.Root),manifest.GetProperty("iverilog").GetString());
        Assert.Equal(manifest.GetProperty("iverilog").GetString(),f.Environments[0]["CS_IVERILOG"]);
        Assert.Equal(manifest.GetProperty("vvp").GetString(),f.Environments[0]["CS_VVP"]);
        Assert.All(f.Environments,env=>Assert.Null(env["CS_IVL_ROOT"]));
        Assert.Equal("other library root",environment["CS_IVL_ROOT"]);
    }
    [Fact] public void DifferentSourceBetweenLaunchesCannotQualifyEvenWhenResultRowsMatch()
    {
        using var f=new Fixture();f.Passing();f.AfterLegacy=()=>File.AppendAllText(Path.Combine(f.Work,"source one.sv"),"changed");
        Assert.Throws<InvalidDataException>(()=>f.Compare(marker:null));Assert.Single(f.Commands);
    }
    [Fact] public void InputMutationAfterNativeCannotPublishMatchedPair()
    {
        using var f=new Fixture();f.Passing();f.AfterNative=()=>File.AppendAllText(Path.Combine(f.Work,"fixture assembly.runtimeconfig.json"),"changed");
        Assert.Throws<InvalidDataException>(()=>f.Compare(marker:null));Assert.Equal(2,f.Commands.Count);
    }
    [Fact] public void IncludeAdditionRemovalAndManagedDependencyAdditionArePairInputChanges()
    {
        foreach (var change in new[] { "add-include","remove-include","add-dependency" })
        {
            using var f=new Fixture();f.Passing();var include=Path.Combine(f.Work,"includes");Directory.CreateDirectory(include);
            var header=Path.Combine(include,"config.vh");File.WriteAllText(header,"original");f.Manifest["includeDirectories"]=new[] { include };
            f.AfterLegacy=()=> { if (change=="add-include") File.WriteAllText(Path.Combine(include,"new.vh"),"added");
                else if (change=="remove-include") File.Delete(header);else File.WriteAllText(Path.Combine(f.Work,"new dependency.dll"),"added"); };
            Assert.Throws<InvalidDataException>(()=>f.Compare(marker:null));Assert.Single(f.Commands);
        }
    }
    [Fact] public void StablePairPublishesAdditiveCanonicalInputRecords()
    {
        using var f=new Fixture();f.Passing();var report=f.Compare(marker:null);
        Assert.False(report.ContainsKey("pairedInputMatched")); // Existing parity schema remains unchanged.
        var evidence=f.Read("pair-input-manifest.json");Assert.Equal("verified",evidence.GetProperty("status").GetString());
        var inputs=evidence.GetProperty("inputs").EnumerateArray().ToArray();
        Assert.Contains(inputs,input=>input.GetProperty("Role").GetString()=="source" && input.GetProperty("Path").GetString()==Path.GetFullPath("source one.sv",f.Work));
        Assert.Contains(inputs,input=>input.GetProperty("Role").GetString()=="manifest");Assert.All(inputs,input=>Assert.Equal(64,input.GetProperty("Sha256").GetString()!.Length));
    }
    [Fact] public void MetricsCallbackCannotChangeInputsAfterBothLaunchesAndStillPublishMatch()
    {
        using var f=new Fixture { LeftMetrics=DspMetrics,RightMetrics=DspMetrics };f.Passing();
        Assert.Throws<InvalidDataException>(()=>f.Compare(marker:null,metricVariable:"DSP_REPORT",metrics:(_,_)=>File.AppendAllText(Path.Combine(f.Work,"source two.sv"),"changed after simulation")));
        Assert.Equal(2,f.Commands.Count);
    }
    [Fact] public void PairEvidenceDestinationCannotOverwriteADeclaredSource()
    {
        using var f=new Fixture();f.Passing();var path=Path.Combine(f.Work,"pair-input-manifest.json");
        File.WriteAllText(path,"protected source");f.Manifest["sources"]=new[] { path };
        Assert.Throws<InvalidDataException>(()=>f.Compare(marker:null));Assert.Empty(f.Commands);
        Assert.Equal("protected source",File.ReadAllText(path));
    }
}
