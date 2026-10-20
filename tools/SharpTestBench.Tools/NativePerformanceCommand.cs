using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace SharpTestBench.Tools;
internal sealed record PerformanceSample(int round, string variant, Dictionary<string, double> metrics, Dictionary<string, double> managed_allocated_bytes);
internal static class NativePerformanceCommand
{
    const string Baseline = "2ff0ac644602197f64fb7839d52ff9d848d75387";
    public static object Comparison(IReadOnlyList<PerformanceSample> records, string variant, string metric, string baselineVariant = "baseline")
    {
        var left = records.Where(row => row.variant == baselineVariant).ToArray(); var right = records.Where(row => row.variant == variant).ToArray();
        if (left.Length == 0 || left.Length != right.Length || !left.Select(row => row.round).SequenceEqual(right.Select(row => row.round))) throw new InvalidDataException("Performance rounds are not paired");
        var a = left.Select(row => row.metrics[metric]).ToArray(); var b = right.Select(row => row.metrics[metric]).ToArray();
        if (a.Concat(b).Any(value => !double.IsFinite(value) || value <= 0)) throw new InvalidDataException("Performance samples must be positive and finite");
        var random = new PythonRandom(20261016); var ratios = new double[10000];
        for (var iteration = 0; iteration < ratios.Length; iteration++) { var indices = Enumerable.Range(0, a.Length).Select(_ => random.Below(a.Length)).ToArray(); ratios[iteration] = PackedBenchmarkCommand.Median(indices.Select(index => a[index])) / PackedBenchmarkCommand.Median(indices.Select(index => b[index])); }
        Array.Sort(ratios); var medianA = PackedBenchmarkCommand.Median(a); var medianB = PackedBenchmarkCommand.Median(b);
        return new { baseline_median_seconds = medianA, candidate_median_seconds = medianB, speedup = medianA / medianB, paired_bootstrap_95 = new[] { ratios[250], ratios[9749] }, baseline_range = new[] { a.Min(), a.Max() }, candidate_range = new[] { b.Min(), b.Max() } };
    }
    static string HistoricalHost(ToolContext context, string old)
    {
        foreach (var name in new[] { "native/abi.c", "native/abi.h", "native/host.c", "native/platform.h", "native/upstream/gpi.h", "native/upstream/exports.h", "native/upstream/vpi_user.h", "native/upstream/vpi_compatibility.h", "tools/native_build.py" })
        {
            var path = Path.Combine(old, name); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, context.Run(["git", "show", Baseline + ":" + name], capture: true).Output);
        }
        var sdk = NativeBuildCommand.SdkRoot(); var rid = NativeBuildCommand.Rid;
        var headers = NativeBuildCommand.HighestVersion(Directory.EnumerateDirectories(Path.Combine(sdk, "packs/Microsoft.NETCore.App.Host." + rid)), Path.GetFileName);
        headers = Path.Combine(headers, "runtimes", rid, "native"); var host = Path.Combine(old, NativeBuildCommand.HostName);
        // Preserve the historical C compiler flags; do not execute the historical Python helper.
        context.Run([Environment.GetEnvironmentVariable("CC") ?? "gcc", "-shared", "-fPIC", "-Wl,-soname," + NativeBuildCommand.HostName, "-Wall", "-Wextra", "-Werror", Path.Combine(old, "native/host.c"), Path.Combine(old, "native/abi.c"), "-I", Path.Combine(old, "native/upstream"), "-I", headers, "-ldl", "-o", host]); return host;
    }
    public static void Execute(ToolContext context, string[] arguments)
    {
        var options = new Options(arguments, "--ablation"); options.Only("--ablation", "--sim", "--baseline-revision", "--baseline-python", "--baseline-host", "--baseline-managed", "--repeat", "--transactions", "--packed-count", "--output");
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Historical host reconstruction targets Linux");
        bool ablation = options.Has("--ablation"); var simulator = options.Get("--sim", "icarus")!; if (simulator is not ("icarus" or "verilator")) throw new ArgumentException("Unknown performance simulator");
        int repeat = int.Parse(options.Get("--repeat", "9")!, CultureInfo.InvariantCulture), transactions = int.Parse(options.Get("--transactions", "2048")!, CultureInfo.InvariantCulture), packedCount = int.Parse(options.Get("--packed-count", "1000")!, CultureInfo.InvariantCulture);
        if (repeat < 5 || transactions < 16 || packedCount < 500) throw new ArgumentException("Use at least 5 repeats, 16 transactions, and 500 packed round trips");
        var baselinePython = Path.GetFullPath(options.Require("--baseline-python")); if (!File.Exists(Path.Combine(baselinePython, "cocotb_tools/config.py"))) throw new FileNotFoundException("Install historical adapter build into --baseline-python first");
        var baselineManaged = options.Get("--baseline-managed") is string managed ? Path.GetFullPath(managed) : null; var preserved = options.Get("--baseline-host") is string selected ? Path.GetFullPath(selected) : null;
        if (ablation && (baselineManaged is not null || preserved is not null)) throw new ArgumentException("--ablation uses current binaries; omit preserved baseline artifacts");
        var output = Path.GetFullPath(options.Get("--output", context.At("build/native-performance"))!); Directory.CreateDirectory(output); File.Delete(Path.Combine(output, "summary.json"));
        var old = Path.Combine(output, "baseline-host"); Directory.CreateDirectory(old); var newDirectory = Path.Combine(output, "optimized-host"); Directory.CreateDirectory(newDirectory);
        var historical = preserved ?? (ablation ? null : HistoricalHost(context, old)); var optimized = NativeBuildCommand.Build(context, directory: newDirectory);
        var variants = new Dictionary<string, (string Host, string? Python)> { ["baseline"] = (historical ?? optimized, baselinePython), ["bridge_only"] = (optimized, baselinePython), ["full"] = (optimized, null) };
        var features = new Dictionary<string, Dictionary<string, string?>>();
        if (ablation)
        {
            variants = new[] { "baseline", "direct", "batch", "clock", "text", "full" }.ToDictionary(name => name, _ => (Host: optimized, Python: (string?)null));
            foreach (var name in variants.Keys) { var values = new Dictionary<string, string?>(); foreach (var (key, feature) in new[] { ("STB_DIRECT_PACKED", "direct"), ("STB_BATCH_TRANSFERS", "batch"), ("STB_NATIVE_CLOCK", "clock"), ("STB_LINEAR_TEXT", "text") }) values[key] = name == feature || name == "full" ? "1" : "0"; values["AXI_BATCH"] = name is "batch" or "full" ? "1" : "0"; values["AXI_CLOCK_MODE"] = name is "clock" or "full" ? "native" : "manual"; features[name] = values; }
        }
        var models = new Dictionary<string, string>();
        if (simulator == "verilator") { models["micro"] = VerilatorBuildCommand.Build(context, [context.At("tests/Integration/top.sv")], "top", Path.Combine(output, "model-micro"), false); models["axi"] = VerilatorBuildCommand.Build(context, [context.At("examples/AxiMemory/axi_memory.sv")], "axi_memory", Path.Combine(output, "model-axi"), false); }
        using var ownProcess = Process.GetCurrentProcess(); var affinity = unchecked((ulong)(long)ownProcess.ProcessorAffinity); if (affinity == 0) throw new InvalidDataException("No available benchmark CPU"); var pinned = affinity & unchecked(0UL - affinity); ownProcess.ProcessorAffinity = unchecked((nint)(long)pinned); var cpu = System.Numerics.BitOperations.TrailingZeroCount(pinned);
        var planFile = Path.Combine(output, "plan.json"); StrictJson.Write(planFile, WorkloadPlans.Axi(transactions, 20261014)); JsonElement? expectedTrace = null;
        PerformanceSample Run(string variant, int round)
        {
            var selected = variants[variant]; var environment = features.TryGetValue(variant, out var switches) ? new Dictionary<string, string?>(switches) : new(); environment["DOTNET_TieredCompilation"] = "0"; environment["CS_PACKED_COUNT"] = packedCount.ToString(CultureInfo.InvariantCulture); environment["CS_BENCH_CLOCKS"] = ablation ? "1" : "0"; environment["CS_PACKED_REVERSE"] = round % 2 != 0 ? "1" : "0"; environment["PYTHONPATH"] = selected.Python;
            var metrics = new Dictionary<string, double>(); var allocations = new Dictionary<string, double>();
            foreach (var workload in new[] { "micro", "axi" })
            {
                var work = Path.Combine(output, round + "-" + variant + "-" + workload); Directory.CreateDirectory(work);
                var invocation = new List<string> { "dotnet", typeof(ToolContext).Assembly.Location, "run", "--sim", simulator, "--no-build", "--host", selected.Host, "--work-dir", work, "--label", "perf" };
                if (models.TryGetValue(workload, out var model)) invocation.AddRange(["--sim-executable", model]);
                var baselineAssembly = baselineManaged is not null && variant != "full" ? baselineManaged : null;
                if (workload == "micro") { invocation.AddRange(["--project", "Benchmark"]); if (baselineAssembly is not null) invocation.AddRange(["--assembly", Path.Combine(baselineAssembly, "Benchmark/Benchmark.dll")]); }
                else
                {
                    invocation.AddRange(["--assembly", baselineAssembly is null ? context.At("examples/AxiMemory/bin/Release/net10.0/AxiMemory.dll") : Path.Combine(baselineAssembly, "AxiMemory/AxiMemory.dll"), "--source", context.At("examples/AxiMemory/axi_memory.sv"), "--top", "axi_memory", "--entry-type", "Entry, AxiMemory"]);
                    environment["AXI_PLAN"] = planFile; environment["AXI_REPORT"] = Path.Combine(work, "metrics.json"); environment["AXI_FAULT"] = "0"; File.Delete(environment["AXI_REPORT"]!);
                }
                var timer = Stopwatch.StartNew(); var result = context.Run(invocation.ToArray(), environment: environment, capture: true, check: false, timeout: TimeSpan.FromSeconds(300)); metrics[workload + "_wall"] = timer.Elapsed.TotalSeconds; var text = result.Output + result.Error; File.WriteAllText(Path.Combine(work, "driver.log"), text); if (result.ExitCode != 0) throw new InvalidDataException("Performance process failed: " + work + "\n" + text);
                if (workload == "micro")
                {
                    foreach (Match match in Regex.Matches(result.Output, @"^MEASURE (timer|rw32) ([0-9.eE+-]+)", RegexOptions.Multiline)) metrics[match.Groups[1].Value] = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                    foreach (var line in result.Output.Split('\n').Where(line => line.StartsWith("PACKED_MEASURE ", StringComparison.Ordinal))) { using var document = StrictJson.Parse(line[15..]); var data = document.RootElement; if (data.GetProperty("count").GetInt32() != packedCount) throw new InvalidDataException("Packed workload count mismatch"); var key = data.GetProperty("mode").GetString() + "_" + data.GetProperty("width").GetInt32(); metrics[key] = data.GetProperty("seconds").GetDouble(); allocations[key] = data.GetProperty("managedBytes").GetDouble(); }
                    if (metrics.Count != 7) throw new InvalidDataException("Missing benchmark metrics");
                    if (ablation) { foreach (var line in result.Output.Split('\n').Where(line => line.StartsWith("CLOCK_MEASURE ", StringComparison.Ordinal))) { using var document = StrictJson.Parse(line[14..]); var data = document.RootElement; if (data.GetProperty("cycles").GetInt32() != 10000) throw new InvalidDataException("Clock workload count mismatch"); var key = "clock_" + data.GetProperty("mode").GetString(); metrics[key] = data.GetProperty("seconds").GetDouble(); allocations[key] = data.GetProperty("managedBytes").GetDouble(); } if (metrics.Count != 9) throw new InvalidDataException("Missing clock metrics"); }
                }
                else { using var document = StrictJson.Read(Path.Combine(work, "metrics.json")); var data = document.RootElement; var trace = JsonSerializer.SerializeToElement(new[] { "trace", "cycles", "write_stalls", "read_stalls" }.ToDictionary(key => key, data.GetProperty)); if (expectedTrace is null) expectedTrace = trace.Clone(); else if (!JsonElement.DeepEquals(expectedTrace.Value, trace)) throw new InvalidDataException("AXI correctness/trace mismatch"); metrics["axi_body"] = data.GetProperty("seconds").GetDouble(); }
            }
            if (metrics.Values.Any(value => !double.IsFinite(value) || value <= 0)) throw new InvalidDataException("Invalid performance metric"); var sample = new PerformanceSample(round, variant, metrics, allocations); Console.WriteLine(JsonSerializer.Serialize(sample)); return sample;
        }
        foreach (var variant in variants.Keys) Run(variant, -1); var records = new List<PerformanceSample>(); var names = variants.Keys.ToArray();
        for (int round = 0; round < repeat; round++) { var offset = round % names.Length; foreach (var variant in names.Skip(offset).Concat(names.Take(offset))) records.Add(Run(variant, round)); StrictJson.Write(Path.Combine(output, "samples.json"), records); }
        var adapter = CocotbAdapter.Inspect(context); using var upstream = StrictJson.Read(context.At("third_party/UPSTREAM.json")); using var optimizedBuild = StrictJson.Read(Path.Combine(newDirectory, "native-build.json"));
        var libraries = new Dictionary<string, Dictionary<string, string>> { ["baseline"] = Directory.EnumerateFiles(Path.Combine(baselinePython, "cocotb/libs"), "*.so").ToDictionary(path => Path.GetFileName(path), CiCommands.Hash), ["full"] = Directory.EnumerateFiles(adapter.LibsDir, "*.so").ToDictionary(path => Path.GetFileName(path), CiCommands.Hash) };
        var summary = new Dictionary<string, object?> { ["schema"] = 1, ["baseline_revision"] = ablation ? "current code with all four features disabled" : options.Get("--baseline-revision", preserved is null ? Baseline : "preserved binary; see host hashes"), ["baseline_managed_sha256"] = baselineManaged is null ? null : Directory.EnumerateFiles(baselineManaged, "*.dll", SearchOption.AllDirectories).ToDictionary(path => Path.GetRelativePath(baselineManaged, path), CiCommands.Hash), ["candidate_revision"] = context.Run(["git", "rev-parse", "HEAD"], capture: true).Output.Trim(), ["working_tree_dirty"] = context.Run(["git", "status", "--porcelain"], capture: true).Output.Length != 0, ["native_upstream"] = upstream.RootElement.Clone(), ["optimized_host_build"] = optimizedBuild.RootElement.Clone(), ["benchmark_source_sha256"] = CiCommands.Hash(context.At("tests/Benchmark/Bench.cs")), ["repeat"] = repeat, ["packed_count"] = packedCount, ["transactions"] = transactions, ["environment"] = new { platform = RuntimeInformation.OSDescription, cpu_affinity = new[] { cpu }, cpu_model = File.ReadLines("/proc/cpuinfo").FirstOrDefault(line => line.StartsWith("model name", StringComparison.Ordinal)) ?? "", dotnet = context.Run(["dotnet", "--version"], capture: true).Output.Trim(), simulator = simulator == "verilator" ? context.Run(["verilator", "--version"], capture: true).Output.Split('\n') : context.Run([Environment.GetEnvironmentVariable("CS_VVP") ?? "vvp", "-V"], capture: true, check: false).Error.Split('\n').Take(2).ToArray(), tiered_compilation = false }, ["model_sha256"] = models.ToDictionary(pair => pair.Key, pair => CiCommands.Hash(pair.Value)), ["model_builds"] = models.ToDictionary(pair => pair.Key, pair => { using var document = StrictJson.Read(Path.Combine(Path.GetDirectoryName(pair.Value)!, "model-build.json")); return document.RootElement.Clone(); }), ["harness_sha256"] = new[] { "NativePerformanceCommand.cs", "LegacyRunCommand.cs", "VerilatorBuildCommand.cs" }.ToDictionary(name => name, name => CiCommands.Hash(context.At("tools/SharpTestBench.Tools/" + name))), ["hdl_sha256"] = new[] { "tests/Integration/top.sv", "examples/AxiMemory/axi_memory.sv" }.ToDictionary(name => name, name => CiCommands.Hash(context.At(name))), ["host_sha256"] = variants.ToDictionary(pair => pair.Key, pair => CiCommands.Hash(pair.Value.Host)), ["adapter_sha256"] = libraries, ["plan_sha256"] = CiCommands.Hash(planFile), ["axi_trace_sha256"] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(expectedTrace!.Value.GetRawText()))), ["trace_hash_format"] = "System.Text.Json ordered contract UTF-8", ["managed_sha256"] = new[] { "tests/Benchmark/bin/Release/net10.0/Benchmark.dll", "examples/AxiMemory/bin/Release/net10.0/AxiMemory.dll", "src/SharpTestBench/bin/Release/net10.0/SharpTestBench.dll" }.ToDictionary(name => name, name => CiCommands.Hash(context.At(name))), ["feature_sets"] = features, ["records"] = records, ["comparisons"] = variants.Keys.Where(name => name != "baseline").ToDictionary(name => name, name => records[0].metrics.Keys.ToDictionary(metric => metric, metric => Comparison(records, name, metric))), [baselineManaged is null ? "adapter_increment" : "managed_increment"] = ablation ? null : records[0].metrics.Keys.ToDictionary(metric => metric, metric => Comparison(records, "full", metric, "bridge_only")), ["scope"] = (ablation ? "Feature ablation uses identical current binaries with per-process switches. " : baselineManaged is null ? "Same managed/HDL code. " : "Preserved managed baseline and bridge_only; full uses current assemblies. ") + simulator + ", Linux; fresh processes; one warmup/configuration; interleaved paired rounds. Wall includes startup and HDL compilation except shared precompiled models. Body metrics exclude startup/build. Bootstrap intervals describe these samples, not machine-to-machine guarantees. Synthetic packed microbenchmarks and single-outstanding AXI4-Lite-shaped traffic." };
        StrictJson.Write(Path.Combine(output, "summary.json"), summary); Console.WriteLine("NATIVE_PERFORMANCE_COMPLETE " + Path.Combine(output, "summary.json"));
    }
}
