using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

public sealed class LfsrLinearityTests
{
    [Theory]
    [InlineData("crc32", 0UL)]
    [InlineData("crc32", 1UL)]
    [InlineData("crc32", 17UL)]
    [InlineData("crc32", 99UL)]
    [InlineData("crc32", 4294967296UL)]
    [InlineData("crc32", 18446744073709551615UL)]
    [InlineData("crc16", 0UL)]
    [InlineData("crc16", 1UL)]
    [InlineData("crc16", 17UL)]
    [InlineData("crc16", 99UL)]
    [InlineData("crc16", 4294967296UL)]
    [InlineData("crc16", 18446744073709551615UL)]
    [InlineData("crc32c", 0UL)]
    [InlineData("crc32c", 1UL)]
    [InlineData("crc32c", 17UL)]
    [InlineData("crc32c", 99UL)]
    [InlineData("crc32c", 4294967296UL)]
    [InlineData("crc32c", 18446744073709551615UL)]
    [InlineData("prbs7", 0UL)]
    [InlineData("prbs7", 1UL)]
    [InlineData("prbs7", 17UL)]
    [InlineData("prbs7", 99UL)]
    [InlineData("prbs7", 4294967296UL)]
    [InlineData("prbs7", 18446744073709551615UL)]
    [InlineData("scrambler", 0UL)]
    [InlineData("scrambler", 1UL)]
    [InlineData("scrambler", 17UL)]
    [InlineData("scrambler", 99UL)]
    [InlineData("scrambler", 4294967296UL)]
    [InlineData("scrambler", 18446744073709551615UL)]
    [InlineData("descrambler", 0UL)]
    [InlineData("descrambler", 1UL)]
    [InlineData("descrambler", 17UL)]
    [InlineData("descrambler", 99UL)]
    [InlineData("descrambler", 4294967296UL)]
    [InlineData("descrambler", 18446744073709551615UL)]
    [InlineData("galois_ff", 0UL)]
    [InlineData("galois_ff", 1UL)]
    [InlineData("galois_ff", 17UL)]
    [InlineData("galois_ff", 99UL)]
    [InlineData("galois_ff", 4294967296UL)]
    [InlineData("galois_ff", 18446744073709551615UL)]
    [InlineData("minimal", 0UL)]
    [InlineData("minimal", 1UL)]
    [InlineData("minimal", 17UL)]
    [InlineData("minimal", 99UL)]
    [InlineData("minimal", 4294967296UL)]
    [InlineData("minimal", 18446744073709551615UL)]
    [InlineData("scrambler", 9223372036854775808UL)]
    [InlineData("descrambler", 9223372036854775808UL)]
    public void TransitionIsLinearAndBounded(string name, ulong seed)
    {
        var config = LfsrFixture.Cases[name]; var random = new PythonRandom(seed);
        ulong stateA = random.GetBits(config.Width), stateB = random.GetBits(config.Width);
        ulong dataA = random.GetBits(config.DataWidth), dataB = random.GetBits(config.DataWidth);
        var a = LfsrFixture.Transition(config, stateA, dataA); var b = LfsrFixture.Transition(config, stateB, dataB);
        var combined = LfsrFixture.Transition(config, stateA ^ stateB, dataA ^ dataB);
        Assert.Equal((a.State ^ b.State, a.Data ^ b.Data), combined);
        Assert.Equal((0UL, 0UL), LfsrFixture.Transition(config, 0, 0));
        Assert.InRange(combined.State, 0UL, FixtureContracts.Mask(config.Width));
        Assert.InRange(combined.Data, 0UL, FixtureContracts.Mask(config.DataWidth));
        Assert.Equal(stateA, LfsrFixture.Reverse(LfsrFixture.Reverse(stateA, config.Width), config.Width));
    }
}
