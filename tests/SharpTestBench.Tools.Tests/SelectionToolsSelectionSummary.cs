using System.Security.Cryptography;
using SharpTestBench.Tools;
using Xunit;
/// <summary>Failed aggregate qualification attempts cannot advertise a prior successful completion.</summary>
public sealed class SelectionSummaryRetirementTests
{
    sealed class OwnedRoot:IDisposable
    {
        internal readonly string Path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"stb-selection-summary-"+Guid.NewGuid().ToString("N"));
        internal OwnedRoot()=>Directory.CreateDirectory(System.IO.Path.Combine(Path,"build"));
        public void Dispose(){if(System.IO.Path.GetDirectoryName(Path)!=System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetTempPath()))throw new InvalidOperationException("Invalid owned root");Directory.Delete(Path,true);}
    }
    sealed class FailingBuild:ICommandRunner
    {
        internal int Calls;
        public CommandResult Run(IReadOnlyList<string> arguments,string directory,IReadOnlyDictionary<string,string?>? environment=null,bool capture=false,bool check=true,TimeSpan? timeout=null)
        {Calls++;throw new IOException("controlled fixture build failure");}
    }
    [Fact] public void MissingDefaultRunnerRetiresPriorManagedRecipeSummary()
    {
        using var root=new OwnedRoot();string summary=System.IO.Path.Combine(root.Path,"build/selection200-selection-10-managed.json");File.WriteAllText(summary,"{\"matched\":true}");
        Assert.Throws<FileNotFoundException>(()=>SelectionCiCommand.Execute(new ToolContext(root.Path),["--through","10"]));Assert.False(File.Exists(summary));
    }
    [Fact] public void FailedBuildRetiresPriorSummaryForResolvedSuppliedDeployment()
    {
        using var root=new OwnedRoot();string runner=System.IO.Path.Combine(root.Path,"runner.bin");File.WriteAllText(runner,"controlled runner identity; never executed");
        string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(runner))).ToLowerInvariant();string summary=System.IO.Path.Combine(root.Path,"build/selection200-selection-10-published-"+hash[..12]+".json");File.WriteAllText(summary,"{\"matched\":true}");
        var build=new FailingBuild();var error=Assert.Throws<IOException>(()=>SelectionCiCommand.Execute(new ToolContext(root.Path,build),["--through","10","--runner",runner]));
        Assert.Equal("controlled fixture build failure",error.Message);Assert.Equal(1,build.Calls);Assert.False(File.Exists(summary));
    }
}