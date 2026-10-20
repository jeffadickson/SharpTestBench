using System.Text.Json;
using SharpTestBench.Tools;
using Xunit;

public sealed class FixtureTests
{
    [Fact] public void IntegerSeededStreamsMatchCpython()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "random-reference.json")));
        foreach (var row in document.RootElement.EnumerateArray())
        {
            var random = new PythonRandom(row.GetProperty("seed").GetUInt64()); int width = row.GetProperty("width").GetInt32();
            foreach (var expected in row.GetProperty("values").EnumerateArray()) Assert.Equal(expected.GetUInt64(), random.GetBits(width));
        }
    }
    [Theory][InlineData("crc32")][InlineData("crc16")][InlineData("crc32c")][InlineData("prbs7")][InlineData("scrambler")][InlineData("descrambler")][InlineData("galois_ff")][InlineData("minimal")][InlineData("crc64")][InlineData("crc64xz")]
    public void LfsrPlansValidateAndTracesAttest(string name)
    {
        var config = LfsrFixture.Cases[name]; var plan = LfsrFixture.Plan(config, ulong.MaxValue);
        var stimulus = JsonSerializer.SerializeToElement(plan); var validated = LfsrFixture.ValidatePlan(stimulus);
        Assert.Equal(plan.Vectors, validated.Vectors);
        var metrics = JsonSerializer.SerializeToElement(new { trace = plan.Vectors.Select((row, i) => new[] { (ulong)i, row.State, row.Data, row.ExpectedState, row.ExpectedData }).ToArray(), vectors = plan.Vectors.Length });
        Assert.True(JsonElement.DeepEquals(metrics, LfsrFixture.Metrics(metrics, stimulus)));
    }
    [Fact] public void Crc32MatchesStandardCheckVector()
    {
        ulong state = uint.MaxValue; foreach (var value in System.Text.Encoding.ASCII.GetBytes("123456789")) state = LfsrFixture.Transition(LfsrFixture.Cases["crc32"], state, value).State;
        Assert.Equal(0xcbf43926UL, state ^ uint.MaxValue);
    }
    [Fact] public void Crc16MatchesStandardCheckVector()
    {
        ulong state = 0xffff; foreach (var value in System.Text.Encoding.ASCII.GetBytes("123456789")) state = LfsrFixture.Transition(LfsrFixture.Cases["crc16"], state, value).State;
        Assert.Equal(0x29b1UL, state);
    }
}
