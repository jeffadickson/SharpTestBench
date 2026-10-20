using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;
public sealed class InstalledPackageTests
{
    [Theory]
    [InlineData("rid")]
    [InlineData("checksum")]
    [InlineData("parent")]
    [InlineData("backslash")]
    [InlineData("unverified")]
    [InlineData("missing")]
    public void InvalidNativeEvidenceFailsBeforeRunning(string corruption)
    {
        var directory = Path.Combine(Path.GetTempPath(), "stb-package-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            foreach (var name in new[] { "host.so", "gpi.so", "impl.so", "bootstrap.so" }) File.WriteAllText(Path.Combine(directory, name), "fixture " + name);
            var hashes = Directory.EnumerateFiles(directory).ToDictionary(path => Path.GetFileName(path), CiCommands.Hash);
            var manifest = JsonSerializer.SerializeToNode(new { rid = "linux-x64", host = "host.so", gpi = "gpi.so", implementation = "impl.so", bootstrap = "bootstrap.so", sha256 = hashes })!;
            var path = Path.Combine(directory, "native-manifest.json"); File.WriteAllText(path, manifest.ToJsonString());
            Assert.Equal("host.so", InstalledRunCommand.Verify(directory, "linux-x64").GetProperty("host").GetString());
            switch (corruption)
            {
                case "rid": manifest["rid"] = "wrong-rid"; break;
                case "checksum": manifest["sha256"]!["host.so"] = new string('0', 64); break;
                case "parent": manifest["host"] = "../host.so"; break;
                case "backslash": manifest["host"] = "..\\host.so"; break;
                case "unverified": manifest["sha256"]!.AsObject().Remove("host.so"); break;
                case "missing": File.Delete(Path.Combine(directory, "host.so")); break;
            }
            File.WriteAllText(path, manifest.ToJsonString());
            Assert.Throws<InvalidDataException>(() => InstalledRunCommand.Verify(directory, "linux-x64"));
        }
        finally { foreach (var path in Directory.EnumerateFiles(directory)) File.Delete(path); Directory.Delete(directory); }
    }
    [Fact]
    public void FakeNativeSourceUsesCurrentAbiAndClockFixture()
    {
        var source = NativeFaultsCommand.FakeSource("CS_EXPORT int cs_v1_gpi_get_num_elems(void *signal);\nCS_EXPORT void cs_v1_gpi_set_signal_value_int(void *signal, int value);\n", "/*clock-fixture*/\n");
        Assert.Contains("/*clock-fixture*/", source); Assert.Contains("getenv(\"CS_FAKE_WIDTH\")", source); Assert.Contains("++fake_write_count", source);
        Assert.Throws<InvalidDataException>(() => NativeFaultsCommand.FakeSource("", ""));
    }
}
