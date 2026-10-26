using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharpTestBench.Tools;

/// <summary>Bounded JSON persistence; failed replacements leave the prior checkpoint intact and throw.</summary>
internal static class EnduranceStorage
{
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 32 };
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static string HashFile(string path) => Hash(Read(path, 16777216));
    internal static byte[] Read(string path, int maximum)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > maximum) throw new InvalidDataException("Artifact exceeds size limit");
        byte[] bytes = BoundedInput.Read(stream, maximum);
        return bytes;
    }
    internal static T ReadJson<T>(string path) where T : class
    {
        var bytes = Read(path, EnduranceRules.MaximumReportBytes);
        using var strict = StrictJson.Parse(new UTF8Encoding(false, true).GetString(bytes));
        return JsonSerializer.Deserialize<T>(strict.RootElement.GetRawText(), Json) ?? throw new InvalidDataException("Missing JSON record");
    }
    internal static byte[] Serialize<T>(T value) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json) + "\n");
    internal static void WriteJson<T>(string path, T value)
    {
        byte[] bytes = Serialize(value);
        if (bytes.Length > EnduranceRules.MaximumReportBytes) throw new InvalidDataException("Checkpoint exceeds size limit");
        Write(path, bytes);
    }
    internal static void Write(string path, byte[] bytes, Action<string, string>? replace = null, Action<TimeSpan>? pause = null)
    {
        if (bytes.Length > 16777216) throw new InvalidDataException("Local artifact exceeds size limit");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string pending = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }
            // Windows readers/antivirus can temporarily deny a rename. Retry only
            // Replacement can report sharing denial as UnauthorizedAccessException
            // on Windows. Permanent denial still propagates after bounded retries.
            for (int attempt = 0; ; attempt++)
            {
                try { (replace ?? ((from, to) => File.Move(from, to, true)))(pending, path); break; }
                catch (Exception error) when (attempt < 5 && error is IOException or UnauthorizedAccessException) { (pause ?? (delay => Thread.Sleep(delay)))(TimeSpan.FromMilliseconds(20 << attempt)); }
            }
        }
        finally { if (File.Exists(pending)) File.Delete(pending); }
    }
    internal static void NoLinks(string root, string path)
    {
        root = Path.GetFullPath(root); path = Path.GetFullPath(path);
        string relative = Path.GetRelativePath(root, path);
        if (relative != ".") EnduranceRules.RequirePath(relative.Replace('\\', '/'));
        for (var item = new DirectoryInfo(Path.GetDirectoryName(path)!); item is not null; item = item.Parent)
        {
            if (item.Exists && item.LinkTarget is not null) throw new InvalidDataException("Linked artifact directory");
            if (string.Equals(item.FullName, root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) break;
        }
        if (File.Exists(path) && new FileInfo(path).LinkTarget is not null || Directory.Exists(path) && new DirectoryInfo(path).LinkTarget is not null) throw new InvalidDataException("Linked artifact");
    }
}

/// <summary>OS-backed exclusive lease; process termination releases it without deleting another owner's lock.</summary>
internal sealed class EnduranceLease : IDisposable
{
    readonly FileStream stream;
    internal EnduranceLease(string root)
    {
        string directory = Path.Combine(root, "build", "endurance");
        EnduranceStorage.NoLinks(root, Path.Combine(directory, "checkout.lock"));
        Directory.CreateDirectory(directory);
        try { stream = new FileStream(Path.Combine(directory, "checkout.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException error) { throw new IOException("An endurance campaign already owns this checkout", error); }
    }
    public void Dispose() => stream.Dispose();
}
