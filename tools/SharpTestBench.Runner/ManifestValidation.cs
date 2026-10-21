using System.Text.RegularExpressions;

internal static class ManifestValidation
{
    internal static bool ValidIdentity(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value == value.Trim() && !value.Any(char.IsControl);

    internal static void Validate(RunManifest config)
    {
        if (config.SchemaVersion != 1 || config.Simulator != "icarus")
            throw new InvalidDataException("Runner v1 supports schemaVersion=1 and simulator=icarus only.");
        if (config.ExpectedTests is null or <= 0)
            throw new InvalidDataException("expectedTests must be a positive count for CI attestation.");
        if (config.ExpectedNames is not null &&
            (config.ExpectedNames.Length != config.ExpectedTests ||
             config.ExpectedNames.Any(name => !ValidIdentity(name)) ||
             config.ExpectedNames.Distinct(StringComparer.Ordinal).Count() != config.ExpectedNames.Length))
            throw new InvalidDataException("expectedNames must contain unique, nonempty identities matching expectedTests.");
        ValidateExpectedSeeds(config);
        if (config.Sources is not { Length: > 0 })
            throw new InvalidDataException("At least one source is required.");
        if (string.IsNullOrEmpty(config.Top) || !Regex.IsMatch(config.Top, @"\A[A-Za-z_][A-Za-z0-9_$]*\z"))
            throw new InvalidDataException("Top module must be a simple HDL identifier.");
        foreach (var (name, value) in new[] {
            ("assembly", config.Assembly), ("nativeHost", config.NativeHost),
            ("icarusBootstrap", config.IcarusBootstrap), ("gpiLibrary", config.GpiLibrary),
            ("dotnetRoot", config.DotnetRoot), ("outputDirectory", config.OutputDirectory),
            ("gpiImpl", config.GpiImpl), ("gpiEntryPoint", config.GpiEntryPoint) })
            Required(name, value);
        foreach (var source in config.Sources) Required("source", source);
        if (!Regex.IsMatch(config.GpiEntryPoint, @"\A[A-Za-z_][A-Za-z0-9_]*\z"))
            throw new InvalidDataException("gpiEntryPoint must be a simple native symbol.");
        if (config.TestNames is { } selected)
        {
            if (selected.Length == 0 || selected.Any(name => !ValidIdentity(name)) ||
                selected.Distinct(StringComparer.Ordinal).Count() != selected.Length)
                throw new InvalidDataException("testNames must be unique nonempty identities.");
            if (config.ExpectedNames is null || !selected.ToHashSet(StringComparer.Ordinal).SetEquals(config.ExpectedNames))
                throw new InvalidDataException("testNames must match the expectedNames attestation.");
        }
        if (config.EntryType is not null) Required("entryType", config.EntryType);
        if (config.Iverilog is not null) Required("iverilog", config.Iverilog);
        if (config.Vvp is not null) Required("vvp", config.Vvp);
        HdlCompileValidation.Validate(config);
        if (config.MaxLogBytes is < 1024 or > 64 * 1024 * 1024)
            throw new InvalidDataException("maxLogBytes must be between 1024 and 67108864.");
        if (config.TimeoutSeconds is < 1 or > 3600)
            throw new InvalidDataException("timeoutSeconds must be between 1 and 3600.");
    }

    internal static void ValidateExpectedSeeds(RunManifest config)
    {
        if (config.ExpectedSeeds is not { } seeds) return;
        if (config.ExpectedNames is null || seeds.Count != config.ExpectedTests ||
            !seeds.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(config.ExpectedNames))
            throw new InvalidDataException("expectedSeeds must contain exactly the expectedNames identities.");
    }

    private static void Required(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
            throw new InvalidDataException($"{name} must be nonempty and contain no control characters.");
    }

    internal static string ResolveOutput(string root, string output)
    {
        var work = Path.GetFullPath(Path.Combine(root, output));
        var relative = Path.GetRelativePath(root, work);
        if (relative == "." || relative == ".." || Path.IsPathRooted(relative) ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("outputDirectory must be below the manifest directory.");
        var rootPath = new DirectoryInfo(root).FullName;
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        for (var current = new DirectoryInfo(work); !comparer.Equals(current.FullName, rootPath);
             current = current.Parent ?? throw new InvalidDataException("Could not resolve the output directory parent."))
            if (current.LinkTarget is not null)
                throw new InvalidDataException("outputDirectory must not traverse a symbolic link.");
        return work;
    }
}
