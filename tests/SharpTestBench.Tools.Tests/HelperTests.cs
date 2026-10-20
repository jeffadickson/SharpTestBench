using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

public sealed class HelperTests
{
    const string Valid = "<coverage lines-valid=\"10\" lines-covered=\"5\" branches-valid=\"4\" branches-covered=\"1\" line-rate=\"0.5\" branch-rate=\"0.25\"><packages><package name=\"SharpTestBench\"><classes><class name=\"Example\" filename=\"Example.cs\" /></classes></package></packages></coverage>";
    sealed class Folder : IDisposable
    {
        readonly string parent = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "temporary-tests"));
        public string Root { get; }
        public Folder() { Root = Path.Combine(parent, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Root); }
        public string At(string name) => Path.Combine(Root, name);
        public void Dispose() { if (Directory.GetParent(Root)?.FullName != parent) throw new InvalidOperationException("Temporary path escaped its parent"); Directory.Delete(Root, true); }
    }
    sealed class FakeRunner : ICommandRunner
    {
        public List<string[]> Commands { get; } = [];
        public int FailAt { get; set; } = -1;
        public CommandResult Run(IReadOnlyList<string> arguments, string directory, IReadOnlyDictionary<string, string?>? environment = null, bool capture = false, bool check = true, TimeSpan? timeout = null)
        {
            Commands.Add(arguments.ToArray());
            if (Commands.Count == FailAt && check) throw new IOException("Injected child failure");
            return new(0, "package-inventory\n", "");
        }
    }
    sealed class HttpStub(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
    }
    static CoverageReport ReadReport(string xml)
    {
        using var folder = new Folder();
        File.WriteAllText(folder.At("report.xml"), xml);
        return CoverageCommand.Validate(folder.At("report.xml"), "SharpTestBench");
    }
    [Fact] public void CoveragePreservesExactCounters()
    {
        var result = ReadReport(Valid);
        Assert.Equal(5, result.lines.covered); Assert.Equal(10, result.lines.valid);
        Assert.Equal(50d, result.lines.percent); Assert.Equal(25d, result.branches.percent);
        Assert.Equal(new[] { "SharpTestBench" }, result.assemblies);
    }
    [Fact] public void StandardDtdAndTruncatedRatesAreSupported()
    {
        var xml = "<!DOCTYPE coverage SYSTEM \"http://cobertura.sourceforge.net/xml/coverage-04.dtd\">" + Valid;
        xml = xml.Replace("branches-valid=\"4\"", "branches-valid=\"1297\"").Replace("branches-covered=\"1\"", "branches-covered=\"345\"").Replace("branch-rate=\"0.25\"", "branch-rate=\"0.2659\"");
        Assert.Equal(345, ReadReport(xml).branches.covered);
    }
    [Theory]
    [InlineData("lines-valid", "0")]
    [InlineData("lines-valid", "-1")]
    [InlineData("lines-valid", "text")]
    [InlineData("lines-covered", "0")]
    [InlineData("lines-covered", "-1")]
    [InlineData("lines-covered", "11")]
    [InlineData("branches-valid", "0")]
    [InlineData("branches-valid", "-4")]
    [InlineData("branches-covered", "-1")]
    [InlineData("branches-covered", "5")]
    [InlineData("line-rate", "NaN")]
    [InlineData("line-rate", "Infinity")]
    [InlineData("line-rate", "-0.1")]
    [InlineData("line-rate", "1.1")]
    [InlineData("line-rate", "0.9")]
    [InlineData("branch-rate", "NaN")]
    [InlineData("branch-rate", "-Infinity")]
    [InlineData("branch-rate", "0.9")]
    public void InvalidCoverageCountersFail(string attribute, string value)
    {
        var xml = System.Text.RegularExpressions.Regex.Replace(Valid, attribute + "=\"[^\"]*\"", attribute + "=\"" + value + "\"");
        Assert.NotNull(Record.Exception(() => ReadReport(xml)));
    }
    [Theory]
    [InlineData("lines-valid")][InlineData("lines-covered")][InlineData("branches-valid")]
    [InlineData("branches-covered")][InlineData("line-rate")][InlineData("branch-rate")]
    public void MissingCoverageFieldsFail(string attribute)
    {
        var xml = System.Text.RegularExpressions.Regex.Replace(Valid, " " + attribute + "=\"[^\"]*\"", "");
        Assert.NotNull(Record.Exception(() => ReadReport(xml)));
    }
    [Theory]
    [InlineData("<!DOCTYPE coverage []>")]
    [InlineData("<!ENTITY x 'value'>")]
    public void UnsafeXmlDeclarationsFail(string prefix) => Assert.Throws<InvalidDataException>(() => ReadReport(prefix + Valid));
    [Fact] public void WrongCoverageAssemblyFails() => Assert.Throws<InvalidDataException>(() => ReadReport(Valid.Replace("SharpTestBench", "Other")));
    [Fact] public void EmptyCoverageClassesFail() => Assert.Throws<InvalidDataException>(() => ReadReport(Valid.Replace("<class name=\"Example\" filename=\"Example.cs\" />", "")));
    [Fact] public void ZeroBranchHitsAreValid() => Assert.Equal(0, ReadReport(Valid.Replace("branches-covered=\"1\"", "branches-covered=\"0\"").Replace("branch-rate=\"0.25\"", "branch-rate=\"0\"")).branches.covered);
    [Fact] public void Utf16CoverageIsRejected()
    {
        using var folder = new Folder(); File.WriteAllBytes(folder.At("report.xml"), Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(Valid)).ToArray());
        Assert.Throws<DecoderFallbackException>(() => CoverageCommand.Validate(folder.At("report.xml"), "SharpTestBench"));
    }
    [Theory]
    [InlineData("{\"x\":1,\"x\":2}")]
    [InlineData("{\"x\":{\"a\":1,\"a\":2}}")]
    [InlineData("{\"x\":NaN}")]
    [InlineData("{\"x\":1e999}")]
    [InlineData("{\"x\":\"\\ud800\"}")]
    public void StrictJsonRejectsAmbiguousInputs(string text) => Assert.NotNull(Record.Exception(() => { using var _ = StrictJson.Parse(text); }));
    [Fact] public void BoundedInputStopsGrowth() => Assert.Throws<InvalidDataException>(() => BoundedInput.Read(new MemoryStream([1, 2, 3, 4]), 3));
    [Theory]
    [InlineData("linux", "gdb")][InlineData("parity", "iverilog")][InlineData("verilator", "autoconf")][InlineData("documentation", "doxygen")]
    public void DependencyProfilesUseArgumentArrays(string profile, string required)
    {
        using var folder = new Folder(); var runner = new FakeRunner();
        CiCommands.InstallDependencies(new(folder.Root, runner), profile);
        Assert.Equal(new[] { "sudo", "apt-get", "update" }, runner.Commands[0]);
        Assert.Contains(required, runner.Commands[1]);
    }
    [Fact] public void DependencyFailureStopsInstallation()
    {
        using var folder = new Folder(); var runner = new FakeRunner { FailAt = 1 };
        Assert.Throws<IOException>(() => CiCommands.InstallDependencies(new(folder.Root, runner), "linux"));
        Assert.Single(runner.Commands);
    }
    [Fact] public void UnknownDependencyProfileDoesNotLaunch()
    {
        using var folder = new Folder(); var runner = new FakeRunner();
        Assert.Throws<ArgumentException>(() => CiCommands.InstallDependencies(new(folder.Root, runner), "invalid")); Assert.Empty(runner.Commands);
    }
    [Fact] public void DownloadMismatchPreservesExistingArchive()
    {
        using var folder = new Folder(); Directory.CreateDirectory(folder.At("build")); File.WriteAllText(folder.At("build/test.tar.gz"), "previous");
        using var client = new HttpClient(new HttpStub([1, 2, 3]));
        Assert.Throws<InvalidDataException>(() => CiCommands.Download(new(folder.Root), new("https://example.invalid/test", new string('0', 64), "test.tar.gz"), client));
        Assert.Equal("previous", File.ReadAllText(folder.At("build/test.tar.gz"))); Assert.Single(Directory.GetFiles(folder.At("build")));
    }
    [Fact] public void VerifiedDownloadReplacesArchive()
    {
        using var folder = new Folder(); byte[] content = [1, 2, 3]; using var client = new HttpClient(new HttpStub(content));
        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content));
        var result = CiCommands.Download(new(folder.Root), new("https://example.invalid/test", hash, "test.tar.gz"), client);
        Assert.Equal(content, File.ReadAllBytes(result));
    }
    static JsonObject ReleaseRun() => new() { ["status"] = "completed", ["conclusion"] = "success", ["head_branch"] = "main", ["path"] = ".github/workflows/ci.yml", ["event"] = "push", ["head_sha"] = new string('a', 40), ["head_repository"] = new JsonObject { ["full_name"] = "owner/repo" } };
    [Fact] public void VerifiedReleasePinsExactHead()
    {
        using var json = StrictJson.Parse(ReleaseRun().ToJsonString());
        Assert.Equal(new string('a', 40), CiCommands.VerifyReleaseRun(json.RootElement, "owner/repo"));
    }
    [Theory]
    [InlineData("status", "in_progress")][InlineData("conclusion", "failure")][InlineData("head_branch", "feature")]
    [InlineData("path", ".github/workflows/other.yml")][InlineData("event", "pull_request")][InlineData("head_sha", "bad")]
    public void UnqualifiedReleaseRunsFail(string key, string value)
    {
        var node = ReleaseRun(); node[key] = value; using var json = StrictJson.Parse(node.ToJsonString());
        Assert.Throws<InvalidDataException>(() => CiCommands.VerifyReleaseRun(json.RootElement, "owner/repo"));
    }
    [Fact] public void ForkReleaseRunFails()
    {
        using var json = StrictJson.Parse(ReleaseRun().ToJsonString()); Assert.Throws<InvalidDataException>(() => CiCommands.VerifyReleaseRun(json.RootElement, "other/repo"));
    }
    [Theory][InlineData("--value")][InlineData("value")][InlineData("--value", "--other")]
    public void InvalidCommandArgumentsFail(params string[] args) => Assert.Throws<ArgumentException>(() => new Options(args));
    [Fact] public void DuplicateScalarOptionsFail() => Assert.Throws<ArgumentException>(() => new Options(["--value", "1", "--value", "2"]).Get("--value"));
    [Fact] public void RepeatedSourceOptionsPreserveOrder() => Assert.Equal(new[] { "a path.sv", "b.sv" }, new Options(["--source", "a path.sv", "--source", "b.sv"]).All("--source"));
}
