using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

public sealed class DapSegmentationTests
{
    [Theory]
    [InlineData("ascii", 1)]
    [InlineData("ascii", 2)]
    [InlineData("ascii", 3)]
    [InlineData("ascii", 7)]
    [InlineData("ascii", 31)]
    [InlineData("\u03bb", 1)]
    [InlineData("\u03bb", 2)]
    [InlineData("\u03bb", 3)]
    [InlineData("\u03bb", 7)]
    [InlineData("\u03bb", 31)]
    [InlineData("\ud83d\ude00", 1)]
    [InlineData("\ud83d\ude00", 2)]
    [InlineData("\ud83d\ude00", 3)]
    [InlineData("\ud83d\ude00", 7)]
    [InlineData("\ud83d\ude00", 31)]
    [InlineData("e\u0301", 1)]
    [InlineData("e\u0301", 2)]
    [InlineData("e\u0301", 3)]
    [InlineData("e\u0301", 7)]
    [InlineData("e\u0301", 31)]
    [InlineData("\u65e5\u672c\u8a9e", 1)]
    [InlineData("\u65e5\u672c\u8a9e", 2)]
    [InlineData("\u65e5\u672c\u8a9e", 3)]
    [InlineData("\u65e5\u672c\u8a9e", 7)]
    [InlineData("\u65e5\u672c\u8a9e", 31)]
    [InlineData("quote\" and slash\\", 1)]
    [InlineData("quote\" and slash\\", 2)]
    [InlineData("quote\" and slash\\", 3)]
    [InlineData("quote\" and slash\\", 7)]
    [InlineData("quote\" and slash\\", 31)]
    [InlineData("newline\n tab\t", 1)]
    [InlineData("newline\n tab\t", 2)]
    [InlineData("newline\n tab\t", 3)]
    [InlineData("newline\n tab\t", 7)]
    [InlineData("newline\n tab\t", 31)]
    [InlineData("\u0000", 1)]
    [InlineData("\u0000", 2)]
    [InlineData("\u0000", 3)]
    [InlineData("\u0000", 7)]
    [InlineData("\u0000", 31)]
    [InlineData("\u2028", 1)]
    [InlineData("\u2028", 2)]
    [InlineData("\u2028", 3)]
    [InlineData("\u2028", 7)]
    [InlineData("\u2028", 31)]
    [InlineData("\udbff\udfff", 1)]
    [InlineData("\udbff\udfff", 2)]
    [InlineData("\udbff\udfff", 3)]
    [InlineData("\udbff\udfff", 7)]
    [InlineData("\udbff\udfff", 31)]
    public async Task FragmentedMessagesPreserveUtf8AndFrameBoundaries(string text, int chunk)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { type = "event", @event = "output", body = text }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        using var wire = new MemoryStream();
        for (int index = 0; index < 2; index++) { wire.Write(System.Text.Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n")); wire.Write(payload); }
        using var stream = new SegmentedStream(wire.ToArray(), chunk);
        for (int index = 0; index < 2; index++)
        {
            var message = await DapSession.ReadMessage(stream, Xunit.TestContext.Current.CancellationToken);
            Assert.Equal("event", message.GetProperty("type").GetString());
            Assert.Equal(text, message.GetProperty("body").GetString());
        }
        Assert.Equal(stream.Length, stream.Position);
    }
    sealed class SegmentedStream(byte[] bytes, int maximum) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => base.ReadAsync(buffer[..Math.Min(buffer.Length, maximum)], cancellationToken);
    }
}
