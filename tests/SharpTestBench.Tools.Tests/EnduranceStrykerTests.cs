using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

/// <summary>Stryker's selected outcomes are bound to exact production source and character spans, not its exit code alone.</summary>
public sealed class EnduranceStrykerTests
{
    [Fact]public void SourceLinkedGuardSpansAreCharacterIndices()
    {
        using var repo=new EnduranceTestRepository();string source=File.ReadAllText(Path.Combine(repo.Root,"tools/SharpTestBench.Tools/EnduranceRules.cs"));
        var range=Assert.Single(EnduranceStrykerEngine.Ranges(source,["finished"]));Assert.Contains("if (status",source[range.Start..range.End]);Assert.DoesNotContain("throw",source[range.Start..range.End]);
        var prepared=EnduranceStrykerEngine.Prepare(repo.Root,Path.Combine(repo.Root,"build/stryker"),["finished"]);var config=JsonNode.Parse(File.ReadAllText(prepared.Config))!;
        Assert.Equal(1,config["stryker-config"]!["concurrency"]!.GetValue<int>());Assert.Equal("SharpTestBench.Endurance.MutationSubject.csproj",config["stryker-config"]!["project"]!.GetValue<string>());
    }
    [Theory][InlineData("Killed","killed")][InlineData("Survived","survived")][InlineData("CompileError","invalid")][InlineData("RuntimeError","invalid")][InlineData("Timeout","timed-out")][InlineData("NoCoverage","no-coverage")][InlineData("Pending","pending")]
    public void RealStatusesRemainDistinct(string raw,string expected)
    {
        using var repo=new EnduranceTestRepository();string source=File.ReadAllText(Path.Combine(repo.Root,"tools/SharpTestBench.Tools/EnduranceRules.cs")),directory=Path.Combine(repo.Root,"build/stryker");Directory.CreateDirectory(directory);
        EnduranceStorage.WriteJson(Path.Combine(directory,"mutation-report.json"),EnduranceTestData.RawStryker(source,raw));
        var report=EnduranceStrykerEngine.Read(directory,source,["finished"]);Assert.Equal(expected,Assert.Single(report.Mutants).Outcome);Assert.Equal(raw=="Killed",!EnduranceStrykerEngine.HasFindings(report));
    }
    [Fact]public void UnselectedCompileErrorsAreNotCountedAsSelectedKillsOrFailures()
    {
        using var repo=new EnduranceTestRepository();string source=File.ReadAllText(Path.Combine(repo.Root,"tools/SharpTestBench.Tools/EnduranceRules.cs")),directory=Path.Combine(repo.Root,"build/stryker");Directory.CreateDirectory(directory);
        JsonNode selected=JsonSerializer.SerializeToNode(EnduranceTestData.RawStryker(source))!;
        JsonNode excluded=JsonSerializer.SerializeToNode(EnduranceTestData.RawStryker(source,"CompileError",0))!;
        var row=excluded["files"]!["EnduranceRules.cs"]!["mutants"]![0]!.DeepClone();row["id"]="1";selected["files"]!["EnduranceRules.cs"]!["mutants"]!.AsArray().Add(row);
        EnduranceStorage.WriteJson(Path.Combine(directory,"mutation-report.json"),selected);var report=EnduranceStrykerEngine.Read(directory,source,["finished"]);
        Assert.Equal("killed",Assert.Single(report.Mutants).Outcome);Assert.Equal(1,report.Excluded);Assert.Equal(1,report.ExcludedInvalid);
    }
    [Fact]public void StaleSourceAndUnknownOutcomesCannotQualify()
    {
        using var repo=new EnduranceTestRepository();string source=File.ReadAllText(Path.Combine(repo.Root,"tools/SharpTestBench.Tools/EnduranceRules.cs")),directory=Path.Combine(repo.Root,"build/stryker");Directory.CreateDirectory(directory);
        EnduranceStorage.WriteJson(Path.Combine(directory,"mutation-report.json"),EnduranceTestData.RawStryker(source));
        Assert.Throws<InvalidDataException>(()=>EnduranceStrykerEngine.Read(directory,source+"\n",["finished"]));
        EnduranceStorage.WriteJson(Path.Combine(directory,"mutation-report.json"),EnduranceTestData.RawStryker(source,"Success"));Assert.Throws<InvalidDataException>(()=>EnduranceStrykerEngine.Read(directory,source,["finished"]));
    }
}
