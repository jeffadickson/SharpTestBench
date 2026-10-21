using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SharpTestBench.Tools;

internal sealed record CommandResult(int ExitCode, string Output, string Error);
internal interface ICommandRunner
{
    CommandResult Run(IReadOnlyList<string> arguments, string directory, IReadOnlyDictionary<string, string?>? environment = null, bool capture = false, bool check = true, TimeSpan? timeout = null);
}
internal sealed class CommandRunner : ICommandRunner
{
    public CommandResult Run(IReadOnlyList<string> arguments, string directory, IReadOnlyDictionary<string, string?>? environment = null, bool capture = false, bool check = true, TimeSpan? timeout = null)
    {
        if (arguments.Count == 0) throw new ArgumentException("Command is empty", nameof(arguments));
        var start = new ProcessStartInfo(arguments[0]) { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = capture, RedirectStandardError = capture };
        foreach (var argument in arguments.Skip(1)) start.ArgumentList.Add(argument);
        if (environment is not null) foreach (var (name, value) in environment) start.Environment[name] = value;
        return BoundedCapture.Run(start,timeout,check).GetAwaiter().GetResult();
    }
}
internal sealed class ToolContext(string root, ICommandRunner? runner = null)
{
    public string Root { get; } = Path.GetFullPath(root);
    public ICommandRunner Runner { get; } = runner ?? new CommandRunner();
    public string At(string path) => Path.GetFullPath(path, Root);
    public CommandResult Run(string[] arguments, string? directory = null, IReadOnlyDictionary<string, string?>? environment = null, bool capture = false, bool check = true, TimeSpan? timeout = null) => Runner.Run(arguments, directory ?? Root, environment, capture, check, timeout);
    public static string[] Self => SelfInvocation.Prefix(Environment.ProcessPath, typeof(ToolContext).Assembly.Location);
    public void Tool(string name, params string[] arguments) => Run([.. Self, name, .. arguments]);
    public static string FindRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "SharpTestBench.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run inside a SharpTestBench checkout");
    }
}
internal static class StrictJson
{
    public static JsonDocument Parse(string text) => JsonInput.Parse(text);
    public static JsonDocument Read(string path) => Parse(JsonInput.Read(path));
    public static void Write(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var pending = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(pending, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
            File.Move(pending, path, true);
        }
        finally { File.Delete(pending); }
    }
}
internal sealed class Options
{
    readonly Dictionary<string, List<string>> values = new(StringComparer.Ordinal);
    public Options(IEnumerable<string> arguments, params string[] flags)
    {
        var switches = flags.ToHashSet(StringComparer.Ordinal);
        using var enumerator = arguments.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var name = enumerator.Current;
            if (!name.StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Expected an option: " + name);
            var value = "true";
            if (!switches.Contains(name))
            {
                if (!enumerator.MoveNext() || enumerator.Current.StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Missing value: " + name);
                value = enumerator.Current;
            }
            if (!values.TryGetValue(name, out var entries)) values.Add(name, entries = []);
            entries.Add(value);
        }
    }
    public string? Get(string name, string? fallback = null) => values.TryGetValue(name, out var entries) ? entries.Count == 1 ? entries[0] : throw new ArgumentException("Duplicate option: " + name) : fallback;
    public string Require(string name) => Get(name) ?? throw new ArgumentException("Missing option: " + name);
    public string[] All(string name) => values.TryGetValue(name, out var entries) ? entries.ToArray() : [];
    public bool Has(string name) => values.ContainsKey(name);
    public void Only(params string[] names)
    {
        var valid = names.ToHashSet(StringComparer.Ordinal);
        foreach (var name in values.Keys) if (!valid.Contains(name)) throw new ArgumentException("Unknown option: " + name);
    }
}

internal static class BoundedInput
{
    public static byte[] Read(Stream input, int maximum)
    {
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = input.Read(buffer)) != 0)
        {
            if (bytes.Length + count > maximum) throw new InvalidDataException("Input exceeds size limit");
            bytes.Write(buffer, 0, count);
        }
        return bytes.ToArray();
    }
}
