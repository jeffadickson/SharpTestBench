using SharpTestBench.Tools;
using Xunit;

public sealed class CaseFailurePublicationTests
{
    [Fact] public void SecondaryCaseFailurePublicationCannotReplaceOriginalFailure()
    {
        var root=Path.Combine(Path.GetTempPath(),"stb-case-failure-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var original=new InvalidOperationException("controlled original action failure");
            var error=Assert.Throws<InvalidOperationException>(()=>ParityCiCommand.InWorkspace(new ToolContext(root),"case",(work,_)=> {
                Directory.CreateDirectory(Path.Combine(work,"case-failure.json"));throw original;
            }));
            Assert.Same(original,error);var folder=Path.Combine(root,"build/case");
            Assert.False(File.Exists(Path.Combine(folder,".parity.lock")));Assert.False(File.Exists(Path.Combine(folder,"parity.json")));
            Assert.Empty(Directory.EnumerateFiles(folder,"*.tmp"));Assert.True(Directory.Exists(Path.Combine(folder,"case-failure.json")));
        }
        finally { if(Path.GetDirectoryName(root)!=Path.TrimEndingDirectorySeparator(Path.GetTempPath()))throw new InvalidOperationException("Invalid case failure temp root");Directory.Delete(root,true); }
    }
}
