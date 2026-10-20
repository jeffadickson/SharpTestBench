using System.Text;
using System.Text.Json;
using SharpTestBench.Tools;
using Xunit;
public sealed class DapTests
{
    [Theory]
    [InlineData("Content-Length: 0\r\n\r\n")]
    [InlineData("Content-Length: -1\r\n\r\n")]
    [InlineData("Content-Length: 16777217\r\n\r\n")]
    [InlineData("Content-Length: 2\r\nContent-Length: 2\r\n\r\n{}")]
    [InlineData("Other: 2\r\n\r\n{}")]
    public async Task InvalidFramingFails(string frame)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(frame));
        await Assert.ThrowsAsync<InvalidDataException>(() => DapSession.ReadMessage(stream, Xunit.TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Utf8PayloadLengthAndSequentialMessagesArePreserved()
    {
        var payload = Encoding.UTF8.GetBytes("{\"event\":\"stopped\",\"body\":\"λ\"}");
        using var stream = new MemoryStream();
        for (var index = 0; index < 2; index++) { stream.Write(Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n")); stream.Write(payload); }
        stream.Position = 0;
        for (var index = 0; index < 2; index++) Assert.Equal("λ", (await DapSession.ReadMessage(stream, Xunit.TestContext.Current.CancellationToken)).GetProperty("body").GetString());
    }
    [Theory]
    [InlineData("Content-Length: 2\r\n\r\n{")]
    [InlineData("Content-Length: 2\r\n")]
    public async Task TruncatedFramesFail(string frame)
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(frame));
        await Assert.ThrowsAsync<EndOfStreamException>(() => DapSession.ReadMessage(stream, Xunit.TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task DuplicateJsonPropertiesFail()
    {
        const string payload = "{\"event\":1,\"event\":2}";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n{payload}"));
        await Assert.ThrowsAnyAsync<Exception>(() => DapSession.ReadMessage(stream, Xunit.TestContext.Current.CancellationToken));
    }
}
