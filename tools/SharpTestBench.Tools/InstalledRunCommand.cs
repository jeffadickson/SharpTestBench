using System.Text.Json;
namespace SharpTestBench.Tools;
internal static class InstalledRunCommand
{
    public static JsonElement Verify(string native, string rid)
    {
        using var document = StrictJson.Read(Path.Combine(native, "native-manifest.json")); var manifest = document.RootElement;
        if (!manifest.TryGetProperty("rid", out var actual) || actual.GetString() != rid) throw new InvalidDataException("Native package RID mismatch: expected " + rid);
        var hashes = manifest.GetProperty("sha256");
        static bool Simple(string name) => name.Length > 0 && name is not ("." or "..") && name.IndexOfAny(['/', '\\', ':']) < 0 && Path.GetFileName(name) == name;
        foreach (var field in new[] { "host", "gpi", "implementation", "bootstrap" })
        {
            var name = manifest.GetProperty(field).GetString()!;
            if (!Simple(name) || !hashes.TryGetProperty(name, out _)) throw new InvalidDataException("Unverified native manifest reference: " + field);
        }
        foreach (var entry in hashes.EnumerateObject())
        {
            var path = Path.Combine(native, entry.Name);
            if (!Simple(entry.Name) || new FileInfo(path).LinkTarget is not null || !File.Exists(path) || CiCommands.Hash(path) != entry.Value.GetString()) throw new InvalidDataException("Native package checksum mismatch: " + entry.Name);
        }
        return manifest.Clone();
    }
    public static void Execute(string[] arguments, ICommandRunner? runner = null)
    {
        var options = new Options(arguments); options.Only("--native-dir", "--assembly", "--source", "--top", "--entry-type", "--work-dir");
        var native = Path.GetFullPath(options.Require("--native-dir")); var assembly = Path.GetFullPath(options.Require("--assembly")); var source = Path.GetFullPath(options.Require("--source")); var top = options.Require("--top"); var entry = options.Require("--entry-type"); var work = Path.GetFullPath(options.Require("--work-dir"));
        var manifest = Verify(native, NativeBuildCommand.Rid); using var workspace = new WorkspaceLock(work); var context = new ToolContext(work, runner);
        var sdk = NativeBuildCommand.SdkRoot();
        var environment = new Dictionary<string, string?> { ["CS_HOSTFXR"] = NativeBuildCommand.HostFxr(sdk), ["CS_CONFIG"] = Path.ChangeExtension(assembly, ".runtimeconfig.json"), ["CS_ASSEMBLY"] = assembly, ["CS_ENTRY_TYPE"] = entry, ["CS_TOPLEVEL"] = top, ["CS_RESULTS"] = Path.Combine(work, "results.json"), ["CS_TRACE"] = Path.Combine(work, "trace.txt"), ["DOTNET_ROOT"] = sdk, ["CS_GPI_LIBRARY"] = Path.Combine(native, manifest.GetProperty("gpi").GetString()!), ["GPI_IMPL"] = Path.Combine(native, manifest.GetProperty("implementation").GetString()!) + ":" + manifest.GetProperty("implementation_entry").GetString(), ["COCOTB_BOOTSTRAP"] = string.Join(Path.PathSeparator, Path.Combine(native, manifest.GetProperty("gpi").GetString()!) + "," + manifest.GetProperty("gpi_entry").GetString(), Path.Combine(native, manifest.GetProperty("host").GetString()!) + ",sharptestbench_initialize") };
        var loader = NativeBuildCommand.LoaderPath; environment[loader] = native + (OperatingSystem.IsWindows() ? Path.PathSeparator + Environment.GetEnvironmentVariable(loader) : "");
        var ivl = Environment.GetEnvironmentVariable("CS_IVL_ROOT");
        context.Run([Environment.GetEnvironmentVariable("CS_IVERILOG") ?? "iverilog", .. (ivl is null ? Array.Empty<string>() : ["-B", ivl]), "-g2012", "-s", top, "-o", Path.Combine(work, "top.vvp"), source]);
        var report = Path.Combine(work, "results.json"); File.Delete(report);
        var result = context.Run([Environment.GetEnvironmentVariable("CS_VVP") ?? "vvp", "-m", Path.Combine(native, manifest.GetProperty("bootstrap").GetString()!), Path.Combine(work, "top.vvp")], environment: environment, capture: true, check: false, timeout: TimeSpan.FromSeconds(120));
        File.WriteAllText(Path.Combine(work, "run.log"), result.Output + result.Error); Console.WriteLine(result.Output + result.Error);
        if (result.ExitCode != 0 || !File.Exists(report)) throw new InvalidDataException("Installed simulator run failed");
        using var rows = StrictJson.Read(report);
        if (rows.RootElement.GetArrayLength() == 0 || rows.RootElement.EnumerateArray().Any(row => row.GetProperty("Status").GetString() != "passed")) throw new InvalidDataException("Installed consumer tests failed");
    }
}
