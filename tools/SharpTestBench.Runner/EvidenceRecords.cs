internal sealed record InputManifestEvidence(int SchemaVersion, string RunId, InputFingerprint[] Inputs);
internal sealed record SourceEvidence(string Path, string Sha256);
internal sealed record RunEvidence
{
    public InputFingerprint? Compiled { get; init; }
    public int SchemaVersion { get; init; } = 1;
    public string RunId { get; init; } = "";
    public string Simulator { get; init; } = "";
    public string Top { get; init; } = "";
    public ulong Seed { get; init; }
    public InputFingerprint[] Inputs { get; init; } = [];
    public string Platform { get; init; } = "";
    public string Architecture { get; init; } = "";
    public string Framework { get; init; } = "";
    public SourceEvidence[] Sources { get; init; } = [];
    public string AssemblySha256 { get; init; } = "";
    public string Results { get; init; } = "";
    public int Passed { get; init; }
    public int Failed { get; init; }
    public string[] TestNames { get; init; } = [];
    public string Status { get; init; } = "";
}
internal sealed record FailureEvidence
{
    public int SchemaVersion { get; init; } = 1;
    public string RunId { get; init; } = "";
    public string Status { get; init; } = "failed";
    public string Kind { get; init; } = "";
    public string? ErrorType { get; init; }
    public string Error { get; init; } = "";
    public DateTimeOffset RecordedAtUtc { get; init; }
    public string[] LogFiles { get; init; } = [];
}
internal sealed record ProcessEvidence(int SchemaVersion, string Phase, string Executable, string[] Arguments,
    string? Failure, bool Started, int? ExitCode, bool TimedOut, double WallMilliseconds,
    int MaxLogBytes, DrainResult? Stdout, DrainResult? Stderr);
