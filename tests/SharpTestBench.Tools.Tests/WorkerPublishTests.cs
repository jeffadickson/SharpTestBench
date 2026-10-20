using SharpTestBench.Tools;
using Xunit;

public sealed class WorkerPublishTests
{
    [Theory]
    [InlineData("linux-x64")]
    [InlineData("linux-arm64")]
    [InlineData("win-x64")]
    [InlineData("win-arm64")]
    [InlineData("osx-x64")]
    [InlineData("osx-arm64")]
    public void NativeProfilesAreExplicitAndHostMatched(string rid)
    {
        var root = Path.Combine(Path.GetTempPath(), "worker plan with spaces");
        var plans = WorkerPublishCommand.Plan(root, "auto", "all", rid);
        Assert.Equal(2, plans.Length);
        Assert.Equal("self-contained-readytorun", plans[0].Deployment);
        Assert.Contains("-p:PublishReadyToRun=true", plans[0].Command);
        Assert.Contains("-p:PublishTrimmed=false", plans[0].Command);
        Assert.Equal("native-aot", plans[1].Deployment);
        Assert.Contains("-p:PublishAot=true", plans[1].Command);
        Assert.All(plans, plan => { Assert.Equal(rid, plan.Rid); Assert.Contains("--self-contained", plan.Command); Assert.Equal(Path.Combine(root, "build", "workers", rid, plan.Name), plan.Directory); });
    }
}
