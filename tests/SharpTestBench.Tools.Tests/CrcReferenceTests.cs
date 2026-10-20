using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

// Expected values were generated with CPython zlib.crc32 and binascii.crc_hqx, not the managed LFSR model.
public sealed class CrcReferenceTests
{
    [Theory]
    [InlineData("crc32", 0, 0UL)]
    [InlineData("crc32", 1, 1455205971UL)]
    [InlineData("crc32", 2, 1122717362UL)]
    [InlineData("crc32", 3, 1368621316UL)]
    [InlineData("crc32", 4, 2458679146UL)]
    [InlineData("crc32", 7, 3329140230UL)]
    [InlineData("crc32", 8, 3357431126UL)]
    [InlineData("crc32", 9, 2893778277UL)]
    [InlineData("crc32", 15, 2103864770UL)]
    [InlineData("crc32", 16, 749441686UL)]
    [InlineData("crc32", 17, 2224914492UL)]
    [InlineData("crc32", 31, 3131981756UL)]
    [InlineData("crc32", 32, 1353758067UL)]
    [InlineData("crc32", 33, 2760777384UL)]
    [InlineData("crc32", 63, 2835830886UL)]
    [InlineData("crc32", 64, 2528101798UL)]
    [InlineData("crc32", 65, 524502747UL)]
    [InlineData("crc32", 127, 3161309291UL)]
    [InlineData("crc32", 128, 2664927763UL)]
    [InlineData("crc32", 129, 1059371839UL)]
    [InlineData("crc32", 255, 3488025878UL)]
    [InlineData("crc32", 256, 1571329663UL)]
    [InlineData("crc32", 257, 2522573416UL)]
    [InlineData("crc32", 511, 1046959654UL)]
    [InlineData("crc32", 1024, 841519369UL)]
    [InlineData("crc16", 0, 65535UL)]
    [InlineData("crc16", 1, 50082UL)]
    [InlineData("crc16", 2, 53334UL)]
    [InlineData("crc16", 3, 30770UL)]
    [InlineData("crc16", 4, 53631UL)]
    [InlineData("crc16", 7, 22795UL)]
    [InlineData("crc16", 8, 62127UL)]
    [InlineData("crc16", 9, 35779UL)]
    [InlineData("crc16", 15, 21939UL)]
    [InlineData("crc16", 16, 17135UL)]
    [InlineData("crc16", 17, 527UL)]
    [InlineData("crc16", 31, 17480UL)]
    [InlineData("crc16", 32, 7204UL)]
    [InlineData("crc16", 33, 61837UL)]
    [InlineData("crc16", 63, 57412UL)]
    [InlineData("crc16", 64, 6244UL)]
    [InlineData("crc16", 65, 40367UL)]
    [InlineData("crc16", 127, 23351UL)]
    [InlineData("crc16", 128, 13584UL)]
    [InlineData("crc16", 129, 50476UL)]
    [InlineData("crc16", 255, 29492UL)]
    [InlineData("crc16", 256, 754UL)]
    [InlineData("crc16", 257, 61456UL)]
    [InlineData("crc16", 511, 34052UL)]
    [InlineData("crc16", 1024, 61698UL)]
    public void ByteStreamsMatchIndependentStandardLibraryVectors(string algorithm, int length, ulong expected)
    {
        ulong state = algorithm == "crc32" ? uint.MaxValue : 0xffffUL;
        var config = LfsrFixture.Cases[algorithm];
        for (int index = 0; index < length; index++) state = LfsrFixture.Transition(config, state, (byte)(index * 73 + 19)).State;
        Assert.Equal(expected, algorithm == "crc32" ? state ^ uint.MaxValue : state);
    }
}
