using System.Text.Json;

namespace SharpTestBench;
public enum SimLogLevel { Debug, Info, Warning, Error }

internal static class SimulationLog
{
    const int TextLimit = 16384;
    static string? path;
    internal static void Initialize()
    {
        path = Environment.GetEnvironmentVariable("CS_LOG_JSON");
        if (string.IsNullOrWhiteSpace(path)) { path = null; return; }
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
    }

    internal static void Write(SimRuntime runtime, string kind, string test, ulong seed, int? stage,
        SimLogLevel level, string message, Exception? error = null,
        string? file = null, int? line = null, string? member = null)
    {
        runtime.CheckThread();
        if (path == null) return; // No serialization/snapshot allocation when disabled.
        bool truncated = false;
        string? Limit(string? text)
        {
            if (text == null || text.Length <= TextLimit) return text;
            truncated = true;
            return text[..TextLimit];
        }
        var job = SimJob.Current;
        // Store exception text rather than retaining exceptions or arbitrary user objects.
        var entry = new
        {
            SchemaVersion = 1, Kind = kind, TimeStep = runtime.Now,
            Test = Limit(test), Seed = seed, Stage = stage,
            TaskId = job?.Task.Id, TaskName = Limit(job?.Name),
            Level = level.ToString(), Message = Limit(message),
            ExceptionType = Limit(error?.GetType().FullName),
            ExceptionMessage = Limit(error?.Message), ExceptionDetail = Limit(error?.ToString()),
            SourceFile = Limit(file), SourceLine = line, SourceMember = Limit(member),
            Truncated = truncated
        };
        try { File.AppendAllText(path, JsonSerializer.Serialize(entry) + "\n"); }
        catch
        {
            path = null; // Avoid a second logging failure while reporting the first.
            throw;
        }
    }
}
