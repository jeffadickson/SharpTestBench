internal sealed class WorkspaceInUseException(string path, Exception error)
    : IOException("Workspace is already in use (confirm its owner exited before removing a stale lock): " + path, error);

internal sealed class RunWorkspace : IDisposable
{
    internal static readonly string[] Artifacts = ["run-manifest.json", "failure-manifest.json", "input-manifest.json",
        "icarus-results.json", "icarus-results.xml", "icarus-results.coverage.json", "icarus-results.discovery.json",
        "icarus-trace.txt.scheduled", "icarus-trace.txt.force", "icarus-trace.txt.capabilities.json", "icarus-trace.txt", "icarus-trace.txt.delta", "top.vvp", "DutBindings.cs",
        "compile.stdout.log", "compile.stderr.log", "simulate.stdout.log", "simulate.stderr.log",
        "compile-process.json", "simulate-process.json"];
    private readonly string path;
    private readonly FileStream stream;

    private RunWorkspace(string path, FileStream stream) { this.path = path; this.stream = stream; }

    internal static RunWorkspace Acquire(string directory)
    {
        var path = Path.Combine(directory, ".sharptestbench-run.lock");
        FileStream stream;
        try { stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
        catch (IOException error) when (File.Exists(path)) { throw new WorkspaceInUseException(directory, error); }
        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            stream.Write(bytes);
            stream.Flush();
            return new(path, stream);
        }
        catch
        {
            stream.Dispose();
            File.Delete(path);
            throw;
        }
    }

    internal static void ValidateOwnership(string work, IEnumerable<string> inputs)
    {
        static string Resolve(string path)
        {
            path=Path.GetFullPath(path);var root=Path.GetPathRoot(path)!;var current=root;
            foreach (var part in path[root.Length..].Split(new[] { Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar },StringSplitOptions.RemoveEmptyEntries))
            {
                current=Path.Combine(current,part);
                FileSystemInfo info=File.Exists(current) ? new FileInfo(current) : new DirectoryInfo(current);
                if (info.LinkTarget is not null) current=info.ResolveLinkTarget(true)?.FullName ?? throw new InvalidDataException("Unresolved input link: "+current);
            }
            return current;
        }
        var comparer=OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var protectedFiles=inputs.Select(Resolve).ToHashSet(comparer);
        foreach (var name in Artifacts.Append(".sharptestbench-run.lock"))
        {
            var path=Path.Combine(work,name);
            if (protectedFiles.Contains(Resolve(path))) throw new InvalidDataException("An input path collides with a generated runner artifact.");
            if (new FileInfo(path).LinkTarget is not null) throw new InvalidDataException("Generated runner artifact must not be a symbolic link: "+path);
        }
    }

    internal void ClearArtifacts()
    {
        foreach (var artifact in Artifacts) File.Delete(Path.Combine(Path.GetDirectoryName(path)!, artifact));
    }

    public void Dispose()
    {
        stream.Dispose();
        File.Delete(path);
    }
}
