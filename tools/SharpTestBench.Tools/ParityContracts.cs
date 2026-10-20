using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace SharpTestBench.Tools;

internal static class ParityContracts
{
    public static readonly string[] SelectionVariables = ["CS_TEST_FILTER", "CS_TEST_NAMES", "CS_TEST_STAGES", "CS_LIST_TESTS", "CS_STAGE_FAILURE_POLICY", "LFSR_PLAN", "LFSR_REPORT", "LFSR_FAULT", "AXIS_MODE", "AXIS_PLAN", "AXIS_REPORT", "AXIS_FAULT", "AXIS_SIDEBANDS", "DSP_FAULT", "CPU_FAULT", "DSP_PLAN", "CPU_PLAN", "DSP_REPORT", "CPU_REPORT", "CS_INJECT_EARLY", "CS_WALL_TIMEOUT"];
    static string Identity(JsonElement row)
    {
        JsonElement value;
        if (!row.TryGetProperty("QualifiedName", out value) || value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.String && value.GetString() == "")
            if (!row.TryGetProperty("Name", out value)) throw new InvalidDataException("Each result needs a test identity");
        if (value.ValueKind != JsonValueKind.String) throw new InvalidDataException("Test identity must be text");
        var name = value.GetString() ?? "";
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Any(c => c < 32 || c == 127)) throw new InvalidDataException("Test identity contains whitespace padding or control characters");
        return name;
    }
    public static string[][] Signature(JsonElement rows)
    {
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() == 0) throw new InvalidDataException("Runner must produce a nonempty result array");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Each result must be an object");
            var name = Identity(row);
            if (!names.Add(name)) throw new InvalidDataException("Duplicate test identity: " + name);
            if (!row.TryGetProperty("Status", out var status) || status.ValueKind != JsonValueKind.String || status.GetString() != "passed") throw new InvalidDataException("Required test did not pass: " + name);
        }
        return names.Order(StringComparer.Ordinal).Select(name => new[] { name, "passed" }).ToArray();
    }
    public static HashSet<string> Expected(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0) throw new InvalidDataException("Expected identities must be a nonempty array");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) throw new InvalidDataException("Expected identity must be text");
            var name = item.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Any(c => c < 32 || c == 127) || !names.Add(name)) throw new InvalidDataException("Invalid or duplicate expected identity");
        }
        return names;
    }
    public static Dictionary<string, object> Compare(JsonElement legacy, JsonElement native, JsonElement expectedNames)
    {
        var expected = Expected(expectedNames); var a = Signature(legacy); var b = Signature(native);
        static Dictionary<string, ulong> Seeds(JsonElement rows) => rows.EnumerateArray().ToDictionary(Identity, row => FixtureContracts.Integer(row.GetProperty("Seed"), 0, ulong.MaxValue, "seed"), StringComparer.Ordinal);
        var left = Seeds(legacy); var right = Seeds(native);
        var missing = new { legacy = expected.Except(left.Keys).Order(StringComparer.Ordinal).ToArray(), native = expected.Except(right.Keys).Order(StringComparer.Ordinal).ToArray() };
        var unexpected = new { legacy = left.Keys.Except(expected).Order(StringComparer.Ordinal).ToArray(), native = right.Keys.Except(expected).Order(StringComparer.Ordinal).ToArray() };
        var different = left.Keys.Intersect(right.Keys).Where(name => left[name] != right[name]).Order(StringComparer.Ordinal).ToArray();
        return new() { ["schemaVersion"] = 1, ["legacy"] = a, ["native"] = b, ["expected"] = expected.Order(StringComparer.Ordinal).ToArray(), ["missing"] = missing, ["unexpected"] = unexpected, ["seedMismatches"] = different,
            ["matched"] = a.Select(row => row[0]).SequenceEqual(b.Select(row => row[0])) && missing.legacy.Length == 0 && missing.native.Length == 0 && unexpected.legacy.Length == 0 && unexpected.native.Length == 0 && different.Length == 0 };
    }
    public static bool FaultDetected(JsonElement rows, int exitCode, JsonElement expectedNames, string marker)
    {
        var expected = Expected(expectedNames);
        if (exitCode != 1 || rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() != expected.Count) return false;
        var observed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object) return false;
            string name; try { name = Identity(row); } catch (InvalidDataException) { return false; }
            if (!observed.Add(name) || !row.TryGetProperty("Status", out var status) || status.ValueKind != JsonValueKind.String || status.GetString() != "failed" || !row.TryGetProperty("Error", out var error) || error.ValueKind != JsonValueKind.String || !error.GetString()!.Contains(marker, StringComparison.Ordinal) || !row.TryGetProperty("Seed", out var seed) || seed.ValueKind != JsonValueKind.Number || !seed.TryGetUInt64(out _)) return false;
        }
        return observed.SetEquals(expected);
    }
    public static Dictionary<string, string?> CleanEnvironment(IReadOnlyDictionary<string, string?> environment) => environment.Where(pair => !SelectionVariables.Contains(pair.Key, StringComparer.Ordinal)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    public static IReadOnlyDictionary<string, string?> EnvironmentValues() => Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().ToDictionary(pair => (string)pair.Key, pair => (string?)pair.Value, StringComparer.Ordinal);
    public static void ClearCase(string work)
    {
        foreach (var directory in new[] { "legacy", "native" })
        {
            var folder = Path.Combine(work, directory);
            if (new DirectoryInfo(folder).LinkTarget is not null) throw new InvalidDataException("Parity output must not be a symbolic link");
            if (File.Exists(Path.Combine(folder, ".sharptestbench-run.lock"))) throw new IOException("Child workspace is already in use");
        }
        foreach (var name in new[] { "parity.json", "negative-control.json", "case-failure.json", "manifest.json", "expected.json", "plan.json", "build.log", "legacy.log", "native.log" }) File.Delete(Path.Combine(work, name));
        foreach (var directory in new[] { "legacy", "native" })
        {
            var folder = Path.Combine(work, directory); Directory.CreateDirectory(folder);
            foreach (var name in new[] { "metrics.json", "legacy-results.json", "legacy-results.xml", "icarus-results.json", "run-manifest.json", "failure-manifest.json", "input-manifest.json" }) File.Delete(Path.Combine(folder, name));
        }
    }
    public static JsonElement Metrics(string fixture, JsonElement data)
    {
        string[] keys = fixture switch { "dsp" => ["trace", "cycles", "accepted", "consumed", "stalls"], "cpu" => ["trace", "cycles", "commits", "memoryStalls", "pauses", "memory"], _ => throw new InvalidDataException("No workload metrics contract") };
        if (data.ValueKind != JsonValueKind.Object || keys.Any(key => !data.TryGetProperty(key, out _))) throw new InvalidDataException("Missing workload metric fields");
        static bool Integer(JsonElement value) => value.ValueKind == JsonValueKind.Number && System.Text.RegularExpressions.Regex.IsMatch(value.GetRawText(), "^-?[0-9]+$");
        var trace = data.GetProperty("trace");
        if (trace.ValueKind != JsonValueKind.Array || trace.GetArrayLength() == 0 || trace.EnumerateArray().Any(row => row.ValueKind != JsonValueKind.Array || row.GetArrayLength() == 0 || row.EnumerateArray().Any(value => !Integer(value)))) throw new InvalidDataException("Workload trace must contain nonempty integer rows");
        foreach (var key in keys.Where(key => key is not ("trace" or "memory")))
            if (!Integer(data.GetProperty(key)) || BigInteger.Parse(data.GetProperty(key).GetRawText(), CultureInfo.InvariantCulture) < (key == "cycles" ? 1 : 0)) throw new InvalidDataException("Invalid workload counter: " + key);
        if (fixture == "cpu")
        {
            var memory = data.GetProperty("memory");
            if (memory.ValueKind != JsonValueKind.Array || memory.GetArrayLength() == 0 || memory.EnumerateArray().Any(value => !Integer(value) || !value.TryGetUInt32(out _))) throw new InvalidDataException("CPU memory must contain unsigned 32-bit words");
        }
        return JsonSerializer.SerializeToElement(keys.ToDictionary(key => key, data.GetProperty));
    }
    public static void Execute(string[] arguments)
    {
        var options = new Options(arguments); options.Only("--legacy", "--native", "--expected", "--report");
        var path = Path.GetFullPath(options.Require("--report"));
        var inputs = new[] { "--legacy", "--native", "--expected" }.Select(key => Path.GetFullPath(options.Require(key))).ToArray();
        if (inputs.Contains(path, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)) throw new InvalidDataException("Parity report must not overwrite an input");
        Dictionary<string, object> report;
        try { using var a = StrictJson.Read(inputs[0]); using var b = StrictJson.Read(inputs[1]); using var expected = StrictJson.Read(inputs[2]); report = Compare(a.RootElement, b.RootElement, expected.RootElement); }
        catch (Exception error) when (error is InvalidDataException or IOException or JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException) { report = new() { ["schemaVersion"] = 1, ["matched"] = false, ["error"] = error.Message }; }
        StrictJson.Write(path, report); Console.WriteLine(JsonSerializer.Serialize(report));
        if (!(bool)report["matched"]) throw new InvalidDataException("Runner parity qualification failed");
    }
}
