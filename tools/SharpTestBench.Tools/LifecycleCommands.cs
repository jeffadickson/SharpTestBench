using System.Globalization;
using System.Text.Json;
namespace SharpTestBench.Tools;
internal static class LifecycleCommands
{
    static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    static CommandResult Run(ToolContext context, string label, string[] arguments, Dictionary<string, string?> environment, int timeout)
    {
        File.Delete(context.At("build/" + label + "-results.json"));
        var result = context.Run(["dotnet", typeof(ToolContext).Assembly.Location, "run", "--no-build", "--label", label, .. arguments], environment: environment, capture: true, check: false, timeout: TimeSpan.FromSeconds(timeout));
        File.WriteAllText(context.At("build/" + label + "-driver.log"), result.Output + result.Error);
        return result;
    }
    public static void CleanupEvidence(string mode, int exitCode, JsonElement rows)
    {
        var expected = mode == "hang" ? new[] { "Cleanup_A_Run" } : new[] { "Cleanup_A_Run", "Cleanup_B_Verify" };
        Require(rows.ValueKind == JsonValueKind.Array && rows.EnumerateArray().Select(row => row.GetProperty("Name").GetString()).SequenceEqual(expected), "Cleanup test identities differ");
        Require(rows[0].GetProperty("Status").GetString() == (mode == "normal" ? "passed" : "failed") && (exitCode == 0) == (mode == "normal"), "Cleanup outcome differs");
        if (mode != "hang") Require(rows[1].GetProperty("Status").GetString() == "passed", "Cleanup verification failed");
        var error = rows[0].TryGetProperty("Error", out var diagnostic) ? diagnostic.GetString() ?? "" : "";
        string[] markers = mode switch { "normal" => [], "both" => ["body marker", "cleanup marker"], "timeout" => ["exceeded 10"], "cleanup-timeout" => ["cleanup timed out"], "cancel-error" => ["cancellation marker"], "hang" => ["regression aborted"], _ => throw new ArgumentException("Unknown cleanup mode") };
        Require(markers.All(marker => error.Contains(marker, StringComparison.Ordinal)), "Cleanup diagnostic differs");
    }
    public static bool LifecycleEvidence(int exitCode, string output, JsonElement rows, bool negative, bool early)
    {
        if (output.Contains("fatal library error", StringComparison.OrdinalIgnoreCase) || rows.ValueKind != JsonValueKind.Array) return false;
        if (early) return exitCode == 1 && rows.EnumerateArray().Any(row => row.GetProperty("Name").GetString() == "simulator-termination" && row.GetProperty("Status").GetString() == "failed");
        if (rows.GetArrayLength() != 1) return false;
        return negative ? exitCode == 1 && rows[0].GetProperty("Status").GetString() == "failed" && rows[0].GetProperty("Error").GetString()!.Contains("Test leaked tasks or callbacks", StringComparison.Ordinal) : exitCode == 0 && rows[0].GetProperty("Status").GetString() == "passed";
    }
    public static void Execute(ToolContext context, string command, string[] arguments)
    {
        var options = new Options(arguments); options.Only(command == "lifecycle" ? ["--repeat"] : []);
        Directory.CreateDirectory(context.At("build"));
        if (command == "cleanup-tests")
        {
            foreach (var mode in new[] { "normal", "both", "timeout", "cleanup-timeout", "cancel-error", "hang" })
            {
                var label = "cleanup-" + mode;
                var result = Run(context, label, ["--sim", "icarus", "--filter", "Cleanup_"], new() { ["CS_CLEANUP_MODE"] = mode }, 60);
                using var rows = StrictJson.Read(context.At("build/" + label + "-results.json")); CleanupEvidence(mode, result.ExitCode, rows.RootElement);
                Console.WriteLine("CLEANUP_PASS " + mode);
            }
        }
        else if (command == "lifecycle")
        {
            var repeat = int.Parse(options.Get("--repeat", "20")!, CultureInfo.InvariantCulture); if (repeat < 1 || repeat > int.MaxValue - 2) throw new ArgumentException("--repeat must be positive and bounded");
            var reports = new List<object>(); var failures = 0;
            for (int index = 0; index < repeat + 2; index++)
            {
                bool negative = index == repeat, early = index == repeat + 1;
                var label = "lifecycle-" + index.ToString("D3", CultureInfo.InvariantCulture);
                var result = Run(context, label, ["--filter", negative ? "Z_Leak" : "Y_SameTime"], new() { ["CS_INJECT_FAILURE"] = null, ["CS_INJECT_LEAK"] = negative ? "1" : null, ["CS_INJECT_EARLY"] = early ? "1" : null }, 180);
                var path = context.At("build/" + label + "-results.json");
                using var rows = File.Exists(path) ? StrictJson.Read(path) : StrictJson.Parse("[]");
                var output = result.Output + result.Error; var passed = LifecycleEvidence(result.ExitCode, output, rows.RootElement, negative, early);
                if (!passed) failures++;
                reports.Add(new { label, negative, early, exit_code = result.ExitCode, fatal_diagnostic = output.Contains("fatal library error", StringComparison.OrdinalIgnoreCase), passed });
            }
            StrictJson.Write(context.At("build/lifecycle-summary.json"), reports);
            Require(failures == 0, $"Lifecycle qualification failed: {failures}");
        }
        else if (command == "wave-tests")
        {
            const string label = "wave-failure";
            string[] selection = ["--exact", "P_ExitStatus"];
            var result = Run(context, label, [.. selection, "--waves", context.At("build/failure trace.vcd")], new() { ["CS_INJECT_FAILURE"] = "1" }, 120);
            Require(result.ExitCode == 1, "Expected waveform failure did not occur"); ValidationCommands.Waves(context, label, false);
            using (var rows = StrictJson.Read(context.At("build/" + label + "-results.json"))) Require(rows.RootElement.GetArrayLength() == 1 && rows.RootElement[0].GetProperty("Status").GetString() == "failed", "Waveform failure evidence differs");
            result = Run(context, label, selection, new() { ["CS_INJECT_FAILURE"] = null }, 120);
            File.WriteAllText(context.At("build/wave-disabled-driver.log"), result.Output + result.Error);
            Require(result.ExitCode == 0 && !File.Exists(context.At("build/" + label + "-artifacts.json")), "Disabled waveform retained stale artifacts");
            using var finalRows = StrictJson.Read(context.At("build/" + label + "-results.json"));
            Require(finalRows.RootElement.EnumerateArray().All(row => !row.TryGetProperty("Artifacts", out _)), "Disabled waveform retained result links");
            Console.WriteLine("WAVE_PASS failure retention and disabled capture");
        }
        else throw new ArgumentException("Unknown lifecycle command");
    }
}
