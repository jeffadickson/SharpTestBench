using SharpTestBench.Tools;
using Xunit;

public sealed class OracleEvidenceTests
{
    [Fact] public void ImportedParserMutationInvalidatesOracleEvenWhenMainSourceIsUnchanged()
    {
        var root=Path.Combine(Path.GetTempPath(),"stb-oracle-evidence-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root,"oracle.py"),"from parser import parse\n");
            var dependency=Path.Combine(root,"parser.py");File.WriteAllText(dependency,"original parser\n");
            string[] files=["oracle.py","parser.py"];var before=OracleEvidence.Capture(root,files);
            Assert.Equal(new[] { "oracle.py","parser.py" },before.Keys);OracleEvidence.Verify(root,files,before);
            var main=before["oracle.py"];File.WriteAllText(dependency,"modified parser\n");
            Assert.Equal(main,OracleEvidence.Capture(root,files)["oracle.py"]);
            Assert.Throws<InvalidDataException>(()=>OracleEvidence.Verify(root,files,before));
            Assert.Throws<InvalidDataException>(()=>OracleEvidence.Capture(root,["oracle.py","oracle.py"]));
            Assert.Throws<InvalidDataException>(()=>OracleEvidence.Capture(root,["../outside.py"]));
        }
        finally
        {
            if (Path.GetDirectoryName(Path.GetFullPath(root))!=Path.TrimEndingDirectorySeparator(Path.GetTempPath())) throw new InvalidOperationException("Invalid oracle temp root");
            Directory.Delete(root,true);
        }
    }
}
