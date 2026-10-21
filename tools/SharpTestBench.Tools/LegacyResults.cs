using System.Text.Json;

namespace SharpTestBench.Tools;

internal static class LegacyResults
{
    internal static void Validate(JsonElement rows, bool discovery)
    {
        if (rows.ValueKind != JsonValueKind.Array || !discovery && rows.GetArrayLength() == 0)
            throw new InvalidDataException("No results; expected a result array.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Each result must be an object.");
            var name = ParityContracts.Identity(row);
            if (!names.Add(name)) throw new InvalidDataException("Duplicate test identity: " + name);
            if (!row.TryGetProperty("Seed",out var seed) || seed.ValueKind != JsonValueKind.Number || !seed.TryGetUInt64(out _))
                throw new InvalidDataException("Test result needs an unsigned 64-bit Seed.");
            if (!row.TryGetProperty("Status",out var status) || status.ValueKind != JsonValueKind.String || status.GetString() is not ("passed" or "failed" or "skipped"))
                throw new InvalidDataException("Unknown or missing test status.");
        }
    }
}
