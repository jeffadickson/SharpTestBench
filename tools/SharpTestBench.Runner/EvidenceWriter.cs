using System.Text.Json;

internal static class EvidenceWriter
{
    internal static void Write(string path, JsonElement evidence)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".evidence-" + Guid.NewGuid().ToString("N") + ".tmp");
        if (Directory.Exists(path)) throw new InvalidDataException("Evidence target is a directory.");
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true })) evidence.WriteTo(writer);
        var data = buffer.ToArray();
        if (data.Length > JsonInput.MaximumBytes) throw new InvalidDataException("Evidence exceeds the JSON output limit.");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }
}
