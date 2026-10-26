using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SharpTestBench.Tools;

/// <summary>Git plumbing with fixed argument boundaries; report publication never checks out a results branch.</summary>
internal sealed class EnduranceGit(string root)
{
    internal string Root { get; } = Path.GetFullPath(root);
    internal byte[] Bytes(string[] arguments, byte[]? input = null, IReadOnlyDictionary<string, string?>? environment = null)
    {
        var clean = new Dictionary<string,string?> { ["GIT_OPTIONAL_LOCKS"]="0", ["GIT_DIR"]=null, ["GIT_WORK_TREE"]=null, ["GIT_COMMON_DIR"]=null, ["GIT_INDEX_FILE"]=null };
        if(environment is not null)foreach(var item in environment)clean[item.Key]=item.Value;
        var result = EnduranceProcess.Run("git", ["--no-replace-objects", .. arguments], Root, TimeSpan.FromSeconds(30), 16777216, environment: clean, stdin: input);
        if (!result.Passed) throw new IOException("Git operation failed: " + arguments[0]);
        return result.Stdout;
    }
    internal string Text(params string[] arguments) => new UTF8Encoding(false, true).GetString(Bytes(arguments)).Trim();
    internal bool Dirty => Text("status", "--porcelain", "--untracked-files=normal").Length != 0;
    internal string Head => Text("rev-parse", "HEAD");
    internal string SymbolicHead => Text("rev-parse", "--symbolic-full-name", "HEAD");
    internal string[] Tracked() => new UTF8Encoding(false, true).GetString(Bytes(["ls-files", "-z"])).Split('\0', StringSplitOptions.RemoveEmptyEntries);
    internal sealed record Entry(string Path, string Object, string Mode);
    internal Entry[] Tree(string commit)
    {
        if (!EnduranceRules.Commit(commit)) throw new InvalidDataException("Invalid source commit");
        string[] lines = new UTF8Encoding(false, true).GetString(Bytes(["ls-tree", "-rz", "--full-tree", commit])).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length is < 1 or > 4096) throw new InvalidDataException("Source inventory exceeds bounds");
        return lines.Select(line =>
        {
            int tab = line.IndexOf('\t'); if (tab < 0) throw new InvalidDataException("Malformed Git source tree");
            string[] header = line[..tab].Split(' '); string path = line[(tab + 1)..]; EnduranceRules.RequirePath(path);
            if (header.Length != 3 || header[0] is not ("100644" or "100755") || header[1] != "blob" || !EnduranceRules.Commit(header[2])) throw new InvalidDataException("Linked/submodule source is unsupported");
            return new Entry(path, header[2], header[0]);
        }).ToArray();
    }
    internal SortedDictionary<string, string> CommittedHashes(string commit)
    {
        var entries = Tree(commit); var objects = entries.Select(entry => entry.Object).Distinct(StringComparer.Ordinal).ToArray();
        var info = new ProcessStartInfo("git") { WorkingDirectory = Root, UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string name in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_COMMON_DIR", "GIT_INDEX_FILE" }) info.Environment.Remove(name);
        info.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        foreach (string arg in new[] { "--no-replace-objects", "cat-file", "--batch" }) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException("Cannot read Git blobs");
        var errors = process.StandardError.ReadToEndAsync();
        var feed = Task.Run(async () => { foreach (string id in objects) await process.StandardInput.WriteLineAsync(id); process.StandardInput.Close(); });
        var read = Task.Run(async () =>
        {
            long total = 0; var hashes = new Dictionary<string, string>(StringComparer.Ordinal); var stream = process.StandardOutput.BaseStream;
            foreach (string id in objects)
            {
                var header = new StringBuilder(); int next;
                while ((next = stream.ReadByte()) != '\n') { if (next < 0 || header.Length >= 100) throw new InvalidDataException("Incomplete Git blob header"); header.Append((char)next); }
                string[] fields = header.ToString().Split(' ');
                if (fields.Length != 3 || fields[0] != id || fields[1] != "blob" || !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int size) || size < 0 || size > 16777216 || (total += size) > 268435456) throw new InvalidDataException("Git blob exceeds source bounds");
                byte[] content = new byte[size]; await stream.ReadExactlyAsync(content);
                if (stream.ReadByte() != '\n') throw new InvalidDataException("Malformed Git blob separator");
                hashes.Add(id, EnduranceStorage.Hash(content));
            }
            return hashes;
        });
        try
        {
            Task.WhenAll(feed, read).WaitAsync(TimeSpan.FromSeconds(60)).GetAwaiter().GetResult();
            if (!process.WaitForExit(2000) || process.ExitCode != 0 || !errors.Wait(2000) || errors.Result.Length != 0) throw new IOException("Git blob read did not complete");
            return new SortedDictionary<string, string>(entries.ToDictionary(entry => entry.Path, entry => read.Result[entry.Object], StringComparer.Ordinal), StringComparer.Ordinal);
        }
        catch { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } throw; }
    }
    internal SortedDictionary<string, string> Snapshot(string destination)
    {
        string[] paths = Tracked();
        if (paths.Length is < 1 or > 4096 || paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length || Directory.Exists(destination)) throw new InvalidDataException("Invalid/occupied source snapshot");
        long total = 0; var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in paths)
        {
            EnduranceRules.RequirePath(path); string source = Path.Combine(Root, path); EnduranceStorage.NoLinks(Root, source);
            byte[] content = EnduranceStorage.Read(source, 16777216);
            if ((total += content.Length) > 268435456) throw new InvalidDataException("Source snapshot exceeds 256 MiB");
            string copy = Path.Combine(destination, path); Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            using (var stream = new FileStream(copy, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Write(content);
            hashes.Add(path, EnduranceStorage.Hash(content));
        }
        return hashes;
    }
    internal static bool Matches(string snapshot, IReadOnlyDictionary<string, string> hashes) => hashes.All(item =>
    {
        string file=Path.Combine(snapshot,item.Key);EnduranceStorage.NoLinks(snapshot,file);
        return File.Exists(file)&&EnduranceStorage.HashFile(file)==item.Value;
    });
    internal void VerifySource(EnduranceReport report, string expected)
    {
        EnduranceRules.RequireRevision(report.SourceCommit, expected); EnduranceRules.RequireClean(report.Dirty);
        var actual = CommittedHashes(expected);
        // JSON does not preserve a SortedDictionary's comparer. Compare the key
        // sets ordinally rather than depending on the reader's current culture.
        if (!actual.Keys.Order(StringComparer.Ordinal).SequenceEqual(report.SourceHashes.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal)) throw new InvalidDataException("Source inventory differs");
        foreach (var (path, hash) in actual) EnduranceRules.RequireHash(report.SourceHashes[path], hash);
    }
}
