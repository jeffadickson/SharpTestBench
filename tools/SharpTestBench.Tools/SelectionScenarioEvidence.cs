using System.Text.Json;
namespace SharpTestBench.Tools;

/// <summary>Independent acceptance rules for known selection/failure recipes, separate from required-pass runner attestation.</summary>
internal static class SelectionScenarioEvidence
{
    internal sealed record Expected(string Identity,ulong Seed,string Status,string? Error=null);
    internal const string BodyMarker="SELECTION_BODY_ENTERED:";
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
}