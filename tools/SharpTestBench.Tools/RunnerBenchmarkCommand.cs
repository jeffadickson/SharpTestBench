using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace SharpTestBench.Tools;

internal sealed record RunnerTiming(int Round, bool Warmup, int Order, string Runner,
    double HelpRoundTripMilliseconds, double TotalMilliseconds, double CompileMilliseconds,
    double SimulationMilliseconds, double OtherMilliseconds);
internal static class RunnerBenchmarkCommand
{
    internal static string[] Order(int round) => (round & 1) == 0 ? ["legacy", "standalone"] : ["standalone", "legacy"];
    internal static object Distribution(IEnumerable<double> source)
    {
        var values = source.Order().ToArray();
        if (values.Length == 0 || values.Any(value => !double.IsFinite(value) || value < 0)) throw new InvalidDataException("Invalid timing distribution");
        int middle = values.Length / 2;
        return new { count = values.Length, min = values[0], median = values.Length % 2 == 0 ? (values[middle - 1] + values[middle]) / 2 : values[middle], max = values[^1] };
    }
    internal static double Phase(JsonElement report, string phase)
    {
        if (report.GetProperty("SchemaVersion").GetInt32() != 1 || report.GetProperty("Phase").GetString() != phase ||
            !report.GetProperty("Started").GetBoolean() || report.GetProperty("ExitCode").GetInt32() != 0 ||
            report.GetProperty("TimedOut").GetBoolean() || report.GetProperty("Failure").ValueKind != JsonValueKind.Null)
            throw new InvalidDataException("Unsuccessful benchmark phase");
        return Duration(report.GetProperty("WallMilliseconds").GetDouble());
    }
    internal static double Duration(double value) => double.IsFinite(value) && value >= 0 ? value : throw new InvalidDataException("Invalid benchmark duration");
    internal static double Remainder(double total, double compile, double simulate)
    {
        Duration(total); Duration(compile); Duration(simulate);
        var value = total - compile - simulate;
        if (value < 0) throw new InvalidDataException("Phase durations exceed total wall time");
        return value;
    }
    static double Run(ToolContext context, string[] command, string log, Dictionary<string, string?> environment)
    {
        var watch = Stopwatch.StartNew();
        CommandResult result;
        try { result = context.Run(command, environment: environment, capture: true, check: false, timeout: TimeSpan.FromSeconds(300)); }
        catch (Exception error) { File.WriteAllText(log, "Invocation failed before complete output was available: " + error); throw; }
        double elapsed = watch.Elapsed.TotalMilliseconds;
        File.WriteAllText(log, result.Output + result.Error);
        if (result.ExitCode != 0) throw new InvalidDataException("Benchmark child failed; see " + log);
        return elapsed;
    }
    internal static void Qualify(JsonElement left, JsonElement right, JsonElement expected, ulong seed)
    {
        if (!(bool)ParityContracts.Compare(left, right, expected)["matched"] ||
            left.EnumerateArray().Concat(right.EnumerateArray()).Any(row => FixtureContracts.Integer(row.GetProperty("Seed"), 0, ulong.MaxValue, "seed") != seed))
            throw new InvalidDataException("Benchmark results or expected seeds differ");
    }
    static Dictionary<string, string> Hashes(IEnumerable<string> paths) => paths.Select(Path.GetFullPath).Distinct().Order(StringComparer.Ordinal).ToDictionary(path => path, CiCommands.Hash, StringComparer.Ordinal);
    public static void Execute(ToolContext context, string[] arguments)
    {
        var options = new Options(arguments); options.Only("--fixture", "--seed", "--repeats", "--warmups", "--runner-directory");
        var fixture = options.Get("--fixture", "counter")!;
        if (!RunnerManifestCommand.Fixtures.TryGetValue(fixture, out var definition)) throw new ArgumentException("Unknown benchmark fixture");
        if (!int.TryParse(options.Get("--repeats", "5"), out int repeats) || repeats is < 3 or > 30 ||
            !int.TryParse(options.Get("--warmups", "1"), out int warmups) || warmups is < 1 or > 5 ||
            !ulong.TryParse(options.Get("--seed", "17"), out ulong seed)) throw new ArgumentException("Invalid benchmark repeat, warmup or seed");
        string[] standalone = ["dotnet", context.At("tools/SharpTestBench.Runner/bin/Release/net10.0/SharpTestBench.Runner.dll")];
        WorkerPublish? worker = null;
        if (options.Get("--runner-directory") is string directory)
        {
            worker = new("runner", NativeBuildCommand.Rid, "native-aot", Path.GetFullPath(directory), []);
            WorkerArtifact.Verify(worker);
            standalone = [Path.Combine(worker.Directory, WorkerArtifact.EntryPoint(worker))];
        }
        var work = context.At($"build/runner-benchmark-{fixture}-{Guid.NewGuid():N}"); Directory.CreateDirectory(work);
        Console.WriteLine("Benchmark evidence: " + work);
        using var lease = new WorkspaceLock(work, ".benchmark.lock");
        var samples = new List<RunnerTiming>();
        try
        {
            context.Run(["dotnet", "build", context.At(definition.Project), "-c", "Release", "-p:RestoreLockedMode=true", "--disable-build-servers", "-m:1"]);
            NativeBuildCommand.Build(context);
            var adapter = RunnerManifestCommand.InstalledAdapter(context); adapter["dotnetRoot"] = NativeBuildCommand.SdkRoot();
            var environment = ParityContracts.ChildEnvironment(new Dictionary<string, string?>()); environment["STB_NATIVE_VPI_TIME"] = "1";
            // Both compilers use the selected executable's built-in library root.
            // The standalone manifest has no equivalent of legacy's -B override.
            environment["CS_IVL_ROOT"] = null;
            var template = RunnerManifestCommand.Make(context.Root, fixture, seed, adapter, Path.Combine(work, "unused"));
            template["iverilog"] = NativeBuildCommand.Executable(Environment.GetEnvironmentVariable("CS_IVERILOG") ?? "iverilog");
            template["vvp"] = NativeBuildCommand.Executable(Environment.GetEnvironmentVariable("CS_VVP") ?? "vvp");
            var protectedPaths = new List<string> { (string)template["assembly"], (string)template["nativeHost"], (string)adapter["bootstrap"], (string)adapter["library"], typeof(ToolContext).Assembly.Location };
            protectedPaths.AddRange((string[])template["sources"]);
            protectedPaths.Add(standalone[^1]);
            protectedPaths.Add((string)template["iverilog"]); protectedPaths.Add((string)template["vvp"]);
            var implementation = (string)adapter["implementation"];
            protectedPaths.Add(implementation[..implementation.LastIndexOf(':')]);
            protectedPaths.Add(NativeBuildCommand.Executable(ToolContext.Self[0])); protectedPaths.Add(NativeBuildCommand.Executable(standalone[0]));
            foreach (var assembly in new[] { (string)template["assembly"], typeof(ToolContext).Assembly.Location }.Concat(worker is null ? standalone.Skip(1) : []))
            {
                foreach (var suffix in new[] { ".runtimeconfig.json", ".deps.json" }) protectedPaths.Add(Path.ChangeExtension(assembly, suffix));
                protectedPaths.AddRange(Directory.EnumerateFiles(Path.GetDirectoryName(assembly)!, "*.dll"));
            }
            protectedPaths.Add(NativeBuildCommand.HostFxr(NativeBuildCommand.SdkRoot()));
            if (worker is not null) protectedPaths.Add(Path.Combine(worker.Directory, "worker-build.json"));
            if (fixture != "counter")
            {
                var plan = Path.Combine(work, "plan.json");
                StrictJson.Write(plan, fixture == "dsp" ? (object)WorkloadPlans.Dsp(128, seed) : WorkloadPlans.Cpu(16));
                environment[fixture.ToUpperInvariant() + "_PLAN"] = plan; environment[fixture.ToUpperInvariant() + "_FAULT"] = "0"; protectedPaths.Add(plan);
            }
            var before = Hashes(protectedPaths);
            var toolVersions = new[] { new[] { "dotnet", "--version" }, new[] { (string)template["iverilog"], "-V" }, new[] { (string)template["vvp"], "-V" } }.ToDictionary(command => command[0], command => { var result = context.Run(command, environment: environment, capture: true); return result.Output + result.Error; });
            for (int round = -warmups; round < repeats; round++)
            {
                var caseWork = Path.Combine(work, "round-" + (round < 0 ? "warm" + -round : round.ToString(CultureInfo.InvariantCulture)));
                Directory.CreateDirectory(caseWork);
                var manifest = new Dictionary<string, object>(template) { ["outputDirectory"] = Path.Combine(caseWork, "standalone") };
                var manifestPath = Path.Combine(caseWork, "manifest.json"); StrictJson.Write(manifestPath, manifest);
                int order = 0;
                foreach (var runner in Order(round))
                {
                    var folder = Path.Combine(caseWork, runner); Directory.CreateDirectory(folder);
                    var env = new Dictionary<string, string?>(environment);
                    if (fixture != "counter") env[fixture.ToUpperInvariant() + "_REPORT"] = Path.Combine(folder, "metrics.json");
                    var prefix = runner == "legacy" ? ToolContext.Self : standalone;
                    double help = Run(context, [.. prefix, "--help"], Path.Combine(folder, "help.log"), env);
                    string[] command = runner == "legacy" ? [.. prefix, "run", "--sim", "icarus", "--no-build", "--timings", "--assembly", (string)manifest["assembly"], "--source", ((string[])manifest["sources"])[0], "--top", definition.Top, "--entry-type", (string)manifest["entryType"], "--work-dir", folder, "--label", "legacy", "--seed", seed.ToString(CultureInfo.InvariantCulture)] : [.. prefix, "run", manifestPath];
                    double total = Run(context, command, Path.Combine(folder, "driver.log"), env);
                    double compile, simulate;
                    if (runner == "legacy")
                    {
                        using var timing = StrictJson.Read(Path.Combine(folder, "legacy-timings.json"));
                        if (timing.RootElement.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("Invalid legacy timing schema");
                        compile = Duration(timing.RootElement.GetProperty("compileMilliseconds").GetDouble()); simulate = Duration(timing.RootElement.GetProperty("simulationMilliseconds").GetDouble());
                    }
                    else
                    {
                        using var a = StrictJson.Read(Path.Combine(folder, "compile-process.json")); using var b = StrictJson.Read(Path.Combine(folder, "simulate-process.json"));
                        compile = Phase(a.RootElement, "compile"); simulate = Phase(b.RootElement, "simulate");
                    }
                    samples.Add(new(round, round < 0, order++, runner, help, total, compile, simulate, Remainder(total, compile, simulate)));
                    StrictJson.Write(Path.Combine(work, "samples.json"), samples);
                }
                using var left = StrictJson.Read(Path.Combine(caseWork, "legacy/legacy-results.json")); using var right = StrictJson.Read(Path.Combine(caseWork, "standalone/icarus-results.json"));
                Qualify(left.RootElement, right.RootElement, JsonSerializer.SerializeToElement(manifest["expectedNames"]), seed);
                if (fixture != "counter")
                {
                    using var a = StrictJson.Read(Path.Combine(caseWork, "legacy/metrics.json")); using var b = StrictJson.Read(Path.Combine(caseWork, "standalone/metrics.json"));
                    if (!JsonElement.DeepEquals(ParityContracts.Metrics(fixture, a.RootElement), ParityContracts.Metrics(fixture, b.RootElement))) throw new InvalidDataException("Benchmark functional traces differ");
                }
            }
            if (!before.OrderBy(pair => pair.Key).SequenceEqual(Hashes(protectedPaths).OrderBy(pair => pair.Key))) throw new InvalidDataException("Benchmark inputs changed");
            if (worker is not null) WorkerArtifact.Verify(worker);
            var summary = new[] { "legacy", "standalone" }.ToDictionary(runner => runner, runner => {
                var measured = samples.Where(sample => !sample.Warmup && sample.Runner == runner).ToArray();
                return new { helpRoundTrip = Distribution(measured.Select(row => row.HelpRoundTripMilliseconds)), total = Distribution(measured.Select(row => row.TotalMilliseconds)), compile = Distribution(measured.Select(row => row.CompileMilliseconds)), simulation = Distribution(measured.Select(row => row.SimulationMilliseconds)), other = Distribution(measured.Select(row => row.OtherMilliseconds)) };
            });
            var runtimeSettings = new[] { "DOTNET_TieredCompilation", "DOTNET_ReadyToRun", "DOTNET_TC_QuickJit", "COMPlus_TieredCompilation", "COMPlus_ReadyToRun", "CS_IVL_ROOT" }.ToDictionary(key => key, Environment.GetEnvironmentVariable);
            var report = new { schemaVersion = 1, fixture, seed, repeats, warmups, units = "milliseconds", scope = "Icarus process wall time; prebuilt managed fixture/native host; help round trip is a separate startup proxy", standaloneDeployment = worker is null ? "managed" : "native-aot-artifact", legacyCommand = ToolContext.Self, standaloneCommand = standalone, platform = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), controllerRuntime = RuntimeInformation.FrameworkDescription, runtimeSettings, childOverrides = environment, toolVersions, inputs = before, samples, summary };
            StrictJson.Write(Path.Combine(work, "summary.json"), report); Console.WriteLine(JsonSerializer.Serialize(report));
        }
        catch (Exception error) { File.Delete(Path.Combine(work, "summary.json")); StrictJson.Write(Path.Combine(work, "failure.json"), new { schemaVersion = 1, error = error.Message, kind = error.GetType().Name, samples }); throw; }
    }
}
