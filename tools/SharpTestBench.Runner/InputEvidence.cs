using System.Security.Cryptography;

internal sealed record InputFingerprint(string Role, string Path, long Bytes, string Sha256);

internal static class InputEvidence
{
    internal static InputFingerprint[] Files(IEnumerable<string> paths) => paths.Select(path => File("input", path))
        .OrderBy(item => item.Path, StringComparer.Ordinal).ToArray();

    internal static void Verify(InputFingerprint[] before, InputFingerprint[] after)
    {
        var left = before.OrderBy(item => item.Role, StringComparer.Ordinal).ThenBy(item => item.Path, StringComparer.Ordinal).ToArray();
        var right = after.OrderBy(item => item.Role, StringComparer.Ordinal).ThenBy(item => item.Path, StringComparer.Ordinal).ToArray();
        if (!left.SequenceEqual(right)) throw new InvalidDataException("Runner inputs changed during execution.");
    }

    internal static InputFingerprint[] Capture(string root, string manifest, RunManifest config, string hostfxr)
    {
        var inputs = new List<InputFingerprint> { File("manifest", manifest) };
        string Resolve(string path) => System.IO.Path.GetFullPath(System.IO.Path.Combine(root, path));
        foreach (var source in CompilePlan.Sources(root, config)) inputs.Add(File("source", source));
        var assembly = Resolve(config.Assembly);
        inputs.Add(File("assembly", assembly));
        foreach (var dll in Directory.EnumerateFiles(System.IO.Path.GetDirectoryName(assembly)!, "*.dll").Order(StringComparer.Ordinal))
            if (dll != assembly) inputs.Add(File("managed-dependency", dll));
        foreach (var extension in new[] { ".runtimeconfig.json", ".deps.json" })
        {
            var path = System.IO.Path.ChangeExtension(assembly, extension);
            if (System.IO.File.Exists(path)) inputs.Add(File("managed-config", path));
        }
        foreach (var (role, path) in new[] { ("native-host", config.NativeHost), ("bootstrap", config.IcarusBootstrap),
            ("gpi", config.GpiLibrary), ("hostfxr", hostfxr) }) inputs.Add(File(role, Resolve(path)));
        var separator = config.GpiImpl!.LastIndexOf(':');
        if (separator <= 0 || separator == config.GpiImpl.Length - 1)
            throw new InvalidDataException("gpiImpl must specify library:symbol.");
        inputs.Add(File("gpi-implementation", Resolve(config.GpiImpl[..separator])));
        foreach (var include in config.IncludeDirectories ?? [])
            foreach (var path in IncludeFiles(Resolve(include))) inputs.Add(File("include", path));
        return inputs.ToArray();
    }

    private static IEnumerable<string> IncludeFiles(string directory)
    {
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(directory));
        int count = 0;
        while (pending.TryPop(out var current))
        {
            if (current.LinkTarget is not null) throw new InvalidDataException("Include trees must not contain symbolic links.");
            foreach (var entry in current.EnumerateFileSystemInfos().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                if (++count > 4096) throw new InvalidDataException("Include tree exceeds 4096 entries.");
                if (entry.LinkTarget is not null) throw new InvalidDataException("Include trees must not contain symbolic links.");
                if (entry is DirectoryInfo subdirectory) pending.Push(subdirectory);
                else yield return entry.FullName;
            }
        }
    }

    internal static InputFingerprint File(string role, string path)
    {
        path = System.IO.Path.GetFullPath(path);
        using var stream = System.IO.File.OpenRead(path);
        var length = stream.Length;
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        if (stream.Length != length) throw new InvalidDataException("Input changed while hashing: " + path);
        return new(role, path, length, hash);
    }
}
