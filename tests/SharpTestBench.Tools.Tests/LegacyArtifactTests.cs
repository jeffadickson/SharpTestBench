using SharpTestBench.Tools;
using Xunit;

public sealed class LegacyArtifactTests
{
    public static bool IsWindows => OperatingSystem.IsWindows();
    sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(),"stb-legacy-artifacts-"+Guid.NewGuid().ToString("N"));
        public string Work => Path.Combine(Root,"output");
        public Fixture() => Directory.CreateDirectory(Work);
        public void Dispose()
        {
            if (Path.GetDirectoryName(Path.GetFullPath(Root)) != Path.TrimEndingDirectorySeparator(Path.GetTempPath())) throw new InvalidOperationException("Invalid temporary legacy root");
            Directory.Delete(Root,true);
        }
    }
    [Fact] public void EveryKnownReportAndCompilerArtifactRejectsAnInputAlias()
    {
        using var f=new Fixture();
        // Independent ownership inventory, not derived from the production list.
        foreach (var file in new[] { "case-results.json","case-results.xml","case-results.coverage.json","case-results.discovery.json","top.vvp","DutBindings.cs","case-run.log","case-simulate.stdout.log","case-simulate.stderr.log","case-simulate-capture.json","case-artifacts.json","case-timings.json","case-wave-dump.sv","case-compile.json","case-compile-process.json","case-compile.stdout.log","case-compile.stderr.log","case-failure.json","case-invocation.json","case-input-manifest.json","cocotb_bootstrap.vpi" })
        {
            var path=Path.Combine(f.Work,file); File.WriteAllText(path,"protected input");
            Assert.Throws<InvalidDataException>(() => LegacyArtifacts.Validate([path],LegacyArtifacts.Outputs(f.Work,"case")));
            Assert.Equal("protected input",File.ReadAllText(path));
        }
    }
    [Fact] public void CanonicalDotSegmentsCannotBypassInputOwnership()
    {
        using var f=new Fixture(); Directory.CreateDirectory(Path.Combine(f.Work,"sub"));
        var source=Path.Combine(f.Work,"sub","..","top.vvp"); File.WriteAllText(source,"input");
        Assert.Throws<InvalidDataException>(() => LegacyArtifacts.Validate([source],LegacyArtifacts.Outputs(f.Work,"case")));
        Assert.Equal("input",File.ReadAllText(source));
    }
    [Fact] public void OutputDestinationsCannotAliasEachOther()
    {
        using var f=new Fixture(); var path=Path.Combine(f.Work,"case-results.json");
        Assert.Throws<InvalidDataException>(() => LegacyArtifacts.Validate([],LegacyArtifacts.Outputs(f.Work,"case").Append(path)));
    }
    [Fact] public void CaseComparisonMatchesTheCurrentFilesystemPlatform()
    {
        using var f=new Fixture(); var input=Path.Combine(f.Work,"TOP.vvp"); File.WriteAllText(input,"input");
        if (IsWindows) Assert.Throws<InvalidDataException>(() => LegacyArtifacts.Validate([input],LegacyArtifacts.Outputs(f.Work,"case")));
        else LegacyArtifacts.Validate([input],LegacyArtifacts.Outputs(f.Work,"case"));
        Assert.Equal("input",File.ReadAllText(input));
    }
    [Fact(Skip="Unix symbolic-link semantics",SkipWhen=nameof(IsWindows))]
    public void LinkedGeneratedLeafCannotRedirectWritesToUnlistedData()
    {
        using var f=new Fixture();var target=Path.Combine(f.Root,"unrelated");File.WriteAllText(target,"owner data");
        var link=Path.Combine(f.Work,"top.vvp");File.CreateSymbolicLink(link,target);
        try { Assert.Throws<InvalidDataException>(() => LegacyArtifacts.Validate([],LegacyArtifacts.Outputs(f.Work,"case")));Assert.Equal("owner data",File.ReadAllText(target)); }
        finally { File.Delete(link); }
    }
    [Fact(Skip="Unix symbolic-link semantics",SkipWhen=nameof(IsWindows))]
    public void LinkedInputDirectoryAliasesResolveBeforeCollisionChecks()
    {
        using var f=new Fixture();var link=Path.Combine(f.Root,"input-alias");Directory.CreateSymbolicLink(link,f.Work);
        try
        {
            File.WriteAllText(Path.Combine(f.Work,"top.vvp"),"input");
            Assert.Throws<InvalidDataException>(() => LegacyArtifacts.Validate([Path.Combine(link,"top.vvp")],LegacyArtifacts.Outputs(f.Work,"case")));
            Assert.Equal("input",File.ReadAllText(Path.Combine(f.Work,"top.vvp")));
        }
        finally { Directory.Delete(link); }
    }
    [Fact] public void GeneratedBackendDirectoryCannotContainAnInput()
    {
        using var f=new Fixture();var folder=Path.Combine(f.Work,"verilator");Directory.CreateDirectory(folder);
        var source=Path.Combine(folder,"user.sv");File.WriteAllText(source,"input");
        Assert.Throws<InvalidDataException>(() => LegacyArtifacts.Validate([source],LegacyArtifacts.Outputs(f.Work,"case"),[folder]));
        Assert.Equal("input",File.ReadAllText(source));
    }
    [Fact] public void IncludeTreeCannotContainGeneratedOutputs()
    {
        using var f=new Fixture();
        Assert.Throws<InvalidDataException>(() => LegacyArtifacts.Validate([],LegacyArtifacts.Outputs(f.Work,"case"),includeDirectories:[f.Root]));
        var sibling=Path.Combine(f.Root,"includes");Directory.CreateDirectory(sibling);
        LegacyArtifacts.Validate([],LegacyArtifacts.Outputs(f.Work,"case"),includeDirectories:[sibling]);
    }
    [Fact] public void ReportRetirementPreservesNeighborFilesAndDoesNotTouchTheLease()
    {
        using var f=new Fixture();
        foreach (var suffix in new[] { ".json",".xml",".coverage.json",".discovery.json" }) File.WriteAllText(Path.Combine(f.Work,"case-results"+suffix),"old success");
        var neighbor=Path.Combine(f.Work,"unrelated.txt");File.WriteAllText(neighbor,"owner");
        var lease=Path.Combine(f.Work,".sharptestbench-run.lock");File.WriteAllText(lease,"lease");
        LegacyArtifacts.RetireReports(f.Work,"case");
        Assert.All(LegacyArtifacts.Reports(f.Work,"case"),path => Assert.False(File.Exists(path)));
        Assert.Equal("owner",File.ReadAllText(neighbor));Assert.Equal("lease",File.ReadAllText(lease));
    }
    [Fact] public void CompiledImageFingerprintUsesExactBytesAndDetectsSameSizeMutation()
    {
        using var f=new Fixture();var path=Path.Combine(f.Work,"top.vvp");File.WriteAllText(path,"abc");
        var image=LegacyImage.Capture(path);Assert.Equal(3,image.Bytes);
        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD",image.Sha256);image.Verify();
        File.WriteAllText(path,"abd");Assert.Throws<InvalidDataException>(() => image.Verify());
    }
    [Fact] public void EmptyOrMissingCompiledImageCannotProduceAValidFingerprint()
    {
        using var f=new Fixture();var path=Path.Combine(f.Work,"top.vvp");File.WriteAllBytes(path,[]);
        Assert.Throws<IOException>(() => LegacyImage.Capture(path));File.Delete(path);
        Assert.Throws<FileNotFoundException>(() => LegacyImage.Capture(path));
    }
    [Fact] public void InheritedLogDestinationKeepsValidatedAbsolutePathAcrossBackendWorkingDirectories()
    {
        using var f=new Fixture();
        var inherited=new Dictionary<string,string?> { ["CS_LOG_JSON"]="../input.vhd" };
        var validated=Path.GetFullPath("../input.vhd",f.Work);
        var environment=LegacyRunCommand.SimulatorEnvironment(inherited,f.Root,f.Root,validated);
        Assert.Equal(validated,environment["CS_LOG_JSON"]);
        Assert.Equal(validated,Path.GetFullPath(environment["CS_LOG_JSON"]!,Path.Combine(f.Work,"ghdl-work")));
        Assert.Equal("../input.vhd",inherited["CS_LOG_JSON"]);
    }
}
