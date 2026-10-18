using System.Text.Json;

internal sealed record AttestedResults(int Passed, int Failed, string[] Names);

internal static class ResultAttestation
{
    internal static AttestedResults Verify(JsonElement rows, RunManifest config)
    {
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() == 0)
            throw new InvalidDataException("No test results; expected a nonempty result array.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        int passed = 0, failed = 0;
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Each test result must be an object.");
            var properties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in row.EnumerateObject())
                if (!properties.Add(property.Name))
                    throw new InvalidDataException("Duplicate result property: " + property.Name);
            string? identity = null;
            if (row.TryGetProperty("QualifiedName", out var qualified) && qualified.ValueKind != JsonValueKind.Null)
            {
                if (qualified.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException("QualifiedName must be a string.");
                identity = qualified.GetString();
            }
            if (identity is null or "")
            {
                if (!row.TryGetProperty("Name", out var shortName) || shortName.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException("Test result missing name.");
                identity = shortName.GetString();
            }
            if (!ManifestValidation.ValidIdentity(identity))
                throw new InvalidDataException("Test identity must be nonempty without padding or control characters.");
            if (!names.Add(identity!)) throw new InvalidDataException("Duplicate test identity: " + identity);
            if (!row.TryGetProperty("Seed", out var seed) || seed.ValueKind != JsonValueKind.Number || !seed.TryGetUInt64(out _))
                throw new InvalidDataException("Test result needs an unsigned 64-bit Seed.");
            if (!row.TryGetProperty("Status", out var status) || status.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("Test result without Status.");
            switch (status.GetString())
            {
                case "passed": passed++; break;
                case "failed": failed++; break;
                case "skipped": throw new InvalidDataException("Skipped test cases cannot satisfy the required CI manifest.");
                default: throw new InvalidDataException("Unknown test status: " + status.GetString());
            }
        }
        if (rows.GetArrayLength() != config.ExpectedTests)
            throw new InvalidDataException($"Expected {config.ExpectedTests} test results, observed {rows.GetArrayLength()}.");
        if (config.ExpectedNames is not null && !names.SetEquals(config.ExpectedNames))
            throw new InvalidDataException($"Test identities differ. Missing: {string.Join(", ", config.ExpectedNames.Except(names))}; unexpected: {string.Join(", ", names.Except(config.ExpectedNames))}");
        return new(passed, failed, names.Order(StringComparer.Ordinal).ToArray());
    }
}
