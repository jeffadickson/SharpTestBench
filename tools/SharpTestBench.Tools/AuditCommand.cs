using System.Globalization;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
namespace SharpTestBench.Tools;
internal static class AuditCommand
{
    internal static string SourceDirectory(string source)
    {
        source = Path.GetFullPath(source);
        var checkout = Path.Combine(source, "src");
        if (Directory.Exists(Path.Combine(checkout, "cocotb"))) return checkout;
        if (Directory.Exists(Path.Combine(source, "cocotb"))) return source;
        if (Path.GetFileName(Path.TrimEndingDirectorySeparator(source)) == "cocotb" &&
            File.Exists(Path.Combine(source, "_base_triggers.py")))
            return Directory.GetParent(source)!.FullName;
        throw new DirectoryNotFoundException("Expected a cocotb source checkout, package container, or installed cocotb package: " + source);
    }

    internal static string EvidencePath(string root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) throw new InvalidDataException("Review evidence path must be repository-relative.");
        root = RunnerManifestCommand.RealPath(root);
        var resolved = RunnerManifestCommand.RealPath(Path.GetFullPath(path, root));
        var relative = Path.GetRelativePath(root, resolved);
        if (relative == "." || relative == ".." || Path.IsPathRooted(relative) || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("Review evidence escapes the repository.");
        return resolved;
    }

    public static object Validate(ToolContext context, string source)
    {
        var sourceDirectory = SourceDirectory(source);
        using var parser = new TextFieldParser(context.At("docs/UPSTREAM_API_INVENTORY.csv")); parser.SetDelimiters(","); parser.HasFieldsEnclosedInQuotes = true; parser.TrimWhiteSpace = false;
        var header = parser.ReadFields() ?? throw new InvalidDataException("API inventory header missing");
        if (!header.SequenceEqual(new[] { "Module", "Definition", "SourceLine", "ExactSemanticParity" })) throw new InvalidDataException("API inventory columns differ");
        var keys = new HashSet<(string Module, string Definition, int Line)>(); int inventory = 0;
        while (!parser.EndOfData)
        {
            var row = parser.ReadFields()!;
            if (row.Length != 4 || row.Take(2).Any(value => string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Any(char.IsControl)) ||
                !int.TryParse(row[2], NumberStyles.None, CultureInfo.InvariantCulture, out int line) || line <= 0 || (row[3] is not ("review-required" or "reviewed-mapping-see-TASK_API_MAP.md")))
                throw new InvalidDataException("Invalid API inventory identity, source line or status.");
            if (!keys.Add((row[0], row[1], line))) throw new InvalidDataException("Duplicate API inventory identity.");
            inventory++;
        }
        if (inventory == 0) throw new InvalidDataException("API inventory must contain public definitions.");
        using var reviewDocument = StrictJson.Read(context.At("docs/API_REVIEWS.json")); using var upstreamDocument = StrictJson.Read(context.At("third_party/UPSTREAM.json")); var review = reviewDocument.RootElement;
        if (review.ValueKind != JsonValueKind.Object || !review.TryGetProperty("schema_version", out var schema) ||
            schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1 ||
            !review.TryGetProperty("reviews", out var entries) || entries.ValueKind != JsonValueKind.Array ||
            !review.TryGetProperty("source_hashes", out var hashes) || hashes.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("API review document requires schema_version=1, reviews array and source_hashes object.");
        if (upstreamDocument.RootElement.ValueKind != JsonValueKind.Object ||
            !upstreamDocument.RootElement.TryGetProperty("commit", out var commit) || commit.ValueKind != JsonValueKind.String ||
            commit.GetString() is not { Length: 40 } revision || revision.Any(c => !char.IsAsciiHexDigit(c)) ||
            !review.TryGetProperty("source_revision", out var reviewedRevision) || reviewedRevision.ValueKind != JsonValueKind.String ||
            reviewedRevision.GetString() != revision)
            throw new InvalidDataException("Review revision must match the pinned 40-digit upstream commit.");
        var seen = new HashSet<(string Module, string Definition, int Line)>(); var counts = new Dictionary<string, int>();
        string[] statuses = ["idiomatic-equivalent", "intentional-difference", "gap", "internal-adapter", "deprecated-omission"];
        foreach (var row in review.GetProperty("reviews").EnumerateArray())
        {
            var module = row.GetProperty("module").GetString()!; var key = (module, row.GetProperty("definition").GetString()!, row.GetProperty("source_line").GetInt32());
            if (!keys.Contains(key) || !seen.Add(key)) throw new InvalidDataException("Unknown or duplicate API review: " + key);
            var status = row.GetProperty("classification").GetString()!; if (!statuses.Contains(status)) throw new InvalidDataException("Unknown review classification");
            if (string.IsNullOrWhiteSpace(row.GetProperty("reason").GetString()) || row.GetProperty("evidence").GetArrayLength() == 0) throw new InvalidDataException("Review evidence missing");
            foreach (var evidence in row.GetProperty("evidence").EnumerateArray())
            {
                if (!evidence.TryGetProperty("contains", out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                    throw new InvalidDataException("Review evidence must have a nonempty text anchor: " + key);
                var anchor = value.GetString()!;
                if (!evidence.TryGetProperty("path", out var location) || location.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException("Review evidence path must be repository-relative text: " + key);
                var path = EvidencePath(context.Root, location.GetString());
                if (!File.Exists(path) || !File.ReadAllText(path).Contains(anchor, StringComparison.Ordinal)) throw new InvalidDataException("Stale review evidence: " + key);
            }
            if (!review.GetProperty("source_hashes").TryGetProperty(module, out _)) throw new InvalidDataException("Review lacks a source hash"); counts[status] = counts.GetValueOrDefault(status) + 1;
        }
        foreach (var module in review.GetProperty("source_hashes").EnumerateObject())
        {
            var path = Path.Combine(sourceDirectory, module.Name.Replace('.', Path.DirectorySeparatorChar) + ".py"); if (CiCommands.Hash(path) != module.Value.GetString()) throw new InvalidDataException("Upstream source changed: " + module.Name + "; re-review required");
            var actualDefinitions = Definitions(path).ToHashSet();
            foreach (var key in seen.Where(key => key.Module == module.Name))
                if (!actualDefinitions.Contains((key.Definition, key.Line)))
                    throw new InvalidDataException("Reviewed definition does not exist at its claimed source line: " + key);
        }
        return new { inventoried_definitions = inventory, reviewed_definitions = seen.Count, unreviewed_definitions = keys.Except(seen).Count(), classifications = counts, note = "Review counts are not a parity percentage; aliases and dynamic exports remain outside this inventory." };
    }
    const string ParseDefinitions = "import ast,json,sys\ntree=ast.parse(sys.stdin.read())\ndef public(n): return isinstance(n,(ast.ClassDef,ast.FunctionDef,ast.AsyncFunctionDef)) and not n.name.startswith('_')\nprint(json.dumps([dict(name=n.name,line=n.lineno,methods=[dict(name=c.name,line=c.lineno) for c in n.body if public(c) and not isinstance(c,ast.ClassDef)] if isinstance(n,ast.ClassDef) else []) for n in tree.body if public(n)]))\n";
    internal static List<(string Definition, int Line)> Definitions(string path)
    {
            var settings = new System.Diagnostics.ProcessStartInfo(Environment.GetEnvironmentVariable("CS_PYTHON") ?? "python") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true }; settings.ArgumentList.Add("-c"); settings.ArgumentList.Add(ParseDefinitions);
            using var process = System.Diagnostics.Process.Start(settings) ?? throw new IOException("Unable to start Python AST parser"); var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); process.StandardInput.Write(File.ReadAllText(path)); process.StandardInput.Close();
            if (!process.WaitForExit(30000)) { process.Kill(true); throw new TimeoutException("Python AST parser timed out"); }
            if (process.ExitCode != 0) throw new InvalidDataException("Python AST parser failed: " + error.GetAwaiter().GetResult());
            using var document = StrictJson.Parse(output.GetAwaiter().GetResult());
            var definitions = new List<(string Definition, int Line)>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var name = item.GetProperty("name").GetString()!;
                definitions.Add((name, item.GetProperty("line").GetInt32()));
                foreach (var method in item.GetProperty("methods").EnumerateArray())
                    definitions.Add((name + "." + method.GetProperty("name").GetString(), method.GetProperty("line").GetInt32()));
            }
            return definitions;
    }
    public static void Execute(ToolContext context, string command, string[] arguments)
    {
        var options = new Options(arguments); options.Only("--source"); var source = Path.GetFullPath(options.Require("--source"));
        if (command == "audit-reviews") { Console.WriteLine(JsonSerializer.Serialize(Validate(context, source), new JsonSerializerOptions { WriteIndented = true })); return; }
        // Python's own AST parser is the external language parser; C# owns file discovery and CSV generation.
        var rows = new List<string[]>(); var root = SourceDirectory(source);
        foreach (var path in Directory.EnumerateFiles(Path.Combine(root, "cocotb"), "*.py", System.IO.SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var module = Path.ChangeExtension(Path.GetRelativePath(root, path), null).Replace(Path.DirectorySeparatorChar, '.');
            foreach (var definition in Definitions(path))
                rows.Add([module, definition.Definition, definition.Line.ToString(CultureInfo.InvariantCulture), "review-required"]);
        }

        if (rows.Count == 0) throw new InvalidDataException("No public definitions found; preserving the existing inventory.");
        static string Csv(string value) => value.IndexOfAny([',', '"', '\r', '\n']) < 0 ? value : "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        var destination = context.At("docs/UPSTREAM_API_INVENTORY.csv");
        var pending = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(pending, "Module,Definition,SourceLine,ExactSemanticParity\n" + string.Join("\n", rows.Select(row => string.Join(',', row.Select(Csv)))) + "\n");
            File.Move(pending, destination, true);
        }
        finally { File.Delete(pending); }
        Console.WriteLine(rows.Count + " public definitions inventoried (excludes aliases and dynamic members)");
    }
}
