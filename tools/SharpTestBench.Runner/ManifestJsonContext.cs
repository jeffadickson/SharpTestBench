using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(RunManifest))]
[JsonSerializable(typeof(string[]))]
internal partial class ManifestJsonContext : JsonSerializerContext { }
