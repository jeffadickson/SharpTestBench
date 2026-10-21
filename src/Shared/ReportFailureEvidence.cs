namespace SharpTestBench.Internal;

// This diagnostic is emitted by the CLR-hosted test runtime and consumed by
// both parent orchestration paths, even if the simulator itself exits zero.
internal static class ReportFailureEvidence
{
    internal const string Marker = "CS_REPORT_FAILURE ";
    internal static void ThrowIfReported(IEnumerable<string> lines)
    {
        if (lines.Any(line => line.StartsWith(Marker, StringComparison.Ordinal)))
            throw new IOException("Simulator could not publish complete result artifacts.");
    }
}
