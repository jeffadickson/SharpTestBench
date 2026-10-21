using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

public sealed class ProvenanceFailureTests
{
    sealed class Fixture : IDisposable, ICommandRunner
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "stb-provenance200-" + Guid.NewGuid().ToString("N"));
        public string Kind { get; }
        public string Folder => Path.Combine(Root, "tests/Fixtures/verilog-" + Kind);
        public string Source => Path.Combine(Folder, Kind switch { "priority" => "priority_encoder.v", "axis" => "axis_register.v", _ => "lfsr.v" });
        public string Work => Path.Combine(Root, "build", Kind switch {
            "priority" => "priority-83",
            "axis" => $"axis-{AxisFixture.Configuration(2, true)}-83-64-3-32",
            _ => $"lfsr-{LfsrFixture.Fixture("crc32", "REDUCTION").Top}-83-fault0" });
        public int Calls { get; private set; }
        public Fixture(string kind)
        {
            Kind = kind;
            Directory.CreateDirectory(Folder);
            foreach (var path in Directory.EnumerateFiles(Path.Combine(ToolContext.FindRoot(), "tests/Fixtures/verilog-" + kind)))
                File.Copy(path, Path.Combine(Folder, Path.GetFileName(path)));
            Directory.CreateDirectory(Work);
            File.WriteAllText(Path.Combine(Work, "unrelated.keep"), "preserve");
        }
        public void Corrupt(string mode)
        {
            var inventory = Path.Combine(Folder, "provenance.json");
            switch (mode)
            {
                case "hash": File.AppendAllText(Source, "\n// modified clone\n"); break;
                case "license": case "revision":
                    var data = JsonNode.Parse(File.ReadAllText(inventory))!;
                    data[mode] = "incorrect"; File.WriteAllText(inventory, data.ToJsonString()); break;
                case "missing-copying": File.Delete(Path.Combine(Folder, "COPYING")); break;
                case "missing-inventory": File.Delete(inventory); break;
                case "bad-json": File.WriteAllText(inventory, "{"); break;
                default: throw new ArgumentException(mode);
            }
        }
        public void Call()
        {
            var context = new ToolContext(Root, this);
            if (Kind == "priority") ParityCiCommand.Priority(context, 83);
            else if (Kind == "axis") ParityCiCommand.Axis(context, 2, 83);
            else ParityCiCommand.Lfsr(context, "crc32", "REDUCTION", 83);
        }
        public CommandResult Run(IReadOnlyList<string> arguments, string directory, IReadOnlyDictionary<string, string?>? environment = null, bool capture = false, bool check = true, TimeSpan? timeout = null)
        {
            Calls++; throw new InvalidOperationException("No child may launch with invalid provenance");
        }
        public void Dispose()
        {
            var full = Path.GetFullPath(Root);
            if (Path.GetDirectoryName(full) != Path.TrimEndingDirectorySeparator(Path.GetTempPath())) throw new InvalidOperationException("Temporary root escaped parent");
            Directory.Delete(full, true);
        }
    }
    static void Check(string kind, string artifact = "parity.json", string mode = "hash")
    {
        using var fixture = new Fixture(kind);
        var stale = Path.Combine(fixture.Work, artifact);
        Directory.CreateDirectory(Path.GetDirectoryName(stale)!);
        const string old = "{\"schemaVersion\":1,\"matched\":true}";
        File.WriteAllText(stale, old);
        fixture.Corrupt(mode);
        var error = Record.Exception(fixture.Call);
        Assert.NotNull(error);
        if (mode is "hash" or "license" or "revision") Assert.IsType<InvalidDataException>(error);
        else if (mode == "bad-json") Assert.IsAssignableFrom<JsonException>(error);
        else Assert.IsAssignableFrom<IOException>(error);
        Assert.Equal(0, fixture.Calls);
        Assert.False(File.Exists(Path.Combine(fixture.Work, "parity.json")));
        if (artifact != "case-failure.json") Assert.False(File.Exists(stale));
        using var failure = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Work, "case-failure.json")));
        Assert.Equal(1, failure.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("failed", failure.RootElement.GetProperty("status").GetString());
        Assert.Equal(error.GetType().Name, failure.RootElement.GetProperty("kind").GetString());
        Assert.Equal(error.Message, failure.RootElement.GetProperty("error").GetString());
        Assert.False(failure.RootElement.TryGetProperty("matched", out _));
        Assert.False(File.Exists(Path.Combine(fixture.Work, ".parity.lock")));
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(fixture.Work, "unrelated.keep")));
    }
    static void OwnerProtected()
    {
        using var fixture = new Fixture("priority");
        var previous = Path.Combine(fixture.Work, "parity.json");
        File.WriteAllText(previous, "owner evidence");
        fixture.Corrupt("hash");
        using var owner = new FileStream(Path.Combine(fixture.Work, ".parity.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var error = Assert.Throws<IOException>(fixture.Call);
        Assert.Contains("already in use", error.Message);
        Assert.Equal("owner evidence", File.ReadAllText(previous));
        Assert.False(File.Exists(Path.Combine(fixture.Work, "case-failure.json")));
        Assert.Equal(0, fixture.Calls);
    }
    [Fact] public void RetirePriorityArtifact0() => Check("priority", "parity.json");
    [Fact] public void RetirePriorityArtifact1() => Check("priority", "negative-control.json");
    [Fact] public void RetirePriorityArtifact2() => Check("priority", "case-failure.json");
    [Fact] public void RetirePriorityArtifact3() => Check("priority", "manifest.json");
    [Fact] public void RetirePriorityArtifact4() => Check("priority", "expected.json");
    [Fact] public void RetirePriorityArtifact5() => Check("priority", "plan.json");
    [Fact] public void RetirePriorityArtifact6() => Check("priority", "build.log");
    [Fact] public void RetirePriorityArtifact7() => Check("priority", "legacy.log");
    [Fact] public void RetirePriorityArtifact8() => Check("priority", "native.log");
    [Fact] public void RetirePriorityArtifact9() => Check("priority", "legacy/legacy-results.json");
    [Fact] public void RetireAxisArtifact0() => Check("axis", "parity.json");
    [Fact] public void RetireAxisArtifact1() => Check("axis", "negative-control.json");
    [Fact] public void RetireAxisArtifact2() => Check("axis", "case-failure.json");
    [Fact] public void RetireAxisArtifact3() => Check("axis", "manifest.json");
    [Fact] public void RetireAxisArtifact4() => Check("axis", "expected.json");
    [Fact] public void RetireAxisArtifact5() => Check("axis", "plan.json");
    [Fact] public void RetireAxisArtifact6() => Check("axis", "build.log");
    [Fact] public void RetireAxisArtifact7() => Check("axis", "legacy.log");
    [Fact] public void RetireAxisArtifact8() => Check("axis", "native.log");
    [Fact] public void RetireAxisArtifact9() => Check("axis", "legacy/legacy-results.json");
    [Fact] public void RetireLfsrArtifact0() => Check("lfsr", "parity.json");
    [Fact] public void RetireLfsrArtifact1() => Check("lfsr", "negative-control.json");
    [Fact] public void RetireLfsrArtifact2() => Check("lfsr", "case-failure.json");
    [Fact] public void RetireLfsrArtifact3() => Check("lfsr", "manifest.json");
    [Fact] public void RetireLfsrArtifact4() => Check("lfsr", "expected.json");
    [Fact] public void RetireLfsrArtifact5() => Check("lfsr", "plan.json");
    [Fact] public void RetireLfsrArtifact6() => Check("lfsr", "build.log");
    [Fact] public void RetireLfsrArtifact7() => Check("lfsr", "legacy.log");
    [Fact] public void RetireLfsrArtifact8() => Check("lfsr", "native.log");
    [Fact] public void RetireLfsrArtifact9() => Check("lfsr", "legacy/legacy-results.json");
    [Fact] public void RecordPriorityProvenanceFailure0() => Check("priority", "native/icarus-results.json", "hash");
    [Fact] public void RecordPriorityProvenanceFailure1() => Check("priority", "native/icarus-results.json", "license");
    [Fact] public void RecordPriorityProvenanceFailure2() => Check("priority", "native/icarus-results.json", "revision");
    [Fact] public void RecordPriorityProvenanceFailure3() => Check("priority", "native/icarus-results.json", "missing-copying");
    [Fact] public void RecordPriorityProvenanceFailure4() => Check("priority", "native/icarus-results.json", "missing-inventory");
    [Fact] public void RecordPriorityProvenanceFailure5() => Check("priority", "native/icarus-results.json", "bad-json");
    [Fact] public void RecordAxisProvenanceFailure0() => Check("axis", "native/icarus-results.json", "hash");
    [Fact] public void RecordAxisProvenanceFailure1() => Check("axis", "native/icarus-results.json", "license");
    [Fact] public void RecordAxisProvenanceFailure2() => Check("axis", "native/icarus-results.json", "revision");
    [Fact] public void RecordAxisProvenanceFailure3() => Check("axis", "native/icarus-results.json", "missing-copying");
    [Fact] public void RecordAxisProvenanceFailure4() => Check("axis", "native/icarus-results.json", "missing-inventory");
    [Fact] public void RecordAxisProvenanceFailure5() => Check("axis", "native/icarus-results.json", "bad-json");
    [Fact] public void RecordLfsrProvenanceFailure0() => Check("lfsr", "native/icarus-results.json", "hash");
    [Fact] public void RecordLfsrProvenanceFailure1() => Check("lfsr", "native/icarus-results.json", "license");
    [Fact] public void RecordLfsrProvenanceFailure2() => Check("lfsr", "native/icarus-results.json", "revision");
    [Fact] public void RecordLfsrProvenanceFailure3() => Check("lfsr", "native/icarus-results.json", "missing-copying");
    [Fact] public void RecordLfsrProvenanceFailure4() => Check("lfsr", "native/icarus-results.json", "missing-inventory");
    [Fact] public void RecordLfsrProvenanceFailure5() => Check("lfsr", "native/icarus-results.json", "bad-json");
    [Fact] public void ProtectActiveOwnerBeforeProvenanceCheck() => OwnerProtected();
}
