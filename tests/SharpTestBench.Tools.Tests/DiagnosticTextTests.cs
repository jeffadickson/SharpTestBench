using SharpTestBench.Internal;
using SharpTestBench.Tools;
using Xunit;

public sealed class DiagnosticTextTests
{
    [Fact] public void OrdinaryDiagnosticsRemainByteForByteUnchanged()
    {
        const string text="Ω 😀 \0\n precise failure";Assert.Same(text,DiagnosticText.Limit(text));
    }
    [Fact] public void TruncationCannotSplitASupplementaryUnicodeScalar()
    {
        int prefix=DiagnosticText.MaximumCharacters-DiagnosticText.Marker.Length-1;
        var result=DiagnosticText.Limit(new string('x',prefix)+"😀"+new string('y',100));
        Assert.True(result.Length<=DiagnosticText.MaximumCharacters);Assert.EndsWith(DiagnosticText.Marker,result);
        Assert.DoesNotContain(result,char.IsSurrogate);
    }
    [Fact] public void InvalidDiagnosticBudgetsRejectBeforeUse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>DiagnosticText.Limit("short",0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>DiagnosticText.Limit("short",DiagnosticText.Marker.Length));
    }
    [Fact] public void CaseFailureBoundsSerializedTextWithoutChangingOriginalException()
    {
        var root=Path.Combine(Path.GetTempPath(),"stb-diagnostic-bound-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var original=new InvalidOperationException(new string('Ω',100000));
            Assert.Same(original,Assert.Throws<InvalidOperationException>(()=>ParityCiCommand.InWorkspace(new ToolContext(root),"case",(_,_)=>throw original)));
            using var evidence=StrictJson.Read(Path.Combine(root,"build/case/case-failure.json"));
            var message=evidence.RootElement.GetProperty("error").GetString()!;
            Assert.True(message.Length<=DiagnosticText.MaximumCharacters);Assert.EndsWith(DiagnosticText.Marker,message);Assert.Equal(100000,original.Message.Length);
        }
        finally { if(Path.GetDirectoryName(root)!=Path.TrimEndingDirectorySeparator(Path.GetTempPath()))throw new InvalidOperationException("Invalid diagnostic temp root");Directory.Delete(root,true); }
    }
    [Fact] public void LegacyFailureBoundsControlHeavyTextAndKeepsEvidenceShape()
    {
        var root=Path.Combine(Path.GetTempPath(),"stb-legacy-text-bound-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            LegacyFailure.Write(root,"case","attempt","compile",new IOException(string.Concat(Enumerable.Repeat("\0\aΩ😀",10000))),false,null);
            using var evidence=StrictJson.Read(Path.Combine(root,"case-failure.json"));
            var message=evidence.RootElement.GetProperty("error").GetString()!;Assert.True(message.Length<=DiagnosticText.MaximumCharacters);
            Assert.EndsWith(DiagnosticText.Marker,message);Assert.Equal("build_failure",evidence.RootElement.GetProperty("kind").GetString());
            Assert.Equal("attempt",evidence.RootElement.GetProperty("runId").GetString());
        }
        finally { if(Path.GetDirectoryName(root)!=Path.TrimEndingDirectorySeparator(Path.GetTempPath()))throw new InvalidOperationException("Invalid legacy text temp root");Directory.Delete(root,true); }
    }
}
