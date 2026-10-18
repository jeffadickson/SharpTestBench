using System.Text.Json;
try
{
    var text = Console.In.ReadToEnd();
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    if (args is ["results"])
    {
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        var config = new RunManifest(1, "icarus", "top", ["top.sv"], "a.dll", "h.so", "b.vpi", "g.so", "sdk", "out",
            ExpectedTests: root.GetProperty("count").GetInt32(),
            ExpectedNames: root.GetProperty("names").Deserialize<string[]>());
        var result = ResultAttestation.Verify(root.GetProperty("rows"), config);
        Console.WriteLine(JsonSerializer.Serialize(new { valid = true, passed = result.Passed, failed = result.Failed }));
    }
    else
    {
        var config = JsonSerializer.Deserialize<RunManifest>(text, options) ?? throw new InvalidDataException("Empty manifest");
        ManifestValidation.Validate(config);
        Console.WriteLine(JsonSerializer.Serialize(new { valid = true }));
    }
}
catch (Exception error)
{
    Console.WriteLine(JsonSerializer.Serialize(new { valid = false, error = error.Message }));
}
