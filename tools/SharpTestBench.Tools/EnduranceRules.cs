using System.Globalization;
using System.Text.RegularExpressions;

namespace SharpTestBench.Tools;

/// <summary>Pure acceptance rules shared by campaign checkpoints, publication, import and replay.</summary>
internal static class EnduranceRules
{
    internal const int MaximumReportBytes = 524288;
    internal const string StrykerVersion="5.0.0";
    internal const string StrykerScope="Stryker.NET character-span selection around the requested EnduranceRules guard conditions; not whole-project mutation strength";
    internal static bool StrykerHasFindings(EnduranceStryker value)=>value.Status!="completed"||value.Mutants.Any(item=>item.Outcome is not ("killed" or "ignored"));
    internal static void ValidateStryker(EnduranceStryker value)
    {
        if(value is null||value.Version!=StrykerVersion||value.Scope!=StrykerScope||value.Status is not ("not-run" or "completed" or "partial" or "failed" or "timed-out" or "unavailable")||value.Mutants is null||value.Excluded<0||value.ExcludedInvalid<0||value.ExcludedInvalid>value.Excluded||value.Mutants.Count+value.Excluded>4096||value.Mutants.Any(item=>item is null||!Regex.IsMatch(item.Id,"\\Astryker-[0-9]{1,8}\\z",RegexOptions.CultureInvariant)||item.Outcome is not ("killed" or "survived" or "invalid" or "timed-out" or "no-coverage" or "ignored" or "pending"))||value.Mutants.Select(item=>item.Id).Distinct(StringComparer.Ordinal).Count()!=value.Mutants.Count||value.ReportSha256 is not null&&!Hash(value.ReportSha256)||value.Status=="completed"&&(value.ReportSha256 is null||!value.Mutants.Any(item=>item.Outcome!="ignored")||value.Mutants.Any(item=>item.Outcome=="pending")))throw new InvalidDataException("Invalid Stryker evidence");
    }
    internal static readonly string[] MutationIds = ["finished", "path", "hash", "dirty", "revision", "coverage"];
    internal static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static bool Commit(string? value) => value is { Length: 40 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static bool Label(string? value) => value is not null && Regex.IsMatch(value, "\\A[a-z0-9][a-z0-9-]{0,39}\\z", RegexOptions.CultureInvariant);
    internal static bool Relative(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 240 || value.Contains('\\') || value.Contains(':') || value.Any(char.IsControl)) return false;
        return !value.StartsWith('/') && value.Split('/').All(part => part.Length > 0 && part is not "." and not ".." && part.Trim() == part && !part.EndsWith('.') && !part.Equals(".git", StringComparison.OrdinalIgnoreCase) && part.IndexOfAny(['<','>','"','|','?','*']) < 0 && !Regex.IsMatch(part, "\\A(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\\.|\\z)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    }
    internal static TimeSpan Duration(string value)
    {
        if (!TimeSpan.TryParseExact(value, ["c", "g"], CultureInfo.InvariantCulture, out var duration) || duration < TimeSpan.FromSeconds(1) || duration > TimeSpan.FromDays(1))
            throw new ArgumentException("Duration must be between one second and 24 hours (hh:mm:ss)");
        return duration;
    }
    internal static ulong Seed(string value) => ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed) ? seed : throw new ArgumentException("Seed must be an unsigned 64-bit integer");
    // A Weyl sequence with an odd increment visits every UInt64 seed before
    // repeating. FsCheck owns random generation; this is not a security RNG.
    internal static ulong Next(ulong seed)
    {
        return unchecked(seed + 0x9e3779b97f4a7c15UL);
    }
    internal static void ValidateOptions(EnduranceOptions value)
    {
        if (value.Duration < TimeSpan.FromSeconds(1) || value.Duration > TimeSpan.FromDays(1) || value.PropertyCases is < 1 or > 10000 || value.FuzzCases is < 1 or > 100000 || value.MutationLimit is < 1 or > 6 || value.ChildSeconds is < 1 or > 1800 || value.MaximumLogBytes is < 4096 or > 1048576 || value.MaximumArtifactBytes is < 1048576 or > 1073741824 || value.MaximumBatches is < 1 or > 10000 || !Label(value.MachineLabel))
            throw new ArgumentException("Endurance configuration exceeds its documented bounds");
    }
    internal static void RequireFinished(string status)
    {
        if (status is not ("completed-budget" or "partial" or "interrupted" or "failed" or "unavailable")) // MUTATE:finished
            throw new InvalidDataException("Unfinished campaign");
    }
    internal static void RequirePath(string path)
    {
        if (!Relative(path)) // MUTATE:path
            throw new InvalidDataException("Unsafe portable path");
    }
    internal static void RequireHash(string expected, string actual)
    {
        if (!Hash(expected) || expected != actual) // MUTATE:hash
            throw new InvalidDataException("Stale source hash");
    }
    internal static void RequireClean(bool dirty)
    {
        if (dirty) // MUTATE:dirty
            throw new InvalidDataException("Dirty source cannot be published");
    }
    internal static void RequireRevision(string recorded, string expected)
    {
        if (!Commit(recorded) || recorded != expected) // MUTATE:revision
            throw new InvalidDataException("Source revision differs");
    }
    internal static void ValidateCoverage(EnduranceCoverage value)
    {
        if (value is null) throw new InvalidDataException("Missing coverage evidence");
        bool counts = value.LinesCovered is >= 0 && value.LinesValid is > 0 && value.LinesCovered <= value.LinesValid && value.BranchesCovered is >= 0 && value.BranchesValid is > 0 && value.BranchesCovered <= value.BranchesValid;
        if (value.Scope != "SharpTestBench.Tools.Endurance* unit/integration tests; excludes other production types, live campaign execution, HDL and native/AOT instructions" ||
            (value.Status == "available" ? !counts : value.Status is not ("unavailable" or "failed" or "not-requested") || value.LinesCovered is not null || value.LinesValid is not null || value.BranchesCovered is not null || value.BranchesValid is not null)) // MUTATE:coverage
            throw new InvalidDataException("Invalid coverage evidence");
    }
    internal const string CoverageScope = "SharpTestBench.Tools.Endurance* unit/integration tests; excludes other production types, live campaign execution, HDL and native/AOT instructions";
    internal static void Validate(EnduranceReport report, bool finished = true)
    {
        if (finished) RequireFinished(report.Status);
        else if (report.Status != "running") RequireFinished(report.Status);
        if (report.SchemaVersion != 1 || !Guid.TryParseExact(report.RunId, "N", out _) || !Commit(report.SourceCommit) || report.Platform is not ("windows" or "linux") || report.Architecture is not ("x64" or "arm64") || !Label(report.MachineLabel) || report.StartedUtc.Offset != TimeSpan.Zero || (report.FinishedUtc is { } end && (end.Offset != TimeSpan.Zero || end < report.StartedUtc)) || (report.Status == "running") != (report.FinishedUtc is null))
            throw new InvalidDataException("Invalid campaign identity or lifecycle");
        if (!double.IsFinite(report.RequestedSeconds) || report.RequestedSeconds is < 1 or > 86400 || !double.IsFinite(report.ActualSeconds) || report.ActualSeconds < 0 || !double.IsFinite(report.SetupSeconds) || report.SetupSeconds < 0 || !double.IsFinite(report.CoverageSeconds) || report.CoverageSeconds < 0 || report.PropertyCasesPerBatch is < 1 or > 10000 || report.FuzzCasesPerBatch is < 1 or > 100000 || report.CompletedBatches is < 0 or > 10000 || report.CompletedPropertyCases != (long)report.CompletedBatches * 3 * report.PropertyCasesPerBatch || report.CompletedFuzzCases != (long)report.CompletedBatches * report.FuzzCasesPerBatch)
            throw new InvalidDataException("Invalid duration or completed case accounting");
        if (report.RandomizedTesting != "deterministic randomized FsCheck properties and malformed regex testing; not coverage-guided fuzzing" || !Label(report.Reason) || report.SourceHashes is null || report.SourceHashes.Count is < 1 or > 4096 || report.LogHashes is null || report.LogHashes.Count > 512 || report.Tools is null || report.Tools.Count is < 2 or > 9 || report.RequestedMutations is null || report.RequestedMutations.Length is < 1 or > 6 || report.RequestedMutations.Distinct(StringComparer.Ordinal).Count() != report.RequestedMutations.Length || report.RequestedMutations.Any(id => !MutationIds.Contains(id, StringComparer.Ordinal)) || report.Mutations is null || report.Mutations.Any(item => item is null) || report.Mutations.Count > report.RequestedMutations.Length || report.Mutations.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != report.Mutations.Count || report.Mutations.Any(item => !report.RequestedMutations.Contains(item.Id, StringComparer.Ordinal) || item.Outcome is not ("killed" or "survived" or "invalid" or "timed-out")))
            throw new InvalidDataException("Invalid inventory or testing scope");
        foreach (var map in new[] { report.SourceHashes, report.LogHashes }) foreach (var (path, hash) in map) { RequirePath(path); if (!Hash(hash)) throw new InvalidDataException("Invalid evidence hash"); }
        foreach (string name in report.LogHashes.Keys) if (!Regex.IsMatch(name, "\\Alogs/[a-z0-9-]+\\.(stdout|stderr)\\.log\\z", RegexOptions.CultureInvariant)) throw new InvalidDataException("Non-log artifact in capture inventory");
        if(new[]{"dotnet-sdk","git","fscheck","coverlet","os-architecture"}.Any(name=>!report.Tools.ContainsKey(name)))throw new InvalidDataException("Required tool identity missing");
        foreach (var (name, version) in report.Tools) if (!new[]{"dotnet-sdk","git","fscheck","coverlet","os-architecture","powershell","python","strictdoc","stryker"}.Contains(name,StringComparer.Ordinal) || version is null || version.Length is < 1 or > 80 || !Regex.IsMatch(version, "\\A[a-zA-Z0-9 .+_-]+\\z", RegexOptions.CultureInvariant)) throw new InvalidDataException("Unsafe tool version");
        if (report.ImportReexecutedChecks || report.FailureInputSha256 is not null && !Hash(report.FailureInputSha256) || report.Imported != (report.ImportedEvidenceSha256 is not null) || report.ImportedEvidenceSha256 is not null && !Hash(report.ImportedEvidenceSha256)) throw new InvalidDataException("Invalid failure/import provenance");
        ulong next = report.InitialSeed; for (int i = 0; i < report.CompletedBatches; i++) next = Next(next);
        if (report.NextSeed != next) throw new InvalidDataException("Replay seed chain differs");
        if (report.FailureReplay is not null && report.FailureReplay != Replay(next, report.PropertyCasesPerBatch, report.FuzzCasesPerBatch)) throw new InvalidDataException("Unsafe replay command");
        if (report.Baseline is null || report.Baseline.Status is not ("passed" or "failed" or "unavailable" or "not-run") || report.Baseline.Scope != "Core, Generators and Endurance Tools tests; no simulator qualification" || report.Baseline.Passed < 0 || report.Baseline.Skipped < 0 || report.Baseline.Requirements is not ("passed" or "failed" or "unavailable" or "not-run")) throw new InvalidDataException("Invalid baseline evidence");
        ValidateCoverage(report.Coverage);
        ValidateStryker(report.Stryker);
        if (report.CompilationStatus is not ("passed" or "failed" or "not-run" or "timed-out") || report.MutationControlStatus is not ("passed" or "failed" or "not-run" or "invalid" or "timed-out")) throw new InvalidDataException("Invalid compilation/control outcome");
        if (report.Limits is not { Concurrency: 1, ChildSeconds: >= 1 and <= 1800, LogBytes: >= 4096 and <= 1048576, ArtifactBytes: >= 1048576 and <= 1073741824, Batches: >= 1 and <= 10000, SourceBytes: 268435456 } || report.CompletedBatches > report.Limits.Batches) throw new InvalidDataException("Invalid recorded resource bounds");
        if (!report.SnapshotUnchanged && report.Status is not ("failed" or "running")) throw new InvalidDataException("Changed tested snapshot");
        if(report.ActualSecondsIsLowerBound && report.Status is not ("interrupted" or "failed"))throw new InvalidDataException("Recovered duration must remain a lower bound");
        if (report.Status == "completed-budget" && (report.ActualSeconds < report.RequestedSeconds || report.CompletedBatches == 0 || StrykerHasFindings(report.Stryker) || report.CompilationStatus != "passed" || report.MutationControlStatus != "passed" || report.Mutations.Count != report.RequestedMutations.Length || report.Mutations.Any(item => item.Outcome != "killed") || report.FailureInputSha256 is not null || !report.SnapshotUnchanged || report.Coverage.Status == "failed" || report.Baseline.Status == "failed" || report.Baseline.Requirements == "failed")) throw new InvalidDataException("Incomplete budget outcome");
    }
    internal static string Replay(ulong seed, int properties, int fuzz) => $"pwsh -NoProfile -File tools/Endurance.ps1 -Mode Replay -Seed {seed.ToString(CultureInfo.InvariantCulture)} -PropertyCases {properties} -FuzzCases {fuzz}";
}
