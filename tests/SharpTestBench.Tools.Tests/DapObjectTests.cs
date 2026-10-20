using System.Text;
using SharpTestBench.Tools;
using Xunit;

public sealed class DapObjectTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("17")]
    [InlineData("\"text\"")]
    [InlineData("[]")]
    [InlineData("[{}]")]
    public async Task ProtocolMessageMustBeAnObject(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n").Concat(bytes).ToArray());
        await Assert.ThrowsAsync<InvalidDataException>(() => DapSession.ReadMessage(stream, Xunit.TestContext.Current.CancellationToken));
    }
}
