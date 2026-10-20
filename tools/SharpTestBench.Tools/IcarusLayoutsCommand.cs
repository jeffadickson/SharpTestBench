using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SharpTestBench.Tools;

internal sealed class OwnedDirectory : IDisposable
{
    readonly string parent;
    public string Path { get; }
    public OwnedDirectory(string parent, string prefix)
    {
        this.parent = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(parent)); Directory.CreateDirectory(this.parent);
        Path = System.IO.Path.Combine(this.parent, prefix + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path);
    }
    public void Dispose()
    {
        if (System.IO.Path.GetDirectoryName(Path) != parent) throw new IOException("Temporary directory escaped its parent");
        Directory.Delete(Path, true);
    }
}
internal static class IcarusLayoutsCommand
{
    [DllImport("kernel32.dll")] static extern uint SetErrorMode(uint mode);
    public static string[] TargetFlags(string platform, string compiler)
    {
        string[] profile = ["-std=c++20", "-O3", "-flto", .. (compiler.Contains("clang", StringComparison.OrdinalIgnoreCase) ? new[] { "-fvectorize", "-fslp-vectorize" } : ["-ftree-loop-vectorize", "-ftree-slp-vectorize"])];
        return platform switch { "darwin" => [.. profile, "-bundle", "-fPIC", "-undefined", "dynamic_lookup"], "win32" => [.. profile, "-shared", "-static"], "linux" => [.. profile, "-shared", "-fPIC"], _ => throw new PlatformNotSupportedException("Unsupported Icarus target platform") };
    }
    static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file, System.IO.Path.Combine(target, System.IO.Path.GetFileName(file)), true);
        foreach (var directory in Directory.EnumerateDirectories(source)) CopyDirectory(directory, System.IO.Path.Combine(target, System.IO.Path.GetFileName(directory)));
    }
    public static JsonObject Export(ToolContext context, string[] sources, string top, string output, string baseDirectory, string include)
    {
        if (OperatingSystem.IsWindows()) _ = SetErrorMode(0x0001 | 0x0002 | 0x8000);
        sources = sources.Select(RunnerManifestCommand.RealPath).ToArray(); output = RunnerManifestCommand.RealPath(output); baseDirectory = RunnerManifestCommand.RealPath(baseDirectory); include = RunnerManifestCommand.RealPath(include);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output)!);
        using var temporary = new OwnedDirectory(context.At("build/temporary"), "stb-layouts-"); var stage = temporary.Path;
        foreach (var entry in Directory.EnumerateFileSystemEntries(baseDirectory))
        {
            var destination = System.IO.Path.Combine(stage, System.IO.Path.GetFileName(entry));
            if (OperatingSystem.IsWindows()) { if (Directory.Exists(entry)) CopyDirectory(entry, destination); else File.Copy(entry, destination, true); }
            else if (Directory.Exists(entry)) Directory.CreateSymbolicLink(destination, entry); else File.CreateSymbolicLink(destination, entry);
        }
        var helper = System.IO.Path.Combine(stage, "stb_layouts.tgt"); var compiler = Environment.GetEnvironmentVariable("CXX") ?? (OperatingSystem.IsMacOS() ? "clang++" : "g++");
        var version = context.Run([compiler, "--version"], capture: true).Output.Split('\n')[0].TrimEnd('\r');
        var platform = OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsMacOS() ? "darwin" : "linux";
        var flags = TargetFlags(platform, version); var imports = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            var definition = System.IO.Path.Combine(stage, "stb_ivl.def");
            var names = Regex.Matches(File.ReadAllText(context.At("native/icarus_layouts.cpp")), @"\b(ivl_\w+)\s*\(").Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
            File.WriteAllText(definition, "LIBRARY ivl.exe\nEXPORTS\n" + string.Join("\n", names) + "\n");
            var library = System.IO.Path.Combine(stage, "stb_ivl.a"); var machine = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "i386:x86-64";
            context.Run([Environment.GetEnvironmentVariable("DLLTOOL") ?? "dlltool", "-m", machine, "-d", definition, "-l", library], timeout: TimeSpan.FromSeconds(60)); imports.Add(library);
        }
        context.Run([compiler, .. flags, "-Wall", "-Wextra", "-Werror", "-I", include, context.At("native/icarus_layouts.cpp"), .. imports, "-o", helper], timeout: TimeSpan.FromSeconds(120));
        File.WriteAllText(System.IO.Path.Combine(stage, "stb_layouts.conf"), "flag:DLL=" + helper.Replace('\\', '/') + "\n");
        var raw = System.IO.Path.Combine(stage, "layouts.json");
        context.Run([Environment.GetEnvironmentVariable("CS_IVERILOG") ?? "iverilog", "-B", stage, "-g2012", "-s", top, "-t", "stb_layouts", "-o", raw, .. sources], timeout: TimeSpan.FromSeconds(120));
        using var document = StrictJson.Read(raw); var data = JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
        if (data["schema"]?.GetValue<int>() != 1 || data["signals"] is not JsonArray signals || signals.Count == 0) throw new InvalidDataException("No packed metadata exported");
        var paths = signals.Select(row => row!["path"]!.ToJsonString()).ToArray();
        if (paths.Distinct(StringComparer.Ordinal).Count() != paths.Length) throw new InvalidDataException("Ambiguous exported paths");
        data["provenance"] = JsonSerializer.SerializeToNode(new { top, sources = sources.ToDictionary(path => path, CiCommands.Hash), helper_sha256 = CiCommands.Hash(context.At("native/icarus_layouts.cpp")), header_sha256 = CiCommands.Hash(System.IO.Path.Combine(include, "ivl_target.h")), compiler_profile = flags, compiler = version, platform, architecture = RuntimeInformation.ProcessArchitecture.ToString(), limitation = "Separate elaboration; not authenticated against a simulation binary. No automatic runtime import." });
        StrictJson.Write(output, data); return data;
    }
    public static void Check(ToolContext context, string baseDirectory, string include)
    {
        var work = context.At("build/icarus-layout-check"); Directory.CreateDirectory(work);
        var source = context.At("tests/Integration/top.sv"); var metadata = System.IO.Path.Combine(work, "layouts.json");
        var data = Export(context, [source], "top", metadata, baseDirectory, include);
        var signals = data["signals"]!.AsArray();
        JsonNode Find(string name) => signals.Single(row => row!["path"]!.ToJsonString() == JsonSerializer.Serialize(new[] { "top", name }))!;
        void Dimensions(string name, int[][] expected) { if (!JsonNode.DeepEquals(Find(name)["dimensions"], JsonSerializer.SerializeToNode(expected))) throw new InvalidDataException("Unexpected elaborated dimensions: " + name); }
        Dimensions("packed_matrix", [[1, 0], [2, 0], [7, 0]]); Dimensions("packed_mixed", [[-1, 0], [3, 1], [7, 0]]); Dimensions("packed_packet", [[15, 0]]);
        context.Run(["dotnet", "build", context.At("tests/LayoutMetadata"), "-c", "Release"]);
        void Run(string label, string path, string? marker = null)
        {
            var folder = System.IO.Path.Combine(work, label); Directory.CreateDirectory(folder);
            var result = context.Run(["dotnet", typeof(ToolContext).Assembly.Location, "run", "--no-build", "--assembly", context.At("tests/LayoutMetadata/bin/Release/net10.0/LayoutMetadata.dll"), "--entry-type", "Entry, LayoutMetadata", "--source", source, "--top", "top", "--work-dir", folder], environment: new Dictionary<string, string?> { ["STB_LAYOUT_METADATA"] = path }, capture: true, check: false, timeout: TimeSpan.FromSeconds(120));
            var output = result.Output + result.Error; File.WriteAllText(System.IO.Path.Combine(folder, "driver.log"), output);
            if (marker is null ? result.ExitCode != 0 || !output.Contains("CS_SUMMARY 1 passed 0 failed", StringComparison.Ordinal) : result.ExitCode == 0 || !output.Contains(marker, StringComparison.Ordinal)) throw new InvalidDataException("Elaboration metadata check failed: " + output);
        }
        Run("valid", metadata);
        var dimensions = Find("packed_mixed")["dimensions"]!.AsArray(); var first = dimensions[0]!.AsArray(); var reversed = first.Select(item => item!.GetValue<int>()).Reverse().ToArray(); dimensions[0] = JsonSerializer.SerializeToNode(reversed);
        var wrong = System.IO.Path.Combine(work, "wrong-order.json"); StrictJson.Write(wrong, data); Run("wrong-order", wrong, "LAYOUT_SELECTION_MISMATCH");
        using var original = StrictJson.Read(metadata); data = JsonNode.Parse(original.RootElement.GetRawText())!.AsObject(); data["provenance"]!["sources"]![source] = new string('0', 64);
        var stale = System.IO.Path.Combine(work, "stale.json"); StrictJson.Write(stale, data); Run("stale", stale, "LAYOUT_SOURCE_CHANGED"); Console.WriteLine("ICARUS_LAYOUT_PASS");
    }
    public static void Execute(ToolContext context, string name, string[] arguments)
    {
        var options = new Options(arguments);
        if (name == "icarus-layouts-check") { options.Only("--ivl-root", "--include"); Check(context, options.Require("--ivl-root"), options.Require("--include")); }
        else { options.Only("--source", "--top", "--output", "--ivl-root", "--include"); var sources = options.All("--source"); if (sources.Length == 0) throw new ArgumentException("At least one source required"); var data = Export(context, sources, options.Require("--top"), options.Require("--output"), options.Require("--ivl-root"), options.Require("--include")); Console.WriteLine("PACKED_LAYOUTS_EXPORTED " + data["signals"]!.AsArray().Count); }
    }
}
