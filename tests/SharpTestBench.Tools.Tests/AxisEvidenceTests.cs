using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

public sealed class AxisEvidenceTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 255)]
    [InlineData(1, 32768)]
    [InlineData(1, 65535)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(2, 255)]
    [InlineData(2, 32768)]
    [InlineData(2, 65535)]
    [InlineData(3, 0)]
    [InlineData(3, 1)]
    [InlineData(3, 255)]
    [InlineData(3, 32768)]
    [InlineData(3, 65535)]
    [InlineData(4, 0)]
    [InlineData(4, 1)]
    [InlineData(4, 255)]
    [InlineData(4, 32768)]
    [InlineData(4, 65535)]
    [InlineData(5, 0)]
    [InlineData(5, 1)]
    [InlineData(5, 255)]
    [InlineData(5, 32768)]
    [InlineData(5, 65535)]
    [InlineData(6, 0)]
    [InlineData(6, 1)]
    [InlineData(6, 255)]
    [InlineData(6, 32768)]
    [InlineData(6, 65535)]
    [InlineData(7, 0)]
    [InlineData(7, 1)]
    [InlineData(7, 255)]
    [InlineData(7, 32768)]
    [InlineData(7, 65535)]
    [InlineData(8, 0)]
    [InlineData(8, 1)]
    [InlineData(8, 255)]
    [InlineData(8, 32768)]
    [InlineData(8, 65535)]
    [InlineData(9, 0)]
    [InlineData(9, 1)]
    [InlineData(9, 255)]
    [InlineData(9, 32768)]
    [InlineData(9, 65535)]
    [InlineData(10, 0)]
    [InlineData(10, 1)]
    [InlineData(10, 255)]
    [InlineData(10, 32768)]
    [InlineData(10, 65535)]
    public void StalledPayloadMustRemainStable(int stallCycles, int payload)
    {
        var trace = new List<int[]> { new int[11] { 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 } };
        for (int index = 0; index < stallCycles; index++)
            trace.Add([trace.Count, 0, index == 0 ? 1 : 0, 1, 0, payload, 3, 1, 15, 7, 2]);
        trace.Add([trace.Count, 0, 0, 1, 1, payload, 3, 1, 15, 7, 2]);
        trace.Add([trace.Count, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0]);
        var metrics = JsonSerializer.SerializeToElement(new { trace, accepted = 1, consumed = 1, flushed = 0, stalls = stallCycles, resets = 1, cycles = trace.Count });
        Assert.True(JsonElement.DeepEquals(metrics, AxisFixture.Metrics(metrics)));
        // A held output is still owned by the sender on the cycle it becomes ready.
        trace[^2][5] ^= 1;
        var changed = JsonSerializer.SerializeToElement(new { trace, accepted = 1, consumed = 1, flushed = 0, stalls = stallCycles, resets = 1, cycles = trace.Count });
        Assert.Throws<InvalidDataException>(() => AxisFixture.Metrics(changed));
        trace[^2][5] ^= 1;
        var miscounted = JsonSerializer.SerializeToElement(new { trace, accepted = 2, consumed = 1, flushed = 0, stalls = stallCycles, resets = 1, cycles = trace.Count });
        Assert.Throws<InvalidDataException>(() => AxisFixture.Metrics(miscounted));
    }
}
