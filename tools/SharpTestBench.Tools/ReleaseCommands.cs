using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace SharpTestBench.Tools;

internal static class ReleaseCommands
{
    public static readonly string[] Rids = ["linux-x64", "win-x64", "osx-arm64"];
    static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    static string Text(JsonElement value, string name) => value.GetProperty(name).GetString() ?? throw new InvalidDataException("Missing text: " + name);
    public static (string Id, string Version) Metadata(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var entries = archive.Entries.Where(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).ToArray();
        Require(entries.Length == 1, "Expected one nuspec: " + path);
        using var input = entries[0].Open();
        using var data = new MemoryStream(BoundedInput.Read(input, 1024 * 1024));
        using var reader = XmlReader.Create(data, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 });
        var root = XDocument.Load(reader).Root;
        var metadata = root?.Elements().SingleOrDefault(element => element.Name.LocalName == "metadata") ?? throw new InvalidDataException("Missing package metadata");
        string Value(string name) => metadata.Elements().SingleOrDefault(element => element.Name.LocalName == name)?.Value ?? throw new InvalidDataException("Missing package " + name);
        return (Value("id"), Value("version"));
    }
    static string SingleFile(string root, string filename, string directoryName)
    {
        var paths = Directory.EnumerateFiles(root, filename, SearchOption.AllDirectories).Where(path => Path.GetFileName(Path.GetDirectoryName(path)) == directoryName).ToArray();
        if (paths.Length != 1) throw new InvalidDataException("Missing or duplicate validated evidence/package: " + directoryName + "/" + filename);
        return paths[0];
    }
    public static object Assemble(string artifacts, string output, string revision)
    {
        artifacts = Path.GetFullPath(artifacts); output = Path.GetFullPath(output);
        Require(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(), "Output directory must be empty");
        var selected = new List<(string Path, string Hash, string Id)>();
        string? version = null;
        foreach (var rid in Rids)
        {
            var path = SingleFile(artifacts, "packages.json", "package-evidence-" + rid);
            var folder = Path.GetDirectoryName(path)!;
            using var indexDocument = StrictJson.Read(path); var index = indexDocument.RootElement;
            Require(Text(index, "source_revision") == revision && !index.GetProperty("source_dirty").GetBoolean(), "Unverified source revision: " + rid);
            Require(Text(index, "rid") == rid, "RID mismatch");
            version ??= Text(index, "version"); Require(version == Text(index, "version"), "Mixed package versions");
            using var results = StrictJson.Read(Path.Combine(folder, "results.json")); var rows = results.RootElement.EnumerateArray().ToArray();
            Require(rows.Length == 1 && Text(rows[0], "Status") == "passed" && Text(rows[0], "Name").EndsWith("ResetAndCount", StringComparison.Ordinal), "Installed consumer did not pass expected test: " + rid);
            using var checks = StrictJson.Read(Path.Combine(folder, "negative-checks.json"));
            var names = checks.RootElement.ValueKind == JsonValueKind.Object ? checks.RootElement.EnumerateObject().Select(property => property.Name) : checks.RootElement.EnumerateArray().Select(element => element.GetString() ?? "");
            Require(names.ToHashSet(StringComparer.Ordinal).SetEquals(["rid", "checksum"]), "Missing negative checks");
            using var native = StrictJson.Read(Path.Combine(folder, "manifest.json"));
            Require(Text(native.RootElement, "source_revision") == revision && !native.RootElement.GetProperty("source_dirty").GetBoolean() && Text(native.RootElement, "package_version") == version, "Native provenance mismatch");
            var wanted = new HashSet<string>(StringComparer.Ordinal) { "SharpTestBench.Native." + rid };
            if (rid == "linux-x64") wanted.UnionWith(["SharpTestBench", "SharpTestBench.Generators"]);
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var checksum in index.GetProperty("sha256").EnumerateObject())
            {
                var filename = checksum.Name;
                Require(filename == Path.GetFileName(filename) && !filename.Contains('\\') && !filename.Contains('/'), "Unsafe package name");
                var package = SingleFile(artifacts, filename, "packages-" + rid);
                var hash = checksum.Value.GetString() ?? "";
                Require(CiCommands.Hash(package) == hash, "Package checksum mismatch: " + filename);
                var metadata = Metadata(package);
                Require(metadata.Version == version, "Stale package in feed: " + filename);
                if (wanted.Contains(metadata.Id))
                {
                    Require(found.Add(metadata.Id), "Duplicate package identity");
                    selected.Add((package, hash, metadata.Id));
                }
            }
            Require(found.SetEquals(wanted), "Missing required package identities: " + rid);
        }
        Require(selected.Count == 5, "Release must contain exactly five packages");
        Directory.CreateDirectory(output);
        foreach (var package in selected) File.Copy(package.Path, Path.Combine(output, Path.GetFileName(package.Path)), true);
        var manifest = new { version, source_revision = revision, packages = selected.Select(package => new { id = package.Id, file = Path.GetFileName(package.Path), sha256 = package.Hash }).ToArray() };
        StrictJson.Write(Path.Combine(output, "release-manifest.json"), manifest);
        File.WriteAllText(Path.Combine(output, "SHA256SUMS"), string.Concat(selected.Select(package => package.Hash + "  " + Path.GetFileName(package.Path) + "\n")));
        return manifest;
    }
    public static void Preflight(string directory, HttpClient? supplied = null)
    {
        var bundle = Path.GetFullPath(directory);
        using var document = StrictJson.Read(Path.Combine(bundle, "release-manifest.json"));
        var version = Text(document.RootElement, "version");
        var records = document.RootElement.GetProperty("packages").EnumerateArray().ToArray();
        Require(records.Length == 5, "Release requires five validated packages");
        using var owned = supplied is null ? new HttpClient { Timeout = TimeSpan.FromSeconds(30) } : null;
        var client = supplied ?? owned!;
        foreach (var record in records)
        {
            var filename = Text(record, "file");
            Require(filename == Path.GetFileName(filename) && !filename.Contains('\\') && !filename.Contains('/'), "Unsafe package name");
            Require(CiCommands.Hash(Path.Combine(bundle, filename)) == Text(record, "sha256"), "Release checksum mismatch");
            var id = Text(record, "id");
            using var response = client.GetAsync("https://api.nuget.org/v3-flatcontainer/" + Uri.EscapeDataString(id.ToLowerInvariant()) + "/index.json", HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            if (response.StatusCode == HttpStatusCode.NotFound) continue;
            response.EnsureSuccessStatusCode();
            using var input = response.Content.ReadAsStream();
            using var versions = StrictJson.Parse(new System.Text.UTF8Encoding(false, true).GetString(BoundedInput.Read(input, 1024 * 1024)));
            Require(!versions.RootElement.GetProperty("versions").EnumerateArray().Any(item => string.Equals(item.GetString(), version, StringComparison.OrdinalIgnoreCase)), "Version already exists: " + id);
        }
        Console.WriteLine("RELEASE_PREFLIGHT_PASS: all five versions absent; publishing remains a separate step");
    }
    public static void Execute(string name, string[] arguments)
    {
        var options = new Options(arguments);
        if (name == "release-bundle")
        {
            options.Only("--artifacts", "--output", "--revision");
            Console.WriteLine(JsonSerializer.Serialize(Assemble(options.Require("--artifacts"), options.Require("--output"), options.Require("--revision"))));
        }
        else { options.Only("--bundle"); Preflight(options.Require("--bundle")); }
    }
}
