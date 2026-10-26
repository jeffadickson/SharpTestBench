using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SharpTestBench.Tools;

internal sealed record EndurancePublicationResult(string Status, string Branch, string Commit, string RetryCommand);

/// <summary>Report-only trees and unique refs: no checkout, merge, source-index writes or force push.</summary>
internal static class EndurancePublication
{
    internal static string Branch(string platform, string architecture, string label, string source) =>
        $"remote-runs/{platform}-{architecture}-{label}/{DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ",CultureInfo.InvariantCulture)}-{source[..12]}-{Guid.NewGuid():N}";
    internal static void ValidateBranch(string branch, EnduranceReport? report = null)
    {
        if (!Regex.IsMatch(branch, "\\Aremote-runs/(windows|linux)-(x64|arm64)-[a-z0-9][a-z0-9-]{0,39}/[0-9]{8}T[0-9]{9}Z-[0-9a-f]{12}-[0-9a-f]{32}\\z", RegexOptions.CultureInvariant) || report is not null && (!branch.StartsWith($"remote-runs/{report.Platform}-{report.Architecture}-{report.MachineLabel}/", StringComparison.Ordinal) || !branch.Contains("-" + report.SourceCommit[..12] + "-", StringComparison.Ordinal)))
            throw new InvalidDataException("Invalid results branch");
    }
    internal static void ValidateRemote(string value)
    {
        if (value.Length is < 1 or > 2048 || value.Any(char.IsControl) || value.StartsWith('-')) throw new InvalidDataException("Unsafe results remote");
        if (Path.IsPathFullyQualified(value)) return; // Local bare remotes are used by the integration tests.
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "ssh" or "file" && uri.Query.Length == 0 && uri.Fragment.Length == 0 && (uri.Scheme == "ssh" ? uri.UserInfo is "" or "git" : uri.UserInfo.Length == 0)) return;
        if (Regex.IsMatch(value, "\\Agit@[a-zA-Z0-9.-]+:[a-zA-Z0-9_./-]+\\z", RegexOptions.CultureInvariant)) return;
        throw new InvalidDataException("Use a credential-free HTTPS/SSH URL or a local bare repository");
    }
    internal static string Preflight(string root, string expectedSource, string label, string? branch = null)
    {
        var git = new EnduranceGit(root); EnduranceRules.RequireRevision(git.Head, expectedSource); EnduranceRules.RequireClean(git.Dirty);
        // Byte identity, not only Git's clean flag: autocrlf and clean/smudge
        // filters can leave clean worktrees with different tested input bytes.
        foreach(var item in git.CommittedHashes(expectedSource))
        {
            string file=Path.Combine(root,item.Key);EnduranceStorage.NoLinks(root,file);
            EnduranceRules.RequireHash(item.Value,EnduranceStorage.HashFile(file));
        }
        if (!EnduranceRules.Label(label)) throw new ArgumentException("Invalid machine label");
        branch ??= Branch(EnduranceCampaign.Platform, EnduranceCampaign.Architecture, label, expectedSource); ValidateBranch(branch);
        ValidateRemote(git.Text("remote", "get-url", "origin"));
        if (git.Text("ls-remote", "--heads", "origin", "refs/heads/" + branch).Length != 0) throw new IOException("Results branch already exists on the remote");
        git.Text("push", "--dry-run", "origin", expectedSource + ":refs/heads/" + branch);
        return branch;
    }
    internal static EnduranceReport Finished(string root, string path, string expectedSource, bool requireLocalArtifacts)
    {
        var report = EnduranceStorage.ReadJson<EnduranceReport>(path); EnduranceRules.Validate(report);
        new EnduranceGit(root).VerifySource(report, expectedSource);
        if(requireLocalArtifacts && report.Imported)
        {
            string work=Path.GetDirectoryName(Path.GetFullPath(path))!;
            string originalPath=Path.Combine(work,"imported-summary.json");
            var sums=EnduranceStorage.ReadJson<SortedDictionary<string,string>>(Path.Combine(work,"imported-checksums.json"));
            if(sums.Count!=1||!sums.TryGetValue("summary.json",out string? expected))throw new InvalidDataException("Imported provenance is missing");
            byte[] original=EnduranceStorage.Read(originalPath,EnduranceRules.MaximumReportBytes);
            EnduranceRules.RequireHash(expected,EnduranceStorage.Hash(original));EnduranceRules.RequireHash(report.ImportedEvidenceSha256!,expected);
            var projection=Finished(root,originalPath,expectedSource,requireLocalArtifacts:false);
            projection.Imported=true;projection.ImportedEvidenceSha256=expected;
            if(EnduranceStorage.Hash(EnduranceStorage.Serialize(projection))!=EnduranceStorage.Hash(EnduranceStorage.Serialize(report)))throw new InvalidDataException("Imported report differs from its checksummed origin");
        }
        if (requireLocalArtifacts && !report.Imported)
        {
            string work = Path.GetDirectoryName(Path.GetFullPath(path))!; EnduranceStorage.NoLinks(root, path);
            foreach (var (relative, hash) in report.LogHashes)
            {
                string file = Path.Combine(work, relative); EnduranceStorage.NoLinks(work, file); EnduranceRules.RequireHash(hash, EnduranceStorage.HashFile(file));
            }
            string snapshot = Path.Combine(work, "source");
            if (EnduranceGit.Matches(snapshot, report.SourceHashes) != report.SnapshotUnchanged) throw new InvalidDataException("Preserved source snapshot differs from declared consistency");
            if(report.Stryker.ReportSha256 is not null)
            {
                string directory=Path.Combine(work,"stryker");string[] raw=Directory.GetFiles(directory,"mutation-report.json",SearchOption.AllDirectories);
                if(raw.Length!=1)throw new InvalidDataException("Missing local Stryker completion artifact");
                EnduranceStorage.NoLinks(work,raw[0]);EnduranceRules.RequireHash(report.Stryker.ReportSha256,EnduranceStorage.HashFile(raw[0]));
                var tested=EnduranceStrykerEngine.Read(directory,File.ReadAllText(Path.Combine(snapshot,"tools/SharpTestBench.Tools/EnduranceRules.cs")),report.RequestedMutations);
                if(EnduranceStorage.Hash(EnduranceStorage.Serialize(tested))!=EnduranceStorage.Hash(EnduranceStorage.Serialize(report.Stryker)))throw new InvalidDataException("Stryker aggregate differs from raw outcomes");
            }
            if (report.FailureInputSha256 is not null) EnduranceRules.RequireHash(report.FailureInputSha256, EnduranceStorage.HashFile(Path.Combine(work, "failure-input.json")));
        }
        return report;
    }
    internal static EndurancePublicationResult Publish(string root, string path, string expectedSource, string? branch = null, bool push = true)
    {
        using var lease = new EnduranceLease(root);
        var git = new EnduranceGit(root); EnduranceRules.RequireClean(git.Dirty);
        var report = Finished(root, path, expectedSource, requireLocalArtifacts: true);
        branch ??= Branch(report.Platform, report.Architecture, report.MachineLabel, report.SourceCommit); ValidateBranch(branch, report);
        if (push) Preflight(root, expectedSource, report.MachineLabel, branch);
        string sourceHead = git.Head, symbolic = git.SymbolicHead;
        string work = Path.GetDirectoryName(Path.GetFullPath(path))!;
        EnduranceStorage.NoLinks(root, Path.Combine(work, "publication.json"));
        byte[] summary = EnduranceStorage.Serialize(report);
        if (summary.Length > EnduranceRules.MaximumReportBytes) throw new InvalidDataException("Portable summary exceeds limit");
        byte[] sums = EnduranceStorage.Serialize(new SortedDictionary<string, string>(StringComparer.Ordinal) { ["summary.json"] = EnduranceStorage.Hash(summary) });
        string index = Path.Combine(work, "publication-index-" + Guid.NewGuid().ToString("N"));
        var environment = new Dictionary<string, string?>
        {
            ["GIT_INDEX_FILE"] = index, ["GIT_AUTHOR_NAME"] = "SharpTestBench endurance", ["GIT_AUTHOR_EMAIL"] = "endurance@invalid",
            ["GIT_COMMITTER_NAME"] = "SharpTestBench endurance", ["GIT_COMMITTER_EMAIL"] = "endurance@invalid"
        };
        string Text(string[] args, byte[]? input = null) => Encoding.UTF8.GetString(git.Bytes(args, input, environment)).Trim();
        string commit;
        try
        {
            Text(["read-tree", "--empty"]);
            foreach (var (name, bytes) in new[] { ("summary.json", summary), ("checksums.json", sums) })
            {
                string blob = Text(["hash-object", "-w", "--stdin"], bytes);
                Text(["update-index", "--add", "--cacheinfo", "100644," + blob + ",reports/endurance/" + report.RunId + "/" + name]);
            }
            string tree = Text(["write-tree"]);
            commit = Text(["commit-tree", tree, "-p", report.SourceCommit], Encoding.UTF8.GetBytes("Endurance " + report.Status + " " + report.RunId + "\n"));
            if (!EnduranceRules.Commit(commit)) throw new InvalidDataException("Invalid result commit");
            Text(["update-ref", "refs/heads/" + branch, commit, new string('0', 40)]);
        }
        finally { if (File.Exists(index)) File.Delete(index); if (File.Exists(index + ".lock")) File.Delete(index + ".lock"); }
        string retry = "git push origin " + commit + ":refs/heads/" + branch;
        var result = new EndurancePublicationResult(push ? "pushed" : "local-only", branch, commit, retry);
        if (push)
        {
            try { git.Text("push", "origin", commit + ":refs/heads/" + branch); }
            catch (IOException) { result = result with { Status = "push-failed" }; }
        }
        if (git.Head != sourceHead || git.SymbolicHead != symbolic) throw new InvalidDataException("Source HEAD changed during publication");
        EnduranceStorage.WriteJson(Path.Combine(work, "publication.json"), result); return result;
    }
    internal static string Import(string root, string summaryPath, string checksumsPath, string expectedSource)
    {
        using var lease = new EnduranceLease(root);
        var checksums = EnduranceStorage.ReadJson<SortedDictionary<string, string>>(checksumsPath);
        if (checksums.Count != 1 || !checksums.TryGetValue("summary.json", out string? hash)) throw new InvalidDataException("Invalid imported checksums");
        byte[] original = EnduranceStorage.Read(summaryPath, EnduranceRules.MaximumReportBytes); EnduranceRules.RequireHash(hash, EnduranceStorage.Hash(original));
        var report = Finished(root, summaryPath, expectedSource, requireLocalArtifacts: false);
        report.Imported = true; report.ImportedEvidenceSha256 = EnduranceStorage.Hash(original);
        string work = Path.Combine(root, "build/endurance-imports", Guid.NewGuid().ToString("N")); EnduranceStorage.NoLinks(root, Path.Combine(work, "report.json"));
        EnduranceRules.Validate(report); string output = Path.Combine(work, "report.json");
        EnduranceStorage.Write(Path.Combine(work,"imported-summary.json"),original);
        EnduranceStorage.WriteJson(Path.Combine(work,"imported-checksums.json"),checksums);
        EnduranceStorage.WriteJson(output, report); return output;
    }
    internal static EnduranceReport Review(string root, string commit, string expectedSource)
    {
        var git = new EnduranceGit(root); var entries = git.Tree(commit);
        if (entries.Length != 2 || entries.Any(entry => entry.Mode!="100644" || !Regex.IsMatch(entry.Path, "\\Areports/endurance/[0-9a-f]{32}/(summary|checksums)\\.json\\z", RegexOptions.CultureInvariant)) || entries.Select(entry => Path.GetDirectoryName(entry.Path)).Distinct(StringComparer.Ordinal).Count() != 1)
            throw new InvalidDataException("Results tree contains non-allowlisted files");
        string summary = entries.Single(entry => entry.Path.EndsWith("/summary.json", StringComparison.Ordinal)).Path;
        string checksums = entries.Single(entry => entry.Path.EndsWith("/checksums.json", StringComparison.Ordinal)).Path;
        byte[] bytes = git.Bytes(["show", commit + ":" + summary]);
        byte[] sums = git.Bytes(["show", commit + ":" + checksums]);
        if (bytes.Length > EnduranceRules.MaximumReportBytes || sums.Length > 4096) throw new InvalidDataException("Published report exceeds bounds");
        using var check = StrictJson.Parse(Encoding.UTF8.GetString(sums));
        if (check.RootElement.EnumerateObject().Count() != 1) throw new InvalidDataException("Published checksum inventory differs");
        EnduranceRules.RequireHash(check.RootElement.GetProperty("summary.json").GetString()!, EnduranceStorage.Hash(bytes));
        using var strict = StrictJson.Parse(new UTF8Encoding(false, true).GetString(bytes));
        var report = System.Text.Json.JsonSerializer.Deserialize<EnduranceReport>(strict.RootElement.GetRawText(), EnduranceStorage.Json) ?? throw new InvalidDataException("Missing result report");
        EnduranceRules.Validate(report); git.VerifySource(report, expectedSource);
        if (summary != "reports/endurance/" + report.RunId + "/summary.json") throw new InvalidDataException("Published run identity differs from path");
        return report;
    }
}
