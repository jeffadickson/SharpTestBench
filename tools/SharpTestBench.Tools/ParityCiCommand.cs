using System.Text.Json;

namespace SharpTestBench.Tools;

internal static class ParityCiCommand
{
    static int Execute(ToolContext context, string work, string label, string[] command, IReadOnlyDictionary<string, string?> environment)
    {
        var result = context.Run(command, environment: environment, capture: true, check: false, timeout: TimeSpan.FromSeconds(300));
        File.WriteAllText(Path.Combine(work, label + ".log"), result.Output + result.Error); return result.ExitCode;
    }
    static string[] LegacyCommand(ToolContext context, Dictionary<string, object> manifest, string work, ulong seed, string? exact)
    {
        var arguments = new List<string> { "dotnet", typeof(ToolContext).Assembly.Location, "run", "--sim", "icarus", "--no-build", "--assembly", (string)manifest["assembly"], "--top", (string)manifest["top"], "--entry-type", (string)manifest["entryType"], "--work-dir", Path.Combine(work, "legacy"), "--label", "legacy", "--seed", seed.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        foreach (var source in (string[])manifest["sources"]) arguments.AddRange(["--source", source]);
        if (exact is not null) arguments.AddRange(["--exact", exact]);
        return arguments.ToArray();
    }
    static Dictionary<string, object> Compare(ToolContext context, string work, Dictionary<string, object> manifest, ulong seed, Dictionary<string, string?> environment, string? exact, string? metricsVariable, string? marker, Action<JsonElement, JsonElement>? metrics)
    {
        var leftEnvironment = new Dictionary<string, string?>(environment); var rightEnvironment = new Dictionary<string, string?>(environment);
        if (metricsVariable is not null) { leftEnvironment[metricsVariable] = Path.Combine(work, "legacy/metrics.json"); rightEnvironment[metricsVariable] = Path.Combine(work, "native/metrics.json"); }
        int leftCode = Execute(context, work, "legacy", LegacyCommand(context, manifest, work, seed, exact), leftEnvironment);
        int rightCode = Execute(context, work, "native", ["dotnet", context.At("tools/SharpTestBench.Runner/bin/Release/net10.0/SharpTestBench.Runner.dll"), "run", Path.Combine(work, "manifest.json")], rightEnvironment);
        using var left = StrictJson.Read(Path.Combine(work, "legacy/legacy-results.json")); using var right = StrictJson.Read(Path.Combine(work, "native/icarus-results.json"));
        var expected = JsonSerializer.SerializeToElement(manifest["expectedNames"]);
        Dictionary<string, object> report;
        if (marker is not null)
        {
            bool a = ParityContracts.FaultDetected(left.RootElement, leftCode, expected, marker), b = ParityContracts.FaultDetected(right.RootElement, rightCode, expected, marker);
            report = new() { ["schemaVersion"] = 1, ["negativeControl"] = true, ["legacyDetected"] = a, ["nativeDetected"] = b, ["matched"] = a && b };
        }
        else
        {
            report = ParityContracts.Compare(left.RootElement, right.RootElement, expected);
            report["matched"] = (bool)report["matched"] && leftCode == 0 && rightCode == 0;
            if (metrics is not null)
            {
                using var a = StrictJson.Read(Path.Combine(work, "legacy/metrics.json")); using var b = StrictJson.Read(Path.Combine(work, "native/metrics.json"));
                metrics(a.RootElement, b.RootElement); report["traceMatched"] = true;
            }
        }
        report["exitCodes"] = new { legacy = leftCode, native = rightCode }; return report;
    }
    static Dictionary<string, object> Prepare(ToolContext context, string work, string project, string fixture, ulong seed, IReadOnlyDictionary<string, string?> environment)
    {
        if (Execute(context, work, "build", ["dotnet", "build", project, "-c", "Release", "-v:q"], environment) != 0) throw new IOException("Fixture build failed; see build.log");
        NativeBuildCommand.Build(context);
        return RunnerManifestCommand.Make(context.Root, fixture, seed, RunnerManifestCommand.InstalledAdapter(context), Path.Combine(work, "native"));
    }
    static Dictionary<string, object> InWorkspace(ToolContext context, string name, Func<string, Dictionary<string, string?>, Dictionary<string, object>> action)
    {
        var work = context.At("build/" + name); Directory.CreateDirectory(work); using var workspace = new WorkspaceLock(work, ".parity.lock");
        try
        {
            ParityContracts.ClearCase(work); var environment = ParityContracts.CleanEnvironment(ParityContracts.EnvironmentValues()); environment["STB_NATIVE_VPI_TIME"] = "1";
            var report = action(work, environment); bool negative = report.TryGetValue("negativeControl", out var flag) && flag is true;
            StrictJson.Write(Path.Combine(work, negative ? "negative-control.json" : "parity.json"), report);
            if (!(bool)report["matched"]) throw new InvalidDataException("Runner parity qualification failed");
            Console.WriteLine(JsonSerializer.Serialize(report)); return report;
        }
        catch (Exception error) { StrictJson.Write(Path.Combine(work, "case-failure.json"), new { schemaVersion = 1, status = "failed", error = error.Message, kind = error.GetType().Name }); throw; }
    }
    public static Dictionary<string, object> Dual(ToolContext context, string fixture, ulong seed, bool negative = false)
    {
        if (!RunnerManifestCommand.Fixtures.TryGetValue(fixture, out var item) || negative && fixture == "counter") throw new InvalidDataException("Invalid fixture or negative control");
        return InWorkspace(context, $"dual-{fixture}-{seed}" + (negative ? "-negative" : ""), (work, environment) => {
            var manifest = Prepare(context, work, item.Project, fixture, seed, environment);
            StrictJson.Write(Path.Combine(work, "manifest.json"), manifest); StrictJson.Write(Path.Combine(work, "expected.json"), manifest["expectedNames"]);
            if (fixture != "counter")
            {
                object plan = fixture == "dsp" ? WorkloadPlans.Dsp(128, seed) : WorkloadPlans.Cpu(16);
                StrictJson.Write(Path.Combine(work, "plan.json"), plan); environment[fixture.ToUpperInvariant() + "_PLAN"] = Path.Combine(work, "plan.json"); environment[fixture.ToUpperInvariant() + "_FAULT"] = negative ? "1" : "0";
            }
            return Compare(context, work, manifest, seed, environment, null, fixture == "counter" ? null : fixture.ToUpperInvariant() + "_REPORT", negative ? fixture.ToUpperInvariant() + "_MISMATCH" : null, fixture == "counter" ? null : (a, b) => { if (!JsonElement.DeepEquals(ParityContracts.Metrics(fixture, a), ParityContracts.Metrics(fixture, b))) throw new InvalidDataException("Workload traces differ"); });
        });
    }
    public static Dictionary<string, object> Lfsr(ToolContext context, string fixture, string style, ulong seed, int fault = 0)
    {
        var selected = LfsrFixture.Fixture(fixture, style); if (fault < 0 || fault > 2) throw new InvalidDataException("Invalid fault");
        var plan = LfsrFixture.Plan(selected.Config, seed); var stimulus = JsonSerializer.SerializeToElement(plan); LfsrFixture.ValidatePlan(stimulus);
        var provenance = FixtureContracts.VerifySources(context.Root, "lfsr");
        return InWorkspace(context, $"lfsr-{selected.Top}-{seed}-fault{fault}", (work, environment) => {
            environment["LFSR_PLAN"] = Path.Combine(work, "plan.json"); environment["LFSR_FAULT"] = fault.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var manifest = Prepare(context, work, "examples/Lfsr", "counter", seed, environment);
            manifest["top"] = selected.Top; manifest["sources"] = new[] { context.At("examples/Lfsr/lfsr_fixture.sv"), context.At("tests/Fixtures/verilog-lfsr/lfsr.v") }; manifest["assembly"] = context.At("examples/Lfsr/bin/Release/net10.0/Lfsr.dll"); manifest["entryType"] = "Entry, Lfsr"; manifest["expectedNames"] = new[] { "Entry.SerialScoreboard" }; manifest["testNames"] = new[] { "Entry.SerialScoreboard" };
            StrictJson.Write(Path.Combine(work, "manifest.json"), manifest); StrictJson.Write(Path.Combine(work, "plan.json"), plan); var digest = LfsrFixture.PlanDigest(Path.Combine(work, "plan.json"));
            var report = Compare(context, work, manifest, seed, environment, "Entry.SerialScoreboard", "LFSR_REPORT", fault == 0 ? null : "LFSR_MISMATCH", (a, b) => { LfsrFixture.Metrics(a, stimulus); LfsrFixture.Metrics(b, stimulus); if (!JsonElement.DeepEquals(a, b)) throw new InvalidDataException("LFSR traces differ"); });
            if (LfsrFixture.PlanDigest(Path.Combine(work, "plan.json")) != digest) throw new InvalidDataException("LFSR stimulus changed during execution"); FixtureContracts.VerifySources(context.Root, "lfsr");
            report["fixture"] = fixture; report["style"] = style; report["seed"] = seed; report["fault"] = fault; report["revision"] = provenance.GetProperty("revision").GetString()!; report["planSha256"] = digest; return report;
        });
    }
    public static Dictionary<string, object> Axis(ToolContext context, int mode, ulong seed, bool sidebands = true, bool negative = false, int count = 64, int stall = 3, int? resetAt = 32)
    {
        var top = AxisFixture.Configuration(mode, sidebands); var plan = AxisFixture.Plan(seed, count, stall, resetAt); AxisFixture.ValidatePlan(JsonSerializer.SerializeToElement(plan));
        var provenance = FixtureContracts.VerifySources(context.Root, "axis");
        return InWorkspace(context, $"axis-{top}-{seed}-{count}-{stall}-{resetAt?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "None"}" + (negative ? "-negative" : ""), (work, environment) => {
            environment["AXIS_MODE"] = mode.ToString(System.Globalization.CultureInfo.InvariantCulture); environment["AXIS_PLAN"] = Path.Combine(work, "plan.json"); environment["AXIS_SIDEBANDS"] = sidebands ? "1" : "0"; environment["AXIS_FAULT"] = negative ? "1" : "0";
            var manifest = Prepare(context, work, "examples/AxisRegister", "counter", seed, environment);
            manifest["top"] = top; manifest["sources"] = new[] { context.At("examples/AxisRegister/axis_fixture.sv"), context.At("tests/Fixtures/verilog-axis/axis_register.v") }; manifest["assembly"] = context.At("examples/AxisRegister/bin/Release/net10.0/AxisRegister.dll"); manifest["entryType"] = "Entry, AxisRegister"; manifest["expectedNames"] = new[] { "Entry.StreamScoreboard" }; manifest["testNames"] = new[] { "Entry.StreamScoreboard" };
            StrictJson.Write(Path.Combine(work, "manifest.json"), manifest); StrictJson.Write(Path.Combine(work, "plan.json"), plan);
            var report = Compare(context, work, manifest, seed, environment, "Entry.StreamScoreboard", "AXIS_REPORT", negative ? "AXIS_MISMATCH" : null, (a, b) => { AxisFixture.Metrics(a); AxisFixture.Metrics(b); if (!JsonElement.DeepEquals(a, b)) throw new InvalidDataException("Stream traces differ"); });
            FixtureContracts.VerifySources(context.Root, "axis"); report["top"] = top; report["seed"] = seed; report["revision"] = provenance.GetProperty("revision").GetString()!; return report;
        });
    }
    public static void Execute(ToolContext context, string name, string[] arguments)
    {
        var options = new Options(arguments, "--negative-control", "--disabled-sidebands");
        if (!ulong.TryParse(options.Get("--seed", "1"), out var seed)) throw new ArgumentException("Invalid seed");
        switch (name)
        {
            case "dual-runner-ci": options.Only("--fixture", "--seed", "--negative-control"); Dual(context, options.Require("--fixture"), seed, options.Has("--negative-control")); break;
            case "lfsr-ci": options.Only("--fixture", "--style", "--seed", "--fault"); if (!int.TryParse(options.Get("--fault", "0"), out var fault)) throw new ArgumentException("Invalid fault"); Lfsr(context, options.Require("--fixture"), options.Get("--style", "REDUCTION")!, seed, fault); break;
            case "axis-ci": options.Only("--mode", "--seed", "--disabled-sidebands", "--negative-control"); if (!int.TryParse(options.Get("--mode", "2"), out var mode)) throw new ArgumentException("Invalid mode"); Axis(context, mode, seed, !options.Has("--disabled-sidebands"), options.Has("--negative-control")); break;
        }
    }
}
