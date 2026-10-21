using Xunit;

public sealed class PriorityOracleTests
{
    [Theory]
    [InlineData(0u, 2, true, -1)]
    [InlineData(0u, 2, false, -1)]
    [InlineData(1u, 2, true, 0)]
    [InlineData(2u, 2, true, 1)]
    [InlineData(3u, 2, true, 0)]
    [InlineData(3u, 2, false, 1)]
    [InlineData(4u, 3, true, 2)]
    [InlineData(5u, 3, true, 0)]
    [InlineData(5u, 3, false, 2)]
    [InlineData(7u, 3, false, 2)]
    [InlineData(8u, 3, true, -1)]
    [InlineData(16u, 5, true, 4)]
    [InlineData(17u, 5, true, 0)]
    [InlineData(17u, 5, false, 4)]
    [InlineData(32u, 5, false, -1)]
    [InlineData(0u, 8, false, -1)]
    [InlineData(128u, 8, true, 7)]
    [InlineData(129u, 8, true, 0)]
    [InlineData(129u, 8, false, 7)]
    [InlineData(255u, 8, true, 0)]
    [InlineData(255u, 8, false, 7)]
    [InlineData(256u, 8, false, -1)]
    [InlineData(0x10000u, 17, true, 16)]
    [InlineData(0x10001u, 17, true, 0)]
    [InlineData(0x10001u, 17, false, 16)]
    [InlineData(0x1ffffu, 17, false, 16)]
    [InlineData(0x20000u, 17, true, -1)]
    [InlineData(0x80000000u, 32, true, 31)]
    [InlineData(0x80000001u, 32, false, 31)]
    [InlineData(0x80000001u, 32, true, 0)]
    public void DirectedWinner(uint mask, int width, bool lowFirst, int expected) =>
        Assert.Equal(expected, Entry.Winner(mask, width, lowFirst));
}
