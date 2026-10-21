using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

public sealed class AuditRequirementTests
{
    sealed class Fixture : IDisposable
    {
        readonly string parent = Path.TrimEndingDirectorySeparator(Path.GetTempPath());
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "stb-requirement-audit-" + Guid.NewGuid().ToString("N"));
        public string Repository => Path.Combine(Root, "repository");
        public string Source => Path.Combine(Root, "upstream");
        public string Package { get; }
        public string Reviews => Path.Combine(Repository, "docs/API_REVIEWS.json");
        public Fixture(bool installed = false)
        {
            Package = Path.Combine(Source, installed ? "cocotb" : "src/cocotb");
            Directory.CreateDirectory(Package);
            Directory.CreateDirectory(Path.Combine(Repository, "docs"));
            Directory.CreateDirectory(Path.Combine(Repository, "third_party"));
            Directory.CreateDirectory(Path.Combine(Repository, "src"));
            const string code = "class Thing:\n    pass\n";
            File.WriteAllText(Path.Combine(Package, "sample.py"), code);
            File.WriteAllText(Path.Combine(Package, "_base_triggers.py"), "# fixture\n");
            File.WriteAllText(Path.Combine(Repository, "src/Evidence.cs"), "required-anchor");
            File.WriteAllText(Path.Combine(Repository, "docs/UPSTREAM_API_INVENTORY.csv"), "Module,Definition,SourceLine,ExactSemanticParity\ncocotb.sample,Thing,1,review-required\n");
            string revision = new('a', 40);
            File.WriteAllText(Path.Combine(Repository, "third_party/UPSTREAM.json"), JsonSerializer.Serialize(new { commit = revision }));
            File.WriteAllText(Reviews, JsonSerializer.Serialize(new {
                schema_version = 1, source_revision = revision,
                source_hashes = new Dictionary<string, string> { ["cocotb.sample"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))).ToLowerInvariant() },
                reviews = new[] { new { module = "cocotb.sample", definition = "Thing", source_line = 1, classification = "internal-adapter", reason = "fixture", evidence = new[] { new { path = "src/Evidence.cs", contains = "required-anchor" } } } }
            }));
        }
        public void Change(Action<JsonNode> change)
        {
            var data = JsonNode.Parse(File.ReadAllText(Reviews))!; change(data); File.WriteAllText(Reviews, data.ToJsonString());
        }
        public void Verify(string? source = null) => AuditCommand.Validate(new ToolContext(Repository), source ?? Source);
        public void Dispose()
        {
            if (Path.GetDirectoryName(Path.GetFullPath(Root)) != parent) throw new InvalidOperationException("Temporary audit root escaped its parent");
            Directory.Delete(Root, true);
        }
    }
    [Fact] public void SourceCheckoutLayoutPreservesHashValidation() { using var f = new Fixture(); f.Verify(); }
    [Fact] public void InstalledContainerReusesExistingPinnedSource() { using var f = new Fixture(true); f.Verify(); }
    [Fact] public void InstalledPackagePathReusesExistingPinnedSource() { using var f = new Fixture(true); f.Verify(f.Package); }
    [Fact] public void UnsupportedSourceLayoutFailsExplicitly()
    {
        using var f = new Fixture();
        Assert.Throws<DirectoryNotFoundException>(() => AuditCommand.SourceDirectory(f.Repository));
    }
    [Fact] public void VacuousAndNontextAnchorsCannotQualifyEvidence()
    {
        foreach (var anchor in new JsonNode?[] { null, JsonValue.Create(""), JsonValue.Create("  "), JsonValue.Create(1), JsonValue.Create(false) })
        {
            using var f = new Fixture(); f.Change(data => data["reviews"]![0]!["evidence"]![0]!["contains"] = anchor);
            Assert.Throws<InvalidDataException>(() => f.Verify());
        }
    }
    [Fact] public void ParentTraversalCannotUseExternalEvidence()
    {
        using var f = new Fixture(); File.WriteAllText(Path.Combine(f.Root, "outside.cs"), "required-anchor");
        f.Change(data => data["reviews"]![0]!["evidence"]![0]!["path"] = "../outside.cs");
        Assert.Throws<InvalidDataException>(() => f.Verify());
    }
    [Fact] public void AbsolutePathsCannotUseExternalEvidence()
    {
        using var f = new Fixture(); var external = Path.Combine(f.Root, "outside.cs"); File.WriteAllText(external, "required-anchor");
        f.Change(data => data["reviews"]![0]!["evidence"]![0]!["path"] = external);
        Assert.Throws<InvalidDataException>(() => f.Verify());
    }
    public static bool IsWindows => OperatingSystem.IsWindows();
    [Fact(Skip = "Unix symbolic-link semantics", SkipWhen = nameof(IsWindows))]
    public void LinkedEvidenceCannotEscapeRepository()
    {
        using var f = new Fixture(); var external = Path.Combine(f.Root, "outside.cs"); File.WriteAllText(external, "required-anchor");
        File.CreateSymbolicLink(Path.Combine(f.Repository, "src/linked.cs"), external);
        f.Change(data => data["reviews"]![0]!["evidence"]![0]!["path"] = "src/linked.cs");
        Assert.Throws<InvalidDataException>(() => f.Verify());
    }
    [Fact] public void UnknownAndMalformedReviewSchemasCannotQualify()
    {
        foreach (var version in new JsonNode?[] { null, JsonValue.Create(0), JsonValue.Create(2), JsonValue.Create("1"), JsonValue.Create(true), JsonValue.Create(1e40) })
        {
            using var f = new Fixture(); f.Change(data => data["schema_version"] = version);
            Assert.Throws<InvalidDataException>(() => f.Verify());
        }
    }
    [Fact] public void MissingReviewSchemaCannotQualify()
    {
        using var f = new Fixture(); f.Change(data => data.AsObject().Remove("schema_version"));
        Assert.Throws<InvalidDataException>(() => f.Verify());
    }
    [Fact] public void WrongCollectionShapesCannotQualify()
    {
        foreach (var name in new[] { "reviews", "source_hashes" })
        {
            using var f = new Fixture(); f.Change(data => data[name] = false);
            Assert.Throws<InvalidDataException>(() => f.Verify());
        }
    }
    [Fact] public void DuplicateInventoryRowsCannotInflateCompletionCounts()
    {
        using var f = new Fixture(); var path = Path.Combine(f.Repository, "docs/UPSTREAM_API_INVENTORY.csv");
        File.AppendAllText(path, "cocotb.sample,Thing,1,review-required\n");
        Assert.Throws<InvalidDataException>(() => f.Verify());
    }
    [Fact] public void InvalidInventoryCoordinatesCannotQualify()
    {
        foreach (var line in new[] { "0", "-1", " 1", "2147483648", "bad" })
        {
            using var f = new Fixture(); var path = Path.Combine(f.Repository, "docs/UPSTREAM_API_INVENTORY.csv");
            File.WriteAllText(path, "Module,Definition,SourceLine,ExactSemanticParity\ncocotb.sample,Thing," + line + ",review-required\n");
            Assert.Throws<InvalidDataException>(() => f.Verify());
        }
    }
    [Fact] public void InventoryCannotEmbedUnreviewedParityClaims()
    {
        using var f = new Fixture(); var path = Path.Combine(f.Repository, "docs/UPSTREAM_API_INVENTORY.csv");
        File.WriteAllText(path, File.ReadAllText(path).Replace("review-required", "verified"));
        Assert.Throws<InvalidDataException>(() => f.Verify());
    }
    [Fact] public void MatchingInvalidRevisionsCannotQualify()
    {
        foreach (var revision in new JsonNode?[] { null, JsonValue.Create("main"), JsonValue.Create(new string('z', 40)), JsonValue.Create(123) })
        {
            using var f = new Fixture(); var text = revision?.ToJsonString() ?? "null";
            File.WriteAllText(Path.Combine(f.Repository, "third_party/UPSTREAM.json"), "{\"commit\":" + text + "}");
            f.Change(data => data["source_revision"] = revision);
            Assert.Throws<InvalidDataException>(() => f.Verify());
        }
    }
    [Fact] public void DifferentPinnedRevisionRequiresNewReview()
    {
        using var f = new Fixture(); f.Change(data => data["source_revision"] = new string('b', 40));
        Assert.Throws<InvalidDataException>(() => f.Verify());
    }

    [Fact] public void FabricatedInventoryAndReviewCannotQualifyAgainstUnchangedSource()
    {
        using var f = new Fixture(); var path = Path.Combine(f.Repository, "docs/UPSTREAM_API_INVENTORY.csv");
        File.WriteAllText(path, File.ReadAllText(path).Replace("Thing", "Invented"));
        f.Change(data => data["reviews"]![0]!["definition"] = "Invented");
        Assert.Throws<InvalidDataException>(() => f.Verify());
    }
    [Fact] public void MatchingStaleCoordinatesStillRequireActualSourceDefinition()
    {
        using var f = new Fixture(); var path = Path.Combine(f.Repository, "docs/UPSTREAM_API_INVENTORY.csv");
        File.WriteAllText(path, File.ReadAllText(path).Replace(",1,", ",2,"));
        f.Change(data => data["reviews"]![0]!["source_line"] = 2);
        Assert.Throws<InvalidDataException>(() => f.Verify());
    }

    [Fact] public void SyntaxErrorCannotSilentlyRemoveInventoryDefinitions()
    {
        using var f = new Fixture(); File.WriteAllText(Path.Combine(f.Package, "sample.py"), "class Broken(\n");
        var path = Path.Combine(f.Repository, "docs/UPSTREAM_API_INVENTORY.csv"); var original = File.ReadAllText(path);
        Assert.Throws<InvalidDataException>(() => AuditCommand.Execute(new ToolContext(f.Repository), "audit-surface", ["--source", f.Source]));
        Assert.Equal(original, File.ReadAllText(path));
    }
    [Fact] public void AsyncAndClassMethodsRetainTheirOwnSourceCoordinates()
    {
        using var f = new Fixture(); var path = Path.Combine(f.Package, "sample.py");
        File.WriteAllText(path, "async def run():\n    pass\nclass Thing:\n    def get(self):\n        pass\n    def _private(self):\n        pass\n");
        Assert.Equal(new[] { ("run", 1), ("Thing", 3), ("Thing.get", 4) }, AuditCommand.Definitions(path));
    }

    [Fact] public void EmptyGenerationCannotEraseExistingRequirements()
    {
        using var f = new Fixture(); File.WriteAllText(Path.Combine(f.Package, "sample.py"), "# no public API\n");
        var path = Path.Combine(f.Repository, "docs/UPSTREAM_API_INVENTORY.csv"); var original = File.ReadAllText(path);
        Assert.Throws<InvalidDataException>(() => AuditCommand.Execute(new ToolContext(f.Repository), "audit-surface", ["--source", f.Source]));
        Assert.Equal(original, File.ReadAllText(path));
    }
    [Fact] public void EmptyInventoryCannotClaimCompleteCoverage()
    {
        using var f = new Fixture(); File.WriteAllText(Path.Combine(f.Repository, "docs/UPSTREAM_API_INVENTORY.csv"), "Module,Definition,SourceLine,ExactSemanticParity\n");
        f.Change(data => data["reviews"] = new JsonArray());
        Assert.Throws<InvalidDataException>(() => f.Verify());
    }

    [Fact] public void SuccessfulInventoryReplacementRetainsCanonicalSchemaAndNoTemporaryFiles()
    {
        using var f = new Fixture(); var directory = Path.Combine(f.Repository, "docs");
        AuditCommand.Execute(new ToolContext(f.Repository), "audit-surface", ["--source", f.Source]);
        Assert.Equal("Module,Definition,SourceLine,ExactSemanticParity\ncocotb.sample,Thing,1,review-required\n", File.ReadAllText(Path.Combine(directory, "UPSTREAM_API_INVENTORY.csv")));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp")); f.Verify();
    }
    [Fact] public void FailedInventoryReplacementCleansOnlyItsOwnPendingFile()
    {
        using var f = new Fixture(); var destination = Path.Combine(f.Repository, "docs/UPSTREAM_API_INVENTORY.csv");
        File.Delete(destination); Directory.CreateDirectory(destination);
        var sentinel = Path.Combine(f.Repository, "docs", "unrelated.tmp"); File.WriteAllText(sentinel, "preserve");
        var error = Record.Exception(() => AuditCommand.Execute(new ToolContext(f.Repository), "audit-surface", ["--source", f.Source]));
        Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
        Assert.True(Directory.Exists(destination)); Assert.Equal("preserve", File.ReadAllText(sentinel));
        Assert.Equal(new[] { sentinel }, Directory.GetFiles(Path.GetDirectoryName(destination)!, "*.tmp"));
    }

    [Fact] public void LegacyTaskMappingNoteRemainsInventoryOnly()
    {
        using var f = new Fixture(); var path = Path.Combine(f.Repository, "docs/UPSTREAM_API_INVENTORY.csv");
        File.WriteAllText(path, File.ReadAllText(path).Replace("review-required", "reviewed-mapping-see-TASK_API_MAP.md"));
        f.Change(data => data["reviews"] = new JsonArray());
        var result = JsonSerializer.SerializeToElement(AuditCommand.Validate(new ToolContext(f.Repository), f.Source));
        Assert.Equal(1, result.GetProperty("inventoried_definitions").GetInt32());
        Assert.Equal(0, result.GetProperty("reviewed_definitions").GetInt32());
        Assert.Equal(1, result.GetProperty("unreviewed_definitions").GetInt32());
    }
}