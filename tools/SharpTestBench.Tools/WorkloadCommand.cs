using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
namespace SharpTestBench.Tools;
internal static class WorkloadCommand
{
    public static void Execute(ToolContext context, string command, string[] arguments)
    {
        var kind = command.Split('-')[0]; var options = new Options(arguments, "--skip-negative");
        options.Only(kind == "axi" ? ["--transactions", "--seed", "--repeat", "--skip-negative"] : kind == "dsp" ? ["--cycles", "--seed", "--repeat"] : ["--iterations", "--repeat"]);
        var count = int.Parse(options.Get(kind == "axi" ? "--transactions" : kind == "dsp" ? "--cycles" : "--iterations", kind == "axi" ? "128" : kind == "dsp" ? "1024" : "256")!, CultureInfo.InvariantCulture);
        var repeat = int.Parse(options.Get("--repeat", "3")!, CultureInfo.InvariantCulture); var seed = ulong.Parse(options.Get("--seed", kind == "axi" ? "20261014" : "20261016")!, CultureInfo.InvariantCulture);
        if (repeat < 1 || (kind == "axi" ? count < 16 : kind == "dsp" ? count < 64 : count < 2 || count > 32767)) throw new ArgumentException("Invalid workload count or repeat");
        var definition = kind switch { "axi" => (Project: "AxiMemory", Top: "axi_memory"), "dsp" => (Project: "DspFir", Top: "dsp_fir"), "cpu" => (Project: "CpuCore", Top: "cpu_core"), _ => throw new ArgumentException("Unknown workload") };
        var work = context.At("build/" + kind + "-workload"); Directory.CreateDirectory(work); File.Delete(Path.Combine(work, "summary.json"));
        var inputs = Path.Combine(work, "plan.json"); object plan = kind switch { "axi" => WorkloadPlans.Axi(count, seed), "dsp" => WorkloadPlans.Dsp(count, seed), _ => WorkloadPlans.Cpu(count) }; StrictJson.Write(inputs, plan);
        if (!File.Exists(ReferenceCommand.OraclePython(context))) throw new FileNotFoundException("Provision pinned oracle using structured-reference-env");
        JsonElement? baseline = null;
        Dictionary<string, object> Run(string language, string label, int fault = 0, bool profile = false)
        {
            var folder = Path.Combine(work, label); Directory.CreateDirectory(folder); var report = Path.Combine(folder, "metrics.json"); File.Delete(report);
            var prefix = kind.ToUpperInvariant(); var environment = new Dictionary<string, string?> { [prefix + "_PLAN"] = inputs, [prefix + "_REPORT"] = report, [prefix + "_FAULT"] = fault.ToString(CultureInfo.InvariantCulture) }; if (kind == "axi") environment["AXI_PROFILE"] = profile ? "1" : "0";
            string[] invocation = language == "csharp" ? [.. ToolContext.Self, "run", "--no-build", "--assembly", context.At("examples/" + definition.Project + "/bin/Release/net10.0/" + definition.Project + ".dll"), "--source", context.At("examples/" + definition.Project + "/" + definition.Top + ".sv"), "--top", definition.Top, "--entry-type", "Entry, " + definition.Project, "--work-dir", folder, "--label", kind] : [.. ToolContext.Self, kind + "-python", "--work-dir", folder];
            var timer = Stopwatch.StartNew(); var result = context.Run(invocation, environment: environment, capture: true, check: false, timeout: TimeSpan.FromSeconds(180)); var wall = timer.Elapsed.TotalSeconds; var output = result.Output + result.Error; File.WriteAllText(Path.Combine(folder, "driver.log"), output);
            if (fault != 0)
            {
                var marker = kind == "axi" ? fault == 3 ? "AXI_TIMEOUT" : "AXI_DATA" : prefix + "_MISMATCH";
                if (result.ExitCode == 0 || !output.Contains(marker, StringComparison.Ordinal)) throw new InvalidDataException(language + " missed fault " + fault);
                return new() { ["language"] = language, ["fault"] = fault, ["detected"] = true };
            }
            if (result.ExitCode != 0 || !File.Exists(report)) throw new InvalidDataException("Workload simulator failed: " + label + "\n" + output);
            using var document = StrictJson.Read(report); var data = document.RootElement;
            var contract = kind == "axi" ? JsonSerializer.SerializeToElement(new[] { "trace", "cycles", "write_stalls", "read_stalls" }.ToDictionary(key => key, data.GetProperty)) : ParityContracts.Metrics(kind, data);
            if (baseline is null) baseline = contract.Clone(); else if (!JsonElement.DeepEquals(baseline.Value, contract)) throw new InvalidDataException("Full workload cycle trace differs: " + language);
            if (kind == "cpu" && data.GetProperty("commits").GetInt32() != 9 * count + 6) throw new InvalidDataException("Unexpected instruction count");
            var record = data.EnumerateObject().Where(item => item.Name != "trace").ToDictionary(item => item.Name, item => (object)item.Value.Clone()); record["language"] = language; record["wall_seconds"] = wall; if (kind == "axi") record["label"] = label; return record;
        }
        foreach (var language in new[] { "csharp", "python" }) Run(language, language + "-warmup");
        var records = new List<Dictionary<string, object>>();
        for (var iteration = 0; iteration < repeat; iteration++) foreach (var language in iteration % 2 == 0 ? new[] { "csharp", "python" } : ["python", "csharp"]) records.Add(Run(language, language + "-" + iteration));
        var profileRecord = kind == "axi" ? Run("python", "python-allocation", profile: true) : null;
        var negatives = new List<Dictionary<string, object>>(); if (!options.Has("--skip-negative")) for (int fault = 1; fault <= 3; fault++) foreach (var language in new[] { "csharp", "python" }) negatives.Add(Run(language, language + "-fault" + fault, fault));
        double Number(object value) => value is JsonElement element ? element.GetDouble() : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        var medians = new[] { "csharp", "python" }.ToDictionary(language => language, language => new[] { "seconds", "wall_seconds" }.ToDictionary(key => key, key => PackedBenchmarkCommand.Median(records.Where(row => (string)row["language"] == language).Select(row => Number(row[key])))));
        var revision = context.Run(["git", "rev-parse", "HEAD"], capture: true).Output.Trim(); bool dirty = context.Run(["git", "status", "--porcelain"], capture: true).Output.Length != 0;
        var simulator = context.Run([Environment.GetEnvironmentVariable("CS_VVP") ?? "vvp", "-V"], capture: true, check: false).Error.Split('\n').Take(2).ToArray();
        using var upstream = StrictJson.Read(context.At("third_party/UPSTREAM.json")); using var nativeManifest = StrictJson.Read(context.At("build/native-build.json"));
        var summary = new Dictionary<string, object?> { ["schema"] = 1, ["seed"] = seed, ["iterations"] = kind == "cpu" ? count : null, ["commands"] = kind == "axi" ? ((Array)plan).Length : null, ["plan_sha256"] = CiCommands.Hash(inputs), ["trace_sha256"] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(baseline!.Value.GetRawText()))), ["trace_hash_format"] = "System.Text.Json ordered contract UTF-8", ["contract"] = baseline.Value.EnumerateObject().Where(item => item.Name != "trace").ToDictionary(item => item.Name, item => item.Value.Clone()), ["records"] = records, ["negative_tests"] = negatives, ["medians"] = medians, ["python_allocation_profile"] = profileRecord, ["environment"] = new { platform = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), cocotb = "2.1.0", revision, dirty, simulator, native_manifest = nativeManifest.RootElement.Clone(), native_upstream = upstream.RootElement.Clone(), assembly_sha256 = CiCommands.Hash(context.At("examples/" + definition.Project + "/bin/Release/net10.0/" + definition.Project + ".dll")) }, ["source_hashes"] = new[] { "examples/" + definition.Project + "/" + definition.Top + ".sv", "examples/" + definition.Project + "/Tests.cs", "examples/" + definition.Project + "/test_" + kind + ".py", "tools/SharpTestBench.Tools/WorkloadCommand.cs", "tools/SharpTestBench.Tools/WorkloadPlans.cs", "tools/SharpTestBench.Tools/ReferenceCommand.cs" }.ToDictionary(path => path, path => CiCommands.Hash(context.At(path))), ["methodology"] = "One unmeasured fresh-process warmup per language, alternating measured processes. Wall includes HDL compilation and startup; body includes bus driving, scoreboard and trace collection. C# cumulative allocations and separately-profiled Python peak memory are different metrics. Icarus fixture only; no general throughput claim." };
        StrictJson.Write(Path.Combine(work, "summary.json"), summary); Console.WriteLine(kind.ToUpperInvariant() + "_WORKLOAD_PASS " + JsonSerializer.Serialize(medians) + " " + negatives.Count + " fault checks");
    }
}
