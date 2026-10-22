using SharpTestBench.Tools;
using System.Text.Json;
using Xunit;
public sealed class ScopeBackendSelectionTests
{
    static object Row(string name,ulong seed,string[] simulators,bool skip=false)=>new{Name=name,Seed=seed,Skip=skip,Simulators=simulators};
    static JsonElement Discovery(params object[] rows)=>JsonSerializer.SerializeToElement(new{Tests=rows});
    [Fact] public void BackendSelectionRetainsOnlyCompatibleCasesAndExactDiscoveredSeeds()
    {var map=ContractsCiCommand.SelectForIcarus(Discovery(Row("Entry.R001_A",ulong.MaxValue,["icarus"]),Row("Entry.R002_B",2,["Verilator"]),Row("Entry.R003_C",0,[]),Row("Entry.R004_D",4,["Icarus Verilog"])),4,71);Assert.Equal(3,map.Count);Assert.Equal(ulong.MaxValue,map["Entry.R001_A"]);Assert.Equal(0UL,map["Entry.R003_C"]);Assert.Equal(4UL,map["Entry.R004_D"]);Assert.DoesNotContain("Entry.R002_B",map.Keys);}
    [Fact] public void MalformedFutureSimulatorMetadataCannotHideOutsideSelection()
    {foreach(object? simulators in new object?[]{null,"icarus",new object[]{1},new[]{""},new[]{" icarus"},new[]{"icarus\n"}}){var bad=new{Name="Entry.R002_B",Seed=2UL,Skip=false,Simulators=simulators};Assert.Throws<InvalidDataException>(()=>ContractsCiCommand.SelectForIcarus(Discovery(Row("Entry.R001_A",1,[]),bad),1,71));}}
    [Fact] public void ExplicitSkipOrMissingBooleanCannotSatisfyQualification()
    {foreach(object bad in new object[]{Row("Entry.R002_B",2,["Verilator"],true),new{Name="Entry.R002_B",Seed=2UL,Simulators=new[]{"icarus"}},new{Name="Entry.R002_B",Seed=2UL,Skip="false",Simulators=new[]{"icarus"}}})Assert.Throws<InvalidDataException>(()=>ContractsCiCommand.SelectForIcarus(Discovery(Row("Entry.R001_A",1,[]),bad),1,71));}
    [Fact] public void EntirelyUnavailableMilestoneCannotProduceAnEmptyRequiredManifest()
    {Assert.Throws<InvalidDataException>(()=>ContractsCiCommand.SelectForIcarus(Discovery(Row("Entry.R001_A",1,["Verilator"])),1,71));}
    [Fact] public void BackendFilteringCannotConcealMissingOrOutOfDomainIdentities()
    {Assert.Throws<InvalidDataException>(()=>ContractsCiCommand.SelectForIcarus(Discovery(Row("Entry.R001_A",1,[]),Row("Entry.R003_C",3,["Verilator"])),2,71));Assert.Throws<InvalidDataException>(()=>ContractsCiCommand.SelectForIcarus(Discovery(Row("Entry.R001_A",1,[]),Row("Entry.R072_B",72,["Verilator"])),1,71));}
    [Fact] public void StableTagsAreCaseInsensitiveButNeverSubstringGuesses()
    {var map=ContractsCiCommand.SelectForIcarus(Discovery(Row("Entry.R001_A",1,["ICARUS"]),Row("Entry.R002_B",2,["not-icarus"]),Row("Entry.R003_C",3,["Verilator","Icarus Verilog"])),3,71);Assert.Equal(new[]{"Entry.R001_A","Entry.R003_C"},map.Keys);}
}
