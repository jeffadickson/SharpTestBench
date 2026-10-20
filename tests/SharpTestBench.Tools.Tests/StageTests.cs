using System.Text.Json;
using SharpTestBench.Tools;
using Xunit;
public sealed class StageTests
{
    public static TheoryData<string> Cases => new(StageCommand.Scenarios.Select(scenario => scenario.Name));
    [Theory]
    [MemberData(nameof(Cases))]
    public void SelectedRowsRetainDiscoverySeedsAndStages(string name)
    {
        var scenario = StageCommand.Scenarios.Single(item => item.Name == name);
        string[] all = ["Z_First[0]", "Z_First[1]", "A_Fail", "B_After", "A_Last"];
        var rows = scenario.Names.Select((item, index) => new { Name = item, Status = scenario.Statuses[index], Seed = 100 + Array.IndexOf(all, item), QualifiedName = "StageTests." + item, Stage = Array.IndexOf(all, item) < 0 ? 0 : new[] { -2, -2, 0, 0, 2 }[Array.IndexOf(all, item)], Error = name == "stop" && item == "A_Last" ? "Earlier stage 0 failed" : "" }).ToArray();
        using var evidence = JsonDocument.Parse(JsonSerializer.Serialize(rows));
        var exitCode = scenario.Statuses.Contains("failed") ? 1 : 0;
        StageCommand.Validate(scenario, exitCode, evidence.RootElement);
        Assert.Throws<InvalidDataException>(() => StageCommand.Validate(scenario, 1 - exitCode, evidence.RootElement));
        if (rows.Length > 0)
        {
            using var corrupted = JsonDocument.Parse(JsonSerializer.Serialize(rows.Select(row => new { row.Name, row.Status, Seed = 999, row.QualifiedName, row.Stage, row.Error })));
            if (name != "empty") Assert.Throws<InvalidDataException>(() => StageCommand.Validate(scenario, exitCode, corrupted.RootElement));
        }
    }
}
