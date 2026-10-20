using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SharpTestBench.Tools;

internal static class ValidationCommands
{
    static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    static string Text(JsonElement value, string name) => value.GetProperty(name).GetString() ?? throw new InvalidDataException("Missing text: " + name);
    static BigInteger Number(JsonElement value, string name) => BigInteger.Parse(value.GetProperty(name).GetRawText(), CultureInfo.InvariantCulture);
    public static void Logging(ToolContext context, string label)
    {
        using var results = StrictJson.Read(context.At("build/" + label + "-results.json"));
        var result = results.RootElement.EnumerateArray().Single(row => Text(row, "Name") == "AR_StructuredLogging");
        var rows = new List<JsonElement>();
        foreach (var line in File.ReadLines(context.At("build/" + label + "-events.jsonl")))
        {
            using var document = StrictJson.Parse(line);
            if (Text(document.RootElement, "Test") == Text(result, "QualifiedName")) rows.Add(document.RootElement.Clone());
        }
        Require(rows.Select(row => Text(row, "Kind")).SequenceEqual(new[] { "test-start", "message", "message", "message", "test-end" }), "Structured log event kinds differ");
        foreach (var row in rows) Require(Number(row, "SchemaVersion") == 1 && Number(row, "Seed") == Number(result, "Seed") && Number(row, "Stage") == Number(result, "Stage"), "Structured log identity differs");
        Require(rows.Select(row => Number(row, "TimeStep")).SequenceEqual(rows.Select(row => Number(row, "TimeStep")).Order()), "Structured log time moved backwards");
        var parent = rows[1]; var child = rows[2]; var large = rows[3];
        Require(Text(parent, "Message") == "parent\nmessage" && Text(parent, "Level") == "Warning" && parent.GetProperty("TaskId").ValueKind == JsonValueKind.Null, "Parent log fields differ");
        Require(Text(child, "TaskName") == "log-child" && child.GetProperty("TaskId").ValueKind == JsonValueKind.Number, "Child task identity differs");
        Require(Text(child, "ExceptionType") == "System.FormatException" && Text(child, "ExceptionDetail").Contains("AR_StructuredLogging", StringComparison.Ordinal), "Log exception differs");
        Require(Text(child, "SourceFile").EndsWith("Tests.cs", StringComparison.Ordinal) && Number(child, "SourceLine") > 0 && Text(child, "SourceMember") == "AR_StructuredLogging", "Log source mapping differs");
        Require(large.GetProperty("Truncated").GetBoolean() && Text(large, "Message").Length == 16384 && Text(rows[^1], "Message") == "passed", "Bounded log fields differ");
        Console.WriteLine("LOGGING_PASS " + label);
    }
    public static void FunctionalCoverage(ToolContext context, string label)
    {
        using var report = StrictJson.Read(context.At("build/" + label + "-results.coverage.json"));
        using var results = StrictJson.Read(context.At("build/" + label + "-results.json"));
        Require(Number(report.RootElement, "SchemaVersion") == 1 && Text(report.RootElement, "Kind") == "functional", "Functional coverage schema differs");
        var row = report.RootElement.GetProperty("Tests").EnumerateArray().Single(row => Text(row, "Test") == "AQ_FunctionalCoverage");
        var result = results.RootElement.EnumerateArray().Single(result => Text(result, "Name") == Text(row, "Test"));
        Require(Number(row, "Seed") == Number(result, "Seed") && Text(row, "Status") == "passed" && Text(result, "Status") == "passed", "Functional coverage identity differs");
        var coverage = row.GetProperty("Coverage");
        var point = coverage.GetProperty("Points").EnumerateArray().Single();
        Require(Text(point, "Name") == "packet-payload" && Number(point, "Samples") == 4 && Number(point, "Unmatched") == 0 && Number(point, "Percent") == 50 && Number(coverage, "Percent") == 50, "Functional coverage counts differ");
        var bins = point.GetProperty("Bins").EnumerateArray().ToArray();
        Require(bins.Length == 2 && Text(bins[0], "Name") == "zero" && Number(bins[0], "Hits") == 2 && Number(bins[0], "Goal") == 1 && bins[0].GetProperty("Covered").GetBoolean(), "Zero bin differs");
        Require(Text(bins[1], "Name") == "nonzero" && Number(bins[1], "Hits") == 2 && Number(bins[1], "Goal") == 3 && !bins[1].GetProperty("Covered").GetBoolean(), "Nonzero bin differs");
        Console.WriteLine("COVERAGE_PASS " + label);
    }
    public static void Waves(ToolContext context, string label, bool knownValues = true)
    {
        using var manifest = StrictJson.Read(context.At("build/" + label + "-artifacts.json"));
        var root = manifest.RootElement;
        var artifact = root.GetProperty("Artifacts").EnumerateArray().Single();
        var wave = context.At("build/" + Text(artifact, "Path"));
        Require(Number(root, "SchemaVersion") == 1 && Text(artifact, "Format") == "vcd", "Waveform schema differs");
        Require(artifact.GetProperty("Exists").GetBoolean() && File.Exists(wave) && new FileInfo(wave).Length > 0 && Number(artifact, "Bytes") == new FileInfo(wave).Length, "Waveform size differs");
        var text = File.ReadAllText(wave);
        Require(text.Contains("$enddefinitions", StringComparison.Ordinal) && Regex.IsMatch(text, @"^#\d+", RegexOptions.Multiline), "Waveform definitions or time missing");
        if (knownValues)
        {
            var match = Regex.Match(text, @"\$var\s+\S+\s+8\s+(\S+)\s+data(?:\[|\s)");
            Require(match.Success, "Data signal missing in VCD");
            var values = Regex.Matches(text, @"^b([01]+)\s+" + Regex.Escape(match.Groups[1].Value) + @"\s*$", RegexOptions.Multiline).Select(row => Convert.ToInt32(row.Groups[1].Value, 2)).ToHashSet();
            Require(values.Contains(20) && values.Contains(50), "Known data values missing in VCD");
        }
        var resultPath = context.At("build/" + Text(root, "Results"));
        using var results = StrictJson.Read(resultPath);
        var rows = results.RootElement.EnumerateArray().ToArray();
        Require(rows.Length > 0 && rows.All(row => row.GetProperty("Artifacts").GetArrayLength() == 1 && JsonElement.DeepEquals(row.GetProperty("Artifacts")[0], artifact)), "Result waveform links differ");
        var cases = XDocument.Load(Path.ChangeExtension(resultPath, ".xml")).Descendants("testcase").ToArray();
        Require(cases.Length == rows.Length && cases.All(test => (string?)test.Element("properties")?.Elements("property").SingleOrDefault(p => (string?)p.Attribute("name") == "waveform")?.Attribute("value") == Text(artifact, "Path")), "JUnit waveform links differ");
        Console.WriteLine("WAVE_PASS " + label);
    }
    public static void Typecheck(ToolContext context)
    {
        var output = context.At("build/typecheck"); Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "Check.csproj"), new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"), new XElement("PropertyGroup", new XElement("TargetFramework", "net10.0")), new XElement("ItemGroup", new XElement("ProjectReference", new XAttribute("Include", context.At("src/SharpTestBench/SharpTestBench.csproj"))), new XElement("Compile", new XAttribute("Include", "../DutBindings.cs")))).ToString());
        var path = Path.Combine(output, "Wrong.cs");
        File.WriteAllText(path, "using SharpTestBench; public class Check { public void Good(UnsignedSignal<W8> s) {s.Write(new UInt<W8>(1));} public void Field(PackedField<W16,W4> f) {f.Write(new UInt<W4>(1));}}");
        CommandResult Build() => context.Run(["dotnet", "build", Path.Combine(output, "Check.csproj"), "-c", "Release", "--nologo"], capture: true, check: false);
        var first = Build(); Require(first.ExitCode == 0, first.Output + first.Error);
        var logs = new List<string> { "Generated bindings + valid typed call: PASS" };
        foreach (var (name, source) in new[] {
            ("width", "using SharpTestBench; public class Check { public void Wrong(UnsignedSignal<W8> s) {s.Write(new UInt<W16>(1));}}"),
            ("signedness", "using SharpTestBench; public class Check { public void Wrong(UnsignedSignal<W8> s) {s.Write(new SInt<W8>(1));}}"),
            ("packed field width", "using SharpTestBench; public class Check { public void Wrong(PackedField<W16,W4> field) {field.Write(new UInt<W8>(1));}}") })
        {
            File.WriteAllText(path, source); var result = Build();
            Require(result.ExitCode != 0 && result.Output.Contains("CS1503", StringComparison.Ordinal), "Compiler failed to reject " + name + "\n" + result.Output + result.Error);
            logs.Add(name + ": correctly rejected by compiler\n" + result.Output);
        }
        File.Delete(path); File.WriteAllLines(context.At("build/typecheck.log"), logs); Console.WriteLine("TYPECHECK_PASS");
    }
    public static void Documentation(ToolContext context, string dotnet, bool managedOnly)
    {
        var missing = new HashSet<string>(StringComparer.Ordinal);
        Directory.CreateDirectory(context.At("build/docs"));
        foreach (var name in new[] { "SharpTestBench", "SharpTestBench.Generators" })
        {
            var result = context.Run([dotnet, "build", context.At("src/" + name), "-c", "Release", "-t:Rebuild", "-m:1", "/nodeReuse:false", "-p:RestoreLockedMode=true"], capture: true, check: false);
            var log = result.Output + result.Error; File.WriteAllText(context.At("build/docs/" + name + ".log"), log);
            Require(result.ExitCode == 0, "Documentation build failed: " + name);
            foreach (Match match in Regex.Matches(log, "warning CS1591: Missing XML comment for publicly visible type or member '([^']+)'")) missing.Add(match.Groups[1].Value);
            var framework = name == "SharpTestBench" ? "net10.0" : "netstandard2.0";
            var xml = XDocument.Load(context.At($"src/{name}/bin/Release/{framework}/{name}.xml"));
            Require(xml.Root?.Element("members")?.Elements("member").Any() == true, "Empty API documentation: " + name);
        }
        var baseline = File.ReadAllLines(context.At("docs/api/csharp-undocumented.txt")).ToHashSet(StringComparer.Ordinal);
        Require(!missing.Except(baseline).Any(), "Add XML comments for new public APIs: " + string.Join(", ", missing.Except(baseline).Order()));
        Console.WriteLine($"C# XML valid; {missing.Count} existing undocumented public members; {baseline.Except(missing).Count()} backlog entries resolved.");
        if (!managedOnly)
        {
            context.Run(["doxygen", "docs/api/Doxyfile"]);
            context.Run([Environment.GetEnvironmentVariable("CS_PYTHON") ?? "python", "-m", "sphinx", "-W", "--keep-going", "-b", "html", "docs/api", "build/docs/html"]);
        }
    }
    public static void Execute(ToolContext context, string name, string[] arguments)
    {
        var options = new Options(arguments, "--managed-only");
        switch (name)
        {
            case "logging-check": options.Only("--label"); Logging(context, options.Get("--label", "icarus")!); break;
            case "coverage-check": options.Only("--label"); FunctionalCoverage(context, options.Get("--label", "icarus")!); break;
            case "wave-check": options.Only("--label"); Waves(context, options.Require("--label")); break;
            case "typecheck": options.Only(); Typecheck(context); break;
            case "documentation": options.Only("--dotnet", "--managed-only"); Documentation(context, options.Get("--dotnet", Environment.GetEnvironmentVariable("DOTNET") ?? "dotnet")!, options.Has("--managed-only")); break;
            default: throw new ArgumentException("Unknown validation helper");
        }
    }
}
