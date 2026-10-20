using SharpTestBench.Tools;
using Xunit;

public sealed class SelfInvocationTests
{
    [Theory]
    [InlineData("/worker/SharpTestBench.Tools", "/worker/SharpTestBench.Tools.dll", true)]
    [InlineData("/sdk/dotnet", "/worker/SharpTestBench.Tools.dll", false)]
    [InlineData("/different/SharpTestBench.Tools", "/worker/SharpTestBench.Tools.dll", false)]
    [InlineData("/worker/SharpTestBench.Tools.Tests", "/worker/SharpTestBench.Tools.dll", false)]
    public void ApphostSelectionPreservesManagedFallback(string executable, string assembly, bool native)
    {
        if (native && OperatingSystem.IsWindows()) executable += ".exe";
        var prefix = SelfInvocation.Prefix(executable, assembly);
        Assert.Equal(native ? new[] { executable } : new[] { "dotnet", assembly }, prefix);
    }
}
