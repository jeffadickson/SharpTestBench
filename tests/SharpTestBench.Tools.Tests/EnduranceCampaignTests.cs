using System.Text.Json;
using SharpTestBench.Tools;
using Xunit;

/// <summary>Deterministic injected time tests budget semantics; separate real short campaigns test external execution.</summary>
public sealed class EnduranceCampaignTests
{
    sealed class Execution(string failure="",CancellationTokenSource? stop=null):IEnduranceExecution
    {
        TimeSpan elapsed;bool started;int batches;int toolRestores;
        public void StartBudget(){elapsed=TimeSpan.Zero;started=true;}
        public TimeSpan Elapsed=>elapsed;
        public EnduranceProcessResult Run(string[] command,string directory,TimeSpan timeout,int logBytes,CancellationToken cancellation)
        {
            if(command.Contains("--version"))
            {
                if(failure=="missing-sdk")throw new System.ComponentModel.Win32Exception("Controlled missing executable");
                return EnduranceTestData.Child(failure=="sdk"?"10.0.403":"10.0.402");
            }
            if(started)elapsed+=timeout<TimeSpan.FromSeconds(1)?timeout:TimeSpan.FromSeconds(1);
            if(command.Length>2&&command[1]=="tool"&&command[2]=="restore")
            {
                bool coverageRestore=toolRestores++>0;
                if(failure=="coverage-start"&&coverageRestore)throw new System.ComponentModel.Win32Exception("Controlled coverage launch failure");
                if(failure=="coverage-stop-restore"&&coverageRestore){stop!.Cancel();return EnduranceTestData.Child("") with{Interrupted=true};}
                return EnduranceTestData.Child("restored");
            }
            if(command.Contains("coverlet")&&failure=="coverage-stop-collection"){stop!.Cancel();return EnduranceTestData.Child("") with{Interrupted=true};}
            if(command.Length>3&&command[1]=="tool"&&command[2]=="run"&&command[3]=="dotnet-stryker")
            {
                string output=command[Array.IndexOf(command,"--output")+1];Directory.CreateDirectory(output);
                string snapshot=Path.GetFullPath("../..",directory);string source=File.ReadAllText(Path.Combine(snapshot,"tools/SharpTestBench.Tools/EnduranceRules.cs"));
                var raw=EnduranceTestData.RawStryker(source);
                File.WriteAllText(Path.Combine(output,"mutation-report.json"),JsonSerializer.Serialize(raw));return EnduranceTestData.Child("Stryker completed");
            }
            if(command[1]=="build")
            {
                if(failure.StartsWith("coverage-",StringComparison.Ordinal)&&command[2]==EnduranceCampaign.BuildTarget)
                {
                    string assembly=Path.Combine(directory,"tests/SharpTestBench.Tools.Tests/bin/Release/net10.0/SharpTestBench.Tools.Tests.dll");Directory.CreateDirectory(Path.GetDirectoryName(assembly)!);File.WriteAllText(assembly,"controlled fake compilation output");
                }
                return EnduranceTestData.Child("compiled",failure=="compile"?1:failure=="invalid"&&command[2].Contains("mutations")&&!command[2].Contains("control")?1:0);
            }
            if(command[^1]=="all")return EnduranceTestData.Child(failure=="control"?"not a control":"ENDURANCE_PROBE_PASSED",failure=="control"?1:0);
            if(command[1]=="fsi")
            {
                if(failure=="early-property")return EnduranceTestData.Child("ENDURANCE_FAILING_INPUT [\"before shrinking\"]",1);
                if(failure=="stop"){stop!.Cancel();return EnduranceTestData.Child("") with{Interrupted=true};}
                if(failure=="deadline"&&batches++>0){elapsed+=timeout-TimeSpan.FromSeconds(1);return EnduranceTestData.Child("",-1,true);}
                ulong seed=ulong.Parse(command[Array.IndexOf(command,"--seed")+1],System.Globalization.CultureInfo.InvariantCulture);
                int properties=int.Parse(command[Array.IndexOf(command,"--property-cases")+1]),fuzz=int.Parse(command[Array.IndexOf(command,"--fuzz-cases")+1]);
                bool failed=failure=="property";
                var batch=new EnduranceBatch(failure=="seed"?unchecked(seed+1):seed,failed?0:3,failed?0:3*properties,failed?0:fuzz,failed?0:fuzz,0,0,failed?"failed":"passed",failed?"[\"controlled counterexample\"]":null,failed?"controlled failure":null);
                return EnduranceTestData.Child("ENDURANCE_BATCH "+JsonSerializer.Serialize(batch),failed?1:0);
            }
            string id=command[^1];return failure switch
            {
                "survived"=>EnduranceTestData.Child("ENDURANCE_PROBE_PASSED"),
                "timeout"=>EnduranceTestData.Child("",-1,true),
                "unrelated"=>EnduranceTestData.Child("UNRELATED_PROBE_FAILURE:"+id,2),
                _=>EnduranceTestData.Child("ENDURANCE_MUTANT_DETECTED:"+id,1)
            };
        }
    }
    static EnduranceOptions Options()=>new(TimeSpan.FromSeconds(10),17,10,20,1,Coverage:false);
    [Theory][InlineData("coverage-stop-restore")][InlineData("coverage-stop-collection")]
    public void CoverageCancellationKeepsInterruptionAndVerifiedCounts(string mode)
    {
        using var repo=new EnduranceTestRepository();using var stop=CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var report=EnduranceCampaign.Run(repo.Root,Options() with{Coverage=true},execution:new Execution(mode,stop),cancellation:stop.Token);
        Assert.Equal("interrupted",report.Status);Assert.Equal("stop-request",report.Reason);Assert.True(report.CompletedBatches>0);EnduranceRules.Validate(report);
        var saved=EnduranceStorage.ReadJson<EnduranceReport>(Path.Combine(repo.Root,"build/endurance",report.RunId,"report.json"));Assert.Equal("interrupted",saved.Status);Assert.Equal(report.CompletedBatches,saved.CompletedBatches);
    }
    [Fact]public void CoverageLaunchFailureFinishesWithoutLosingVerifiedCounts()
    {
        using var repo=new EnduranceTestRepository();var report=EnduranceCampaign.Run(repo.Root,Options() with{Coverage=true},execution:new Execution("coverage-start"),cancellation:TestContext.Current.CancellationToken);
        Assert.Equal("failed",report.Status);Assert.Equal("coverage-error",report.Reason);Assert.Equal("unavailable",report.Coverage.Status);Assert.True(report.CompletedBatches>0);EnduranceRules.Validate(report);
        var saved=EnduranceStorage.ReadJson<EnduranceReport>(Path.Combine(repo.Root,"build/endurance",report.RunId,"report.json"));Assert.NotNull(saved.FinishedUtc);Assert.Equal(report.CompletedBatches,saved.CompletedBatches);
    }
    [Fact]public void MissingSdkLeavesValidFinishedEvidence()
    {
        using var repo=new EnduranceTestRepository();
        var report=EnduranceCampaign.Run(repo.Root,Options(),execution:new Execution("missing-sdk"),cancellation:TestContext.Current.CancellationToken);
        Assert.Equal("unavailable",report.Status);Assert.Equal(0,report.ActualSeconds);EnduranceRules.Validate(report);
    }
    [Fact]public void MalformedBaselineLeavesValidFinishedEvidence()
    {
        using var repo=new EnduranceTestRepository();string baseline=Path.Combine(repo.Parent,"invalid-baseline.json");File.WriteAllText(baseline,"{");
        var report=EnduranceCampaign.Run(repo.Root,Options(),baseline:baseline,execution:new Execution(),cancellation:TestContext.Current.CancellationToken);
        Assert.Equal("failed",report.Status);Assert.Equal("setup-failed",report.Reason);EnduranceRules.Validate(report);
    }
    [Fact]public void EarlyCancellationLeavesValidFinishedEvidence()
    {
        using var repo=new EnduranceTestRepository();using var stop=CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);stop.Cancel();
        var report=EnduranceCampaign.Run(repo.Root,Options(),execution:new Execution(),cancellation:stop.Token);
        Assert.Equal("interrupted",report.Status);Assert.Equal(0,report.ActualSeconds);EnduranceRules.Validate(report);
    }
    [Fact]public void CompilationMutationsAndChangingSeedsShareOneBudget()
    {
        using var repo=new EnduranceTestRepository(true);var report=EnduranceCampaign.Run(repo.Root,Options(),execution:new Execution(),cancellation:TestContext.Current.CancellationToken);
        Assert.Equal("completed-budget",report.Status);Assert.Equal(10,report.ActualSeconds);Assert.Equal("passed",report.CompilationStatus);Assert.Equal("passed",report.MutationControlStatus);
        Assert.Equal("killed",Assert.Single(report.Mutations).Outcome);Assert.True(report.CompletedBatches>1);Assert.Equal(30L*report.CompletedBatches,report.CompletedPropertyCases);Assert.Equal(20L*report.CompletedBatches,report.CompletedFuzzCases);
        Assert.True(report.SnapshotUnchanged);Assert.NotEqual(report.InitialSeed,report.NextSeed);
    }
    [Theory][InlineData("compile","failed")][InlineData("control","failed")][InlineData("sdk","unavailable")][InlineData("seed","failed")]
    public void FailedPremisesCannotBecomeBudgetSuccess(string mode,string expected)
    {
        using var repo=new EnduranceTestRepository(true);var report=EnduranceCampaign.Run(repo.Root,Options(),execution:new Execution(mode),cancellation:TestContext.Current.CancellationToken);Assert.Equal(expected,report.Status);Assert.Equal(0,report.CompletedBatches);
    }
    [Theory][InlineData("survived","survived","failed")][InlineData("invalid","invalid","failed")][InlineData("timeout","timed-out","partial")][InlineData("unrelated","invalid","failed")]
    public void MutationOutcomesRemainDistinct(string mode,string outcome,string status)
    {
        using var repo=new EnduranceTestRepository(true);var report=EnduranceCampaign.Run(repo.Root,Options(),execution:new Execution(mode),cancellation:TestContext.Current.CancellationToken);Assert.Equal(status,report.Status);Assert.Equal(outcome,Assert.Single(report.Mutations).Outcome);Assert.Equal(0,report.CompletedBatches);
    }
    [Fact]public void ADeadlineAfterVerifiedBatchesNeverCountsTheUnfinishedBatch()
    {
        using var repo=new EnduranceTestRepository(true);var report=EnduranceCampaign.Run(repo.Root,Options(),execution:new Execution("deadline"),cancellation:TestContext.Current.CancellationToken);
        Assert.Equal("completed-budget",report.Status);Assert.Equal("budget-unverified-final-batch",report.Reason);Assert.Equal(1,report.CompletedBatches);Assert.Equal(30,report.CompletedPropertyCases);Assert.Null(report.FailureInputSha256);
    }
    [Fact]public void RealPropertyFailurePreservesInputHashAndExactReplay()
    {
        using var repo=new EnduranceTestRepository(true);var report=EnduranceCampaign.Run(repo.Root,Options(),execution:new Execution("property"),cancellation:TestContext.Current.CancellationToken);
        Assert.Equal("failed",report.Status);Assert.NotNull(report.FailureInputSha256);Assert.Equal(EnduranceRules.Replay(17,10,20),report.FailureReplay);
        string input=Path.Combine(repo.Root,"build/endurance",report.RunId,"failure-input.json");Assert.Equal(report.FailureInputSha256,EnduranceStorage.HashFile(input));Assert.Contains("counterexample",File.ReadAllText(input));
    }
    [Fact]public void FailureBeforeBatchCompletionRetainsJsonWithoutProtocolPrefix()
    {
        using var repo=new EnduranceTestRepository();var report=EnduranceCampaign.Run(repo.Root,Options(),execution:new Execution("early-property"),cancellation:TestContext.Current.CancellationToken);
        Assert.Equal("failed",report.Status);Assert.Equal(0,report.CompletedBatches);
        using var input=JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.Root,"build/endurance",report.RunId,"failure-input.json")));
        Assert.Equal("before shrinking",input.RootElement[0].GetString());Assert.Equal(EnduranceRules.Replay(17,10,20),report.FailureReplay);
    }
    [Fact]public void ExplicitCancellationRemainsInterrupted()
    {
        using var repo=new EnduranceTestRepository(true);using var stop=CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);var report=EnduranceCampaign.Run(repo.Root,Options(),execution:new Execution("stop",stop),cancellation:stop.Token);Assert.Equal("interrupted",report.Status);Assert.Equal(0,report.CompletedBatches);
    }
    [Fact]public void RunDirectoriesAreUniqueAndNeverReused()
    {
        using var repo=new EnduranceTestRepository(true);string id=Guid.NewGuid().ToString("N");EnduranceCampaign.Run(repo.Root,Options(),id,execution:new Execution(),cancellation:TestContext.Current.CancellationToken);
        Assert.Throws<IOException>(()=>EnduranceCampaign.Run(repo.Root,Options(),id,execution:new Execution(),cancellation:TestContext.Current.CancellationToken));
    }
}
