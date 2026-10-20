using SharpTestBench.Tools;
using Xunit;

public sealed class WorkerInvocationBoundaryTests
{
    [Theory]
    [InlineData("worker", "SharpTestBench.Tools", true, true)]
    [InlineData("worker", "SharpTestBench.Tools", false, false)]
    [InlineData("worker", "SharpTestBench.Tools.Tests", true, false)]
    [InlineData("worker", "dotnet", true, false)]
    [InlineData("worker", "OtherWorker", true, false)]
    [InlineData("worker", "SharpTestBench.Tools-copy", true, false)]
    [InlineData("worker with spaces", "SharpTestBench.Tools", true, true)]
    [InlineData("worker with spaces", "SharpTestBench.Tools", false, false)]
    [InlineData("worker with spaces", "SharpTestBench.Tools.Tests", true, false)]
    [InlineData("worker with spaces", "dotnet", true, false)]
    [InlineData("worker with spaces", "OtherWorker", true, false)]
    [InlineData("worker with spaces", "SharpTestBench.Tools-copy", true, false)]
    [InlineData("worker-long-name", "SharpTestBench.Tools", true, true)]
    [InlineData("worker-long-name", "SharpTestBench.Tools", false, false)]
    [InlineData("worker-long-name", "SharpTestBench.Tools.Tests", true, false)]
    [InlineData("worker-long-name", "dotnet", true, false)]
    [InlineData("worker-long-name", "OtherWorker", true, false)]
    [InlineData("worker-long-name", "SharpTestBench.Tools-copy", true, false)]
    [InlineData("worker.unicode", "SharpTestBench.Tools", true, true)]
    [InlineData("worker.unicode", "SharpTestBench.Tools", false, false)]
    [InlineData("worker.unicode", "SharpTestBench.Tools.Tests", true, false)]
    [InlineData("worker.unicode", "dotnet", true, false)]
    [InlineData("worker.unicode", "OtherWorker", true, false)]
    [InlineData("worker.unicode", "SharpTestBench.Tools-copy", true, false)]
    public void ActualPlatformNamesAndDirectoriesControlInvocation(string folder, string name, bool sameDirectory, bool expected)
    {
        var root = Path.Combine(Path.GetTempPath(), folder);
        var executable = Path.Combine(sameDirectory ? root : Path.Combine(root, "other"), name + (OperatingSystem.IsWindows() ? ".exe" : ""));
        var assembly = Path.Combine(root, "SharpTestBench.Tools.dll");
        Assert.Equal(expected ? new[] { executable } : new[] { "dotnet", assembly }, SelfInvocation.Prefix(executable, assembly));
    }
}
