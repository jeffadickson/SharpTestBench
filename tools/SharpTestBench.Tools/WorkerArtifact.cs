using System.Text.Json;
namespace SharpTestBench.Tools;

internal static class WorkerArtifact
{
    public static Dictionary<string, string> Files(string directory)
    {
        directory = Path.GetFullPath(directory);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var pending = new Stack<DirectoryInfo>(); pending.Push(new DirectoryInfo(directory));
        while (pending.TryPop(out var folder))
        {
            if (folder.LinkTarget is not null) throw new InvalidDataException("Worker artifacts must not contain links");
            foreach (var item in folder.EnumerateFileSystemInfos().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                if (item.LinkTarget is not null) throw new InvalidDataException("Worker artifacts must not contain links");
                if (item is DirectoryInfo child) { pending.Push(child); continue; }
                var name = Path.GetRelativePath(directory, item.FullName).Replace('\\', '/');
                if (name == "worker-build.json") continue;
                if (result.Count >= 4096) throw new InvalidDataException("Worker artifact file limit exceeded");
                result.Add(name, CiCommands.Hash(item.FullName));
            }
        }
        return result;
    }
    public static string EntryPoint(WorkerPublish plan) => "SharpTestBench." + (plan.Name == "tools" ? "Tools" : "Runner") + (plan.Rid.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : "");
    public static void Write(WorkerPublish plan)
    {
        var files = Files(plan.Directory); var entry = EntryPoint(plan);
        if (!files.ContainsKey(entry) || plan.Name == "tools" && !files.ContainsKey("SharpTestBench.Tools.dll")) throw new InvalidDataException("Missing worker executable or managed implementation");
        StrictJson.Write(Path.Combine(plan.Directory, "worker-build.json"), new { schemaVersion = 1, name = plan.Name, rid = plan.Rid, deployment = plan.Deployment, entryPoint = entry, files });
    }
    public static void Verify(WorkerPublish plan)
    {
        using var document = StrictJson.Read(Path.Combine(plan.Directory, "worker-build.json")); var value = document.RootElement;
        FixtureContracts.Fields(value, "schemaVersion", "name", "rid", "deployment", "entryPoint", "files");
        if (value.GetProperty("schemaVersion").GetInt32() != 1 || value.GetProperty("name").GetString() != plan.Name || value.GetProperty("rid").GetString() != plan.Rid || value.GetProperty("deployment").GetString() != plan.Deployment || value.GetProperty("entryPoint").GetString() != EntryPoint(plan)) throw new InvalidDataException("Worker artifact identity mismatch");
        var files = value.GetProperty("files");
        if (files.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid worker file map");
        var actual = Files(plan.Directory); var expected = files.EnumerateObject().ToArray();
        if (expected.Length != actual.Count || !actual.ContainsKey(EntryPoint(plan))) throw new InvalidDataException("Worker artifact file set changed");
        foreach (var file in expected)
            if (file.Value.ValueKind != JsonValueKind.String || !actual.TryGetValue(file.Name, out var hash) || hash != file.Value.GetString()) throw new InvalidDataException("Worker artifact checksum mismatch: " + file.Name);
        if (plan.Name == "tools" && !actual.ContainsKey("SharpTestBench.Tools.dll")) throw new InvalidDataException("Missing helper implementation");
    }
}
