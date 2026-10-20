using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace SharpTestBench.Tools;
internal static class BenchmarkCommand
{
    public static Dictionary<string, double> Parse(string text)
    {
        var checksums = Regex.Matches(text, @"(?<!\S)checksum=([^\s]+)");
        if (checksums.Count != 1 || checksums[0].Groups[1].Value != "694140833") throw new InvalidDataException("Benchmark checksum differs or is ambiguous");
        var metrics = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var raw in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (!line.StartsWith("MEASURE", StringComparison.Ordinal)) continue;
            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length is < 3 or > 4 || tokens[0] != "MEASURE" || tokens[1] is not ("timer" or "rw32") || tokens.Length == 4 && tokens[3] != "checksum=694140833" ||
                !double.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || value <= 0 || !metrics.TryAdd(tokens[1], value))
                throw new InvalidDataException("Benchmark measurement is invalid or duplicated");
        }
        if (metrics.Count != 2) throw new InvalidDataException("Benchmark measurements missing");
        return metrics;
    }
    public static void Execute(ToolContext context, string[] arguments)
    {
        if (arguments.Length != 0) throw new ArgumentException("benchmark accepts no arguments");
        var environment = new Dictionary<string, string?> { ["DOTNET_TieredCompilation"] = "0" };
        context.Run(["dotnet", typeof(ToolContext).Assembly.Location, "run", "--project", "Benchmark", "--label", "benchmark-warm"], environment: environment);
        var adapter = CocotbAdapter.Inspect(context); var build = context.At("build"); File.Delete(Path.Combine(build, "benchmark.json")); var records = new List<Dictionary<string, object>>();
        for (var repeat = 0; repeat < 5; repeat++) foreach (var language in repeat % 2 == 0 ? new[] { "python", "csharp" } : ["csharp", "python"])
        {
            CommandResult result; string text;
            if (language == "csharp") { var label = "benchmark-csharp-" + repeat; result = context.Run(["dotnet", typeof(ToolContext).Assembly.Location, "run", "--project", "Benchmark", "--no-build", "--label", label], environment: environment, capture: true, check: false); text = File.ReadAllText(Path.Combine(build, label + "-run.log")); }
            else
            {
                var report = Path.Combine(build, "benchmark-python-" + repeat + ".xml"); File.Delete(report);
                var pythonEnvironment = new Dictionary<string, string?>(environment) { ["GPI_IMPL"] = adapter.Implementation, ["PYTHONPATH"] = context.At("tests/Benchmark"), ["PYGPI_PYTHON_BIN"] = adapter.Python, ["COCOTB_TOPLEVEL"] = "top", ["COCOTB_TEST_MODULES"] = "python_bench", ["COCOTB_RESULTS_FILE"] = report, ["COCOTB_TRUST_INERTIAL_WRITES"] = null, ["COCOTB_BOOTSTRAP"] = string.Join(Path.PathSeparator, adapter.GpiEntryPoint, adapter.LibPython, adapter.PyGpiEntryPoint) };
                result = context.Run([Environment.GetEnvironmentVariable("CS_VVP") ?? "vvp", "-m", adapter.Bootstrap, Path.Combine(build, "top.vvp")], directory: build, environment: pythonEnvironment, capture: true, check: false); text = result.Output + result.Error; File.WriteAllText(Path.Combine(build, "benchmark-python-" + repeat + ".log"), text); ReferenceCommand.ValidateReport(report, false);
            }
            if (result.ExitCode != 0) throw new InvalidDataException("Benchmark process failed");
            var row = Parse(text).ToDictionary(pair => pair.Key, pair => (object)pair.Value); row["language"] = language; row["repeat"] = repeat; records.Add(row);
        }
        var summary = new Dictionary<string, Dictionary<string, double>>();
        foreach (var metric in new[] { "timer", "rw32" }) { var row = new[] { "python", "csharp" }.ToDictionary(language => language, language => PackedBenchmarkCommand.Median(records.Where(sample => (string)sample["language"] == language).Select(sample => (double)sample[metric]))); row["python_over_csharp"] = row["python"] / row["csharp"]; summary[metric] = row; }
        StrictJson.Write(Path.Combine(build, "benchmark.json"), new { records, summary }); Console.WriteLine(JsonSerializer.Serialize(summary));
    }
}
