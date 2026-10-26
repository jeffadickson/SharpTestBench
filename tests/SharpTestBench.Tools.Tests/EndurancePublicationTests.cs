using System.Text;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

/// <summary>Real local bare Git transport proves publication cannot alter source main/index or execute reports.</summary>
public sealed class EndurancePublicationTests
{
    [Fact]public void SuccessfulPublicationHasOnlyChecksummedReportsAndPreservesSource()
    {
        using var repo=new EnduranceTestRepository();var (report,path)=repo.Finished();
        string head=repo.Git.Head,index=EnduranceStorage.HashFile(Path.Combine(repo.Root,".git/index"));
        var result=EndurancePublication.Publish(repo.Root,path,head);
        Assert.Equal("pushed",result.Status);Assert.Equal(head,repo.Git.Head);Assert.Equal("refs/heads/main",repo.Git.SymbolicHead);
        Assert.Equal(index,EnduranceStorage.HashFile(Path.Combine(repo.Root,".git/index")));
        Assert.Equal(head,new EnduranceGit(repo.Bare).Text("rev-parse","refs/heads/main"));
        Assert.Equal(result.Commit,new EnduranceGit(repo.Bare).Text("rev-parse","refs/heads/"+result.Branch));
        Assert.Equal(2,repo.Git.Tree(result.Commit).Length);
        var reviewed=EndurancePublication.Review(repo.Root,result.Commit,head);Assert.Equal(report.RunId,reviewed.RunId);
        Assert.Throws<IOException>(()=>EndurancePublication.Publish(repo.Root,path,head,result.Branch));
    }
    [Fact]public void FailedUploadRetainsLocalResultCommitAndSafeRetry()
    {
        using var repo=new EnduranceTestRepository();var (_,path)=repo.Finished();
        // Git's receive-hook protocol requires this POSIX entrypoint, including
        // Git for Windows; it is confined to the owned temporary bare fixture.
        string hook=Path.Combine(repo.Bare,"hooks/pre-receive");File.WriteAllText(hook,"#!/bin/sh\nexit 1\n",new UTF8Encoding(false));
        if(!OperatingSystem.IsWindows())File.SetUnixFileMode(hook,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        var result=EndurancePublication.Publish(repo.Root,path,repo.Git.Head);
        Assert.Equal("push-failed",result.Status);Assert.Equal(result.Commit,repo.Git.Text("rev-parse","refs/heads/"+result.Branch));
        Assert.Equal("git push origin "+result.Commit+":refs/heads/"+result.Branch,result.RetryCommand);
        Assert.Empty(new EnduranceGit(repo.Bare).Text("for-each-ref","--format=%(refname)","refs/heads/remote-runs/"));
        Assert.Equal("push-failed",EnduranceStorage.ReadJson<EndurancePublicationResult>(Path.Combine(Path.GetDirectoryName(path)!,"publication.json")).Status);
    }
    [Theory][InlineData("failed")][InlineData("partial")][InlineData("interrupted")][InlineData("unavailable")]
    public void ValidFailureEvidencePublishesAsFailure(string status)
    {
        using var repo=new EnduranceTestRepository();var (report,path)=repo.Finished();report.Status=status;report.Reason="controlled-failure";EnduranceStorage.WriteJson(path,report);
        var publication=EndurancePublication.Publish(repo.Root,path,repo.Git.Head);
        Assert.Equal(status,EndurancePublication.Review(repo.Root,publication.Commit,repo.Git.Head).Status);
    }
    [Fact]public void DirtyAndUnfinishedEvidenceCannotCreateResultRefs()
    {
        using var repo=new EnduranceTestRepository();var (report,path)=repo.Finished();
        var node=System.Text.Json.JsonSerializer.SerializeToNode(report,EnduranceStorage.Json)!;node["Dirty"]=true;EnduranceStorage.WriteJson(path,node);Assert.Throws<InvalidDataException>(()=>EndurancePublication.Publish(repo.Root,path,repo.Git.Head,falseBranch(),false));
        report.Status="running";report.FinishedUtc=null;EnduranceStorage.WriteJson(path,report);Assert.Throws<InvalidDataException>(()=>EndurancePublication.Publish(repo.Root,path,repo.Git.Head,falseBranch(),false));
        Assert.Empty(repo.Git.Text("for-each-ref","--format=%(refname)","refs/heads/remote-runs/"));
        string falseBranch()=>EndurancePublication.Branch(report.Platform,report.Architecture,report.MachineLabel,report.SourceCommit);
    }
    [Fact]public void DirtyCheckoutIsRejectedEvenIfTheFinishedRecordClaimsClean()
    {
        using var repo=new EnduranceTestRepository();var (_,path)=repo.Finished();File.AppendAllText(Path.Combine(repo.Root,"subject.cs"),"// new work\n");
        Assert.Throws<InvalidDataException>(()=>EndurancePublication.Publish(repo.Root,path,repo.Git.Head,push:false));
        Assert.Contains("new work",File.ReadAllText(Path.Combine(repo.Root,"subject.cs")));
    }
    [Fact]public void StaleHashesAndMissingSourceFilesRejectBeforePublication()
    {
        using var repo=new EnduranceTestRepository();var (report,path)=repo.Finished();report.SourceHashes["subject.cs"]=new string('b',64);EnduranceStorage.WriteJson(path,report);
        Assert.Throws<InvalidDataException>(()=>EndurancePublication.Publish(repo.Root,path,repo.Git.Head,push:false));
        report.SourceHashes.Remove("subject.cs");EnduranceStorage.WriteJson(path,report);
        Assert.Throws<InvalidDataException>(()=>EndurancePublication.Publish(repo.Root,path,repo.Git.Head,push:false));
    }
    [Fact]public void MalformedAndDuplicateMetadataCannotPublish()
    {
        using var repo=new EnduranceTestRepository();var (_,path)=repo.Finished();
        File.WriteAllText(path,"{\"SchemaVersion\":1,\"SchemaVersion\":2}");Assert.ThrowsAny<Exception>(()=>EndurancePublication.Publish(repo.Root,path,repo.Git.Head,push:false));
        File.WriteAllText(path,"{}");Assert.Throws<System.Text.Json.JsonException>(()=>EndurancePublication.Publish(repo.Root,path,repo.Git.Head,push:false));
        File.WriteAllText(path,new string('x',EnduranceRules.MaximumReportBytes+1));Assert.Throws<InvalidDataException>(()=>EnduranceStorage.ReadJson<EnduranceReport>(path));
    }
    [Fact]public void ImportIsChecksumBoundAndNeverRepeatsBaselineOrCoverage()
    {
        using var repo=new EnduranceTestRepository();var (_,path)=repo.Finished();
        byte[] original=File.ReadAllBytes(path);string sums=Path.Combine(Path.GetDirectoryName(path)!,"checksums.json");
        EnduranceStorage.WriteJson(sums,new SortedDictionary<string,string>{["summary.json"]=EnduranceStorage.Hash(original)});
        string imported=EndurancePublication.Import(repo.Root,path,sums,repo.Git.Head);var report=EnduranceStorage.ReadJson<EnduranceReport>(imported);
        Assert.True(report.Imported);Assert.False(report.ImportReexecutedChecks);Assert.Equal(EnduranceStorage.Hash(original),report.ImportedEvidenceSha256);
        var publication=EndurancePublication.Publish(repo.Root,imported,repo.Git.Head,push:false);Assert.True(EndurancePublication.Review(repo.Root,publication.Commit,repo.Git.Head).Imported);
        report.Status="failed";EnduranceStorage.WriteJson(imported,report);Assert.Throws<InvalidDataException>(()=>EndurancePublication.Publish(repo.Root,imported,repo.Git.Head,push:false));
        EnduranceStorage.WriteJson(sums,new SortedDictionary<string,string>{["summary.json"]=new string('a',64)});
        Assert.Throws<InvalidDataException>(()=>EndurancePublication.Import(repo.Root,path,sums,repo.Git.Head));
    }
    [Fact]public void WrongSourceRevisionCannotBeImportedOrReviewed()
    {
        using var repo=new EnduranceTestRepository();var (_,path)=repo.Finished();
        var result=EndurancePublication.Publish(repo.Root,path,repo.Git.Head,push:false);
        Assert.Throws<InvalidDataException>(()=>EndurancePublication.Review(repo.Root,result.Commit,new string('b',40)));
    }
    [Fact]public void NonReportTreeIsRejectedWithoutCheckingItOut()
    {
        using var repo=new EnduranceTestRepository();string head=repo.Git.Head;
        Assert.Throws<InvalidDataException>(()=>EndurancePublication.Review(repo.Root,head,head));Assert.Equal(head,repo.Git.Head);
    }
    [Fact]public void TamperedLocalCaptureHashCannotBePublished()
    {
        using var repo=new EnduranceTestRepository();var (report,path)=repo.Finished();string log=Path.Combine(Path.GetDirectoryName(path)!,"logs/batch-slot-0.stdout.log");Directory.CreateDirectory(Path.GetDirectoryName(log)!);File.WriteAllText(log,"changed");
        report.LogHashes["logs/batch-slot-0.stdout.log"]=EnduranceStorage.Hash(Encoding.UTF8.GetBytes("original"));EnduranceStorage.WriteJson(path,report);
        Assert.Throws<InvalidDataException>(()=>EndurancePublication.Publish(repo.Root,path,repo.Git.Head,push:false));
    }
}
