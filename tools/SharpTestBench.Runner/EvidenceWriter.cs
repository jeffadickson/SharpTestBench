using System.Text.Json;

internal static class EvidenceWriter
{
    internal static void Write(string path, object evidence)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".evidence-" + Guid.NewGuid().ToString("N") + ".tmp");
        if (Directory.Exists(path)) throw new InvalidDataException("Evidence target is a directory.");
        var data = JsonSerializer.SerializeToUtf8Bytes(evidence, new JsonSerializerOptions { WriteIndented = true });
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
