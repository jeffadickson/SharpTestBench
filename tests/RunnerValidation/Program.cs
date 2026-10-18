using System.Text.Json;
try
{
    var text = Console.In.ReadToEnd();
    var config = JsonSerializer.Deserialize<RunManifest>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("Empty manifest");
    ManifestValidation.Validate(config);
    Console.WriteLine(JsonSerializer.Serialize(new { valid = true }));
}
catch (Exception error)
{
    Console.WriteLine(JsonSerializer.Serialize(new { valid = false, error = error.Message }));
}
