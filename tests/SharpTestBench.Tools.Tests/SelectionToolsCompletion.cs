using System.Text.Json;
using SharpTestBench.Tools;
using Xunit;
/// <summary>Completion evidence must distinguish rejected attestation from a valid failed test run.</summary>
public sealed class SelectionCompletionTests
{
    sealed class OwnedEvidence:IDisposable
    {
        internal readonly string DirectoryPath=Path.Combine(Path.GetTempPath(),"stb-selection-completion-"+Guid.NewGuid().ToString("N"));
        internal const string RunId="0123456789abcdef0123456789abcdef";
        internal OwnedEvidence(){Directory.CreateDirectory(DirectoryPath);Write("input-manifest.json",new{RunId});}
        internal void Write(string name,object value)=>File.WriteAllText(Path.Combine(DirectoryPath,name),JsonSerializer.Serialize(value));
        internal void Failure(string kind="configuration_error")=>Write("failure-manifest.json",new{SchemaVersion=1,RunId,Status="failed",Kind=kind,ErrorType="System.IO.InvalidDataException",Error="Skipped test cases cannot satisfy the required CI manifest."});
        public void Dispose(){if(Path.GetDirectoryName(DirectoryPath)!=Path.TrimEndingDirectorySeparator(Path.GetTempPath()))throw new InvalidOperationException("Invalid owned evidence root");Directory.Delete(DirectoryPath,true);}
    }
    static readonly SelectionScenarioEvidence.Expected[] Skipped=[new("Probe.Skip",83,"skipped","Explicit skip")];
    [Fact] public void PrematureRunManifestCannotQualifyRejectedAttestation()
    {
        using var evidence=new OwnedEvidence();evidence.Failure();
        SelectionScenarioEvidence.VerifyNativeCompletion(evidence.DirectoryPath,Skipped,83,"configuration_error","Skipped test cases");
        evidence.Write("run-manifest.json",new{Status="passed"});
        var error=Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.VerifyNativeCompletion(evidence.DirectoryPath,Skipped,83,"configuration_error","Skipped test cases"));
        Assert.Equal("Rejected selection published a run manifest",error.Message);
    }
    [Fact] public void WrongFailureClassificationCannotQualifyRequiredResultRejection()
    {
        using var evidence=new OwnedEvidence();evidence.Failure("simulator_failure");
        var error=Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.VerifyNativeCompletion(evidence.DirectoryPath,Skipped,83,"configuration_error","Skipped test cases"));
        Assert.Equal("Native rejection classification differs from selection recipe",error.Message);
    }
    [Fact] public void AttestedFailedTestRequiresFailedCompletionAndMatchingRunIdentity()
    {
        using var evidence=new OwnedEvidence();SelectionScenarioEvidence.Expected[] cases=[new("Probe.Fail",153,"failed","controlled assertion")];
        evidence.Write("run-manifest.json",new{SchemaVersion=1,OwnedEvidence.RunId,Seed=83UL,Passed=0,Failed=1,Status="failed",TestNames=new[]{"Probe.Fail"}});
        SelectionScenarioEvidence.VerifyNativeCompletion(evidence.DirectoryPath,cases,83);
        evidence.Write("run-manifest.json",new{SchemaVersion=1,RunId="fedcba98765432100123456789abcdef",Seed=83UL,Passed=0,Failed=1,Status="failed",TestNames=new[]{"Probe.Fail"}});
        Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.VerifyNativeCompletion(evidence.DirectoryPath,cases,83));
        evidence.Write("run-manifest.json",new{SchemaVersion=1,OwnedEvidence.RunId,Seed=83UL,Passed=0,Failed=1,Status="passed",TestNames=new[]{"Probe.Fail"}});
        Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.VerifyNativeCompletion(evidence.DirectoryPath,cases,83));
    }
}
