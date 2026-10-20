using SharpTestBench.Tools;
using Xunit;
public sealed class DependencyTests
{
    sealed class OutputRunner(Func<IReadOnlyList<string>, string> output) : ICommandRunner
    {
        public CommandResult Run(IReadOnlyList<string> arguments, string directory, IReadOnlyDictionary<string, string?>? environment = null, bool capture = false, bool check = true, TimeSpan? timeout = null) => new(0, output(arguments), "");
    }
    static void Fixture(string name, Action<string> action)
    {
        var folder = Path.Combine(Path.GetTempPath(), "stb-deps-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, name), "");
        try { action(folder); } finally { File.Delete(Path.Combine(folder, name)); Directory.Delete(folder); }
    }
    [Fact]
    public void MachOIdentityIsNotAnImport() => Fixture("host.dylib", folder =>
    {
        var host = Path.Combine(folder, "host.dylib"); var context = new ToolContext(folder, new OutputRunner(args => args[1] == "-D" ? host + ":\n/tmp/123-host.dylib\n" : host + ":\n\t/tmp/123-host.dylib (compatibility version 0.0.0)\n\t/usr/lib/libSystem.B.dylib (compatibility version 1.0.0)\n"));
        var result = System.Text.Json.JsonSerializer.SerializeToElement(NativeChecks.Dependencies(context, folder, platform: "darwin")); Assert.True(result.GetProperty("imports").TryGetProperty("host.dylib", out _));
    });
    [Fact]
    public void MissingMachOImportFails() => Fixture("host.dylib", folder =>
    {
        var context = new ToolContext(folder, new OutputRunner(args => args[1] == "-D" ? "host:\n@rpath/host.dylib\n" : "host:\n\t@rpath/host.dylib (compatibility version 0.0.0)\n\t@rpath/missing.dylib (compatibility version 1.0.0)\n"));
        Assert.Contains("missing.dylib", Assert.Throws<InvalidDataException>(() => NativeChecks.Dependencies(context, folder, platform: "darwin")).Message);
    });
    [Fact]
    public void WindowsSimulatorImportRequiresDeclaration() => Fixture("icarus.dll", folder =>
    {
        var context = new ToolContext(folder, new OutputRunner(_ => "DLL Name: vvp.exe\nDLL Name: KERNEL32.dll\n"));
        Assert.Contains("vvp.exe", Assert.Throws<InvalidDataException>(() => NativeChecks.Dependencies(context, folder, platform: "win32")).Message);
        var result = System.Text.Json.JsonSerializer.SerializeToElement(NativeChecks.Dependencies(context, folder, ["vvp.exe"], "win32")); Assert.Equal("vvp.exe", result.GetProperty("simulator_hosts")[0].GetString());
    });
    [Fact]
    public void DeclaredSimulatorDoesNotAllowMissingSupportLibrary() => Fixture("icarus.dll", folder =>
    {
        var context = new ToolContext(folder, new OutputRunner(_ => "DLL Name: vvp.exe\nDLL Name: missing.dll\n")); Assert.Contains("missing.dll", Assert.Throws<InvalidDataException>(() => NativeChecks.Dependencies(context, folder, ["vvp.exe"], "win32")).Message);
    });
}
