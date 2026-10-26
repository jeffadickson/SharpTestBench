using System.Text;

namespace SharpTestBench.Tools;

/// <summary>Baseline and coverage checks have their own bounded deadlines outside the campaign clock.</summary>
internal static class EnduranceVerification
{
    // Full xUnit discovery is structured evidence, not a diagnostic prefix. The
    // complete inventory can exceed normal logs; retain it within a separate cap.
    internal const int DiscoveryBytes = 16777216;
    internal const string BaselineScope = "Core, Generators and Endurance Tools tests; no simulator qualification";
    internal static string Quote(string path)
    {
        if (path.Contains('"') || path.Any(char.IsControl)) throw new ArgumentException("Unsupported quoted target path");
        return "\"" + path.Replace('\\', '/') + "\"";
    }
    internal static EnduranceCoverage Coverage(string snapshot, string work, EnduranceReport report, IEnduranceExecution execution, CancellationToken cancellation)
    {
        string assembly = Path.Combine(snapshot, "tests/SharpTestBench.Tools.Tests/bin/Release/net10.0/SharpTestBench.Tools.Tests.dll");
        if (!File.Exists(assembly)) return new("unavailable", EnduranceRules.CoverageScope);
        var restored = execution.Run(["dotnet", "tool", "restore"], snapshot, TimeSpan.FromSeconds(120), report.Limits.LogBytes, cancellation);
        EnduranceCampaign.SaveLogs(work, report, "coverage-tools", restored);
        if (!restored.Succeeded) return new("unavailable", EnduranceRules.CoverageScope);
        string output = Path.Combine(work, "coverage"); Directory.CreateDirectory(output);
        string xml = Path.Combine(output, "endurance.cobertura.xml");
        // Instrument the test assembly's Tools dependency, not the already-loaded
        // campaign controller. The include filter is intentionally narrow.
        var collected = execution.Run(["dotnet", "tool", "run", "coverlet", assembly, "--target", "dotnet", "--targetargs",
            Quote(assembly) + " -class Endurance* -xml " + Quote(Path.Combine(output, "tests.xml")), "--format", "cobertura", "--output", Path.Combine(output, "endurance"), "--include", "[SharpTestBench.Tools]SharpTestBench.Tools.Endurance*"], snapshot, TimeSpan.FromSeconds(300), report.Limits.LogBytes, cancellation);
        EnduranceCampaign.SaveLogs(work, report, "coverage-tests", collected);
        if (!collected.Succeeded || !File.Exists(xml)) return new("failed", EnduranceRules.CoverageScope);
        try
        {
            var counts = CoverageCommand.Validate(xml, "SharpTestBench.Tools");
            return new("available", EnduranceRules.CoverageScope, counts.lines.covered, counts.lines.valid, counts.branches.covered, counts.branches.valid);
        }
        catch (Exception error) when (error is IOException or System.Xml.XmlException or InvalidDataException or FormatException or OverflowException) { return new("failed", EnduranceRules.CoverageScope); }
    }
    internal static EnduranceBaselineEvidence Baseline(string root, string output, string? strictdoc = null)
    {
        using var lease = new EnduranceLease(root);
        EnduranceStorage.NoLinks(root, output);
        if (File.Exists(output)) throw new IOException("Baseline evidence already exists");
        var git = new EnduranceGit(root); var source = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in git.Tracked()) { EnduranceRules.RequirePath(path); EnduranceStorage.NoLinks(root, Path.Combine(root, path)); source.Add(path, EnduranceStorage.HashFile(Path.Combine(root, path))); }
        string commit = git.Head, work = Path.Combine(root, "build/endurance-baseline", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
        long passed = 0; string status = "passed", requirements = "unavailable";
        void Save(string name, EnduranceProcessResult result)
        {
            EnduranceStorage.Write(Path.Combine(work, name + ".stdout.log"), result.Stdout);
            EnduranceStorage.Write(Path.Combine(work, name + ".stderr.log"), result.Stderr);
        }
        try
        {
            var built = EnduranceProcess.Run("dotnet", ["build", "SharpTestBench.slnx", .. EnduranceCampaign.BuildArguments], root, TimeSpan.FromSeconds(300)); Save("build", built);
            if (!built.Succeeded) status = "failed";
            else foreach (string suite in new[] { "SharpTestBench.Tests", "SharpTestBench.Generators.Tests", "SharpTestBench.Tools.Tests" })
            {
                string assembly = Path.Combine(root, "tests", suite, "bin/Release/net10.0", suite + ".dll");
                string[] filter = suite == "SharpTestBench.Tools.Tests" ? ["-class", "Endurance*"] : [];
                var discovered = EnduranceProcess.Run("dotnet", [assembly, "-preEnumerateTheories", "-list", "full/json", .. filter], root, TimeSpan.FromSeconds(120), DiscoveryBytes); Save(suite + "-discovery", discovered);
                if (!discovered.Passed) { status = "failed"; break; }
                var inventory = ManagedTestsCommand.Inventory(discovered.Output, requireMutationTarget: false);
                string xml = Path.Combine(work, suite + ".xml");
                var tested = EnduranceProcess.Run("dotnet", [assembly, .. filter, "-xml", xml], root, TimeSpan.FromSeconds(300)); Save(suite, tested);
                if (!tested.Succeeded) { status = "failed"; break; }
                ManagedTestsCommand.Verify(xml, inventory, mutation: false, requireMutationTarget: false); passed += inventory.Count;
            }
            strictdoc ??= Environment.GetEnvironmentVariable("CS_STRICTDOC") ?? "strictdoc";
            try
            {
                var version = EnduranceProcess.Run(strictdoc, ["--version"], root, TimeSpan.FromSeconds(15)); Save("strictdoc-version", version);
                if (version.Passed && version.Output.Contains("0.30.4", StringComparison.Ordinal))
                {
                    var exported = EnduranceProcess.Run(strictdoc, ["export", Path.Combine(root, "docs/requirements"), "--output-dir", Path.Combine(work, "requirements"), "--no-parallelization"], root, TimeSpan.FromSeconds(180)); Save("strictdoc", exported);
                    requirements = exported.Succeeded && exported.Output.Contains("Export completed.", StringComparison.Ordinal) ? "passed" : "failed";
                }
            }
            catch (System.ComponentModel.Win32Exception) { requirements = "unavailable"; }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or System.Text.Json.JsonException or System.Xml.XmlException or System.ComponentModel.Win32Exception)
        {
            status = error is System.ComponentModel.Win32Exception ? "unavailable" : "failed";
            EnduranceStorage.Write(Path.Combine(work, "error.txt"), Encoding.UTF8.GetBytes(error.ToString()[..Math.Min(8192, error.ToString().Length)]));
        }
        if (!EnduranceGit.Matches(root, source) || git.Head != commit) status = "failed";
        var evidence = new EnduranceBaselineEvidence(commit, EnduranceCampaign.SourceDigest(source), new(status, BaselineScope, passed, 0, requirements));
        EnduranceStorage.WriteJson(output, evidence); return evidence;
    }
}
