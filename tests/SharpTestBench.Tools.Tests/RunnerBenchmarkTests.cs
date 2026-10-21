using System.Text.Json;
using SharpTestBench.Tools;
using Xunit;

public sealed class RunnerBenchmarkTests
{
    static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);
    const string Phase = "{\"SchemaVersion\":1,\"Phase\":\"compile\",\"Started\":true,\"ExitCode\":0,\"TimedOut\":false,\"Failure\":null,\"WallMilliseconds\":12.5}";
    [Theory]
    [InlineData("\"Started\":true", "\"Started\":false")]
    [InlineData("\"ExitCode\":0", "\"ExitCode\":1")]
    [InlineData("\"ExitCode\":0", "\"ExitCode\":null")]
    [InlineData("\"TimedOut\":false", "\"TimedOut\":true")]
    [InlineData("\"Failure\":null", "\"Failure\":\"bad\"")]
    [InlineData("\"Phase\":\"compile\"", "\"Phase\":\"simulate\"")]
    [InlineData("\"SchemaVersion\":1", "\"SchemaVersion\":2")]
    [InlineData("12.5", "-1")]
    public void UnsuccessfulOrInvalidPhaseCannotQualify(string before, string after) =>
        Assert.NotNull(Record.Exception(() => RunnerBenchmarkCommand.Phase(Json(Phase.Replace(before, after)), "compile")));
    [Fact] public void SuccessfulPhasePreservesFractionalMilliseconds() => Assert.Equal(12.5, RunnerBenchmarkCommand.Phase(Json(Phase), "compile"));
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    public void InvalidDurationsCannotEnterSummary(double value) => Assert.Throws<InvalidDataException>(() => RunnerBenchmarkCommand.Distribution([1, value]));
    [Fact] public void EmptyMeasurementsCannotComplete() => Assert.Throws<InvalidDataException>(() => RunnerBenchmarkCommand.Distribution([]));
    [Fact] public void SummaryUsesMedianWithoutDiscardingOutliers()
    {
        var value = JsonSerializer.SerializeToElement(RunnerBenchmarkCommand.Distribution([100, 2, 4, 1]));
        Assert.Equal(4, value.GetProperty("count").GetInt32()); Assert.Equal(3, value.GetProperty("median").GetDouble());
        Assert.Equal(1, value.GetProperty("min").GetDouble()); Assert.Equal(100, value.GetProperty("max").GetDouble());
    }
    [Fact] public void InconsistentPhasesCannotProduceNegativeOverhead() => Assert.Throws<InvalidDataException>(() => RunnerBenchmarkCommand.Remainder(10, 6, 5));
    [Fact] public void RemainderIncludesUnattributedOrchestration() => Assert.Equal(4, RunnerBenchmarkCommand.Remainder(10, 2, 4));
    [Theory]
    [InlineData("failed", 17UL)]
    [InlineData("passed", 18UL)]
    public void IdenticalButWrongResultsCannotQualify(string status, ulong seed)
    {
        var rows = JsonSerializer.SerializeToElement(new[] { new { QualifiedName = "Entry.Test", Status = status, Seed = seed } });
        Assert.Throws<InvalidDataException>(() => RunnerBenchmarkCommand.Qualify(rows, rows, Json("[\"Entry.Test\"]"), 17));
    }
    [Fact] public void MatchingExpectedResultsQualify()
    {
        var rows = Json("[{\"QualifiedName\":\"Entry.Test\",\"Status\":\"passed\",\"Seed\":18446744073709551615}]");
        RunnerBenchmarkCommand.Qualify(rows, rows, Json("[\"Entry.Test\"]"), ulong.MaxValue);
    }
    [Theory]
    [InlineData("--repeats", "0")]
    [InlineData("--repeats", "31")]
    [InlineData("--warmups", "0")]
    [InlineData("--warmups", "6")]
    [InlineData("--fixture", "unknown")]
    [InlineData("--seed", "-1")]
    public void InvalidOptionsCannotLaunchPreparation(string option, string value) =>
        Assert.Throws<ArgumentException>(() => RunnerBenchmarkCommand.Execute(new ToolContext(Path.GetTempPath()), [option, value]));
    [Fact] public void TimingOutputCannotDeleteSourceInput()
    {
        var root = Path.Combine(Path.GetTempPath(), "benchmark-timing-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var input = Path.Combine(root, "sample-timings.json"); File.WriteAllText(input, "keep");
        try
        {
            Assert.Throws<ArgumentException>(() => LegacyRunCommand.Execute(new(root), ["--no-build", "--timings", "--work-dir", root, "--label", "sample", "--source", input]));
            Assert.Equal("keep", File.ReadAllText(input)); Assert.False(File.Exists(Path.Combine(root, ".sharptestbench-run.lock")));
        }
        finally { Directory.Delete(root, true); }
    }
}
