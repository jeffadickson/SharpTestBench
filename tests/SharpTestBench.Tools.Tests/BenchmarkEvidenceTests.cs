using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

public sealed class BenchmarkEvidenceTests
{
    [Theory]
    [InlineData("MEASURE timer 1.25\nMEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE timer 1.25\r\nMEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE timer 1.25\rMEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE\ttimer\t1.25\nMEASURE\trw32\t2.5 checksum=694140833", true)]
    [InlineData("  MEASURE timer 1.25\n  MEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE timer 1.2500\nMEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE timer 1.2500\r\nMEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE timer 1.2500\rMEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE\ttimer\t1.2500\nMEASURE\trw32\t2.5 checksum=694140833", true)]
    [InlineData("  MEASURE timer 1.2500\n  MEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE timer +1.25\nMEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE timer +1.25\r\nMEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE timer +1.25\rMEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE\ttimer\t+1.25\nMEASURE\trw32\t2.5 checksum=694140833", true)]
    [InlineData("  MEASURE timer +1.25\n  MEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE timer 1.25e0\nMEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE timer 1.25e0\r\nMEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE timer 1.25e0\rMEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE\ttimer\t1.25e0\nMEASURE\trw32\t2.5 checksum=694140833", true)]
    [InlineData("  MEASURE timer 1.25e0\n  MEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE timer 125e-2\nMEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE timer 125e-2\r\nMEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE timer 125e-2\rMEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("MEASURE\ttimer\t125e-2\nMEASURE\trw32\t2.5 checksum=694140833", true)]
    [InlineData("  MEASURE timer 125e-2\n  MEASURE rw32 2.5 checksum=694140833", true)]
    [InlineData("checksum=6941408330\nMEASURE timer 1.25\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=1694140833\nMEASURE timer 1.25\nMEASURE rw32 2.5", false)]
    [InlineData("xchecksum=694140833\nMEASURE timer 1.25\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=694140833junk\nMEASURE timer 1.25\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=694140834\nMEASURE timer 1.25\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=694140833\nMEASURE timer 1.25\nMEASURE rw32 2.5\nMEASURE timer 1", false)]
    [InlineData("checksum=694140833\nMEASURE timer 1.25\nMEASURE rw32 2.5\nMEASURE timer 1.25", false)]
    [InlineData("checksum=694140833\nMEASURE timer 1.25\nMEASURE rw32 2.5\nMEASURE rw32 4", false)]
    [InlineData("checksum=694140833\nMEASURE timer 1.25\nMEASURE rw32 2.5\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=694140833\nMEASURE timer 1.25\nMEASURE rw32 2.5\nchecksum=694140833", false)]
    [InlineData("checksum=694140833\nMEASURE timer NaN\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=694140833\nMEASURE timer Infinity\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=694140833\nMEASURE timer -Infinity\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=694140833\nMEASURE timer 0\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=694140833\nMEASURE timer -1\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=694140833\nMEASURE timer 1.25junk\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=694140833\nMEASURE timer 1.25\nMEASURE rw32 2.5junk", false)]
    [InlineData("checksum=694140833\nMEASURE timer 1,25\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=694140833\nMEASURE timer 1.25\nMEASURE rw32 2.5\nMEASURE unknown 1", false)]
    [InlineData("checksum=694140833\nMEASURE timer\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=694140833\nMEASURE timer 1e\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=694140833\nMEASURE timer 1e999\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=694140833\nMEASURE timer --1\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=694140833\nMEASURE rw32 2.5", false)]
    [InlineData("checksum=694140833\nMEASURE timer 1.25\nMEASURE rw32 2.5\nchecksum=incorrect", false)]
    public void CompleteEvidenceIsRequired(string text, bool valid)
    {
        if (!valid) { Assert.Throws<InvalidDataException>(() => BenchmarkCommand.Parse(text)); return; }
        var result = BenchmarkCommand.Parse(text);
        Assert.Equal(2, result.Count); Assert.Equal(1.25, result["timer"]); Assert.Equal(2.5, result["rw32"]);
    }
}
