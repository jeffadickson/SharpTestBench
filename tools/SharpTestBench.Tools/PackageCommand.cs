using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
namespace SharpTestBench.Tools;
internal static class PackageCommand
{
    static readonly string[] ConsumerSources = ["Tests.cs", "counter.sv", "Counter.stb.xml"];
    static void Project(string path, Dictionary<string, string> properties, IEnumerable<XElement> items) => new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"), new XElement("PropertyGroup", properties.Select(pair => new XElement(pair.Key, pair.Value))), new XElement("ItemGroup", items))).Save(path);
    public static void ConsumerProject(string path, string version, string rid, string cache)
    {
        Project(path, new() { ["TargetFramework"] = "net10.0", ["ImplicitUsings"] = "enable", ["Nullable"] = "enable", ["GenerateRuntimeConfigurationFiles"] = "true", ["RestorePackagesPath"] = cache }, new[] { "SharpTestBench", "SharpTestBench.Generators", "SharpTestBench.Native." + rid }.Select(name => new XElement("PackageReference", new XAttribute("Include", name), new XAttribute("Version", version))).Append(new XElement("AdditionalFiles", new XAttribute("Include", "Counter.stb.xml"))));
    }
    public static void Execute(ToolContext context, string[] arguments)
    {
        if (arguments.Length != 0) throw new ArgumentException("native-package accepts no arguments");
        var rid = NativeBuildCommand.Rid; var version = XDocument.Load(context.At("Directory.Build.props")).Descendants("Version").Single().Value;
        var build = context.At("build"); var feed = Path.Combine(build, "packages-" + rid); Directory.CreateDirectory(feed);
        var host = NativeBuildCommand.Build(context);
        foreach (var name in new[] { "SharpTestBench", "SharpTestBench.Generators" }) context.Run(["dotnet", "pack", context.At("src/" + name + "/" + name + ".csproj"), "-c", "Release", "-o", feed, "-p:RestoreLockedMode=true", "--disable-build-servers", "-m:1"]);
        var stage = Path.Combine(build, "native-package-" + rid); var native = Path.Combine(stage, "native"); Directory.CreateDirectory(native);
        foreach (var old in Directory.EnumerateFiles(native)) File.Delete(old);
        var adapter = CocotbAdapter.Inspect(context);
        var gpi = adapter.NativeEntry(); var implementation = adapter.ImplementationEntry();
        foreach (var source in new[] { host, gpi.Library, implementation.Library, adapter.Bootstrap }) File.Copy(source, Path.Combine(native, Path.GetFileName(source)), true);
        var bootstrap = Path.GetFileName(adapter.Bootstrap);
        if (OperatingSystem.IsWindows()) { bootstrap = Path.GetFileNameWithoutExtension(bootstrap) + ".vpi"; File.Copy(adapter.Bootstrap, Path.Combine(native, bootstrap), true); }
        var names = new[] { Path.GetFileName(host), Path.GetFileName(gpi.Library), Path.GetFileName(implementation.Library), Path.GetFileName(adapter.Bootstrap), bootstrap }.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var dependencies = NativeChecks.Dependencies(context, native, OperatingSystem.IsWindows() ? ["vvp.exe"] : []); StrictJson.Write(Path.Combine(stage, "dependencies.json"), dependencies);
        var revision = context.Run(["git", "rev-parse", "HEAD"], capture: true).Output.Trim(); var dirty = context.Run(["git", "status", "--porcelain", "--untracked-files=no"], capture: true).Output.Length != 0;
        using var upstream = StrictJson.Read(context.At("third_party/UPSTREAM.json"));
        var manifest = new { schema = 1, package_version = version, source_revision = revision, source_dirty = dirty, dependencies, rid, host = NativeBuildCommand.HostName, gpi = Path.GetFileName(gpi.Library), gpi_entry = gpi.Entry, implementation = Path.GetFileName(implementation.Library), implementation_entry = implementation.Entry, bootstrap, upstream = upstream.RootElement.Clone(), sha256 = names.ToDictionary(name => name, name => CiCommands.Hash(Path.Combine(native, name))) };
        StrictJson.Write(Path.Combine(native, "native-manifest.json"), manifest);
        var packageId = "SharpTestBench.Native." + rid;
        XElement Pack(string source, string destination) => new("None", new XAttribute("Include", source), new XAttribute("Pack", "true"), new XAttribute("PackagePath", destination));
        var helperFolder = Path.GetDirectoryName(typeof(ToolContext).Assembly.Location)!;
        var helpers = new[] { "SharpTestBench.Tools.dll", "SharpTestBench.Tools.deps.json", "SharpTestBench.Tools.runtimeconfig.json" };
        var items = new List<XElement> { Pack(Path.Combine(native, "*"), "runtimes/" + rid + "/native/"), Pack(context.At("LICENSE"), "licenses/"), Pack(context.At("third_party/COCOTB-LICENSE"), "licenses/"), Pack(context.At("docs/PACKAGING.md"), "/") };
        items.AddRange(helpers.Select(name => Pack(Path.Combine(helperFolder, name), "tools/")));
        Project(Path.Combine(stage, "Native.csproj"), new() { ["TargetFramework"] = "net10.0", ["PackageId"] = packageId, ["Version"] = version, ["IncludeBuildOutput"] = "false", ["SuppressDependenciesWhenPacking"] = "true", ["PackageLicenseExpression"] = "MIT AND BSD-3-Clause", ["PackageReadmeFile"] = "PACKAGING.md", ["RepositoryUrl"] = "https://github.com/jeffadickson/SharpTestBench", ["RepositoryCommit"] = revision, ["NoWarn"] = "NU5128" }, items);
        context.Run(["dotnet", "pack", Path.Combine(stage, "Native.csproj"), "-c", "Release", "-o", feed, "--disable-build-servers", "-m:1"]);
        var temporary = Path.Combine(Path.GetTempPath(), "sharptestbench-consumer-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporary);
        var evidence = Path.Combine(build, "package-evidence-" + rid); Directory.CreateDirectory(evidence);
        try
        {
            foreach (var name in ConsumerSources) File.Copy(context.At("examples/Counter/" + name), Path.Combine(temporary, name));
            File.Copy(context.At("global.json"), Path.Combine(temporary, "global.json"));
            new XDocument(new XElement("configuration", new XElement("packageSources", new XElement("clear"), new XElement("add", new XAttribute("key", "local"), new XAttribute("value", feed))))).Save(Path.Combine(temporary, "NuGet.Config"));
            ConsumerProject(Path.Combine(temporary, "Counter.csproj"), version, rid, Path.Combine(temporary, "cache"));
            context.Run(["dotnet", "build", Path.Combine(temporary, "Counter.csproj"), "-c", "Release", "--disable-build-servers", "-m:1"], directory: temporary);
            var installed = Path.Combine(temporary, "cache", packageId.ToLowerInvariant(), version); var installedNative = Path.Combine(installed, "runtimes", rid, "native");
            string[] Command(string work) => ["dotnet", Path.Combine(installed, "tools/SharpTestBench.Tools.dll"), "installed-run", "--native-dir", installedNative, "--assembly", Path.Combine(temporary, "bin/Release/net10.0/Counter.dll"), "--source", Path.Combine(temporary, "counter.sv"), "--top", "counter", "--entry-type", "Entry, Counter", "--work-dir", Path.Combine(temporary, work)];
            context.Run(Command("simulation"), directory: temporary);
            var manifestPath = Path.Combine(installedNative, "native-manifest.json"); var original = File.ReadAllText(manifestPath); var checks = new List<string>();
            foreach (var (kind, expected) in new[] { ("rid", "Native package RID mismatch"), ("checksum", "Native package checksum mismatch") })
            {
                var altered = JsonNode.Parse(original)!; if (kind == "rid") altered["rid"] = "unsupported-rid"; else altered["sha256"]![altered["host"]!.GetValue<string>()] = new string('0', 64);
                File.WriteAllText(manifestPath, altered.ToJsonString());
                try { var bad = context.Run(Command("negative"), directory: temporary, capture: true, check: false); if (bad.ExitCode == 0 || !(bad.Output + bad.Error).Contains(expected, StringComparison.Ordinal)) throw new InvalidDataException("Package negative check failed: " + kind); checks.Add(kind); }
                finally { File.WriteAllText(manifestPath, original); }
            }
            foreach (var name in new[] { "results.json", "results.xml", "run.log" }) File.Copy(Path.Combine(temporary, "simulation", name), Path.Combine(evidence, name), true);
            StrictJson.Write(Path.Combine(evidence, "manifest.json"), manifest); StrictJson.Write(Path.Combine(evidence, "negative-checks.json"), checks); StrictJson.Write(Path.Combine(evidence, "dependencies.json"), dependencies);
        }
        finally
        {
            var target = Path.GetFullPath(temporary); var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            if (Path.GetDirectoryName(target) != parent || !Path.GetFileName(target).StartsWith("sharptestbench-consumer-", StringComparison.Ordinal)) throw new IOException("Consumer cleanup escaped temporary directory");
            Directory.Delete(target, true);
        }
        var packages = Directory.EnumerateFiles(feed, "*.nupkg").ToDictionary(path => Path.GetFileName(path), CiCommands.Hash);
        StrictJson.Write(Path.Combine(evidence, "packages.json"), new { version, rid, source_revision = revision, source_dirty = dirty, sha256 = packages });
        Console.WriteLine("NATIVE_PACKAGE_PASS " + rid + " isolated installed consumer");
    }
}
