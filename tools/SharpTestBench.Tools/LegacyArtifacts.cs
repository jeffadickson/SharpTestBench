namespace SharpTestBench.Tools;

// Validate ownership before removing old evidence or allowing a compiler to write.
internal static class LegacyArtifacts
{
    internal static string[] Reports(string work, string label) =>
        new[] { ".json", ".xml", ".coverage.json", ".discovery.json" }.Select(extension => Path.Combine(work, label + "-results" + extension)).ToArray();
    internal static string[] Outputs(string work, string label) => [.. Reports(work,label),
        Path.Combine(work,"top.vvp"), Path.Combine(work,"DutBindings.cs"), Path.Combine(work,"cocotb_bootstrap.vpi"),
        Path.Combine(work,label+"-trace.txt"), Path.Combine(work,label+"-trace.txt.delta"),
        Path.Combine(work,label+"-trace.txt.scheduled"), Path.Combine(work,label+"-trace.txt.force"),
        Path.Combine(work,label+"-trace.txt.capabilities.json"), Path.Combine(work,label+"-artifacts.json"),
        Path.Combine(work,label+"-timings.json"), Path.Combine(work,label+"-run.log"),
        Path.Combine(work,label+"-invocation.json"),Path.Combine(work,label+"-input-manifest.json"),
        Path.Combine(work,label+"-compile-process.json"),Path.Combine(work,label+"-compile.stdout.log"),Path.Combine(work,label+"-compile.stderr.log"),
        Path.Combine(work,label+"-compile.json"), Path.Combine(work,label+"-failure.json"),
        Path.Combine(work,label+"-wave-dump.sv")];
    static StringComparer Comparison => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    static bool Contains(string directory, string path)
    {
        var relative = Path.GetRelativePath(directory,path);
        return relative == "." || !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal);
    }
    internal static void Validate(IEnumerable<string> inputs, IEnumerable<string> outputs, IEnumerable<string>? generatedDirectories = null, IEnumerable<string>? includeDirectories = null)
    {
        var files = inputs.Select(RunnerManifestCommand.RealPath).ToHashSet(Comparison);
        var destinations = outputs.Select(Path.GetFullPath).ToArray();
        var folders = (generatedDirectories ?? []).Select(Path.GetFullPath).ToArray();
        foreach (var target in destinations.Concat(folders))
            if (new FileInfo(target).LinkTarget is not null || new DirectoryInfo(target).LinkTarget is not null)
                throw new InvalidDataException("Legacy generated output must not be a symbolic link: "+target);
        var resolved = destinations.Select(RunnerManifestCommand.RealPath).ToArray();
        if (resolved.Distinct(Comparison).Count() != resolved.Length) throw new InvalidDataException("Legacy output destinations alias each other.");
        foreach (var target in resolved)
            if (files.Contains(target)) throw new InvalidDataException("Legacy output collides with an input: "+target);
        foreach (var directory in folders.Select(RunnerManifestCommand.RealPath))
            if (files.Any(input => Contains(directory,input))) throw new InvalidDataException("Legacy generated directory contains an input: "+directory);
        foreach (var include in (includeDirectories ?? []).Select(RunnerManifestCommand.RealPath))
            if (resolved.Any(output => Contains(include,output)) || folders.Select(RunnerManifestCommand.RealPath).Any(folder => Contains(include,folder) || Contains(folder,include)))
                throw new InvalidDataException("Legacy generated output overlaps an include directory.");
    }
    internal static void RetireReports(string work, string label)
    {
        // Caller must have acquired its workspace and validated explicit and resolved runtime inputs.
        foreach (var path in Reports(work,label).Concat(new[] { "-artifacts.json", "-timings.json", "-run.log", "-compile.json", "-compile-process.json", "-compile.stdout.log", "-compile.stderr.log", "-failure.json", "-invocation.json", "-input-manifest.json" }.Select(suffix => Path.Combine(work,label+suffix))))
            File.Delete(path);
    }
}
