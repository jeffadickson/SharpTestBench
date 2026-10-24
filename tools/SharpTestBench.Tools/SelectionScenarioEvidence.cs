using System.Text.Json;
using System.Text;
namespace SharpTestBench.Tools;

/// <summary>Independent acceptance rules for known selection/failure recipes, separate from required-pass runner attestation.</summary>
internal static class SelectionScenarioEvidence
{
    internal sealed record Expected(string Identity,ulong Seed,string Status,string? Error=null);
    internal const string BodyMarker="SELECTION_BODY_ENTERED:";
    /// <summary>Checks the producer's final publication boundary, including conflicting success evidence on rejected runs.</summary>
    internal static void VerifyNativeCompletion(string directory,IReadOnlyList<Expected> expected,ulong seed,string? rejectionKind=null,string? rejectionError=null)
    {
        string run=Path.Combine(directory,"run-manifest.json"),failure=Path.Combine(directory,"failure-manifest.json");
        using var input=StrictJson.Read(Path.Combine(directory,"input-manifest.json"));
        string? runId=input.RootElement.GetProperty("RunId").GetString();
        if(runId is not {Length:32}||!runId.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid native completion run identity");
        if(rejectionKind is not null)
        {
            if(File.Exists(run))throw new InvalidDataException("Rejected selection published a run manifest");
            using var document=StrictJson.Read(failure);var row=document.RootElement;
            string errorType=rejectionKind=="configuration_error"?"System.IO.InvalidDataException":"RunnerProcessException";
            if(row.GetProperty("SchemaVersion").GetInt32()!=1||row.GetProperty("RunId").GetString()!=runId||row.GetProperty("Status").GetString()!="failed"||row.GetProperty("Kind").GetString()!=rejectionKind||row.GetProperty("ErrorType").GetString()!=errorType||rejectionError is null||!row.GetProperty("Error").GetString()!.Contains(rejectionError,StringComparison.Ordinal))
                throw new InvalidDataException("Native rejection classification differs from selection recipe");
            return;
        }
        // A test assertion failure may produce a valid failed run manifest. It
        // must never be confused with successful required-result attestation.
        if(File.Exists(failure)||expected.Count==0||expected.Any(item=>item.Status=="skipped"))throw new InvalidDataException("Conflicting native completion evidence");
        using var completed=StrictJson.Read(run);var result=completed.RootElement;
        int failed=expected.Count(item=>item.Status=="failed");
        string[] names=result.GetProperty("TestNames").EnumerateArray().Select(item=>item.GetString()!).ToArray();
        if(result.GetProperty("SchemaVersion").GetInt32()!=1||result.GetProperty("RunId").GetString()!=runId||result.GetProperty("Seed").GetUInt64()!=seed||result.GetProperty("Passed").GetInt32()!=expected.Count-failed||result.GetProperty("Failed").GetInt32()!=failed||result.GetProperty("Status").GetString()!=(failed==0?"passed":"failed")||!names.SequenceEqual(expected.Select(item=>item.Identity).Order(StringComparer.Ordinal),StringComparer.Ordinal))
            throw new InvalidDataException("Native run completion differs from selection recipe");
    }
    internal static void Verify(JsonElement rows,IReadOnlyList<Expected> expected,int exitCode,int expectedExit,string output,IReadOnlyList<string> bodies)
    {
        if(expected.Any(item=>string.IsNullOrWhiteSpace(item.Identity)||item.Identity!=item.Identity.Trim()||item.Identity.Any(char.IsControl)||item.Status is not ("passed" or "failed" or "skipped")||item.Status!="passed"&&string.IsNullOrWhiteSpace(item.Error))||expected.Select(item=>item.Identity).Distinct(StringComparer.Ordinal).Count()!=expected.Count)
            throw new InvalidDataException("Invalid independent selection recipe");
        if(exitCode!=expectedExit)throw new InvalidDataException("Selection process exit differs from recipe");
        if(rows.ValueKind!=JsonValueKind.Array||rows.GetArrayLength()!=expected.Count)throw new InvalidDataException("Selection result inventory differs");
        var required=expected.ToDictionary(item=>item.Identity,StringComparer.Ordinal);var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in rows.EnumerateArray())
        {
            if(row.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Selection row must be an object");
            string identity=ParityContracts.Identity(row);
            if(!required.TryGetValue(identity,out var item)||!seen.Add(identity))throw new InvalidDataException("Unknown or duplicate selection result");
            if(identity!="runtime"&&(!row.TryGetProperty("QualifiedName",out var qualified)||qualified.ValueKind!=JsonValueKind.String||qualified.GetString()!=identity))
                throw new InvalidDataException("Selected case requires its qualified identity");
            if(!row.TryGetProperty("Seed",out var seed)||seed.ValueKind!=JsonValueKind.Number||!seed.TryGetUInt64(out ulong actualSeed)||actualSeed!=item.Seed)throw new InvalidDataException("Selection case seed differs from full-order recipe");
            if(!row.TryGetProperty("Status",out var status)||status.ValueKind!=JsonValueKind.String||status.GetString()!=item.Status)throw new InvalidDataException("Selection status differs from recipe");
            if(item.Error is not null&&(!row.TryGetProperty("Error",out var error)||error.ValueKind!=JsonValueKind.String||!error.GetString()!.Contains(item.Error,StringComparison.Ordinal)))
                throw new InvalidDataException("Selection failure reason differs from recipe");
        }
        VerifyBodies(output,bodies);
    }
    internal static void VerifyBodies(string output,IReadOnlyList<string> expected)
    {
        var observed=output.Split('\n').Select(line=>line.TrimEnd('\r')).Where(line=>line.StartsWith(BodyMarker,StringComparison.Ordinal)).Select(line=>line[BodyMarker.Length..]).ToArray();
        if(observed.Distinct(StringComparer.Ordinal).Count()!=observed.Length||!observed.SequenceEqual(expected,StringComparer.Ordinal))
            throw new InvalidDataException("Executed body marker order/inventory differs from independent selection recipe");
    }
    /// <summary>Reads completed simulator streams rather than the front end's intentionally terse CLI summary.</summary>
    internal static string ReadCapturedOutput(string folder,bool native)
    {
        using var document=StrictJson.Read(Path.Combine(folder,native?"simulate-process.json":"legacy-simulate-capture.json"));var root=document.RootElement;
        if(root.GetProperty(native?"SchemaVersion":"schemaVersion").GetInt32()!=1||native&&root.GetProperty("Phase").GetString()!="simulate")throw new InvalidDataException("Simulator capture schema/phase differs");
        long maximum=root.GetProperty(native?"MaxLogBytes":"maximumCaptureBytes").GetInt64();
        if(maximum<1||maximum>16*1024*1024)throw new InvalidDataException("Recipe capture budget differs from bounded fixture policy");
        if(!native&&(root.GetProperty("status").GetString()!="completed"||!root.GetProperty("captureSettled").GetBoolean()||!root.GetProperty("diagnosticsComplete").GetBoolean()))throw new InvalidDataException("Legacy simulator capture is incomplete");
        var values=new List<string>();foreach(var (field,stream) in new[]{("Stdout","stdout"),("Stderr","stderr")})
        {
            string path=Path.Combine(folder,(native?"simulate.":"legacy-simulate.")+stream+".log");
            long length=new FileInfo(path).Length;if(length>maximum)throw new InvalidDataException("Simulator stream exceeds its retained budget");byte[] bytes=File.ReadAllBytes(path);
            if(bytes.LongLength!=length)throw new InvalidDataException("Simulator stream changed while reading");
            if(native)
            {
                var capture=root.GetProperty(field);
                if(capture.GetProperty("Truncated").GetBoolean()||capture.GetProperty("RetainedBytes").GetInt64()!=length||capture.GetProperty("ObservedBytes").GetInt64()!=length)throw new InvalidDataException("Native simulator capture counters differ from retained stream");
            }
            else if(root.GetProperty(stream+"Limited").GetBoolean())throw new InvalidDataException("Legacy simulator stream was truncated");
            values.Add(new UTF8Encoding(false,true).GetString(bytes));
        }
        // Body markers are emitted only to stdout. Appending stderr diagnostics
        // preserves their order without pretending to order the two OS pipes.
        return string.Join("\n",values);
    }
}
