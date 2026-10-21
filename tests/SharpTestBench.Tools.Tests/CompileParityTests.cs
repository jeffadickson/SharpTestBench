using SharpTestBench.Tools;
using Xunit;

public sealed class CompileParityTests
{
    static Dictionary<string,object> Manifest() => new() {
        ["schemaVersion"]=1,["simulator"]="icarus",["top"]="top",["sources"]=new[] { "first source.sv","second.sv" },
        ["assembly"]="fixture.dll",["nativeHost"]="host",["icarusBootstrap"]="boot",["gpiLibrary"]="gpi",["gpiImpl"]="impl:entry",
        ["gpiEntryPoint"]="entry",["dotnetRoot"]="sdk",["outputDirectory"]="native",["seed"]=17UL,["expectedTests"]=1,
        ["expectedNames"]=new[] { "Entry.Case" },["entryType"]="Entry, Fixture" };
    static string Root => Path.GetFullPath(Path.Combine(Path.GetTempPath(),"compile parity manifest"));
    static string[] Values(string[] command,string option) => command.Select((value,index)=>(value,index)).Where(pair=>pair.value==option).Select(pair=>command[pair.index+1]).ToArray();

    [Fact] public void BridgeResolvesManifestSourcesAssemblyHostAndIncludesAgainstManifestDirectory()
    {
        var manifest=Manifest();manifest["includeDirectories"]=new[] { "include second","include first" };
        var args=ParityLegacyCommand.Build(manifest,Root,17,null);
        Assert.Equal(new[] { Path.Combine(Root,"first source.sv"),Path.Combine(Root,"second.sv") },Values(args,"--source"));
        Assert.Equal(new[] { Path.Combine(Root,"include second"),Path.Combine(Root,"include first") },Values(args,"--include-dir"));
        Assert.Equal(new[] { Path.Combine(Root,"fixture.dll") },Values(args,"--assembly"));
        Assert.Equal(new[] { Path.Combine(Root,"host") },Values(args,"--host"));
    }
    [Fact] public void BridgePreservesBareDefinesExpressionsAndDeterministicMapOrder()
    {
        var manifest=Manifest();manifest["defines"]=new Dictionary<string,string?> { ["Z"]="1==1",["BARE"]=null,["A"]="hello world" };
        manifest["parameters"]=new Dictionary<string,string> { ["WIDTH"]="8",["OFFSET"]="(1 << 2)" };
        manifest["hdlStandard"]="2005-sv";
        var args=ParityLegacyCommand.Build(manifest,Root,17,null);
        Assert.Equal(new[] { "A=hello world","BARE","Z=1==1" },Values(args,"--define"));
        Assert.Equal(new[] { "OFFSET=(1 << 2)","WIDTH=8" },Values(args,"--parameter"));
        Assert.Equal(new[] { "2005-sv" },Values(args,"--hdl-standard"));
    }
    [Fact] public void BridgeForwardsManifestSelectionAndRejectsConflictingExplicitSelection()
    {
        var manifest=Manifest();manifest["testNames"]=new[] { "Entry.Case","Entry.Other" };
        Assert.Equal(new[] { "Entry.Case","Entry.Other" },Values(ParityLegacyCommand.Build(manifest,Root,17,null),"--exact"));
        Assert.Throws<InvalidDataException>(()=>ParityLegacyCommand.Build(manifest,Root,17,"Entry.Case"));
        Assert.Throws<InvalidDataException>(()=>ParityLegacyCommand.Build(manifest,Root,18,null));
    }
    [Fact] public void BridgeExplicitlyRejectsUnsupportedCustomProcessSettings()
    {
        foreach (var (key,value) in new (string,object)[] { ("iverilog","custom"),("vvp","custom"),("timeoutSeconds",1),("maxLogBytes",1024) })
        {
            var manifest=Manifest();manifest[key]=value;
            Assert.Throws<InvalidDataException>(()=>ParityLegacyCommand.Build(manifest,Root,17,null));
        }
    }
    [Fact] public void AdapterMismatchCannotSilentlyQualifyDifferentRunnerInputs()
    {
        var config=ParityLegacyCommand.Read(Manifest());
        var adapter=new CocotbAdapter("fixture","python","libs","boot","gpi,entry","impl:entry",null,null);
        ParityLegacyCommand.ValidateAdapter(config,Root,adapter);
        Assert.Throws<InvalidDataException>(()=>ParityLegacyCommand.ValidateAdapter(config with { GpiLibrary="other" },Root,adapter));
        Assert.Throws<InvalidDataException>(()=>ParityLegacyCommand.ValidateAdapter(config with { GpiImpl="impl:other_entry" },Root,adapter));
    }
    [Fact] public void DirectCliAndManifestUseTheSameCompilerArgumentBuilder()
    {
        var options=new Options(new[] { "--hdl-standard","2001","--define","BARE","--parameter","WIDTH=8" });
        var config=LegacyCompileOptions.Create(options,"icarus","top",new[] { "first.sv" });
        Assert.Equal(new[] { "-g2001","-s","top","-o","image","-D","BARE","-P","top.WIDTH=8",Path.Combine(Root,"first.sv") },CompilePlan.Arguments(Root,config,"image"));
    }
    [Fact] public void OtherBackendsRejectIcarusSpecificOptions()
    {
        Assert.Throws<ArgumentException>(()=>LegacyCompileOptions.Create(new Options(new[] { "--define","FLAG" }),"ghdl","top",new[] { "a.vhd" }));
    }
    [Fact] public void ExactSelectionUsesFullDiscoverySeedRatherThanBaseSeedOrFilteredIndex()
    {
        using var discovery=System.Text.Json.JsonDocument.Parse("{\"Tests\":[{\"QualifiedName\":\"Entry.Before\",\"Seed\":17},{\"QualifiedName\":\"Entry.Selected\",\"Seed\":59}]}");
        var manifest=Manifest();var seed=ManifestCheckCommand.SelectionSeed(discovery.RootElement,"Entry.Selected");
        RunnerManifestCommand.BindSingleCase(manifest,"Entry.Selected",seed);
        Assert.Equal(59UL,((Dictionary<string,ulong>)manifest["expectedSeeds"])["Entry.Selected"]);
        Assert.Equal(17UL,(ulong)manifest["seed"]);
    }
    [Fact] public void MissingDuplicateAndMistypedDiscoverySeedsCannotBindExpectedStimulus()
    {
        foreach (var json in new[] { "{}","{\"Tests\":[]}","{\"Tests\":[{\"Name\":\"Entry.Case\",\"Seed\":true}]}",
            "{\"Tests\":[{\"Name\":\"Entry.Case\",\"Seed\":1},{\"Name\":\"Entry.Case\",\"Seed\":2}]}" })
        {
            using var discovery=System.Text.Json.JsonDocument.Parse(json);
            Assert.Throws<InvalidDataException>(()=>ManifestCheckCommand.SelectionSeed(discovery.RootElement,"Entry.Case"));
        }
    }
    [Fact] public void DiscoverySeedProducerMustUseTheSameDeclaredInputBytesAsSelectedPair()
    {
        var source=new InputFingerprint("source",Path.Combine(Root,"top.sv"),1,new string('A',64));
        var planning=new[] { new InputFingerprint("manifest","discovery invocation",1,new string('B',64)),source };
        var execution=new[] { new InputFingerprint("manifest","selected invocation",1,new string('C',64)),source };
        var evidence=System.Text.Json.JsonSerializer.SerializeToElement(new { schemaVersion=1,status="verified",inputs=planning });
        ManifestCheckCommand.VerifyDiscoveryInputs(evidence,execution);
        Assert.Throws<InvalidDataException>(()=>ManifestCheckCommand.VerifyDiscoveryInputs(evidence,[execution[0],source with { Sha256=new string('D',64) }]));
        var incomplete=System.Text.Json.JsonSerializer.SerializeToElement(new { schemaVersion=1,status="captured",inputs=planning });
        Assert.Throws<InvalidDataException>(()=>ManifestCheckCommand.VerifyDiscoveryInputs(incomplete,execution));
    }
    [Fact] public void PairedCompilerDeadlineIgnoresPoisonedInheritedOverride()
    {
        var left=new Dictionary<string,string?> { ["CS_COMPILE_TIMEOUT"]="invalid" };
        var right=new Dictionary<string,string?> { ["CS_COMPILE_TIMEOUT"]="1" };
        ParityLegacyCommand.BindTools(new ToolContext(Root),Manifest(),Root,left,right);
        Assert.Null(left["CS_COMPILE_TIMEOUT"]);Assert.Null(right["CS_COMPILE_TIMEOUT"]);
        Assert.Contains("CS_COMPILE_TIMEOUT",ParityContracts.SelectionVariables);
    }
}
