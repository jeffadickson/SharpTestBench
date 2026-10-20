using SharpTestBench.Tools;
using Xunit;
public sealed class MeasurementTests
{
    [Fact]
    public void PairedBootstrapPreservesAConstantRatio()
    {
        var records = new List<PerformanceSample>();
        for (int round = 0; round < 5; round++) { records.Add(new(round, "baseline", new() { ["time"] = 2 * (round + 1) }, [])); records.Add(new(round, "candidate", new() { ["time"] = round + 1 }, [])); }
        var result = System.Text.Json.JsonSerializer.SerializeToElement(NativePerformanceCommand.Comparison(records, "candidate", "time"));
        Assert.Equal(2, result.GetProperty("speedup").GetDouble()); Assert.Equal(2, result.GetProperty("paired_bootstrap_95")[0].GetDouble()); Assert.Equal(2, result.GetProperty("paired_bootstrap_95")[1].GetDouble());
    }
    [Fact]
    public void BootstrapRejectsUnpairedRounds()
    {
        PerformanceSample[] records = [new(0, "baseline", new() { ["time"] = 2 }, []), new(1, "candidate", new() { ["time"] = 1 }, [])];
        Assert.Throws<InvalidDataException>(() => NativePerformanceCommand.Comparison(records, "candidate", "time"));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void BootstrapRejectsInvalidSamples(double sample)
    {
        PerformanceSample[] records = [new(0, "baseline", new() { ["time"] = 2 }, []), new(0, "candidate", new() { ["time"] = sample }, [])];
        Assert.Throws<InvalidDataException>(() => NativePerformanceCommand.Comparison(records, "candidate", "time"));
    }
    [Theory]
    [InlineData("MEASURE timer 1\nMEASURE rw32 2")]
    [InlineData("checksum=694140833\nMEASURE timer 1")]
    [InlineData("checksum=694140833\nMEASURE timer 0\nMEASURE rw32 2")]
    public void BenchmarkRequiresCorrectWorkAndCompleteMeasurements(string output) => Assert.Throws<InvalidDataException>(() => BenchmarkCommand.Parse(output));
    [Fact]
    public void BenchmarkParsesInvariantScientificNotation()
    {
        var result = BenchmarkCommand.Parse("checksum=694140833\nMEASURE timer 1.25e-2\nMEASURE rw32 2.5"); Assert.Equal(0.0125, result["timer"]); Assert.Equal(2.5, result["rw32"]);
    }
}
