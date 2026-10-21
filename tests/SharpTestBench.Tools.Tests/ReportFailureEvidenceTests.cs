using SharpTestBench.Internal;
using Xunit;

public sealed class ReportFailureEvidenceTests
{
    [Fact] public void ExplicitPublicationFailureOverridesSimulatorSuccess()
    {
        Assert.Throws<IOException>(() => ReportFailureEvidence.ThrowIfReported(new[] {
            "CS_TEST passed Fixture steps=2 seed=17", "CS_REPORT_FAILURE System.IO.IOException: blocked", "CS_SUMMARY 1 passed 0 failed 0 skipped"
        }));
    }
    [Fact] public void DiagnosticFramingRejectsFailuresWithoutTreatingMentionsAsProtocol()
    {
        foreach (var lines in new[] {
            new[] { "CS_REPORT_FAILURE disk full" },
            new[] { "normal output", "CS_REPORT_FAILURE blocked\r", "more output" },
            new[] { "CS_SUMMARY 1 passed 0 failed 0 skipped", "CS_REPORT_FAILURE late flush" }
        }) Assert.Throws<IOException>(() => ReportFailureEvidence.ThrowIfReported(lines));
        ReportFailureEvidence.ThrowIfReported(new[] {
            "", "CS_SUMMARY 1 passed 0 failed 0 skipped", "User documentation mentions CS_REPORT_FAILURE disk full", "cs_report_failure case-sensitive user text"
        });
    }

}
