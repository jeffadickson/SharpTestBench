using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace SharpTestBench.Tools;

/// <summary>Public CLI routing for bounded local runs and data-only Git transport.</summary>
internal static class EnduranceCommand
{
    internal static int Execute(ToolContext context, string[] arguments)
    {
        if (arguments.Length == 0 || arguments[0] is "--help" or "-h")
        {
            Console.WriteLine("endurance run|replay|baseline|preflight|publish|import|review|recover. Run defaults to --duration 05:00:00; use explicit short durations for validation. See docs/ENDURANCE.md."); return 0;
        }
        string mode = arguments[0]; var options = new Options(arguments[1..], "--skip-coverage", "--no-push");
        string Expected() => options.Get("--expected-source", new EnduranceGit(context.Root).Head)!;
        int Integer(string name, int fallback) => int.TryParse(options.Get(name, fallback.ToString(CultureInfo.InvariantCulture)), NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value : throw new ArgumentException("Invalid integer: " + name);
        switch (mode)
        {
            case "run":
                options.Only("--duration", "--seed", "--property-cases", "--fuzz-cases", "--mutation-limit", "--child-seconds", "--log-bytes", "--artifact-bytes", "--max-batches", "--machine-label", "--run-id", "--baseline", "--skip-coverage");
                var config = new EnduranceOptions(EnduranceRules.Duration(options.Get("--duration", "05:00:00")!), EnduranceRules.Seed(options.Get("--seed", "20261026")!), Integer("--property-cases", 1000), Integer("--fuzz-cases", 2000), Integer("--mutation-limit", 6), Integer("--child-seconds", 300), Integer("--log-bytes", 131072), Integer("--artifact-bytes", 536870912), Integer("--max-batches", 10000), options.Get("--machine-label", "local")!, !options.Has("--skip-coverage"));
                var report = EnduranceCampaign.Run(context.Root, config, options.Get("--run-id"), options.Get("--baseline"));
                return StatusCode(report.Status);
            case "replay":
                options.Only("--seed", "--property-cases", "--fuzz-cases");
                ulong seed = EnduranceRules.Seed(options.Require("--seed")); int properties = Integer("--property-cases", 1000), fuzz = Integer("--fuzz-cases", 2000);
                EnduranceRules.ValidateOptions(new(TimeSpan.FromSeconds(1), seed, properties, fuzz));
                using (var lease = new EnduranceLease(context.Root))
                {
                    var compiled = EnduranceProcess.Run("dotnet", ["build", "src/SharpTestBench/SharpTestBench.csproj", .. EnduranceCampaign.BuildArguments], context.Root, TimeSpan.FromSeconds(120));
                    if (!compiled.Succeeded) { Console.Error.WriteLine("Replay compilation failed"); return 1; }
                    var replayed = EnduranceProcess.Run("dotnet", ["fsi", "--exec", "tools/Endurance/Properties.fsx", "--seed", seed.ToString(CultureInfo.InvariantCulture), "--property-cases", properties.ToString(CultureInfo.InvariantCulture), "--fuzz-cases", fuzz.ToString(CultureInfo.InvariantCulture)], context.Root, TimeSpan.FromSeconds(120));
                    Console.Write(replayed.Output); Console.Error.Write(System.Text.Encoding.UTF8.GetString(replayed.Stderr)); return replayed.Passed ? 0 : 1;
                }
            case "baseline":
                options.Only("--output", "--strictdoc");
                var baseline = EnduranceVerification.Baseline(context.Root, Path.GetFullPath(options.Require("--output"), context.Root), options.Get("--strictdoc"));
                Console.WriteLine(JsonSerializer.Serialize(baseline)); return baseline.Result.Status == "passed" && baseline.Result.Requirements == "passed" ? 0 : 1;
            case "preflight":
                options.Only("--expected-source", "--machine-label", "--branch");
                string branch = EndurancePublication.Preflight(context.Root, Expected(), options.Get("--machine-label", "local")!, options.Get("--branch"));
                Console.WriteLine(JsonSerializer.Serialize(new { branch, source = Expected() })); return 0;
            case "publish":
                options.Only("--run", "--expected-source", "--branch", "--no-push");
                var publication = EndurancePublication.Publish(context.Root, ReportPath(context.Root, options.Require("--run")), Expected(), options.Get("--branch"), !options.Has("--no-push"));
                Console.WriteLine(JsonSerializer.Serialize(publication)); return publication.Status == "push-failed" ? 2 : 0;
            case "import":
                options.Only("--summary", "--checksums", "--expected-source");
                Console.WriteLine(EndurancePublication.Import(context.Root, Path.GetFullPath(options.Require("--summary")), Path.GetFullPath(options.Require("--checksums")), Expected())); return 0;
            case "review":
                options.Only("--commit", "--expected-source");
                Console.WriteLine(JsonSerializer.Serialize(EndurancePublication.Review(context.Root, options.Require("--commit"), Expected()), EnduranceStorage.Json)); return 0;
            case "recover":
                options.Only("--run"); Recover(context.Root, ReportPath(context.Root, options.Require("--run"))); return 0;
            default: throw new ArgumentException("Unknown endurance command");
        }
    }
    internal static int StatusCode(string status) => status switch { "completed-budget" => 0, "partial" => 3, "interrupted" => 130, "unavailable" => 4, _ => 1 };
    internal static string ReportPath(string root, string run)
    {
        string path = Guid.TryParseExact(run, "N", out _) ? Path.Combine(root, "build/endurance", run, "report.json") : Path.GetFullPath(run, root);
        EnduranceStorage.NoLinks(root, path); return path;
    }
    internal static void Recover(string root, string path)
    {
        using var lease = new EnduranceLease(root); var report = EnduranceStorage.ReadJson<EnduranceReport>(path); EnduranceRules.Validate(report, false);
        if (report.Status != "running" || report.Imported) throw new InvalidDataException("Only an interrupted local running checkpoint can be recovered");
        string work = Path.GetDirectoryName(path)!; var owner = EnduranceStorage.ReadJson<EnduranceLocal>(Path.Combine(work, "local-owner.json"));
        try
        {
            using var process = Process.GetProcessById(owner.ProcessId);
            if (!process.HasExited && new DateTimeOffset(process.StartTime.ToUniversalTime()) == owner.ProcessStartedUtc) throw new IOException("Campaign owner is still alive");
        }
        catch (ArgumentException) { }
        // A crash between a ring-slot replacement and a checkpoint can leave a
        // stale capture hash. Omit that capture rather than inventing its content.
        foreach (string name in report.LogHashes.Keys.ToArray())
            if (!File.Exists(Path.Combine(work, name)) || EnduranceStorage.HashFile(Path.Combine(work, name)) != report.LogHashes[name]) report.LogHashes.Remove(name);
        report.SnapshotUnchanged = EnduranceGit.Matches(Path.Combine(work, "source"), report.SourceHashes);
        report.Status = report.SnapshotUnchanged ? "interrupted" : "failed"; report.Reason = "recovered-after-interruption"; report.FinishedUtc = DateTimeOffset.UtcNow; report.ActualSecondsIsLowerBound=true;
        EnduranceRules.Validate(report); EnduranceStorage.WriteJson(path, report);
    }
}
