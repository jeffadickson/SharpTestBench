using System.Text.Json;
namespace SharpTestBench.Tools;
internal static class StageCommand
{
    static readonly string[] Names = ["Z_First[0]", "Z_First[1]", "A_Fail", "B_After", "A_Last"];
    internal sealed record Scenario(string Name, string[] Arguments, bool Fail, string[] Names, string[] Statuses);
    public static Scenario[] Scenarios => [
        new("order", [], false, Names, Enumerable.Repeat("passed", 5).ToArray()),
        new("continue", ["--stage-failure-policy", "continue"], true, Names, ["passed", "passed", "failed", "passed", "passed"]),
        new("stop", ["--stage-failure-policy", "stop-after-stage"], true, Names, ["passed", "passed", "failed", "passed", "skipped"]),
        new("selection", ["--stage", "-2", "--stage", "2"], true, ["Z_First[0]", "Z_First[1]", "A_Last"], ["passed", "passed", "passed"]),
        new("filter", ["--stage", "-2", "--filter", "[1]"], false, ["Z_First[1]"], ["passed"]),
        new("exact", ["--exact", "StageTests.Z_First[1]"], false, ["Z_First[1]"], ["passed"]),
        new("multiple", ["--exact", "Z_First[0]", "--exact", "A_Last"], false, ["Z_First[0]", "A_Last"], ["passed", "passed"]),
        new("empty", ["--stage", "99"], false, ["runtime"], ["failed"]), new("list", ["--list"], true, [], []) ];
    static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    public static void Validate(Scenario scenario, int exitCode, JsonElement rows)
    {
        Require(rows.EnumerateArray().Select(row => row.GetProperty("Name").GetString()).SequenceEqual(scenario.Names), "Stage test identities differ");
        Require(rows.EnumerateArray().Select(row => row.GetProperty("Status").GetString()).SequenceEqual(scenario.Statuses), "Stage outcomes differ");
        Require(exitCode == (scenario.Statuses.Contains("failed") ? 1 : 0), "Stage exit code differs");
        foreach (var row in rows.EnumerateArray())
        {
            var name = row.GetProperty("Name").GetString()!; var index = Array.IndexOf(Names, name); if (index < 0) continue;
            Require(row.GetProperty("Seed").GetUInt64() == (ulong)(100 + index) && row.GetProperty("QualifiedName").GetString() == "StageTests." + name && row.GetProperty("Stage").GetInt32() == new[] { -2, -2, 0, 0, 2 }[index], "Stage identity metadata differs");
        }
        if (scenario.Name == "stop") Require(rows[rows.GetArrayLength() - 1].GetProperty("Error").GetString() == "Earlier stage 0 failed", "Stage skip reason differs");
    }
    public static void Execute(ToolContext context, string[] arguments)
    {
        if (arguments.Length != 0) throw new ArgumentException("stage-tests accepts no arguments");
        context.Run(["dotnet", "build", "tests/StageIntegration", "-c", "Release", "--disable-build-servers", "-m:1"]);
        foreach (var scenario in Scenarios)
        {
            var label = "stage-" + scenario.Name; var events = context.At("build/" + label + "-events.jsonl");
            var environment = new Dictionary<string, string?> { ["CS_TEST_STAGES"] = null, ["CS_TEST_FILTER"] = null, ["CS_STAGE_FAILURE_POLICY"] = null, ["CS_LIST_TESTS"] = null, ["CS_TEST_NAMES"] = null, ["CS_STAGE_INJECT_FAILURE"] = scenario.Fail ? "1" : "0", ["CS_SEED"] = "100" };
            File.Delete(context.At("build/" + label + "-results.json")); File.Delete(events);
            var result = context.Run([.. ToolContext.Self, "run", "--no-build", "--assembly", context.At("tests/StageIntegration/bin/Release/net10.0/StageIntegration.dll"), "--label", label, "--log-json", events, .. scenario.Arguments], environment: environment, capture: true, check: false, timeout: TimeSpan.FromSeconds(120));
            File.WriteAllText(context.At("build/" + label + "-driver.log"), result.Output + result.Error);
            using var rows = StrictJson.Read(context.At("build/" + label + "-results.json")); Validate(scenario, result.ExitCode, rows.RootElement);
            if (scenario.Name == "list")
            {
                using var discovery = StrictJson.Read(context.At("build/" + label + "-results.discovery.json")); var root = discovery.RootElement;
                Require(root.GetProperty("SchemaVersion").GetInt32() == 1 && root.GetProperty("Tests").EnumerateArray().Select(row => row.GetProperty("Name").GetString()).SequenceEqual(Names) && root.GetProperty("Tests").EnumerateArray().Select(row => row.GetProperty("Seed").GetInt32()).SequenceEqual(Enumerable.Range(100, 5)), "Stage discovery differs");
            }
            if (scenario.Name == "stop")
            {
                var found = false;
                foreach (var line in File.ReadLines(events))
                {
                    using var document = StrictJson.Parse(line); var item = document.RootElement;
                    if (item.GetProperty("Kind").GetString() != "test-end" || item.GetProperty("Message").GetString() != "failed") continue;
                    Require(item.GetProperty("Test").GetString() == "StageTests.A_Fail" && item.GetProperty("Seed").GetInt32() == 102 && item.GetProperty("ExceptionType").GetString() == "System.FormatException" && item.GetProperty("ExceptionDetail").GetString()!.Contains("StageTests", StringComparison.Ordinal), "Stage failure event differs"); found = true; break;
                }
                Require(found, "Stage failure event missing");
            }
            Console.WriteLine("STAGE_PASS " + scenario.Name);
        }
    }
}
internal static class WorkspaceCommand
{
    public static void Execute(ToolContext context, string[] arguments)
    {
        if (arguments.Length != 0) throw new ArgumentException("workspace-tests accepts no arguments");
        var root = context.At("build/workspace-check"); Directory.CreateDirectory(root);
        string[] Command(string directory) => [.. ToolContext.Self, "run", "--sim", "icarus", "--no-build", "--work-dir", directory, "--label", "same-label", "--filter", "Cleanup_A_Run"];
        var tasks = new[] { (Name: "success", Mode: "normal"), (Name: "failure", Mode: "both") }.Select(item => Task.Run(() =>
        {
            var work = Path.Combine(root, item.Name); Directory.CreateDirectory(work); File.Delete(Path.Combine(work, "same-label-results.json"));
            var result = context.Run(Command(work), environment: new Dictionary<string, string?> { ["CS_CLEANUP_MODE"] = item.Mode }, capture: true, check: false, timeout: TimeSpan.FromSeconds(120));
            File.WriteAllText(Path.Combine(work, "driver.log"), result.Output + result.Error);
            using var rows = StrictJson.Read(Path.Combine(work, "same-label-results.json"));
            if (rows.RootElement.GetArrayLength() != 1 || rows.RootElement[0].GetProperty("Status").GetString() != (item.Name == "success" ? "passed" : "failed") || (result.ExitCode == 0) != (item.Name == "success") || !File.Exists(Path.Combine(work, "top.vvp")) || File.Exists(Path.Combine(work, ".sharptestbench-run.lock"))) throw new InvalidDataException("Workspace isolation failed: " + item.Name);
        })).ToArray(); Task.WhenAll(tasks).GetAwaiter().GetResult();
        var success = Path.Combine(root, "success");
        using (var occupied = new WorkspaceLock(success))
        {
            var result = context.Run(Command(success), capture: true, check: false, timeout: TimeSpan.FromSeconds(10));
            if (result.ExitCode == 0 || !result.Error.Contains("already in use", StringComparison.Ordinal)) throw new InvalidDataException("Occupied workspace was not rejected");
        }
        Console.WriteLine("WORKSPACE_PASS concurrent isolation and occupied-directory rejection");
    }
}
