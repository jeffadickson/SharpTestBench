using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

public sealed class RunnerBenchmarkReportTests
{
    static JsonObject Valid(int repeats = 3, int warmups = 1)
    {
        var samples = new JsonArray();
        for (int round = -warmups; round < repeats; round++)
        {
            var names = (round & 1) == 0 ? new[] { "legacy", "standalone" } : new[] { "standalone", "legacy" };
            for (int order = 0; order < 2; order++)
            {
                int offset = names[order] == "legacy" ? 0 : 5;
                samples.Add(new JsonObject { ["Round"] = round, ["Warmup"] = round < 0, ["Order"] = order, ["Runner"] = names[order],
                    ["HelpRoundTripMilliseconds"] = 10 + round + offset, ["TotalMilliseconds"] = 100 + 10 * round + offset,
                    ["CompileMilliseconds"] = 10 + round, ["SimulationMilliseconds"] = 20 + round, ["OtherMilliseconds"] = 70 + 8 * round + offset });
            }
        }
        var summary = new JsonObject();
        foreach (var name in new[] { "legacy", "standalone" })
        {
            var group = new JsonObject();
            foreach (var (key, field) in new[] { ("helpRoundTrip", "HelpRoundTripMilliseconds"), ("total", "TotalMilliseconds"), ("compile", "CompileMilliseconds"), ("simulation", "SimulationMilliseconds"), ("other", "OtherMilliseconds") })
            {
                var values = samples.Where(row => !row!["Warmup"]!.GetValue<bool>() && row["Runner"]!.GetValue<string>() == name).Select(row => row![field]!.GetValue<int>()).Order().ToArray();
                double median = values.Length % 2 == 0 ? (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2d : values[values.Length / 2];
                group[key] = new JsonObject { ["count"] = repeats, ["min"] = values[0], ["median"] = median, ["max"] = values[^1] };
            }
            summary[name] = group;
        }
        return new JsonObject { ["schemaVersion"] = 1, ["fixture"] = "counter", ["seed"] = 17, ["standaloneDeployment"] = "managed", ["units"] = "milliseconds", ["repeats"] = repeats, ["warmups"] = warmups, ["samples"] = samples, ["summary"] = summary };
    }
    [Fact] public void RejectUnsupportedSchema()
    {
        var report = Valid(); report["schemaVersion"] = 2; Reject(report);
    }
    [Fact] public void RejectTooFewMeasuredRounds()
    {
        Reject(Valid(2));
    }
    [Fact] public void RejectTooManyMeasuredRounds()
    {
        Reject(Valid(31));
    }
    [Fact] public void RejectMissingWarmupRound()
    {
        Reject(Valid(3, 0));
    }
    [Fact] public void RejectExcessiveWarmupRounds()
    {
        Reject(Valid(3, 6));
    }
    [Fact] public void RejectMissingRawSamples()
    {
        var report = Valid(); report.Remove("samples"); Reject(report);
    }
    [Fact] public void RejectEmptyRawSamples()
    {
        var report = Valid(); report["samples"] = new JsonArray(); Reject(report);
    }
    [Fact] public void RejectIncompleteRunnerPair()
    {
        var report = Valid(); ((JsonArray)report["samples"]!).RemoveAt(1); Reject(report);
    }
    [Fact] public void RejectDuplicateRunnerSample()
    {
        var report = Valid(); report["samples"]![1] = Sample(report).DeepClone(); Reject(report);
    }
    [Fact] public void RejectUnknownRunnerIdentity()
    {
        var report = Valid(); Sample(report)["Runner"] = "unknown"; Reject(report);
    }
    [Fact] public void RejectWrongPairOrder()
    {
        var report = Valid(); Sample(report)["Order"] = 1; Reject(report);
    }
    [Fact] public void RejectWarmupCountedAsMeasurement()
    {
        var report = Valid(); Sample(report)["Warmup"] = false; Reject(report);
    }
    [Fact] public void RejectOutOfRangeRound()
    {
        var report = Valid(); Sample(report)["Round"] = -2; Reject(report);
    }
    [Fact] public void RejectReorderedChronologicalSamples()
    {
        var report = Valid(); var samples = (JsonArray)report["samples"]!; var first = samples[0]!.DeepClone(); samples.RemoveAt(0); samples.Add(first); Reject(report);
    }
    [Fact] public void RejectNegativeHelpDuration()
    {
        var report = Valid(); Sample(report)["HelpRoundTripMilliseconds"] = -1; Reject(report);
    }
    [Fact] public void RejectNegativeTotalDuration()
    {
        var report = Valid(); Sample(report)["TotalMilliseconds"] = -1; Reject(report);
    }
    [Fact] public void RejectNegativeCompileDuration()
    {
        var report = Valid(); Sample(report)["CompileMilliseconds"] = -1; Reject(report);
    }
    [Fact] public void RejectNegativeSimulationDuration()
    {
        var report = Valid(); Sample(report)["SimulationMilliseconds"] = -1; Reject(report);
    }
    [Fact] public void RejectNegativeOtherDuration()
    {
        var report = Valid(); Sample(report)["OtherMilliseconds"] = -1; Reject(report);
    }
    [Fact] public void RejectPhasesExceedingTotal()
    {
        var report = Valid(); Sample(report)["TotalMilliseconds"] = 1; Reject(report);
    }
    [Fact] public void RejectInconsistentOtherDuration()
    {
        var report = Valid(); Sample(report)["OtherMilliseconds"] = 1; Reject(report);
    }
    [Fact] public void RejectWarmupIncludedInSummaryCount()
    {
        var report = Valid(); report["summary"]!["legacy"]!["total"]!["count"] = 4; Reject(report);
    }
    [Fact] public void RejectIncorrectSummaryMinimum()
    {
        var report = Valid(); report["summary"]!["legacy"]!["total"]!["min"] = 1; Reject(report);
    }
    [Fact] public void RejectIncorrectSummaryMedian()
    {
        var report = Valid(); report["summary"]!["standalone"]!["simulation"]!["median"] = 1; Reject(report);
    }
    [Fact] public void RejectIncorrectSummaryMaximum()
    {
        var report = Valid(); report["summary"]!["standalone"]!["compile"]!["max"] = 1; Reject(report);
    }
    [Fact] public void RejectMissingSummaryMetric()
    {
        var report = Valid(); ((JsonObject)report["summary"]!["legacy"]!).Remove("other"); Reject(report);
    }
    [Fact] public void RejectUnexpectedSummaryRunner()
    {
        var report = Valid(); report["summary"]!["third"] = report["summary"]!["legacy"]!.DeepClone(); Reject(report);
    }
    [Fact] public void RejectDifferentMeasurementUnits()
    {
        var report = Valid(); report["units"] = "seconds"; Reject(report);
    }
    [Fact] public void RejectUnknownStandaloneDeployment()
    {
        var report = Valid(); report["standaloneDeployment"] = "unqualified"; Reject(report);
    }
    [Fact] public void RejectInvalidUnsignedSeed()
    {
        var report = Valid(); report["seed"] = -1; Reject(report);
    }
    [Fact] public void MedianPreservesFiniteExtremes()
    {
        Assert.Equal(double.MaxValue, RunnerBenchmarkCommand.Distribution([double.MaxValue, double.MaxValue]).median);
        Assert.Equal(double.MaxValue / 2, RunnerBenchmarkCommand.Distribution([0, double.MaxValue]).median);
        Assert.Equal(double.Epsilon, RunnerBenchmarkCommand.Distribution([double.Epsilon, double.Epsilon]).median);
        Assert.Equal(2 * double.Epsilon, RunnerBenchmarkCommand.Distribution([double.Epsilon, 2 * double.Epsilon]).median);
        Assert.Equal(double.MaxValue, RunnerBenchmarkCommand.Distribution([double.MaxValue]).median);
    }
    [Fact] public void NestedFieldErrorsCannotQualify()
    {
        foreach (var mutation in new[] { "missing", "text", "null", "warmup-type", "runner-null" })
        {
            var report = Valid(); var sample = Sample(report);
            switch (mutation)
            {
                case "missing": sample.Remove("TotalMilliseconds"); break;
                case "text": sample["TotalMilliseconds"] = "90"; break;
                case "null": sample["TotalMilliseconds"] = null; break;
                case "warmup-type": sample["Warmup"] = 1; break;
                case "runner-null": sample["Runner"] = null; break;
            }
            Reject(report);
        }
    }
    [Fact] public void CheckerIsReadOnlyAndAcceptsSupportedUpperBounds()
    {
        var report = Valid(30, 5); report["fixture"] = "cpu"; report["seed"] = ulong.MaxValue; report["standaloneDeployment"] = "native-aot-artifact";
        var directory = Path.Combine(Path.GetTempPath(), "benchmark-check-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "summary.json"); var text = report.ToJsonString(); File.WriteAllText(path, text);
        try { RunnerBenchmarkReport.Execute(["--report", path]); Assert.Equal(text, File.ReadAllText(path)); }
        finally { Directory.Delete(directory, true); }
    }
    static JsonObject Sample(JsonObject report, int index = 0) => (JsonObject)report["samples"]![index]!;
    static void Reject(JsonObject report) => Assert.Throws<InvalidDataException>(() => RunnerBenchmarkReport.Validate(JsonSerializer.SerializeToElement(report)));
    [Fact] public void GoldenReportQualifiesWithSeparateWarmups()
    {
        var report = RunnerBenchmarkReport.Validate(JsonSerializer.SerializeToElement(Valid()));
        Assert.Equal(6, report.measuredSamples); Assert.Equal(2, report.warmupSamples); Assert.Equal("counter", report.fixture);
    }
}
