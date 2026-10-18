using System.Text.Json;
using System.Text.Json.Serialization;

internal static class JsonInput
{
    internal const int MaximumBytes = 16 * 1024 * 1024;
    internal static JsonDocument Parse(string text, bool manifest = false)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(text) > MaximumBytes)
            throw new InvalidDataException("JSON exceeds the 16 MiB input limit.");
        var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 64 });
        try { Inspect(document.RootElement, manifest); return document; }
        catch { document.Dispose(); throw; }
    }

    internal static string Read(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > MaximumBytes) throw new InvalidDataException("JSON exceeds the 16 MiB input limit.");
        using var reader = new StreamReader(stream, new System.Text.UTF8Encoding(false, true), true);
        var text = reader.ReadToEnd();
        if (System.Text.Encoding.UTF8.GetByteCount(text) > MaximumBytes) throw new InvalidDataException("JSON grew beyond the input limit.");
        return text;
    }

    internal static RunManifest Manifest(string text)
    {
        using var document = Parse(text, manifest: true);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Manifest must be an object.");
        return document.RootElement.Deserialize<RunManifest>(new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new InvalidDataException("Empty manifest");
    }

    private static void Inspect(JsonElement element, bool manifest)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(manifest ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate JSON property: " + property.Name);
                Inspect(property.Value, manifest);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var value in element.EnumerateArray()) Inspect(value, manifest);
        else if (element.ValueKind == JsonValueKind.String)
        {
            try { _ = element.GetString(); }
            catch (InvalidOperationException error) { throw new InvalidDataException("Invalid Unicode in JSON string.", error); }
        }
    }
}
