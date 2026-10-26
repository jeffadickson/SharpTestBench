using System.Text;
using System.Text.Json;
using SharpTestBench.Tools;
using Xunit;

/// <summary>Structured discovery needs a complete bounded inventory, unlike diagnostic log prefixes.</summary>
public sealed class EnduranceVerificationTests
{
    [Fact]public void LargeDiscoverySurvivesItsSeparateCaptureBudget()
    {
        using var repo=new EnduranceTestRepository();string directory=Path.Combine(repo.Root,"build");Directory.CreateDirectory(directory);
        var rows=Enumerable.Range(0,1250).Select(i=>new{ID=i.ToString(System.Globalization.CultureInfo.InvariantCulture),Class="Inventory",Method="M"+i,DisplayName="Inventory.M"+i+new string('x',100)}).ToArray();
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(rows);Assert.True(bytes.Length>131072);
        string payload=Path.Combine(directory,"inventory.json"),writer=Path.Combine(directory,"writer.ps1");File.WriteAllBytes(payload,bytes);
        File.WriteAllText(writer,"[Console]::OpenStandardOutput().Write([IO.File]::ReadAllBytes($args[0]))",new UTF8Encoding(false));
        var prefix=EnduranceProcess.Run("pwsh",["-NoProfile","-File",writer,payload],repo.Root,TimeSpan.FromSeconds(20),131072,cancellation:TestContext.Current.CancellationToken);
        Assert.True(prefix.Truncated);Assert.False(prefix.Passed);
        var complete=EnduranceProcess.Run("pwsh",["-NoProfile","-File",writer,payload],repo.Root,TimeSpan.FromSeconds(20),EnduranceVerification.DiscoveryBytes,cancellation:TestContext.Current.CancellationToken);
        Assert.True(complete.Passed);Assert.Equal(bytes.Length,complete.Stdout.Length);
        Assert.Equal(1250,ManagedTestsCommand.Inventory(complete.Output,requireMutationTarget:false).Count);
    }
}
