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
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = stream.Read(buffer)) != 0)
        {
            if (bytes.Length + count > MaximumBytes) throw new InvalidDataException("JSON grew beyond the input limit.");
            bytes.Write(buffer, 0, count);
        }
        var text = new System.Text.UTF8Encoding(false, true).GetString(bytes.ToArray());
        return text.StartsWith('\uFEFF') ? text[1..] : text;
    }

    internal static RunManifest Manifest(string text)
    {
        using var document = Parse(text, manifest: true);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Manifest must be an object.");
        return document.RootElement.Deserialize(ManifestJsonContext.Default.RunManifest) ?? throw new InvalidDataException("Empty manifest");
    }

    private static void Inspect(JsonElement element, bool manifest)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(manifest ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                string name;
                try { name = property.Name; }
                catch (InvalidOperationException error) { throw new InvalidDataException("Invalid Unicode in JSON property name.", error); }
                if (!names.Add(name)) throw new InvalidDataException("Duplicate JSON property: " + name);
                Inspect(property.Value, manifest: false);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var value in element.EnumerateArray()) Inspect(value, manifest);
        else if (element.ValueKind == JsonValueKind.Number &&
                 (!element.TryGetDouble(out var number) || !double.IsFinite(number)))
            throw new InvalidDataException("Nonfinite JSON number.");
        else if (element.ValueKind == JsonValueKind.String)
        {
            try { _ = element.GetString(); }
            catch (InvalidOperationException error) { throw new InvalidDataException("Invalid Unicode in JSON string.", error); }
        }
    }
}
