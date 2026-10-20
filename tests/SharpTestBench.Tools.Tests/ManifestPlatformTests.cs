using SharpTestBench.Tools;
using Xunit;

public sealed class ManifestPlatformTests
{
    [Theory]
    [InlineData("counter")]
    [InlineData("dsp")]
    [InlineData("cpu")]
    public void ManifestUsesTheCurrentPlatformHost(string fixture)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "manifest paths with spaces", Guid.NewGuid().ToString("N"));
        var adapter = new Dictionary<string, object> { ["bootstrap"] = Path.Combine(root, "bootstrap.vpi"), ["library"] = Path.Combine(root, "gpi.dll"), ["entryPoint"] = "entry", ["implementation"] = "library:entry", ["dotnetRoot"] = root };
        var manifest = RunnerManifestCommand.Make(root, fixture, ulong.MaxValue, adapter, "build/parity output");
        var expected = OperatingSystem.IsWindows() ? "sharptestbench_host.dll" : OperatingSystem.IsMacOS() ? "libsharptestbench_host.dylib" : "libsharptestbench_host.so";
        Assert.Equal(Path.Combine(root, "build", expected), manifest["nativeHost"]);
        Assert.Equal(ulong.MaxValue, manifest["seed"]);
        Assert.Equal(Path.Combine(root, "build", "parity output"), manifest["outputDirectory"]);
    }
}
