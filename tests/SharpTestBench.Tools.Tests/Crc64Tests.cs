using System.Text;
using SharpTestBench.Tools;
using Xunit;

public sealed class Crc64Tests
{
    // Published check values: https://reveng.sourceforge.io/crc-catalogue/17plus.htm#crc.cat.crc-64-ecma-182
    [Theory]
    [InlineData("crc64", 0UL, 0UL, 0x6c40df5f0b497347UL)]
    [InlineData("crc64xz", ulong.MaxValue, ulong.MaxValue, 0x995dc9bbdf1939faUL)]
    public void PublishedCheckVectorMatches(string name, ulong initial, ulong finalXor, ulong expected)
    {
        ulong state = initial;
        foreach (var value in Encoding.ASCII.GetBytes("123456789")) state = LfsrFixture.Transition(LfsrFixture.Cases[name], state, value).State;
        Assert.Equal(expected, state ^ finalXor);
    }
}
