using System.Xml.Linq;

namespace SharpTestBench.Tools;

internal static class ManagedTestsCommand
{
    public const int ExpectedCases = 480;
    public static void Verify(string path, int failed)
    {
        var cases = XDocument.Load(path).Descendants("test").ToArray();
        var failures = cases.Where(test => (string?)test.Attribute("result") == "Fail").ToArray();
        if (cases.Length != ExpectedCases || failures.Length != failed || cases.Count(test => (string?)test.Attribute("result") == "Pass") != ExpectedCases - failed) throw new InvalidDataException("Managed case inventory or outcomes differ");
        if (failed != 0 && ((string?)failures[0].Attribute("name") != "ManagedTests.IntegerBoundaries" || !cases.Any(test => (string?)test.Attribute("name") == "ManagedTests.FunctionalCoverage" && (string?)test.Attribute("result") == "Pass"))) throw new InvalidDataException("Injected failure was not isolated");
    }
    public static void Execute(ToolContext context, string[] arguments)
    {
        if (arguments.Length != 0) throw new ArgumentException("managed-tests accepts no arguments");
        var project = context.At("tests/SharpTestBench.Tests");
        var build = context.At("build");
        Directory.CreateDirectory(build);
        var report = Path.Combine(build, "managed-results.xml");
        File.Delete(report);
        context.Run(["dotnet", "run", "--project", project, "-c", "Release", "--no-build", "--", "-xml", report]);
        Verify(report, 0);
        var probe = Path.Combine(build, "managed-failure-probe");
        Directory.CreateDirectory(probe);
        var sourceProject = File.ReadAllText(Path.Combine(project, "SharpTestBench.Tests.csproj"));
        sourceProject = sourceProject.Replace("../../src/SharpTestBench/SharpTestBench.csproj", System.Security.SecurityElement.Escape(context.At("src/SharpTestBench/SharpTestBench.csproj")), StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(probe, "FailureProbe.csproj"), sourceProject);
        var source = File.ReadAllText(Path.Combine(project, "Program.cs"));
        const string original = "new UInt<W8>(255).Value == 255";
        if (source.Split(original, StringSplitOptions.None).Length != 2) throw new InvalidDataException("Failure probe mutation target differs");
        File.WriteAllText(Path.Combine(probe, "Program.cs"), source.Replace(original, "new UInt<W8>(255).Value == 254", StringComparison.Ordinal));
        foreach (var path in Directory.EnumerateFiles(project, "*.cs").Where(path => Path.GetFileName(path) != "Program.cs").Concat(new[] { "array-reference.json", "logic-reference.json", "nine-state-reference.txt", "packages.lock.json" }.Select(name => Path.Combine(project, name)))) File.Copy(path, Path.Combine(probe, Path.GetFileName(path)), true);
        context.Run(["dotnet", "build", Path.Combine(probe, "FailureProbe.csproj"), "-c", "Release", "--disable-build-servers", "-m:1", "-p:RestoreLockedMode=true"]);
        report = Path.Combine(build, "managed-injected-failure.xml");
        File.Delete(report);
        var result = context.Run(["dotnet", Path.Combine(probe, "bin/Release/net10.0/FailureProbe.dll"), "-xml", report], check: false);
        if (result.ExitCode == 0) throw new InvalidDataException("Injected failure must fail the process");
        Verify(report, 1);
        Console.WriteLine($"MANAGED_PASS {ExpectedCases} cases; injected failure named correctly; remaining {ExpectedCases - 1} cases passed");
    }
}
