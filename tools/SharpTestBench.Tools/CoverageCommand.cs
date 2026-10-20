using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace SharpTestBench.Tools;

internal sealed record CoverageCount(long covered, long valid)
{
    public double percent => 100d * covered / valid;
}
internal sealed record CoverageReport(string[] assemblies, CoverageCount lines, CoverageCount branches);
internal static class CoverageCommand
{
    public const int MaximumBytes = 16 * 1024 * 1024;
    const string StandardDtd = "<!DOCTYPE coverage SYSTEM \"http://cobertura.sourceforge.net/xml/coverage-04.dtd\">";
    public static CoverageReport Validate(string path, params string[] expected)
    {
        using var input = File.OpenRead(path);
        if (input.Length > MaximumBytes) throw new InvalidDataException("Coverage exceeds size limit");
        var text = new UTF8Encoding(false, true).GetString(BoundedInput.Read(input, MaximumBytes)).TrimStart('\uFEFF');
        text = text.Replace(StandardDtd, "", StringComparison.Ordinal);
        if (text.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) || text.Contains("<!ENTITY", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Coverage DTD/entity declarations are forbidden");
        using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumBytes });
        var document = XDocument.Load(reader);
        var root = document.Root;
        if (root?.Name != "coverage") throw new InvalidDataException("Expected Cobertura coverage root");
        CoverageCount Count(string kind, string rateName)
        {
            static long Integer(XElement element, string name) => long.Parse(element.Attribute(name)?.Value ?? throw new InvalidDataException("Missing counter: " + name), NumberStyles.Integer, CultureInfo.InvariantCulture);
            long valid = Integer(root, kind + "-valid"), covered = Integer(root, kind + "-covered");
            double rate = double.Parse(root.Attribute(rateName)?.Value ?? throw new InvalidDataException("Missing coverage rate"), CultureInfo.InvariantCulture);
            if (valid <= 0 || covered < 0 || covered > valid || !double.IsFinite(rate) || rate < 0 || rate > 1 || Math.Abs(rate - (double)covered / valid) > 0.000100000001) throw new InvalidDataException("Inconsistent coverage counters or rates");
            if (kind == "lines" && covered == 0) throw new InvalidDataException("Coverage report has no executed lines");
            return new(covered, valid);
        }
        var lines = Count("lines", "line-rate");
        var branches = Count("branches", "branch-rate");
        var packages = root.Element("packages")?.Elements("package").ToArray() ?? [];
        var names = packages.Select(p => p.Attribute("name")?.Value ?? throw new InvalidDataException("Missing coverage assembly")).ToArray();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length || !names.ToHashSet(StringComparer.Ordinal).SetEquals(expected) || packages.Any(p => p.Element("classes")?.Elements("class").Any() != true)) throw new InvalidDataException("Coverage assembly set differs or has no classes");
        return new(names.Order(StringComparer.Ordinal).ToArray(), lines, branches);
    }
    public static void Execute(ToolContext context, string[] arguments)
    {
        if (arguments.Length != 0) throw new ArgumentException("code-coverage accepts no arguments");
        const string output = "build/code-coverage";
        Directory.CreateDirectory(context.At(output));
        Directory.CreateDirectory(context.At(output + "/report"));
        foreach (var name in new[] { "summary.json", "managed.cobertura.xml", "generators.cobertura.xml", "helpers.cobertura.xml", "contracts.cobertura.xml", "runner.cobertura.xml" }) File.Delete(context.At(output + "/" + name));
        foreach (var name in new[] { "index.html", "Summary.json", "Summary.txt", "Cobertura.xml" }) File.Delete(context.At(output + "/report/" + name));
        var summaries = new Dictionary<string, CoverageReport>();
        foreach (var (label, project, assemblyName) in new[] { ("managed", "SharpTestBench.Tests", "SharpTestBench"), ("generators", "SharpTestBench.Generators.Tests", "SharpTestBench.Generators"), ("helpers", "SharpTestBench.Tools.Tests", "SharpTestBench.Tools") })
        {
            string assembly = $"tests/{project}/bin/Release/net10.0/{project}.dll";
            string path = output + "/" + label;
            context.Run(["dotnet", "tool", "run", "coverlet", assembly, "--target", "dotnet", "--targetargs", assembly + " -xml " + output + "/" + label + "-tests.xml", "--format", "cobertura", "--output", path, "--include", "[" + assemblyName + "]*"]);
            summaries.Add(label, Validate(context.At(path + ".cobertura.xml"), assemblyName));
        }
        // Instrument a private copy: the coverage controller itself is already loaded.
        var harness = context.At(output + "/contracts-harness"); Directory.CreateDirectory(harness);
        var source = Path.GetDirectoryName(typeof(ToolContext).Assembly.Location)!;
        foreach (var name in new[] { "SharpTestBench.Tools.dll", "SharpTestBench.Tools.pdb", "SharpTestBench.Tools.deps.json", "SharpTestBench.Tools.runtimeconfig.json" }) File.Copy(Path.Combine(source, name), Path.Combine(harness, name), true);
        var contractAssembly = output + "/contracts-harness/SharpTestBench.Tools.dll";
        context.Run(["dotnet", "tool", "run", "coverlet", contractAssembly, "--target", Environment.GetEnvironmentVariable("CS_PYTHON") ?? "python", "--targetargs", "-m unittest discover -s tests/Tooling -q", "--format", "cobertura", "--output", output + "/contracts", "--include-test-assembly", "--include", "[SharpTestBench.Tools]*"], environment: new Dictionary<string, string?> { ["STB_MANAGED_TEST_API_DLL"] = context.At(contractAssembly) });
        summaries.Add("contracts", Validate(context.At(output + "/contracts.cobertura.xml"), "SharpTestBench.Tools"));
        // The contract harness references the production Runner assembly. Instrument
        // its local dependency copy; the standalone CLI and native workers stay intact.
        var runnerHarness = "tests/RunnerValidation/bin/Release/net10.0/RunnerValidation.dll";
        context.Run(["dotnet", "tool", "run", "coverlet", runnerHarness, "--target", Environment.GetEnvironmentVariable("CS_PYTHON") ?? "python", "--targetargs", "-m unittest discover -s tests/Tooling -p test_*runner*.py -q", "--format", "cobertura", "--output", output + "/runner", "--include", "[SharpTestBench.Runner]*"]);
        summaries.Add("runner", Validate(context.At(output + "/runner.cobertura.xml"), "SharpTestBench.Runner"));
        context.Run(["dotnet", "tool", "run", "reportgenerator", "-reports:" + output + "/*.cobertura.xml", "-targetdir:" + output + "/report", "-reporttypes:Html;Cobertura;JsonSummary;TextSummary", "-verbosity:Warning"]);
        var combined = Validate(context.At(output + "/report/Cobertura.xml"), "SharpTestBench", "SharpTestBench.Generators", "SharpTestBench.Tools", "SharpTestBench.Runner");
        using var json = StrictJson.Read(context.At(output + "/report/Summary.json"));
        var summary = json.RootElement.GetProperty("summary");
        foreach (var (count, covered, valid) in new[] { (combined.lines, "coveredlines", "coverablelines"), (combined.branches, "coveredbranches", "totalbranches") })
            if (count.covered != summary.GetProperty(covered).GetInt64() || count.valid != summary.GetProperty(valid).GetInt64()) throw new InvalidDataException("Merged Cobertura and JSON coverage totals disagree");
        StrictJson.Write(context.At(output + "/summary.json"), new { suites = summaries, combined });
        Console.WriteLine(JsonSerializer.Serialize(combined));
    }
}
