using System.Text.Json;
using SharpTestBench.Tools;
using Xunit;

public sealed class LegacyBuildFailureTests
{
    sealed class FailedBuild(string root) : ICommandRunner
    {
        public CommandResult Run(IReadOnlyList<string> arguments,string directory,IReadOnlyDictionary<string,string?>? environment=null,bool capture=false,bool check=true,TimeSpan? timeout=null)
        {
            if (arguments.Count>1 && arguments[1]=="-c") return new(0,JsonSerializer.Serialize(new CocotbAdapter("fixture","python",root,
                Path.Combine(root,"bootstrap"),Path.Combine(root,"gpi")+",entry",Path.Combine(root,"impl")+":entry",null,null)),"");
            Assert.Contains("build",arguments);
            throw new IOException("controlled managed build failed");
        }
    }
    [Fact] public void AutomaticManagedBuildFailureRetainsBuildPhaseAndReleasesLease()
    {
        var root=Path.Combine(Path.GetTempPath(),"stb-legacy-build-failure-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var source=Path.Combine(root,"input.sv");File.WriteAllText(source,"controlled input; no HDL execution");
            var error=Assert.Throws<IOException>(()=>LegacyRunCommand.Execute(new ToolContext(root,new FailedBuild(root)),["--source",source]));
            Assert.Equal("controlled managed build failed",error.Message);
            using var evidence=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"build/icarus-failure.json")));
            Assert.Equal("compile",evidence.RootElement.GetProperty("phase").GetString());
            Assert.Equal("build_failure",evidence.RootElement.GetProperty("kind").GetString());
            Assert.False(File.Exists(Path.Combine(root,"build/.sharptestbench-run.lock")));
        }
        finally
        {
            if (Path.GetDirectoryName(Path.GetFullPath(root))!=Path.TrimEndingDirectorySeparator(Path.GetTempPath())) throw new InvalidOperationException("Invalid build failure temp root");
            Directory.Delete(root,true);
        }
    }
}
