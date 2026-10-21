using SharpTestBench;
using Xunit;
using TestResult = SharpTestBench.TestResult;

public sealed class ResultPublicationTests
{
    sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "stb-report-" + Guid.NewGuid().ToString("N"));
        public string Output => Path.Combine(Root, "results.json");
        public int Finished { get; private set; }
        public Fixture() => Directory.CreateDirectory(Root);
        public void Publish(IReadOnlyList<object>? coverage = null, Action? finish = null) =>
            ResultPublication.Complete(Output, new[] { new TestResult("test", "passed", 17, 0, 1, 0.1, null) },
                Array.Empty<TestDescriptor>(), coverage ?? Array.Empty<object>(), false, finish ?? (() => Finished++));
        public void Dispose()
        {
            if (Path.GetDirectoryName(Path.GetFullPath(Root)) != Path.TrimEndingDirectorySeparator(Path.GetTempPath())) throw new InvalidOperationException("Invalid temporary report root");
            Directory.Delete(Root, true);
        }
    }
    [Fact] public void FailedArtifactWriteStillRequestsSimulatorCompletion()
    {
        using var f = new Fixture(); Directory.CreateDirectory(Path.ChangeExtension(f.Output, ".discovery.json"));
        var error = Record.Exception(() => f.Publish());
        Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
        Assert.Equal(1, f.Finished); Assert.False(File.Exists(f.Output));
    }
    [Fact] public void PreparationFailurePreservesAllPreviouslyCompletedReports()
    {
        using var f = new Fixture(); f.Publish();
        var before = Directory.GetFiles(f.Root).ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes);
        Assert.Throws<ArgumentException>(() => f.Publish(new object[] { double.NaN }));
        Assert.Equal(2, f.Finished);
        foreach (var pair in before) Assert.Equal(pair.Value, File.ReadAllBytes(Path.Combine(f.Root, pair.Key!)));
        Assert.Equal(before.Count, Directory.GetFiles(f.Root).Length);
    }

    [Fact] public void FailedFinalReplacementPreservesDestinationAndUnrelatedFiles()
    {
        using var f = new Fixture(); f.Publish();
        var xml = Path.ChangeExtension(f.Output, "xml"); File.Delete(xml); Directory.CreateDirectory(xml);
        var unrelated = Path.Combine(f.Root, "unrelated.tmp"); File.WriteAllText(unrelated, "keep");
        var error = Record.Exception(() => f.Publish());
        Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
        Assert.True(Directory.Exists(xml)); Assert.Equal("keep", File.ReadAllText(unrelated));
        Assert.Equal(new[] { unrelated }, Directory.GetFiles(f.Root, "*.tmp")); Assert.Equal(2, f.Finished);
    }

    [Fact] public void JsonDestinationCannotOverwriteItsOwnXmlSidecar()
    {
        using var f = new Fixture(); var output = Path.Combine(f.Root, "results.xml"); File.WriteAllText(output, "previous evidence");
        int finished = 0;
        Assert.Throws<InvalidDataException>(() => ResultPublication.Complete(output, Array.Empty<TestResult>(), Array.Empty<TestDescriptor>(), Array.Empty<object>(), false, () => finished++));
        Assert.Equal(1, finished); Assert.Equal("previous evidence", File.ReadAllText(output));
        Assert.Single(Directory.GetFiles(f.Root));
    }

    [Fact] public void DiscoveryOnlyPublicationDoesNotRequireNativeRuntimeCompletion()
    {
        using var f = new Fixture();
        var descriptor = new TestDescriptor("Read", "Namespace.Tests.Read", 2, 3, ulong.MaxValue, 100, false, new[] { "icarus" });
        ResultPublication.Complete(f.Output, Array.Empty<TestResult>(), new[] { descriptor }, Array.Empty<object>(), true, null);
        using var discovery = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(f.Output, ".discovery.json")));
        var row = discovery.RootElement.GetProperty("Tests")[0];
        Assert.Equal("Namespace.Tests.Read", row.GetProperty("QualifiedName").GetString());
        Assert.Equal(ulong.MaxValue, row.GetProperty("Seed").GetUInt64());
        Assert.Equal(3, row.GetProperty("CaseIndex").GetInt32());
        Assert.Equal("[]", File.ReadAllText(f.Output));
        Assert.Equal("0", System.Xml.Linq.XDocument.Load(Path.ChangeExtension(f.Output, "xml")).Root!.Attribute("tests")!.Value);
    }

    [Fact] public void PromotedPrimaryIsRetiredAfterLaterCoveragePublicationFails()
    {
        using var f = new Fixture(); f.Publish();
        var xml = Path.ChangeExtension(f.Output, "xml"); var previousXml = File.ReadAllBytes(xml);
        var coverage = Path.ChangeExtension(f.Output, ".coverage.json"); File.Delete(coverage); Directory.CreateDirectory(coverage);
        var error = Record.Exception(() => f.Publish());
        Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
        Assert.False(File.Exists(f.Output)); Assert.True(Directory.Exists(coverage));
        Assert.Equal(previousXml, File.ReadAllBytes(xml)); Assert.Equal(2, f.Finished);
        Assert.Empty(Directory.GetFiles(f.Root, "*.tmp"));
    }
    [Fact] public void SimulatorCompletionObservesAllFullyPublishedArtifacts()
    {
        using var f = new Fixture(); int completed = 0;
        f.Publish(finish: () => {
            Assert.Equal(4, Directory.GetFiles(f.Root).Length);
            using var rows = System.Text.Json.JsonDocument.Parse(File.ReadAllText(f.Output));
            Assert.Equal("passed", rows.RootElement[0].GetProperty("Status").GetString());
            using var coverage = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(f.Output, ".coverage.json")));
            Assert.Equal("functional", coverage.RootElement.GetProperty("Kind").GetString());
            using var discovery = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(f.Output, ".discovery.json")));
            Assert.Equal(1, discovery.RootElement.GetProperty("SchemaVersion").GetInt32());
            Assert.Equal("1", System.Xml.Linq.XDocument.Load(Path.ChangeExtension(f.Output, "xml")).Root!.Attribute("tests")!.Value);
            completed++;
        });
        Assert.Equal(1, completed); Assert.Empty(Directory.GetFiles(f.Root, "*.tmp"));
    }

}
