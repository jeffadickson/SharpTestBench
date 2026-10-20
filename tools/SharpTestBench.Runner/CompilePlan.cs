using System.Globalization;
using System.Text.Json;

internal static class CompilePlan
{
    internal static Dictionary<string, string> Environment(RunManifest config)
    {
        var environment = new Dictionary<string, string> { ["CS_SEED"] = config.Seed.ToString(CultureInfo.InvariantCulture) };
        if (config.TestNames is not null) environment["CS_TEST_NAMES"] = JsonSerializer.Serialize(config.TestNames, ManifestJsonContext.Default.StringArray);
        return environment;
    }

    internal static string[] Sources(string root, RunManifest config)
    {
        var sources = config.Sources.Select(path => Path.GetFullPath(Path.Combine(root, path))).ToArray();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (sources.Distinct(comparer).Count() != sources.Length) throw new InvalidDataException("Duplicate source paths.");
        return sources;
    }

    internal static string[] Arguments(string root, RunManifest config, string compiled)
    {
        var arguments = new List<string> { "-g" + config.HdlStandard, "-s", config.Top, "-o", compiled };
        foreach (var directory in config.IncludeDirectories ?? [])
        {
            arguments.Add("-I");
            arguments.Add(Path.GetFullPath(Path.Combine(root, directory)));
        }
        foreach (var (name, value) in (config.Defines ?? []).OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            arguments.Add("-D");
            arguments.Add(name + (value is null ? "" : "=" + value));
        }
        foreach (var (name, value) in (config.Parameters ?? []).OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            arguments.Add("-P");
            arguments.Add(config.Top + "." + name + "=" + value);
        }
        arguments.AddRange(Sources(root, config));
        return arguments.ToArray();
    }
}
