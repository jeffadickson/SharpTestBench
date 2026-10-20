using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

public sealed class PerformancePairingTests
{
    [Theory]
    [InlineData(1, 0.125, "valid")]
    [InlineData(1, 0.5, "valid")]
    [InlineData(1, 1, "valid")]
    [InlineData(1, 2, "valid")]
    [InlineData(1, 8, "valid")]
    [InlineData(2, 0.125, "valid")]
    [InlineData(2, 0.5, "valid")]
    [InlineData(2, 1, "valid")]
    [InlineData(2, 2, "valid")]
    [InlineData(2, 8, "valid")]
    [InlineData(3, 0.125, "valid")]
    [InlineData(3, 0.5, "valid")]
    [InlineData(3, 1, "valid")]
    [InlineData(3, 2, "valid")]
    [InlineData(3, 8, "valid")]
    [InlineData(5, 0.125, "valid")]
    [InlineData(5, 0.5, "valid")]
    [InlineData(5, 1, "valid")]
    [InlineData(5, 2, "valid")]
    [InlineData(5, 8, "valid")]
    [InlineData(8, 0.125, "valid")]
    [InlineData(8, 0.5, "valid")]
    [InlineData(8, 1, "valid")]
    [InlineData(8, 2, "valid")]
    [InlineData(8, 8, "valid")]
    [InlineData(0, 1, "duplicate")]
    [InlineData(0, 1, "negative")]
    [InlineData(0, 1, "missing")]
    [InlineData(0, 1, "nonfinite")]
    [InlineData(0, 1, "overflow")]
    [InlineData(1, 1, "duplicate")]
    [InlineData(1, 1, "negative")]
    [InlineData(1, 1, "missing")]
    [InlineData(1, 1, "nonfinite")]
    [InlineData(1, 1, "overflow")]
    [InlineData(17, 1, "duplicate")]
    [InlineData(17, 1, "negative")]
    [InlineData(17, 1, "missing")]
    [InlineData(17, 1, "nonfinite")]
    [InlineData(17, 1, "overflow")]
    [InlineData(31, 1, "duplicate")]
    [InlineData(31, 1, "negative")]
    [InlineData(31, 1, "missing")]
    [InlineData(31, 1, "nonfinite")]
    [InlineData(31, 1, "overflow")]
    [InlineData(63, 1, "duplicate")]
    [InlineData(63, 1, "negative")]
    [InlineData(63, 1, "missing")]
    [InlineData(63, 1, "nonfinite")]
    [InlineData(63, 1, "overflow")]
    public void PairedEvidenceHasUniqueRoundsAndFiniteRatios(int number, double scale, string corruption)
    {
        var records = new List<PerformanceSample>();
        if (corruption == "valid")
        {
            for (int round = 0; round < number; round++) records.Add(new(round, "baseline", new() { ["time"] = 2 * scale * (round + 1) }, []));
            for (int round = number - 1; round >= 0; round--) records.Add(new(round, "candidate", new() { ["time"] = scale * (round + 1) }, []));
            var result = JsonSerializer.SerializeToElement(NativePerformanceCommand.Comparison(records, "candidate", "time"));
            Assert.Equal(2, result.GetProperty("speedup").GetDouble());
            Assert.Equal(2, result.GetProperty("paired_bootstrap_95")[0].GetDouble());
            Assert.Equal(2, result.GetProperty("paired_bootstrap_95")[1].GetDouble());
            return;
        }
        records.Add(new(number, "baseline", new() { ["time"] = 2 }, []));
        records.Add(new(number, "candidate", new() { ["time"] = 1 }, []));
        switch (corruption)
        {
            case "duplicate": records.AddRange(records.ToArray()); break;
            case "negative": records = records.Select(row => row with { round = -number - 1 }).ToList(); break;
            case "missing": records[0].metrics.Remove("time"); break;
            case "nonfinite": records[1].metrics["time"] = double.NaN; break;
            case "overflow": records[0].metrics["time"] = double.MaxValue; records[1].metrics["time"] = double.Epsilon; break;
            default: throw new InvalidOperationException(corruption);
        }
        Assert.Throws<InvalidDataException>(() => NativePerformanceCommand.Comparison(records, "candidate", "time"));
    }
}
