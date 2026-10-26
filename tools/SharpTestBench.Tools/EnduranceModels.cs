using System.Text.Json.Serialization;

namespace SharpTestBench.Tools;

// Portable records contain no filesystem roots, environment dumps or raw inputs.
// Local diagnostics and source snapshots live beside these records, never in Git.
internal sealed record EnduranceMutation(string Id, string Outcome);
internal sealed record EnduranceStryker(string Version, string Status, string Scope, List<EnduranceMutation> Mutants, string? ReportSha256, int Excluded = 0, int ExcludedInvalid = 0);
internal sealed record EnduranceCoverage(string Status, string Scope, long? LinesCovered = null, long? LinesValid = null,
    long? BranchesCovered = null, long? BranchesValid = null);
internal sealed record EnduranceBaseline(string Status, string Scope, long Passed = 0, long Skipped = 0, string Requirements = "not-run");
internal sealed record EnduranceLimits(int Concurrency, int ChildSeconds, int LogBytes, long ArtifactBytes, int Batches, long SourceBytes);
internal sealed record EnduranceBaselineEvidence(string SourceCommit, string SourceDigest, EnduranceBaseline Result);
internal sealed class EnduranceReport
{
    public required int SchemaVersion { get; init; }
    public required string RunId { get; init; }
    public required string SourceCommit { get; init; }
    public required bool Dirty { get; init; }
    public required bool Imported { get; set; }
    public required string Platform { get; init; }
    public required string Architecture { get; init; }
    public required string MachineLabel { get; init; }
    public required DateTimeOffset StartedUtc { get; init; }
    public required DateTimeOffset? FinishedUtc { get; set; }
    public required string Status { get; set; }
    public required string Reason { get; set; }
    public required double RequestedSeconds { get; init; }
    public required double ActualSeconds { get; set; }
    public required double SetupSeconds { get; set; }
    public required ulong InitialSeed { get; init; }
    public required ulong NextSeed { get; set; }
    public required int PropertyCasesPerBatch { get; init; }
    public required int FuzzCasesPerBatch { get; init; }
    public required int CompletedBatches { get; set; }
    public required long CompletedPropertyCases { get; set; }
    public required long CompletedFuzzCases { get; set; }
    public required bool SnapshotUnchanged { get; set; }
    public required string[] RequestedMutations { get; init; }
    public required List<EnduranceMutation> Mutations { get; init; }
    public required SortedDictionary<string, string> SourceHashes { get; init; }
    public required SortedDictionary<string, string> LogHashes { get; init; }
    public required SortedDictionary<string, string> Tools { get; init; }
    public required EnduranceBaseline Baseline { get; set; }
    public required EnduranceCoverage Coverage { get; set; }
    public required string RandomizedTesting { get; init; }
    public required string? FailureInputSha256 { get; set; }
    public required string? FailureReplay { get; set; }
    public required string? ImportedEvidenceSha256 { get; set; }
    public required EnduranceLimits Limits { get; init; }
    public required bool LogsTruncated { get; set; }
    public required string CompilationStatus { get; set; }
    public required string MutationControlStatus { get; set; }
    public required double CoverageSeconds { get; set; }
    public required bool ImportReexecutedChecks { get; set; }
    public required bool ActualSecondsIsLowerBound { get; set; }
    public required EnduranceStryker Stryker { get; set; }
}

internal sealed record EnduranceBatch(ulong Seed, int PropertiesCompleted, int PropertyCasesCompleted,
    int FuzzCasesCompleted, int Accepted, int Rejected, int MatchTimeouts, string Status, string? LastFailingInput,
    string? FailureDiagnostic);

internal sealed record EnduranceLocal(int ProcessId, DateTimeOffset ProcessStartedUtc);

internal sealed record EnduranceOptions(TimeSpan Duration, ulong Seed, int PropertyCases = 1000, int FuzzCases = 2000,
    int MutationLimit = 6, int ChildSeconds = 300, int MaximumLogBytes = 131072, long MaximumArtifactBytes = 536870912,
    int MaximumBatches = 10000, string MachineLabel = "local", bool Coverage = true);
