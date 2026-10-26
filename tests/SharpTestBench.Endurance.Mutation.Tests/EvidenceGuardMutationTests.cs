using SharpTestBench.Tools;
using Xunit;

/// <summary>Stryker mutates the source-linked production guard conditions; positive controls prevent unconditional rejection.</summary>
public sealed class EvidenceGuardMutationTests
{
    [Theory][InlineData("completed-budget")][InlineData("partial")][InlineData("interrupted")][InlineData("failed")][InlineData("unavailable")]
    public void FinishedOutcomesAreExplicit(string status)=>EnduranceRules.RequireFinished(status);
    [Theory][InlineData("running")][InlineData("")][InlineData("success")]
    public void UnfinishedAndUnknownOutcomesReject(string status)=>Assert.Equal("Unfinished campaign",Assert.Throws<InvalidDataException>(()=>EnduranceRules.RequireFinished(status)).Message);
    [Theory][InlineData("src/code.cs")][InlineData("logs/batch-slot-0.stdout.log")]
    public void RelativePathsAccept(string path)=>EnduranceRules.RequirePath(path);
    [Theory][InlineData("../escape")][InlineData("/etc/secret")][InlineData("C:/secret")][InlineData("a\\b")][InlineData(".git/config")][InlineData("")]
    public void UnsafePathsReject(string path)=>Assert.Equal("Unsafe portable path",Assert.Throws<InvalidDataException>(()=>EnduranceRules.RequirePath(path)).Message);
    [Fact]public void EqualValidHashesPass()=>EnduranceRules.RequireHash(new string('a',64),new string('a',64));
    [Fact]public void DifferentHashesReject()=>Assert.Equal("Stale source hash",Assert.Throws<InvalidDataException>(()=>EnduranceRules.RequireHash(new string('a',64),new string('b',64))).Message);
    [Fact]public void EquallyMalformedHashesReject()=>Assert.Equal("Stale source hash",Assert.Throws<InvalidDataException>(()=>EnduranceRules.RequireHash("bad","bad")).Message);
    [Fact]public void CleanSourcePasses()=>EnduranceRules.RequireClean(false);
    [Fact]public void DirtySourceRejects()=>Assert.Equal("Dirty source cannot be published",Assert.Throws<InvalidDataException>(()=>EnduranceRules.RequireClean(true)).Message);
    [Fact]public void EqualValidRevisionsPass()=>EnduranceRules.RequireRevision(new string('a',40),new string('a',40));
    [Fact]public void DifferentRevisionsReject()=>Assert.Equal("Source revision differs",Assert.Throws<InvalidDataException>(()=>EnduranceRules.RequireRevision(new string('a',40),new string('b',40))).Message);
    [Fact]public void EquallyMalformedRevisionsReject()=>Assert.Equal("Source revision differs",Assert.Throws<InvalidDataException>(()=>EnduranceRules.RequireRevision("bad","bad")).Message);
    [Theory][InlineData("unavailable")][InlineData("failed")][InlineData("not-requested")]
    public void UnmeasuredMetricsRemainNull(string status)=>EnduranceRules.ValidateCoverage(new(status,EnduranceRules.CoverageScope));
    [Fact]public void MeasuredCountersPass()=>EnduranceRules.ValidateCoverage(new("available",EnduranceRules.CoverageScope,1,2,1,2));
    [Fact]public void WrongScopeRejects()=>Reject(new("available","wrong",1,2,1,2));
    [Fact]public void MissingCountersReject()=>Reject(new("available",EnduranceRules.CoverageScope));
    [Fact]public void OvercountedCountersReject()=>Reject(new("available",EnduranceRules.CoverageScope,2,1,0,1));
    [Fact]public void UnknownStatusRejects()=>Reject(new("unknown",EnduranceRules.CoverageScope));
    [Theory][InlineData("unavailable")][InlineData("failed")][InlineData("not-requested")]
    public void UnmeasuredCountsCannotBeInvented(string status)=>Reject(new(status,EnduranceRules.CoverageScope,0,1,0,1));
    static void Reject(EnduranceCoverage value)=>Assert.Equal("Invalid coverage evidence",Assert.Throws<InvalidDataException>(()=>EnduranceRules.ValidateCoverage(value)).Message);
}
