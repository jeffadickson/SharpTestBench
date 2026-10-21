using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

public sealed class FifoFixtureTests
{
    sealed class SourceFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "stb-fifo-source-" + Guid.NewGuid().ToString("N"));
        public string Folder => Path.Combine(Root, "tests", "Fixtures", "verilog-fifo");
        public SourceFixture()
        {
            Directory.CreateDirectory(Folder);
            foreach (var name in new[] { "axis_fifo.v", "COPYING", "provenance.json" })
                File.Copy(Path.Combine(ToolContext.FindRoot(), "tests", "Fixtures", "verilog-fifo", name), Path.Combine(Folder, name));
        }
        public void Dispose()
        {
            if (Path.GetDirectoryName(Path.GetFullPath(Root)) != Path.TrimEndingDirectorySeparator(Path.GetTempPath())) throw new InvalidOperationException("Invalid temporary FIFO root");
            Directory.Delete(Root, true);
        }
    }
    [Fact] public void PinnedFifoSourceAndLicenseAreBoundToReviewedRevision()
    {
        var meta = FixtureContracts.VerifySources(ToolContext.FindRoot(), "fifo");
        Assert.Equal("48ff7a7e2ef782cf778d47910cf85835c64b1bce", meta.GetProperty("revision").GetString());
        Assert.Equal("aefddc67fc3552d919280424606fc6b048e61d7df9ee7ee0f8801c082c1cfc39", meta.GetProperty("files").GetProperty("axis_fifo.v").GetProperty("sha256").GetString());
    }
    [Fact] public void ChangedFifoSourceCannotQualify()
    {
        using var f = new SourceFixture(); File.AppendAllText(Path.Combine(f.Folder, "axis_fifo.v"), "\n// altered source\n");
        Assert.Throws<InvalidDataException>(() => FixtureContracts.VerifySources(f.Root, "fifo"));
    }
    [Fact] public void AlternateRepositoryCannotReuseTheFifoHashes()
    {
        using var f = new SourceFixture(); var path = Path.Combine(f.Folder, "provenance.json");
        var meta = JsonNode.Parse(File.ReadAllText(path))!; meta["repository"] = "https://example.org/unreviewed"; File.WriteAllText(path, meta.ToJsonString());
        Assert.Throws<InvalidDataException>(() => FixtureContracts.VerifySources(f.Root, "fifo"));
    }
    [Fact] public void FifoConfigurationSpaceHasDistinctStableTopIdentities()
    {
        var names = new HashSet<string>();
        foreach (int depth in new[] { 2, 4, 8, 16, 32, 64 })
        foreach (int pipeline in new[] { 0, 1, 2, 3 })
        foreach (bool output in new[] { false, true })
        foreach (bool sidebands in new[] { false, true })
        {
            var config = FifoFixture.Configuration(depth, pipeline, output, sidebands);
            Assert.True(names.Add(config.Top)); Assert.Matches("^[a-z][a-z0-9_]+$", config.Top);
            Assert.Equal(config, FifoFixture.Configuration(depth, pipeline, output, sidebands));
        }
        Assert.Equal(96, names.Count);
    }
    [Fact] public void UnsupportedFifoDepthAndPipelineRejectBeforeCompilation()
    {
        foreach (int depth in new[] { int.MinValue, -1, 0, 1, 3, 65, int.MaxValue })
            Assert.Throws<InvalidDataException>(() => FifoFixture.Configuration(depth, 1, false, true));
        foreach (int pipeline in new[] { int.MinValue, -1, 4, int.MaxValue })
            Assert.Throws<InvalidDataException>(() => FifoFixture.Configuration(8, pipeline, false, true));
    }

    [Fact] public void FifoPlansDrainBeyondRamAndOutputPipelineCapacity()
    {
        foreach (int depth in new[] { 2, 64 })
        foreach (bool output in new[] { false, true })
        {
            var config = FifoFixture.Configuration(depth, 3, output, true);
            var rows = FifoFixture.Plan(config, ulong.MaxValue, count: 2, stall: 32, resetAt: null);
            Assert.Equal(rows, FifoFixture.ValidatePlan(config, JsonSerializer.SerializeToElement(rows)));
            Assert.True(rows[0].Reset);
            Assert.All(rows.TakeLast(FifoFixture.DrainCycles(config)), row => { Assert.False(row.Reset); Assert.False(row.Offer); Assert.True(row.Ready); });
        }
    }
    [Fact] public void RegisterSizedDrainCannotQualifyADeepFifo()
    {
        var config = FifoFixture.Configuration(64, 3, true, true);
        var shortDrain = JsonSerializer.SerializeToElement(AxisFixture.Plan(17, 128, 7, 64));
        Assert.Throws<InvalidDataException>(() => FifoFixture.ValidatePlan(config, shortDrain));
    }
    [Fact] public void InvalidFifoStimulusBudgetsRejectBeforeNativeExecution()
    {
        var config = FifoFixture.Configuration(8, 1, false, true);
        Assert.Throws<InvalidDataException>(() => FifoFixture.Plan(config, 1, count: 0));
        Assert.Throws<InvalidDataException>(() => FifoFixture.Plan(config, 1, count: 2049));
        Assert.Throws<InvalidDataException>(() => FifoFixture.Plan(config, 1, stall: 33));
        Assert.Throws<InvalidDataException>(() => FifoFixture.Plan(config, 1, resetAt: 128));
    }

    static (FifoConfiguration Config, JsonElement Plan, JsonElement Metrics) Sample(bool sidebands = true, int count = 1)
    {
        var config = FifoFixture.Configuration(2, 0, false, sidebands);
        var plan = FifoFixture.Plan(config, 17, count, 0, null);
        var word = plan[1];
        var trace = plan.Select((row, index) => new[] {
            index, row.Reset ? 1 : 0, index == 1 ? 1 : 0, index == 2 ? 1 : 0, row.Ready ? 1 : 0,
            index == 2 ? word.Data : 0, index == 2 ? sidebands ? word.Keep : 3 : 0,
            index == 2 ? sidebands ? word.Last ? 1 : 0 : 1 : 0, index == 2 && sidebands ? word.Id : 0,
            index == 2 && sidebands ? word.Dest : 0, index == 2 && sidebands ? word.User : 0 }).ToArray();
        return (config, JsonSerializer.SerializeToElement(plan), JsonSerializer.SerializeToElement(new {
            trace, accepted = 1, consumed = 1, flushed = 0, stalls = 0, resets = 1, cycles = plan.Length
        }));
    }
    [Fact] public void FifoTraceProjectsEnabledAndDisabledSidebandsFromAcceptedStimulus()
    {
        foreach (bool sidebands in new[] { false, true })
        {
            var s = Sample(sidebands); Assert.True(JsonElement.DeepEquals(s.Metrics, FifoFixture.Metrics(s.Config, s.Metrics, s.Plan)));
        }
    }
    [Fact] public void AgreementOnTheSameWrongFifoPayloadCannotQualify()
    {
        var s = Sample(); var data = JsonNode.Parse(s.Metrics.GetRawText())!;
        data["trace"]![2]![5] = s.Metrics.GetProperty("trace")[2][5].GetInt32() ^ 1;
        var corrupted = JsonSerializer.SerializeToElement(data);
        AxisFixture.Metrics(corrupted); // Numeric conservation alone still passes.
        Assert.Throws<InvalidDataException>(() => FifoFixture.Metrics(s.Config, corrupted, s.Plan));
    }
    [Fact] public void FifoControlsAreBoundToTheRequestedPlanRatherThanOnlyEachOther()
    {
        var s = Sample(); var plan = JsonNode.Parse(s.Plan.GetRawText())!; plan[1]!["Ready"] = false;
        var error = Assert.Throws<InvalidDataException>(() => FifoFixture.Metrics(s.Config, s.Metrics, JsonSerializer.SerializeToElement(plan)));
        Assert.Contains("control differs", error.Message);
    }
    [Fact] public void PhantomFifoAcceptanceIsRejectedEvenWithConsistentCounters()
    {
        var s = Sample(count: 2); var plan = JsonNode.Parse(s.Plan.GetRawText())!; plan[1]!["Offer"] = false;
        var error = Assert.Throws<InvalidDataException>(() => FifoFixture.Metrics(s.Config, s.Metrics, JsonSerializer.SerializeToElement(plan)));
        Assert.Contains("source did not offer", error.Message);
    }
}
