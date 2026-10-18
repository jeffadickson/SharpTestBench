internal sealed class WorkspaceInUseException(string path, Exception error)
    : IOException("Workspace is already in use (confirm its owner exited before removing a stale lock): " + path, error);

internal sealed class RunWorkspace : IDisposable
{
    internal static readonly string[] Artifacts = ["run-manifest.json", "failure-manifest.json", "input-manifest.json",
        "icarus-results.json", "icarus-results.xml", "icarus-trace.txt", "icarus-trace.txt.delta", "top.vvp", "DutBindings.cs",
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
