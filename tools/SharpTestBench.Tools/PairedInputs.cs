namespace SharpTestBench.Tools;

internal static class PairedInputs
{
    internal const string FileName="pair-input-manifest.json";
    internal static InputFingerprint[] Capture(string work,RunManifest config,string manifestName="manifest.json")
    {
        var sdk=Path.GetFullPath(config.DotnetRoot,work);
        var files=InputEvidence.Capture(work,Path.Combine(work,manifestName),config,NativeBuildCommand.HostFxr(sdk));
        var canonical=files.Select(input=>input with { Path=RunnerManifestCommand.RealPath(input.Path) }).ToArray();
        LegacyArtifacts.Validate(canonical.Select(input=>input.Path),[Path.Combine(work,FileName)]);
        return canonical;
    }
    internal static void Verify(InputFingerprint[] before,string work,RunManifest config,string manifestName="manifest.json") =>
        InputEvidence.Verify(before,Capture(work,config,manifestName));
    internal static void Publish(InputFingerprint[] inputs,string work,RunManifest config,string manifestName="manifest.json")
    {
        Verify(inputs,work,config,manifestName);
        StrictJson.Write(Path.Combine(work,FileName),new { schemaVersion=1,status="verified",runId=Guid.NewGuid().ToString("N"),inputs });
    }
}
