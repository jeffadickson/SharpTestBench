using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace SharpTestBench.Tools;

/// <summary>One checkout lease, immutable source copy, one monotonic campaign budget and one child at a time.</summary>
internal static class EnduranceCampaign
{
    internal static readonly string[] BuildArguments = ["-c", "Release", "-p:RestoreLockedMode=true", "-p:NoWarn=CS1591", "--disable-build-servers", "-m:1", "--nologo", "-v:quiet"];
    internal const string BuildTarget = "tools/Endurance/Endurance.slnx";
    internal static string Platform => OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsLinux() ? "linux" : throw new PlatformNotSupportedException("Endurance supports Windows and Linux");
    internal static string Architecture => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
    internal static string SourceDigest(IReadOnlyDictionary<string, string> source) => EnduranceStorage.Hash(EnduranceStorage.Serialize(source));
    internal static EnduranceReport Run(string root, EnduranceOptions options, string? runId = null, string? baseline = null,
        IEnduranceExecution? execution = null, CancellationToken cancellation = default)
    {
        EnduranceRules.ValidateOptions(options); execution ??= new EnduranceExecution();
        using var lease = new EnduranceLease(root);
        runId ??= Guid.NewGuid().ToString("N");
        if (!Guid.TryParseExact(runId, "N", out _)) throw new ArgumentException("Invalid run ID");
        string work = Path.Combine(root, "build/endurance", runId), snapshot = Path.Combine(work, "source");
        EnduranceStorage.NoLinks(root, work);
        if (Directory.Exists(work)) throw new IOException("Run already exists; prior evidence is preserved");
        var setupClock = Stopwatch.StartNew(); var git = new EnduranceGit(root);
        string sourceCommit = git.Head; bool dirty = git.Dirty;
        var hashes = git.Snapshot(snapshot);
        var report = new EnduranceReport
        {
            SchemaVersion = 1, RunId = runId, SourceCommit = sourceCommit, Dirty = dirty, Imported = false,
            Platform = Platform, Architecture = Architecture, MachineLabel = options.MachineLabel,
            StartedUtc = DateTimeOffset.UtcNow, FinishedUtc = null, Status = "running", Reason = "starting",
            RequestedSeconds = options.Duration.TotalSeconds, ActualSeconds = 0, SetupSeconds = 0,
            InitialSeed = options.Seed, NextSeed = options.Seed, PropertyCasesPerBatch = options.PropertyCases,
            FuzzCasesPerBatch = options.FuzzCases, CompletedBatches = 0, CompletedPropertyCases = 0, CompletedFuzzCases = 0,
            SnapshotUnchanged = true, RequestedMutations = EnduranceRules.MutationIds.Take(options.MutationLimit).ToArray(),
            Mutations = [], SourceHashes = hashes, LogHashes = new(StringComparer.Ordinal), Tools = new(StringComparer.Ordinal)
            {
                ["dotnet-sdk"]="unavailable", ["git"]="unavailable", ["fscheck"]="requested-3.6.0",
                ["coverlet"]="requested-10.1.0", ["os-architecture"]=RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
                ["stryker"]="requested-5.0.0", ["powershell"]=BootstrapVersion("POWERSHELL"),
                ["python"]=BootstrapVersion("PYTHON"), ["strictdoc"]=BootstrapVersion("STRICTDOC")
            },
            Baseline = new("not-run", "Core, Generators and Endurance Tools tests; no simulator qualification"),
            Coverage = new(options.Coverage ? "unavailable" : "not-requested", EnduranceRules.CoverageScope),
            RandomizedTesting = "deterministic randomized FsCheck properties and malformed regex testing; not coverage-guided fuzzing",
            FailureInputSha256 = null, FailureReplay = null, ImportedEvidenceSha256 = null,
            Limits = new(1, options.ChildSeconds, options.MaximumLogBytes, options.MaximumArtifactBytes, options.MaximumBatches, 268435456), LogsTruncated = false,
            CompilationStatus = "not-run", MutationControlStatus = "not-run", CoverageSeconds = 0, ImportReexecutedChecks = false, ActualSecondsIsLowerBound = false,
            Stryker=new(EnduranceStrykerEngine.Version,"not-run",EnduranceStrykerEngine.Scope,[],null)
        };
        using var current = Process.GetCurrentProcess();
        EnduranceStorage.WriteJson(Path.Combine(work, "local-owner.json"), new EnduranceLocal(current.Id, new DateTimeOffset(current.StartTime.ToUniversalTime())));
        EnduranceStorage.WriteJson(Path.Combine(work, "report.json"), report);
        Console.WriteLine("Endurance run: " + work);
        // A recoverable checkpoint must be valid even before the first external
        // tool starts. Setup failures are finished evidence, never abandoned runs.
        string? version = null; string? expectedSdk = null;
        try
        {
        cancellation.ThrowIfCancellationRequested();
        if (baseline is not null)
        {
            var evidence = EnduranceStorage.ReadJson<EnduranceBaselineEvidence>(baseline);
            EnduranceRules.RequireRevision(evidence.SourceCommit, report.SourceCommit);
            EnduranceRules.RequireHash(evidence.SourceDigest, SourceDigest(hashes)); report.Baseline = evidence.Result;
        }
        var sdk = execution.Run(["dotnet", "--version"], snapshot, TimeSpan.FromSeconds(15), options.MaximumLogBytes, cancellation);
        version = sdk.Passed ? sdk.Output.Trim() : null;
        report.Tools["dotnet-sdk"] = version is not null && System.Text.RegularExpressions.Regex.IsMatch(version, "\\A[0-9.]+\\z") ? version : "unavailable";
        report.Tools["git"] = git.Text("--version"); report.Tools["fscheck"] = "requested-3.6.0"; report.Tools["coverlet"] = "requested-10.1.0";
        report.Tools["os-architecture"] = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
        report.Tools["stryker"]="requested-5.0.0";
        using var selection = JsonDocument.Parse(File.ReadAllText(Path.Combine(snapshot, "global.json")));
        expectedSdk = selection.RootElement.GetProperty("sdk").GetProperty("version").GetString()!;
        EnduranceRules.Validate(report, false);
        }
        catch (Exception error) when (error is IOException or JsonException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            // A malformed baseline may have supplied invalid nested fields;
            // retain the setup failure without promoting that untrusted result.
            report.Baseline = new("not-run", EnduranceVerification.BaselineScope);
            report.SetupSeconds = setupClock.Elapsed.TotalSeconds;
            report.Status = error is OperationCanceledException ? "interrupted" : error is System.ComponentModel.Win32Exception ? "unavailable" : "failed";
            report.Reason = error is OperationCanceledException ? "setup-interrupted" : "setup-failed";
            report.FinishedUtc = DateTimeOffset.UtcNow;
            EnduranceRules.Validate(report);
            EnduranceStorage.WriteJson(Path.Combine(work, "report.json"), report);
            return report;
        }
        report.SetupSeconds = setupClock.Elapsed.TotalSeconds;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        bool artifactLimit = false;
        // Stop requests work for detached launches too. Resource scanning is at
        // most once per second; this is a retention stop, not a disk-quota claim.
        long scanTicks = 0; int scanning = 0;
        using var monitor = new Timer(_ =>
        {
            try
            {
                if (File.Exists(Path.Combine(work, "stop.request"))) stop.Cancel();
                long now = Stopwatch.GetTimestamp();
                if (Stopwatch.GetElapsedTime(Interlocked.Read(ref scanTicks), now) < TimeSpan.FromSeconds(1) || Interlocked.Exchange(ref scanning, 1) != 0) return;
                try
                {
                    Interlocked.Exchange(ref scanTicks, now);
                    long bytes = Directory.EnumerateFiles(work, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint })
                        .Where(path => !hashes.ContainsKey(Path.GetRelativePath(snapshot, path).Replace('\\', '/')))
                        .Sum(path => { try { return new FileInfo(path).Length; } catch (IOException) { return 0; } });
                    if (bytes > options.MaximumArtifactBytes) { artifactLimit = true; stop.Cancel(); }
                }
                finally { Interlocked.Exchange(ref scanning, 0); }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { artifactLimit = true; stop.Cancel(); }
            catch (ObjectDisposedException) { }
        }, null, 100, 100);
        using var consoleCancellation = new EnduranceCancellation(stop);
        execution.StartBudget();
        try
        {
            if (version != expectedSdk) Finish("unavailable", "sdk-unavailable");
            else if (report.Baseline.Status == "failed" || report.Baseline.Requirements == "failed") Finish("failed", "baseline-failed");
            else if (report.Baseline.Status == "unavailable" || report.Baseline.Requirements == "unavailable") Finish("unavailable", "baseline-unavailable");
            else
            {
                var compiled = RunChild("snapshot-build", ["dotnet", "build", BuildTarget, .. BuildArguments], snapshot);
                report.CompilationStatus = compiled.Succeeded ? "passed" : compiled.TimedOut ? "timed-out" : "failed";
                if (!compiled.Succeeded) EndChild(compiled, "compile-failed");
                else
                {
                    ExecuteStryker();
                }
            }
        }
        catch (OperationCanceledException) { Finish(artifactLimit || !stop.IsCancellationRequested ? "partial" : "interrupted", artifactLimit ? "artifact-limit" : !stop.IsCancellationRequested ? "budget-between-children" : "stop-request"); }
        catch (Exception error) when (error is IOException or JsonException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            EnduranceStorage.Write(Path.Combine(work, "failure.txt"), Encoding.UTF8.GetBytes(error.ToString()[..Math.Min(error.ToString().Length, 8192)]));
            Finish("failed", "campaign-error");
        }
        void ExecuteStryker()
        {
            var tools=RunChild("mutation-tools",["dotnet","tool","restore"],snapshot);
            if(!tools.Succeeded){report.Stryker=report.Stryker with{Status="unavailable"};EndChild(tools,"stryker-unavailable");return;}
            var prepared=EnduranceStrykerEngine.Prepare(snapshot,work,report.RequestedMutations);
            var testedEngine=RunChild("stryker-run",["dotnet","tool","run","dotnet-stryker","--config-file",prepared.Config,"--output",prepared.Output],prepared.TestDirectory);
            report.Stryker=report.Stryker with{Status=testedEngine.TimedOut?"timed-out":"failed"};
            if(!testedEngine.Succeeded){EndChild(testedEngine,"stryker-failed");return;}
            report.Stryker=EnduranceStrykerEngine.Read(prepared.Output,File.ReadAllText(Path.Combine(snapshot,"tools/SharpTestBench.Tools/EnduranceRules.cs")),report.RequestedMutations);
            report.Tools["stryker"]="5.0.0";Checkpoint();
            if(report.Stryker.Status!="completed"){Finish("partial","stryker-incomplete");return;}
            {
                    string control = EnduranceMutations.Prepare(snapshot, Path.Combine(work, "mutations/control"), null);
                    var build = RunChild("control-build", ["dotnet", "build", Path.Combine(work, "mutations/control/Probe.csproj"), .. BuildArguments], snapshot);
                    if (!build.Succeeded) { report.MutationControlStatus = build.TimedOut ? "timed-out" : "invalid"; EndChild(build, "control-invalid"); }
                    else
                    {
                        var tested = RunChild("control-run", ["dotnet", control, "all"], snapshot);
                        if (!tested.Passed || tested.Output.Trim() != "ENDURANCE_PROBE_PASSED") { report.MutationControlStatus = tested.TimedOut ? "timed-out" : "failed"; EndChild(tested, "control-failed"); }
                        else { report.MutationControlStatus = "passed"; ExecuteMutations(); }
                    }
            }
        }
        if(report.Status=="completed-budget"&&EnduranceStrykerEngine.HasFindings(report.Stryker))Finish("failed","stryker-findings");
        report.ActualSeconds = execution.Elapsed.TotalSeconds;
        report.SnapshotUnchanged = EnduranceGit.Matches(snapshot, hashes);
        if (!report.SnapshotUnchanged) Finish("failed", "source-changed");
        if (options.Coverage && version == expectedSdk && !stop.IsCancellationRequested)
        {
            var coverageClock = Stopwatch.StartNew();
            try { report.Coverage = EnduranceVerification.Coverage(snapshot, work, report, execution, stop.Token); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or OperationCanceledException)
            {
                // Coverage is outside the campaign budget, but its startup can
                // still fail. Preserve verified work in finished failure evidence.
                report.Coverage = new(error is System.ComponentModel.Win32Exception ? "unavailable" : "failed", EnduranceRules.CoverageScope);
                Finish(error is OperationCanceledException ? "interrupted" : "failed", "coverage-error");
            }
            if(report.Coverage.Status=="available")report.Tools["coverlet"]="10.1.0";
            report.CoverageSeconds = coverageClock.Elapsed.TotalSeconds;
            if (stop.IsCancellationRequested) Finish(artifactLimit ? "partial" : "interrupted", artifactLimit ? "artifact-limit" : "stop-request");
            else if (report.Coverage.Status == "failed" && report.Status != "interrupted") Finish("failed", "coverage-failed");
        }
        else if (options.Coverage) report.Coverage = new("unavailable", EnduranceRules.CoverageScope);
        report.SnapshotUnchanged = EnduranceGit.Matches(snapshot, hashes);
        if (!report.SnapshotUnchanged) Finish("failed", "source-changed");
        // ActualSeconds is captured before the explicitly outside-budget coverage
        // attempt. Source preparation and bounded cleanup are reported separately.
        report.FinishedUtc = DateTimeOffset.UtcNow;
        EnduranceRules.Validate(report); EnduranceStorage.WriteJson(Path.Combine(work, "report.json"), report);
        Console.WriteLine($"Endurance {report.Status}: {report.CompletedBatches} verified batches, {report.CompletedPropertyCases} property cases, {report.CompletedFuzzCases} randomized malformed inputs.");
        return report;

        TimeSpan Remaining() => options.Duration - execution.Elapsed;
        void Checkpoint() { report.ActualSeconds = execution.Elapsed.TotalSeconds; EnduranceRules.Validate(report, false); EnduranceStorage.WriteJson(Path.Combine(work, "report.json"), report); }
        void Finish(string status, string reason) { report.Status = status; report.Reason = reason; report.FinishedUtc = DateTimeOffset.UtcNow; }
        void EndChild(EnduranceProcessResult result, string fallback)
        {
            if (stop.IsCancellationRequested) Finish(artifactLimit ? "partial" : "interrupted", artifactLimit ? "artifact-limit" : "stop-request");
            else if (Remaining() <= TimeSpan.Zero) Finish("partial", "budget-during-child");
            else Finish(result.TimedOut ? "partial" : "failed", result.TimedOut ? "child-timeout" : fallback);
        }
        EnduranceProcessResult RunChild(string name, string[] command, string directory)
        {
            stop.Token.ThrowIfCancellationRequested();
            var remaining = Remaining(); if (remaining <= TimeSpan.Zero) throw new OperationCanceledException();
            var result = execution.Run(command, directory, remaining < TimeSpan.FromSeconds(options.ChildSeconds) ? remaining : TimeSpan.FromSeconds(options.ChildSeconds), options.MaximumLogBytes, stop.Token);
            SaveLogs(work, report, name, result); Checkpoint(); return result;
        }
        void ExecuteMutations()
        {
            foreach (string id in report.RequestedMutations)
            {
                if (Remaining() <= TimeSpan.Zero) { Finish("partial", "budget-before-mutations"); return; }
                string probe = EnduranceMutations.Prepare(snapshot, Path.Combine(work, "mutations", id), id);
                var built = RunChild("mutant-" + id + "-build", ["dotnet", "build", Path.Combine(work, "mutations", id, "Probe.csproj"), .. BuildArguments], snapshot);
                string outcome;
                if (!built.Succeeded) outcome = built.TimedOut || built.Interrupted ? "timed-out" : "invalid";
                else
                {
                    var tested = RunChild("mutant-" + id + "-run", ["dotnet", probe, id], snapshot);
                    outcome = tested.TimedOut || tested.Interrupted ? "timed-out" : tested.Settled && !tested.Truncated && tested.ExitCode == 1 && tested.Output.Trim() == "ENDURANCE_MUTANT_DETECTED:" + id ? "killed" : tested.Passed ? "survived" : "invalid";
                }
                report.Mutations.Add(new(id, outcome)); Checkpoint();
                if (outcome != "killed") { Finish(outcome == "timed-out" ? "partial" : "failed", "mutant-" + outcome); return; }
            }
            while (Remaining() > TimeSpan.Zero && !stop.IsCancellationRequested)
            {
                if (report.CompletedBatches >= options.MaximumBatches) { Finish("partial", "batch-limit"); return; }
                var result = RunChild("batch-slot-" + report.CompletedBatches % 8,
                    ["dotnet", "fsi", "--exec", Path.Combine(snapshot, "tools/Endurance/Properties.fsx"), "--seed", report.NextSeed.ToString(CultureInfo.InvariantCulture), "--property-cases", options.PropertyCases.ToString(CultureInfo.InvariantCulture), "--fuzz-cases", options.FuzzCases.ToString(CultureInfo.InvariantCulture)], snapshot);
                string? line = result.Output.Split('\n').LastOrDefault(value => value.StartsWith("ENDURANCE_BATCH ", StringComparison.Ordinal));
                EnduranceBatch? batch = line is null ? null : JsonSerializer.Deserialize<EnduranceBatch>(line[16..], EnduranceStorage.Json);
                if (!result.Passed || batch is null || batch.Status != "passed")
                {
                    const string inputPrefix="ENDURANCE_FAILING_INPUT ";
                    string? firstInput=result.Output.Split('\n').FirstOrDefault(value=>value.StartsWith(inputPrefix,StringComparison.Ordinal));
                    string? input = batch?.LastFailingInput ?? firstInput?[inputPrefix.Length..];
                    if (input is not null)
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(input); EnduranceStorage.Write(Path.Combine(work, "failure-input.json"), bytes);
                        report.FailureInputSha256 = EnduranceStorage.Hash(bytes);
                    }
                    if (batch is not null) EnduranceStorage.WriteJson(Path.Combine(work, "failed-batch.json"), batch);
                    if (input is not null || batch?.Status == "failed") { report.FailureReplay = EnduranceRules.Replay(report.NextSeed, options.PropertyCases, options.FuzzCases); Finish("failed", "property-or-fuzz-failure"); }
                    else if (result.TimedOut && Remaining() <= TimeSpan.Zero && !stop.IsCancellationRequested && report.CompletedBatches > 0) Finish("completed-budget", "budget-unverified-final-batch");
                    else { report.FailureReplay = EnduranceRules.Replay(report.NextSeed, options.PropertyCases, options.FuzzCases); EndChild(result, "batch-failed"); }
                    return;
                }
                if (batch.Seed != report.NextSeed || batch.PropertiesCompleted != 3 || batch.PropertyCasesCompleted != 3 * options.PropertyCases || batch.FuzzCasesCompleted != options.FuzzCases || batch.Accepted < 0 || batch.Rejected < 0 || batch.Accepted + batch.Rejected != batch.FuzzCasesCompleted || batch.MatchTimeouts < 0 || batch.MatchTimeouts > batch.Accepted) throw new InvalidDataException("Worker batch accounting differs");
                report.CompletedBatches++; report.CompletedPropertyCases += batch.PropertyCasesCompleted; report.CompletedFuzzCases += batch.FuzzCasesCompleted;
                report.Tools["fscheck"]="3.6.0";
                report.NextSeed = EnduranceRules.Next(report.NextSeed); Checkpoint();
            }
            if (stop.IsCancellationRequested) Finish(artifactLimit ? "partial" : "interrupted", artifactLimit ? "artifact-limit" : "stop-request");
            else Finish(report.CompletedBatches > 0 ? "completed-budget" : "partial", "budget-exhausted");
        }
    }
    // Bootstrap identities contain versions only, never executable paths or raw
    // environment values that might contain private machine information.
    static string BootstrapVersion(string name)
    {
        string? value=Environment.GetEnvironmentVariable("STB_ENDURANCE_"+name+"_VERSION");
        return value is not null && value.Length<=80 && System.Text.RegularExpressions.Regex.IsMatch(value,"\\A[a-zA-Z0-9 .+_-]+\\z") ? value : "unavailable";
    }
    internal static void SaveLogs(string work, EnduranceReport report, string name, EnduranceProcessResult result)
    {
        EnduranceRules.RequirePath(name);
        foreach (var (stream, content) in new[] { ("stdout", result.Stdout), ("stderr", result.Stderr) })
        {
            string relative = "logs/" + name + "." + stream + ".log";
            EnduranceStorage.Write(Path.Combine(work, relative), content); report.LogHashes[relative] = EnduranceStorage.Hash(content);
        }
        report.LogsTruncated |= result.Truncated;
    }
}
