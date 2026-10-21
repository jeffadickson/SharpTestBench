namespace SharpTestBench.Tools;

internal static class LegacyInputs
{
    internal static string Invocation(string work,string label) => Path.Combine(work,label+"-invocation.json");
    internal static string Evidence(string work,string label) => Path.Combine(work,label+"-input-manifest.json");
    static InputFingerprint[] Read(string work,string label,RunManifest config,string fxr) =>
        InputEvidence.Capture(work,Invocation(work,label),config,fxr).Select(input=>input with { Path=RunnerManifestCommand.RealPath(input.Path) }).ToArray();
    internal static InputFingerprint[] Capture(string work,string label,string runId,RunManifest config,string fxr,IReadOnlyDictionary<string,string?> environment)
    {
        var selection=environment.Where(pair=>pair.Key is "CS_SEED" or "CS_TEST_NAMES" or "CS_TEST_FILTER" or "CS_TEST_STAGES" or "CS_LIST_TESTS" or "CS_STAGE_FAILURE_POLICY")
            .ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        StrictJson.Write(Invocation(work,label),new { schemaVersion=1,runId,configuration=config,compilerOptionsApply=config.Simulator=="icarus",selection });
        var inputs=Read(work,label,config,fxr);
        StrictJson.Write(Evidence(work,label),new { schemaVersion=1,status="captured",runId,inputs,generated=Array.Empty<InputFingerprint>() });
        return inputs;
    }
    internal static void Verify(string work,string label,string runId,RunManifest config,string fxr,InputFingerprint[] inputs,InputFingerprint[] generated)
    {
        InputEvidence.Verify(inputs,Read(work,label,config,fxr));
        InputEvidence.Verify(generated,generated.Select(input=>InputEvidence.File(input.Role,input.Path)).ToArray());
        StrictJson.Write(Evidence(work,label),new { schemaVersion=1,status="verified",runId,inputs,generated });
    }
}
