using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

public sealed class LfsrCorruptionTests
{
    [Theory]
    [InlineData("crc32", "basis")]
    [InlineData("crc32", "state")]
    [InlineData("crc32", "data")]
    [InlineData("crc32", "extra")]
    [InlineData("crc32", "seed")]
    [InlineData("crc32", "missing")]
    [InlineData("crc16", "basis")]
    [InlineData("crc16", "state")]
    [InlineData("crc16", "data")]
    [InlineData("crc16", "extra")]
    [InlineData("crc16", "seed")]
    [InlineData("crc16", "missing")]
    [InlineData("crc32c", "basis")]
    [InlineData("crc32c", "state")]
    [InlineData("crc32c", "data")]
    [InlineData("crc32c", "extra")]
    [InlineData("crc32c", "seed")]
    [InlineData("crc32c", "missing")]
    [InlineData("prbs7", "basis")]
    [InlineData("prbs7", "state")]
    [InlineData("prbs7", "data")]
    [InlineData("prbs7", "extra")]
    [InlineData("prbs7", "seed")]
    [InlineData("prbs7", "missing")]
    [InlineData("scrambler", "basis")]
    [InlineData("scrambler", "state")]
    [InlineData("scrambler", "data")]
    [InlineData("scrambler", "extra")]
    [InlineData("scrambler", "seed")]
    [InlineData("scrambler", "missing")]
    [InlineData("descrambler", "basis")]
    [InlineData("descrambler", "state")]
    [InlineData("descrambler", "data")]
    [InlineData("descrambler", "extra")]
    [InlineData("descrambler", "seed")]
    [InlineData("descrambler", "missing")]
    [InlineData("galois_ff", "basis")]
    [InlineData("galois_ff", "state")]
    [InlineData("galois_ff", "data")]
    [InlineData("galois_ff", "extra")]
    [InlineData("galois_ff", "seed")]
    [InlineData("galois_ff", "missing")]
    [InlineData("minimal", "basis")]
    [InlineData("minimal", "state")]
    [InlineData("minimal", "data")]
    [InlineData("minimal", "extra")]
    [InlineData("minimal", "seed")]
    [InlineData("minimal", "missing")]
    [InlineData("crc32", "width")]
    [InlineData("scrambler", "boolean")]
    public void CorruptedStimulusCannotAttest(string name, string mutation)
    {
        var node = JsonSerializer.SerializeToNode(LfsrFixture.Plan(LfsrFixture.Cases[name], 17))!;
        var rows = node["Vectors"]!.AsArray();
        switch (mutation)
        {
            case "basis": rows[0]!["State"] = 1; break;
            case "state": rows[^1]!["ExpectedState"] = rows[^1]!["ExpectedState"]!.GetValue<ulong>() ^ 1; break;
            case "data": rows[^1]!["ExpectedData"] = rows[^1]!["ExpectedData"]!.GetValue<ulong>() ^ 1; break;
            case "extra": rows[^1]!["Unexpected"] = 0; break;
            case "seed": node["Seed"] = -1; break;
            case "missing": rows[^1]!.AsObject().Remove("Data"); break;
            case "width": node["Config"]!["Width"] = 65; break;
            case "boolean": node["Config"]!["Reverse"] = 1; break;
            default: throw new InvalidOperationException(mutation);
        }
        using var document = JsonDocument.Parse(node.ToJsonString());
        Assert.Throws<InvalidDataException>(() => LfsrFixture.ValidatePlan(document.RootElement));
    }
}
