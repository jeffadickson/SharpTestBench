using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

/// <summary>Reject malformed lifecycle, replay, path and accounting claims before any Git write.</summary>
public sealed class EnduranceRulesTests
{
    [Theory]
    [InlineData("")][InlineData("../escape")][InlineData("/absolute")][InlineData("C:/secret")][InlineData("a\\b")][InlineData("a//b")][InlineData("a/./b")][InlineData("a/../b")][InlineData(".git/config")][InlineData("a/.GIT/config")][InlineData("a ")][InlineData("a.")][InlineData("NUL.txt")][InlineData("a/CON")][InlineData("a/LPT1.bin")][InlineData("a|b")][InlineData("a?b")]
    public void UnsafePortablePathsReject(string path)=>Assert.Throws<InvalidDataException>(()=>EnduranceRules.RequirePath(path));
    [Theory][InlineData("src/example.cs")][InlineData("docs/requirements/endurance.sdoc")][InlineData("logs/batch-slot-0.stdout.log")]
    public void RelativePortablePathsAccept(string path)=>EnduranceRules.RequirePath(path);
    [Theory][InlineData("00:00:00")][InlineData("-00:00:01")][InlineData("2.00:00:00")][InlineData("NaN")][InlineData("00:00:00.5")]
    public void InvalidDurationsReject(string value)=>Assert.Throws<ArgumentException>(()=>EnduranceRules.Duration(value));
    [Fact]public void DefaultAndExplicitDurationsAreFinite(){Assert.Equal(TimeSpan.FromHours(5),EnduranceRules.Duration("05:00:00"));Assert.Equal(TimeSpan.FromSeconds(1),EnduranceRules.Duration("00:00:01"));}
    [Theory][InlineData("-1")][InlineData("18446744073709551616")][InlineData("1.5")][InlineData(" 1")]
    public void InvalidSeedsReject(string value)=>Assert.Throws<ArgumentException>(()=>EnduranceRules.Seed(value));
    [Fact]public void UnsignedSeedsAndFullPeriodIncrementArePreserved()
    {
        Assert.Equal(ulong.MaxValue,EnduranceRules.Seed("18446744073709551615"));Assert.Equal(0UL,EnduranceRules.Seed("0"));
        var seen=new HashSet<ulong>();ulong seed=ulong.MaxValue;for(int i=0;i<10000;i++){Assert.True(seen.Add(seed));seed=EnduranceRules.Next(seed);}
    }
    [Fact]public void ValidBudgetReportDoesNotAssertCoverageOrBaselineRan(){var report=EnduranceTestData.Report();EnduranceRules.Validate(report);Assert.Equal("not-run",report.Baseline.Status);Assert.Equal("not-requested",report.Coverage.Status);}
    [Theory][InlineData("Status","running")][InlineData("SourceCommit","invalid")][InlineData("Platform","universal")][InlineData("MachineLabel","/home/person")][InlineData("RandomizedTesting","coverage-guided")][InlineData("FailureReplay","pwsh -c steal")]
    public void InvalidIdentityAndClaimsReject(string field,string value)
    {
        JsonNode node=JsonSerializer.SerializeToNode(EnduranceTestData.Report(),EnduranceStorage.Json)!;node[field]=value;
        Assert.Throws<InvalidDataException>(()=>EnduranceRules.Validate(JsonSerializer.Deserialize<EnduranceReport>(node.ToJsonString(),EnduranceStorage.Json)!));
    }
    [Theory][InlineData("survived")][InlineData("invalid")][InlineData("timed-out")]
    public void UnsuccessfulMutantsCannotClaimBudgetSuccess(string outcome)
    {
        var report=EnduranceTestData.Report();report.Mutations[0]=new("finished",outcome);Assert.Throws<InvalidDataException>(()=>EnduranceRules.Validate(report));
        report.Status="failed";report.Reason="mutant-failure";EnduranceRules.Validate(report);
    }
    [Fact]public void MissingMutantsAndForgedCountsCannotClaimCompletion()
    {
        var report=EnduranceTestData.Report();report.Mutations.Clear();Assert.Throws<InvalidDataException>(()=>EnduranceRules.Validate(report));
        report=EnduranceTestData.Report();report.CompletedFuzzCases++;Assert.Throws<InvalidDataException>(()=>EnduranceRules.Validate(report));
        report=EnduranceTestData.Report();report.NextSeed++;Assert.Throws<InvalidDataException>(()=>EnduranceRules.Validate(report));
        report=EnduranceTestData.Report();report.ActualSeconds=.5;Assert.Throws<InvalidDataException>(()=>EnduranceRules.Validate(report));
    }
    [Fact]public void MissingAndOvercountedCoverageReject()
    {
        Assert.Throws<InvalidDataException>(()=>EnduranceRules.ValidateCoverage(new("available",EnduranceRules.CoverageScope)));
        Assert.Throws<InvalidDataException>(()=>EnduranceRules.ValidateCoverage(new("available",EnduranceRules.CoverageScope,2,1,0,1)));
        Assert.Throws<InvalidDataException>(()=>EnduranceRules.ValidateCoverage(new("unavailable",EnduranceRules.CoverageScope,1,1,0,1)));
        EnduranceRules.ValidateCoverage(new("available",EnduranceRules.CoverageScope,1,2,1,3));
    }
    [Fact]public void ImportsCannotClaimFreshChecksOrLackOriginHash()
    {
        var report=EnduranceTestData.Report();report.Imported=true;Assert.Throws<InvalidDataException>(()=>EnduranceRules.Validate(report));
        report.ImportedEvidenceSha256=new string('b',64);EnduranceRules.Validate(report);
        report.ImportReexecutedChecks=true;Assert.Throws<InvalidDataException>(()=>EnduranceRules.Validate(report));
    }
    [Fact]public void GuardFailuresAreDistinguishable()
    {
        Assert.Equal("Unfinished campaign",Assert.Throws<InvalidDataException>(()=>EnduranceRules.RequireFinished("running")).Message);
        Assert.Equal("Dirty source cannot be published",Assert.Throws<InvalidDataException>(()=>EnduranceRules.RequireClean(true)).Message);
        Assert.Equal("Source revision differs",Assert.Throws<InvalidDataException>(()=>EnduranceRules.RequireRevision(new string('a',40),new string('b',40))).Message);
        Assert.Equal("Stale source hash",Assert.Throws<InvalidDataException>(()=>EnduranceRules.RequireHash(new string('a',64),new string('b',64))).Message);
    }
    [Theory][InlineData("ext::untrusted")][InlineData("https://user:secret@example.com/repo.git")][InlineData("https://example.com/repo?token=secret")][InlineData("--upload-pack=bad")]
    public void CredentialBearingOrExecutableRemotesReject(string remote)=>Assert.Throws<InvalidDataException>(()=>EndurancePublication.ValidateRemote(remote));
    [Fact]public void UniqueBranchesBindPlatformLabelAndSource()
    {
        var report=EnduranceTestData.Report();string first=EndurancePublication.Branch(report.Platform,report.Architecture,report.MachineLabel,report.SourceCommit);
        string second=EndurancePublication.Branch(report.Platform,report.Architecture,report.MachineLabel,report.SourceCommit);Assert.NotEqual(first,second);EndurancePublication.ValidateBranch(first,report);
        Assert.Throws<InvalidDataException>(()=>EndurancePublication.ValidateBranch(first.Replace("fixture/","other/"),report));
    }
    [Theory][InlineData("partial")][InlineData("interrupted")][InlineData("failed")][InlineData("unavailable")]
    public void ValidNonSuccessEvidenceRemainsNonSuccess(string status){var report=EnduranceTestData.Report();report.Status=status;EnduranceRules.Validate(report);Assert.NotEqual(0,EnduranceCommand.StatusCode(status));}
    [Fact]public void GenericXunitInventoryDoesNotWeakenMutationTargetGuard()
    {
        const string json="[{\"ID\":\"1\",\"Class\":\"Example\",\"Method\":\"Works\",\"DisplayName\":\"Example.Works\"}]";
        Assert.Throws<InvalidDataException>(()=>ManagedTestsCommand.Inventory(json));Assert.Single(ManagedTestsCommand.Inventory(json,false));
    }
    [Theory][InlineData("not-requested")][InlineData("unavailable")][InlineData("failed")]
    public void ExplicitlyUnmeasuredCoverageHasNoInventedCounts(string status)=>EnduranceRules.ValidateCoverage(new(status,EnduranceRules.CoverageScope));
    [Fact]public void GuardPositiveControlsAndMalformedEqualHashesAreDistinct()
    {
        EnduranceRules.RequireHash(new string('a',64),new string('a',64));EnduranceRules.RequireRevision(new string('a',40),new string('a',40));EnduranceRules.RequireClean(false);
        Assert.Throws<InvalidDataException>(()=>EnduranceRules.RequireHash("bad","bad"));Assert.Throws<InvalidDataException>(()=>EnduranceRules.RequireRevision("bad","bad"));
        Assert.Equal("Unsafe portable path",Assert.Throws<InvalidDataException>(()=>EnduranceRules.RequirePath("../bad")).Message);
        Assert.Equal("Invalid coverage evidence",Assert.Throws<InvalidDataException>(()=>EnduranceRules.ValidateCoverage(new("available","wrong-scope",1,2,1,2))).Message);
    }}
