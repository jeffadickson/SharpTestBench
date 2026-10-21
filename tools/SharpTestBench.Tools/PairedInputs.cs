namespace SharpTestBench.Tools;

internal static class PairedInputs
{
    internal const string FileName="pair-input-manifest.json";
    internal static InputFingerprint[] Capture(string work,RunManifest config)
    {
        var sdk=Path.GetFullPath(config.DotnetRoot,work);
        var files=InputEvidence.Capture(work,Path.Combine(work,"manifest.json"),config,NativeBuildCommand.HostFxr(sdk));
        var canonical=files.Select(input=>input with { Path=RunnerManifestCommand.RealPath(input.Path) }).ToArray();
        LegacyArtifacts.Validate(canonical.Select(input=>input.Path),[Path.Combine(work,FileName)]);
        return canonical;
    }
    internal static void Verify(InputFingerprint[] before,string work,RunManifest config) =>
        InputEvidence.Verify(before,Capture(work,config));
}
