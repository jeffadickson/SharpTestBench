using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SharpTestBench.Tools;

/// <summary>Owned temporary repositories: local transport only; never user's main or GitHub.</summary>
internal sealed class EnduranceTestRepository : IDisposable
{
    internal string Parent { get; }=Path.Combine(Path.GetTempPath(),"stb endurance "+Guid.NewGuid().ToString("N"));
    internal string Root=>Path.Combine(Parent,"checkout");
    internal string Bare=>Path.Combine(Parent,"remote.git");
    internal EnduranceGit Git=>new(Root);
    internal EnduranceTestRepository(bool mutationSources=true)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(Path.Combine(Root,".gitignore"),"build/\n");
        File.WriteAllText(Path.Combine(Root,"subject.cs"),"// owned evidence source fixture\n");
        if(mutationSources)
        {
            File.WriteAllText(Path.Combine(Root,"SharpTestBench.slnx"),"<Solution />\n");
            File.WriteAllText(Path.Combine(Root,"global.json"),"{\"sdk\":{\"version\":\"10.0.402\"}}\n");
            string source=FindSource();
            foreach(string name in new[]{"EnduranceModels.cs","EnduranceRules.cs"})
            {
                string target=Path.Combine(Root,"tools/SharpTestBench.Tools",name);Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(source,"tools/SharpTestBench.Tools",name),target);
            }
        }
        Git.Text("init","-b","main");Git.Text("config","core.autocrlf","false");
        Git.Text("config","user.name","Endurance fixture");Git.Text("config","user.email","fixture@invalid");
        Git.Text("add",".");Git.Text("commit","-m","Controlled source");
        Git.Text("init","--bare","-b","main",Bare);Git.Text("remote","add","origin",Bare);Git.Text("push","-u","origin","main");
    }
    static string FindSource()
    {
        for(var directory=new DirectoryInfo(AppContext.BaseDirectory);directory is not null;directory=directory.Parent)
            if(File.Exists(Path.Combine(directory.FullName,"SharpTestBench.slnx")))return directory.FullName;
        throw new DirectoryNotFoundException("Test source checkout missing");
    }
    internal (EnduranceReport Report,string Path) Finished()
    {
        var report=EnduranceTestData.Report(Git.Head,Git.CommittedHashes(Git.Head));
        string work=Path.Combine(Root,"build/endurance",report.RunId);Directory.CreateDirectory(work);
        Git.Snapshot(Path.Combine(work,"source"));
        string source=File.ReadAllText(Path.Combine(work,"source/tools/SharpTestBench.Tools/EnduranceRules.cs"));
        string stryker=Path.Combine(work,"stryker");Directory.CreateDirectory(stryker);EnduranceStorage.WriteJson(Path.Combine(stryker,"mutation-report.json"),EnduranceTestData.RawStryker(source));
        report.Stryker=EnduranceStrykerEngine.Read(stryker,source,["finished"]);
        string path=Path.Combine(work,"report.json");EnduranceStorage.WriteJson(path,report);return(report,path);
    }
    public void Dispose()
    {
        string absolute=Path.GetFullPath(Parent);
        if(Path.GetDirectoryName(absolute)!=Path.TrimEndingDirectorySeparator(Path.GetTempPath())||!Path.GetFileName(absolute).StartsWith("stb endurance ",StringComparison.Ordinal))throw new InvalidOperationException("Invalid owned test root");
        foreach(string file in Directory.EnumerateFiles(absolute,"*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint}))File.SetAttributes(file,FileAttributes.Normal);
        Directory.Delete(absolute,true);
    }
}
internal static class EnduranceTestData
{
    internal static EnduranceReport Report(string? commit=null,SortedDictionary<string,string>? hashes=null)=>new()
    {
        SchemaVersion=1,RunId=Guid.NewGuid().ToString("N"),SourceCommit=commit??new string('a',40),Dirty=false,Imported=false,Platform="windows",Architecture="x64",MachineLabel="fixture",
        StartedUtc=DateTimeOffset.UtcNow.AddSeconds(-2),FinishedUtc=DateTimeOffset.UtcNow,Status="completed-budget",Reason="budget-exhausted",RequestedSeconds=1,ActualSeconds=1,SetupSeconds=0,
        InitialSeed=17,NextSeed=EnduranceRules.Next(17),PropertyCasesPerBatch=10,FuzzCasesPerBatch=20,CompletedBatches=1,CompletedPropertyCases=30,CompletedFuzzCases=20,
        SnapshotUnchanged=true,RequestedMutations=["finished"],Mutations=[new("finished","killed")],SourceHashes=hashes??new(StringComparer.Ordinal){["subject.cs"]=new string('a',64)},
        LogHashes=new(StringComparer.Ordinal),Tools=new(StringComparer.Ordinal){["dotnet-sdk"]="10.0.402",["git"]="git version 2.55.0",["fscheck"]="3.6.0",["coverlet"]="requested-10.1.0",["os-architecture"]="x64"},Baseline=new("not-run",EnduranceVerification.BaselineScope),Coverage=new("not-requested",EnduranceRules.CoverageScope),
        RandomizedTesting="deterministic randomized FsCheck properties and malformed regex testing; not coverage-guided fuzzing",FailureInputSha256=null,FailureReplay=null,ImportedEvidenceSha256=null,
        Limits=new(1,120,131072,536870912,10000,268435456),LogsTruncated=false,CompilationStatus="passed",MutationControlStatus="passed",CoverageSeconds=0,ImportReexecutedChecks=false,ActualSecondsIsLowerBound=false,Stryker=new(EnduranceRules.StrykerVersion,"completed",EnduranceRules.StrykerScope,[new("stryker-0","killed")],new string('a',64))
    };
    internal static object RawStryker(string source,string status="Killed",int offset=-1)
    {
        if(offset<0)offset=EnduranceStrykerEngine.Ranges(source,["finished"])[0].Start+12;
        object Point(int index){string[] lines=source[..index].Split('\n');return new{line=lines.Length,column=lines[^1].Length+1};}
        return new{files=new Dictionary<string,object>{["EnduranceRules.cs"]=new{source,mutants=new[]{new{id="0",status,location=new{start=Point(offset),end=Point(offset+1)}}}}}};
    }
    internal static EnduranceProcessResult Child(string text="",int code=0,bool timedOut=false)=>new(code,timedOut,false,false,true,Encoding.UTF8.GetBytes(text),[]);
}
