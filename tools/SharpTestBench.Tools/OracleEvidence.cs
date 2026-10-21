namespace SharpTestBench.Tools;

internal static class OracleEvidence
{
    internal const string FileName="oracle-input-manifest.json";
    internal static SortedDictionary<string,string> Capture(string root,IEnumerable<string> references)
    {
        root=RunnerManifestCommand.RealPath(root);
        var result=new SortedDictionary<string,string>(StringComparer.Ordinal);
        foreach (var reference in references)
        {
            var path=RunnerManifestCommand.RealPath(Path.GetFullPath(reference,root));
            var relative=Path.GetRelativePath(root,path);
            if (Path.IsPathRooted(relative) || relative==".." || relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal))
                throw new InvalidDataException("Oracle source must remain inside the repository.");
            if (!result.TryAdd(relative.Replace('\\','/'),CiCommands.Hash(path))) throw new InvalidDataException("Duplicate oracle source.");
        }
        return result;
    }
    internal static void Verify(string root,IEnumerable<string> references,IReadOnlyDictionary<string,string> before)
    {
        var after=Capture(root,references);
        if (before.Count!=after.Count || before.Any(pair=>!after.TryGetValue(pair.Key,out var hash) || hash!=pair.Value))
            throw new InvalidDataException("Oracle source or declared dependency changed.");
    }
}
