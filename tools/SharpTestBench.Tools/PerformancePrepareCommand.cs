using System.Text.Json;
namespace SharpTestBench.Tools;
internal static class PerformancePrepareCommand
{
    public static void Execute(ToolContext context, string[] arguments)
    {
        if (arguments.Length != 0) throw new ArgumentException("native-performance-prepare accepts no arguments");
        using var document = StrictJson.Read(context.At("third_party/UPSTREAM.json")); var metadata = document.RootElement;
        var archive = context.At("third_party/" + metadata.GetProperty("source_archive").GetString());
        if (CiCommands.Hash(archive) != metadata.GetProperty("source_sha256").GetString()) throw new InvalidDataException("Pinned source archive checksum mismatch");
        var work = context.At("build/native-performance/baseline"); var target = context.At("build/native-performance/baseline-python");
        if (Directory.Exists(target) || File.Exists(target)) throw new IOException("Baseline installation already exists; use it or select a fresh checkout");
        CiCommands.Extract(archive, work); var project = Path.Combine(work, "cocotb-source/pyproject.toml"); var text = File.ReadAllText(project).Replace("\r\n", "\n", StringComparison.Ordinal);
        const string dynamic = "dynamic = [\"version\"]", provider = "[tool.scikit-build.metadata.version]\nprovider = \"setuptools_git_versioning.scikit_metadata\"";
        if (text.Split(dynamic, StringSplitOptions.None).Length != 2 || text.Split(provider, StringSplitOptions.None).Length != 2) throw new InvalidDataException("Pinned upstream packaging changed; review historical preparation");
        File.WriteAllText(project, text.Replace(dynamic, "version = " + JsonSerializer.Serialize(metadata.GetProperty("version").GetString()), StringComparison.Ordinal).Replace(provider, "", StringComparison.Ordinal));
        context.Run([Environment.GetEnvironmentVariable("CS_PYTHON") ?? "python", "-m", "pip", "install", "--no-deps", "--target", target, Path.GetDirectoryName(project)!], environment: new Dictionary<string, string?> { ["CC"] = Environment.GetEnvironmentVariable("CC") ?? "gcc", ["CXX"] = Environment.GetEnvironmentVariable("CXX") ?? "g++", ["CMAKE_BUILD_PARALLEL_LEVEL"] = Environment.GetEnvironmentVariable("CMAKE_BUILD_PARALLEL_LEVEL") ?? "4" }); Console.WriteLine(target);
    }
}
internal static class PackedBenchmarkCommand
{
    public static double Median(IEnumerable<double> values)
    {
        var array = values.Order().ToArray(); if (array.Length == 0 || array.Any(value => !double.IsFinite(value))) throw new InvalidDataException("Invalid benchmark sample set");
        return array.Length % 2 == 0 ? array[array.Length / 2 - 1] / 2 + array[array.Length / 2] / 2 : array[array.Length / 2];
    }
    public static void Execute(ToolContext context, string[] arguments)
    {
        if (arguments.Length != 0) throw new ArgumentException("packed-benchmark accepts no arguments");
        var rows = new List<Dictionary<string, object>>(); Directory.CreateDirectory(context.At("build")); File.Delete(context.At("build/packed-benchmark.json"));
        for (int trial = 0; trial < 3; trial++)
        {
            var label = "v05-packed-bench-" + trial; var result = context.Run(["dotnet", typeof(ToolContext).Assembly.Location, "run", "--project", "Benchmark", "--filter", "PackedVectorBenchmark", "--label", label, .. (trial == 0 ? Array.Empty<string>() : ["--no-build"])], environment: new Dictionary<string, string?> { ["CS_PACKED_REVERSE"] = (trial % 2).ToString(System.Globalization.CultureInfo.InvariantCulture) }, capture: true, check: false);
            File.WriteAllText(context.At("build/" + label + "-driver.log"), result.Output + result.Error); if (result.ExitCode != 0) throw new InvalidDataException("Packed benchmark process failed");
            var lines = result.Output.Split('\n').Where(line => line.StartsWith("PACKED_MEASURE ", StringComparison.Ordinal)).ToArray(); if (lines.Length != 4) throw new InvalidDataException("Expected four benchmark measurements");
            foreach (var line in lines)
            {
                using var item = StrictJson.Parse(line[15..]); var root = item.RootElement;
                rows.Add(new() { ["trial"] = trial, ["width"] = root.GetProperty("width").GetInt32(), ["mode"] = root.GetProperty("mode").GetString()!, ["seconds"] = root.GetProperty("seconds").GetDouble(), ["managedBytes"] = root.GetProperty("managedBytes").GetDouble(), ["count"] = root.GetProperty("count").GetInt32() });
            }
        }
        var summary = new List<object>();
        foreach (var width in new[] { 256, 4096 })
        {
            var times = new Dictionary<string, double>(); var allocations = new Dictionary<string, double>();
            foreach (var mode in new[] { "string", "packed" })
            {
                var samples = rows.Where(row => (int)row["width"] == width && (string)row["mode"] == mode).ToArray();
                if (samples.Length != 3 || samples.Any(row => (double)row["seconds"] <= 0 || (int)row["count"] <= 0 || (double)row["managedBytes"] < 0)) throw new InvalidDataException("Incomplete or invalid packed benchmark samples");
                times[mode] = Median(samples.Select(row => (double)row["seconds"])); allocations[mode] = Median(samples.Select(row => (double)row["managedBytes"] / (int)row["count"]));
            }
            summary.Add(new { width, medianSeconds = times, managedBytesPerRoundTrip = allocations, speedRatio = times["string"] / times["packed"] });
        }
        StrictJson.Write(context.At("build/packed-benchmark.json"), new { description = "500 immediate known-value write/read round trips; 100 warmups per mode; 3 fresh Icarus processes; alternate mode order. Native allocation excluded from managed byte counts. Not a Python or full-project benchmark.", measurements = rows, summary }); Console.WriteLine(JsonSerializer.Serialize(summary));
    }
}
