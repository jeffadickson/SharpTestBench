using System.Text.Json;

namespace SharpTestBench.Tools;

internal sealed record CheckedBenchmark(string fixture, string deployment, int measuredSamples, int warmupSamples);
internal static class RunnerBenchmarkReport
{
    public static void Execute(string[] arguments)
    {
        var options = new Options(arguments); options.Only("--report");
        using var report = StrictJson.Read(Path.GetFullPath(options.Require("--report")));
        Console.WriteLine(JsonSerializer.Serialize(Validate(report.RootElement)));
    }
    public static CheckedBenchmark Validate(JsonElement root)
    {
        try { return Read(root); }
        catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { throw new InvalidDataException("Malformed benchmark report field", error); }
    }
    static CheckedBenchmark Read(JsonElement root)
    {
        if (root.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("Unsupported benchmark report schema");
        int repeats = root.GetProperty("repeats").GetInt32();
        if (repeats is < 3 or > 30) throw new InvalidDataException("Invalid benchmark repeats");
        int warmups = root.GetProperty("warmups").GetInt32();
        if (warmups is < 1 or > 5) throw new InvalidDataException("Invalid benchmark warmups");
        var values = root.GetProperty("samples");
        if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() != 2 * (repeats + warmups)) throw new InvalidDataException("Incomplete benchmark sample inventory");
        string fixture = root.GetProperty("fixture").GetString() ?? "", deployment = root.GetProperty("standaloneDeployment").GetString() ?? "";
        if (!RunnerManifestCommand.Fixtures.ContainsKey(fixture) || deployment is not ("managed" or "native-aot-artifact") || root.GetProperty("units").GetString() != "milliseconds") throw new InvalidDataException("Invalid benchmark fixture, deployment or units");
        FixtureContracts.Integer(root.GetProperty("seed"), 0, ulong.MaxValue, "benchmark seed");
        var samples = values.EnumerateArray().Select(ReadSample).ToArray();
        foreach (var sample in samples)
            foreach (double duration in new[] { sample.HelpRoundTripMilliseconds, sample.TotalMilliseconds, sample.CompileMilliseconds, sample.SimulationMilliseconds, sample.OtherMilliseconds }) RunnerBenchmarkCommand.Duration(duration);
        for (int index = 0; index < samples.Length; index++)
        {
            int round = index / 2 - warmups, order = index % 2;
            var sample = samples[index];
            if (sample.Round != round || sample.Order != order || sample.Warmup != (round < 0) || sample.Runner != RunnerBenchmarkCommand.Order(round)[order]) throw new InvalidDataException("Benchmark sample order, identity or warmup classification differs");
            Equal(sample.OtherMilliseconds, RunnerBenchmarkCommand.Remainder(sample.TotalMilliseconds, sample.CompileMilliseconds, sample.SimulationMilliseconds));
        }
        var summary = root.GetProperty("summary"); FixtureContracts.Fields(summary, "legacy", "standalone");
        var metrics = new Dictionary<string, Func<RunnerTiming, double>> {
            ["helpRoundTrip"] = sample => sample.HelpRoundTripMilliseconds, ["total"] = sample => sample.TotalMilliseconds,
            ["compile"] = sample => sample.CompileMilliseconds, ["simulation"] = sample => sample.SimulationMilliseconds, ["other"] = sample => sample.OtherMilliseconds };
        foreach (var runner in new[] { "legacy", "standalone" })
        {
            var group = summary.GetProperty(runner); FixtureContracts.Fields(group, metrics.Keys.ToArray());
            foreach (var (metric, select) in metrics)
            {
                var reported = group.GetProperty(metric); FixtureContracts.Fields(reported, "count", "min", "median", "max");
                var expected = RunnerBenchmarkCommand.Distribution(samples.Where(sample => !sample.Warmup && sample.Runner == runner).Select(select));
                if (reported.GetProperty("count").GetInt32() != expected.count) throw new InvalidDataException("Benchmark summary sample count differs");
                Equal(reported.GetProperty("min").GetDouble(), expected.min); Equal(reported.GetProperty("median").GetDouble(), expected.median); Equal(reported.GetProperty("max").GetDouble(), expected.max);
            }
        }
        return new(fixture, deployment, 2 * repeats, 2 * warmups);
    }
    static RunnerTiming ReadSample(JsonElement row) => new(row.GetProperty("Round").GetInt32(), row.GetProperty("Warmup").GetBoolean(), row.GetProperty("Order").GetInt32(), row.GetProperty("Runner").GetString() ?? "",
        row.GetProperty("HelpRoundTripMilliseconds").GetDouble(), row.GetProperty("TotalMilliseconds").GetDouble(), row.GetProperty("CompileMilliseconds").GetDouble(), row.GetProperty("SimulationMilliseconds").GetDouble(), row.GetProperty("OtherMilliseconds").GetDouble());
    static void Equal(double actual, double expected)
    {
        RunnerBenchmarkCommand.Duration(actual);
        if (Math.Abs(actual - expected) > 0.000001) throw new InvalidDataException("Benchmark summary or phase counters differ");
    }
}
