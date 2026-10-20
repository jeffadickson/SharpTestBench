using System.Globalization;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
namespace SharpTestBench.Tools;
internal static class AuditCommand
{
    public static object Validate(ToolContext context, string source)
    {
        using var parser = new TextFieldParser(context.At("docs/UPSTREAM_API_INVENTORY.csv")); parser.SetDelimiters(","); parser.HasFieldsEnclosedInQuotes = true;
        var header = parser.ReadFields() ?? throw new InvalidDataException("API inventory header missing");
        if (!header.SequenceEqual(new[] { "Module", "Definition", "SourceLine", "ExactSemanticParity" })) throw new InvalidDataException("API inventory columns differ");
        var keys = new HashSet<(string Module, string Definition, int Line)>(); int inventory = 0;
        while (!parser.EndOfData) { var row = parser.ReadFields()!; if (row.Length != 4) throw new InvalidDataException("Invalid API inventory row"); keys.Add((row[0], row[1], int.Parse(row[2], CultureInfo.InvariantCulture))); inventory++; }
        using var reviewDocument = StrictJson.Read(context.At("docs/API_REVIEWS.json")); using var upstreamDocument = StrictJson.Read(context.At("third_party/UPSTREAM.json")); var review = reviewDocument.RootElement;
        if (review.GetProperty("source_revision").GetString() != upstreamDocument.RootElement.GetProperty("commit").GetString()) throw new InvalidDataException("Review revision differs from pinned upstream");
        var seen = new HashSet<(string Module, string Definition, int Line)>(); var counts = new Dictionary<string, int>();
        string[] statuses = ["idiomatic-equivalent", "intentional-difference", "gap", "internal-adapter", "deprecated-omission"];
        foreach (var row in review.GetProperty("reviews").EnumerateArray())
        {
            var module = row.GetProperty("module").GetString()!; var key = (module, row.GetProperty("definition").GetString()!, row.GetProperty("source_line").GetInt32());
            if (!keys.Contains(key) || !seen.Add(key)) throw new InvalidDataException("Unknown or duplicate API review: " + key);
            var status = row.GetProperty("classification").GetString()!; if (!statuses.Contains(status)) throw new InvalidDataException("Unknown review classification");
            if (string.IsNullOrWhiteSpace(row.GetProperty("reason").GetString()) || row.GetProperty("evidence").GetArrayLength() == 0) throw new InvalidDataException("Review evidence missing");
            foreach (var evidence in row.GetProperty("evidence").EnumerateArray()) { var path = context.At(evidence.GetProperty("path").GetString()!); if (!File.Exists(path) || !File.ReadAllText(path).Contains(evidence.GetProperty("contains").GetString()!, StringComparison.Ordinal)) throw new InvalidDataException("Stale review evidence: " + key); }
            if (!review.GetProperty("source_hashes").TryGetProperty(module, out _)) throw new InvalidDataException("Review lacks a source hash"); counts[status] = counts.GetValueOrDefault(status) + 1;
        }
        foreach (var module in review.GetProperty("source_hashes").EnumerateObject())
        {
            var path = Path.Combine(source, "src", module.Name.Replace('.', Path.DirectorySeparatorChar) + ".py"); if (CiCommands.Hash(path) != module.Value.GetString()) throw new InvalidDataException("Upstream source changed: " + module.Name + "; re-review required");
        }
        return new { inventoried_definitions = inventory, reviewed_definitions = seen.Count, unreviewed_definitions = keys.Except(seen).Count(), classifications = counts, note = "Review counts are not a parity percentage; aliases and dynamic exports remain outside this inventory." };
    }
    const string ParseDefinitions = "import ast,json,sys\ntry:\n tree=ast.parse(sys.stdin.read())\nexcept SyntaxError:\n print('[]');sys.exit(0)\ndef public(n): return isinstance(n,(ast.ClassDef,ast.FunctionDef,ast.AsyncFunctionDef)) and not n.name.startswith('_')\nprint(json.dumps([dict(name=n.name,line=n.lineno,methods=[dict(name=c.name,line=c.lineno) for c in n.body if public(c) and not isinstance(c,ast.ClassDef)] if isinstance(n,ast.ClassDef) else []) for n in tree.body if public(n)]))\n";
    public static void Execute(ToolContext context, string command, string[] arguments)
    {
        var options = new Options(arguments); options.Only("--source"); var source = Path.GetFullPath(options.Require("--source"));
        if (command == "audit-reviews") { Console.WriteLine(JsonSerializer.Serialize(Validate(context, source), new JsonSerializerOptions { WriteIndented = true })); return; }
        // Python's own AST parser is the external language parser; C# owns file discovery and CSV generation.
        var rows = new List<string[]>(); var root = Path.Combine(source, "src");
        foreach (var path in Directory.EnumerateFiles(Path.Combine(root, "cocotb"), "*.py", System.IO.SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var settings = new System.Diagnostics.ProcessStartInfo(Environment.GetEnvironmentVariable("CS_PYTHON") ?? "python") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true }; settings.ArgumentList.Add("-c"); settings.ArgumentList.Add(ParseDefinitions);
            using var process = System.Diagnostics.Process.Start(settings) ?? throw new IOException("Unable to start Python AST parser"); var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); process.StandardInput.Write(File.ReadAllText(path)); process.StandardInput.Close();
            if (!process.WaitForExit(30000)) { process.Kill(true); throw new TimeoutException("Python AST parser timed out"); }
            if (process.ExitCode != 0) throw new InvalidDataException("Python AST parser failed: " + error.GetAwaiter().GetResult());
            var module = Path.ChangeExtension(Path.GetRelativePath(root, path), null).Replace(Path.DirectorySeparatorChar, '.'); using var document = StrictJson.Parse(output.GetAwaiter().GetResult());
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var name = item.GetProperty("name").GetString()!; rows.Add([module, name, item.GetProperty("line").GetInt32().ToString(CultureInfo.InvariantCulture), "review-required"]);
                foreach (var method in item.GetProperty("methods").EnumerateArray()) rows.Add([module, name + "." + method.GetProperty("name").GetString(), method.GetProperty("line").GetInt32().ToString(CultureInfo.InvariantCulture), "review-required"]);
            }
        }
        static string Csv(string value) => value.IndexOfAny([',', '"', '\r', '\n']) < 0 ? value : "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        File.WriteAllText(context.At("docs/UPSTREAM_API_INVENTORY.csv"), "Module,Definition,SourceLine,ExactSemanticParity\n" + string.Join("\n", rows.Select(row => string.Join(',', row.Select(Csv)))) + "\n"); Console.WriteLine(rows.Count + " public definitions inventoried (excludes aliases and dynamic members)");
    }
}
