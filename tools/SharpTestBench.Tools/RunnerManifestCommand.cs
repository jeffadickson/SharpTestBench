using System.Text.Json;

namespace SharpTestBench.Tools;

internal sealed record FixtureDefinition(string Project, string Assembly, string Source, string Top, string Test);
internal static class RunnerManifestCommand
{
    public static readonly IReadOnlyDictionary<string, FixtureDefinition> Fixtures = new Dictionary<string, FixtureDefinition>(StringComparer.Ordinal) {
        ["counter"] = new("examples/Counter", "Counter", "counter.sv", "counter", "Entry.ResetAndCount"),
        ["dsp"] = new("examples/DspFir", "DspFir", "dsp_fir.sv", "dsp_fir", "Entry.FirScoreboard"),
        ["cpu"] = new("examples/CpuCore", "CpuCore", "cpu_core.sv", "cpu_core", "Entry.CpuScoreboard") };
    public static string RealPath(string path)
    {
        path = Path.GetFullPath(path); var root = Path.GetPathRoot(path)!; var current = root;
        foreach (var part in path[root.Length..].Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries).Where(part => part.Length != 0))
        {
            current = Path.Combine(current, part);
            FileSystemInfo info = File.Exists(current) ? new FileInfo(current) : new DirectoryInfo(current);
            if (info.LinkTarget is not null) current = info.ResolveLinkTarget(true)?.FullName ?? throw new IOException("Unresolved link: " + current);
        }
        return current;
    }
    public static Dictionary<string, object> InstalledAdapter(ToolContext context)
    {
        var adapter = CocotbAdapter.Inspect(context); var entry = adapter.NativeEntry();
        return new() { ["bootstrap"] = adapter.Bootstrap, ["library"] = entry.Library, ["entryPoint"] = entry.Entry, ["implementation"] = adapter.Implementation ?? throw new InvalidDataException("Missing GPI implementation"), ["dotnetRoot"] = Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? "/usr/share/dotnet" };
    }
    public static Dictionary<string, object> Make(string root, string fixture, ulong seed, IReadOnlyDictionary<string, object> adapter, string outputDirectory)
    {
        root = RealPath(root);
        if (!Fixtures.TryGetValue(fixture, out var item)) throw new InvalidDataException("Unknown parity fixture");
        foreach (var key in new[] { "bootstrap", "library", "entryPoint", "implementation", "dotnetRoot" }) if (!adapter.TryGetValue(key, out var value) || value is not string text || string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Missing adapter setting: " + key);
        var output = RealPath(Path.GetFullPath(outputDirectory, root)); var build = RealPath(Path.Combine(root, "build")); var relative = Path.GetRelativePath(build, output);
        if (relative == "." || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative)) throw new InvalidDataException("Parity output must be a directory below build/");
        return new() {
            ["schemaVersion"] = 1, ["simulator"] = "icarus", ["top"] = item.Top,
            ["sources"] = new[] { Path.Combine(root, item.Project, item.Source) },
            ["assembly"] = Path.Combine(root, item.Project, "bin/Release/net10.0/" + item.Assembly + ".dll"),
            ["nativeHost"] = Path.Combine(root, "build/libsharptestbench_host.so"), ["icarusBootstrap"] = RealPath((string)adapter["bootstrap"]), ["gpiLibrary"] = RealPath((string)adapter["library"]),
            ["gpiEntryPoint"] = (string)adapter["entryPoint"], ["gpiImpl"] = (string)adapter["implementation"], ["dotnetRoot"] = (string)adapter["dotnetRoot"],
            ["outputDirectory"] = output, ["seed"] = seed, ["entryType"] = "Entry, " + item.Assembly, ["expectedTests"] = 1, ["expectedNames"] = new[] { item.Test } };
    }
    public static void Execute(ToolContext context, string[] arguments)
    {
        var options = new Options(arguments); options.Only("--fixture", "--seed", "--output", "--output-directory");
        if (!ulong.TryParse(options.Get("--seed", "1"), out var seed)) throw new ArgumentException("Invalid seed");
        StrictJson.Write(Path.GetFullPath(options.Require("--output")), Make(context.Root, options.Require("--fixture"), seed, InstalledAdapter(context), options.Require("--output-directory")));
    }
}
