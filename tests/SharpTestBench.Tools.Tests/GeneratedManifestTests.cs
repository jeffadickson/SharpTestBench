using SharpTestBench.Tools;
using Xunit;

public sealed class GeneratedManifestTests
{
    [Theory]
    [InlineData(9007199254740993UL)]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(17UL)]
    [InlineData(99UL)]
    [InlineData(4294967296UL)]
    [InlineData(18446744073709551615UL)]
    [InlineData(9223372036854775808UL)]
    [InlineData(65535UL)]
    [InlineData(65536UL)]
    [InlineData(2147483648UL)]
    public void GeneratedMetadataPreservesFullWidthSeedAndNestedKeys(ulong seed)
    {
        var json = "{\"schemaVersion\":1,\"simulator\":\"icarus\",\"top\":\"top\",\"sources\":[\"a.sv\"],\"assembly\":\"a.dll\",\"nativeHost\":\"host.so\",\"icarusBootstrap\":\"boot.vpi\",\"gpiLibrary\":\"gpi.so\",\"dotnetRoot\":\"sdk\",\"outputDirectory\":\"out\",\"defines\":{\"FOO\":\"1\",\"foo\":\"2\"},\"seed\":" + seed.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
        var manifest = JsonInput.Manifest(json);
        Assert.Equal(seed, manifest.Seed); Assert.Equal(2, manifest.Defines!.Count);
        var serialized = System.Text.Json.JsonSerializer.Serialize(manifest, ManifestJsonContext.Default.RunManifest);
        var restored = JsonInput.Manifest(serialized);
        Assert.Equal(seed, restored.Seed); Assert.Equal(manifest.Sources, restored.Sources);
        Assert.Equal("1", restored.Defines!["FOO"]); Assert.Equal("2", restored.Defines["foo"]);
    }
}
