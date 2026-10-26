using System.Text;
using SharpTestBench.Tools;
using Xunit;

/// <summary>Atomic replacement, checkout exclusion and crash recovery use real filesystem boundaries.</summary>
public sealed class EnduranceStorageTests
{
    [Fact]public void TransientCheckpointContentionRetriesAndPreservesPriorCheckpoint()
    {
        using var repo=new EnduranceTestRepository();string path=Path.Combine(repo.Root,"build/checkpoint.json");Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,"old");int calls=0;var delays=new List<int>();
        EnduranceStorage.Write(path,Encoding.UTF8.GetBytes("new"),(pending,target)=>{calls++;if(calls<3){Assert.Equal("old",File.ReadAllText(path));throw new IOException("controlled sharing contention");}File.Move(pending,target,true);},delay=>delays.Add((int)delay.TotalMilliseconds));
        Assert.Equal(3,calls);Assert.Equal(new[]{20,40},delays);Assert.Equal("new",File.ReadAllText(path));Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!,"*.tmp"));
    }
    [Fact]public void PermanentCheckpointFailureThrowsAndCannotErasePreviousEvidence()
    {
        using var repo=new EnduranceTestRepository();string path=Path.Combine(repo.Root,"build/checkpoint.json");Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,"old");int calls=0;
        Assert.Throws<IOException>(()=>EnduranceStorage.Write(path,Encoding.UTF8.GetBytes("new"),(_,_)=>{calls++;throw new IOException("permanent");},_=>{}));
        Assert.Equal(6,calls);Assert.Equal("old",File.ReadAllText(path));Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!,"*.tmp"));
    }
    [Fact]public void RealReaderSharingCannotProducePartialCheckpoint()
    {
        using var repo=new EnduranceTestRepository();string path=Path.Combine(repo.Root,"build/checkpoint.json");Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,"old");
        using var first=new ManualResetEventSlim();using var released=new ManualResetEventSlim();var token=TestContext.Current.CancellationToken;Exception? failure=null;
        using var reader=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        var thread=new Thread(()=>
        {
            try{EnduranceStorage.Write(path,Encoding.UTF8.GetBytes("complete-new"),(from,to)=>{try{File.Move(from,to,true);}finally{first.Set();}},_=>{if(!released.Wait(TimeSpan.FromSeconds(5),token))throw new TimeoutException("Test sharing gate was not released");});}
            catch(Exception error){failure=error;}
        });
        thread.Start();try{Assert.True(first.Wait(TimeSpan.FromSeconds(5),token));}finally{reader.Dispose();released.Set();}
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));Assert.Null(failure);Assert.Equal("complete-new",File.ReadAllText(path));
    }
    [Fact]public void ASecondCampaignCannotOwnTheCheckoutLease()
    {
        using var repo=new EnduranceTestRepository();using(var first=new EnduranceLease(repo.Root)){Assert.Throws<IOException>(()=>new EnduranceLease(repo.Root));}
        using var next=new EnduranceLease(repo.Root);
    }
    [Fact]public void LiveOwnersCannotBeRecoveredAndDeadOwnersBecomeInterruptedLowerBounds()
    {
        using var repo=new EnduranceTestRepository();var (report,path)=repo.Finished();report.Status="running";report.FinishedUtc=null;EnduranceStorage.WriteJson(path,report);string owner=Path.Combine(Path.GetDirectoryName(path)!,"local-owner.json");
        using var current=System.Diagnostics.Process.GetCurrentProcess();EnduranceStorage.WriteJson(owner,new EnduranceLocal(current.Id,new DateTimeOffset(current.StartTime.ToUniversalTime())));
        Assert.Throws<IOException>(()=>EnduranceCommand.Recover(repo.Root,path));
        EnduranceStorage.WriteJson(owner,new EnduranceLocal(int.MaxValue,DateTimeOffset.UtcNow));EnduranceCommand.Recover(repo.Root,path);
        var recovered=EnduranceStorage.ReadJson<EnduranceReport>(path);Assert.Equal("interrupted",recovered.Status);Assert.True(recovered.ActualSecondsIsLowerBound);Assert.True(recovered.SnapshotUnchanged);
    }
    [Fact]public void StorageLimitsAndStrictUnknownMembersAreEnforced()
    {
        using var repo=new EnduranceTestRepository();string path=Path.Combine(repo.Root,"build/record.json");Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,"{\"Extra\":\"not accepted\"}");Assert.Throws<System.Text.Json.JsonException>(()=>EnduranceStorage.ReadJson<EnduranceReport>(path));
        Assert.Throws<InvalidDataException>(()=>EnduranceStorage.WriteJson(path,new string('a',EnduranceRules.MaximumReportBytes)));
    }
    [Fact]public void SnapshotCopyNeverOverwritesAnExistingDirectory()
    {
        using var repo=new EnduranceTestRepository();string copy=Path.Combine(repo.Root,"build/existing");Directory.CreateDirectory(copy);File.WriteAllText(Path.Combine(copy,"keep.txt"),"user data");
        Assert.Throws<InvalidDataException>(()=>repo.Git.Snapshot(copy));Assert.Equal("user data",File.ReadAllText(Path.Combine(copy,"keep.txt")));
    }
}
