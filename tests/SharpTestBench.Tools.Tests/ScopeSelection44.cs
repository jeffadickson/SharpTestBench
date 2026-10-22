using SharpTestBench.Tools;
using System.Text.Json;
using Xunit;
public sealed class ScopeSelectionTests
{
    static JsonElement Discovery(params (string Name,ulong Seed)[] rows)=>JsonSerializer.SerializeToElement(new {Tests=rows.Select(r=>new {r.Name,r.Seed})});
    [Fact] public void S041_DeclaredSuiteDomainRejectsFutureOutOfDomainProducer()
    {Assert.Throws<InvalidDataException>(()=>ContractsCiCommand.Select(Discovery(("Entry.R001_A",1),("Entry.R072_Outside",2)),1,71));}
    [Fact] public void S042_FutureWithinDomainRemainsValidAndRetainsSelectedSeed()
    {var map=ContractsCiCommand.Select(Discovery(("Entry.R001_A",ulong.MaxValue),("Entry.R071_Future",0)),1,71);Assert.Single(map);Assert.Equal(ulong.MaxValue,map["Entry.R001_A"]);}
    [Fact] public void S043_InvalidDomainRejectsBeforeMalformedDiscovery()
    {foreach(int maximum in new[]{0,-1,101,int.MaxValue})Assert.Equal("maximum",Assert.Throws<ArgumentOutOfRangeException>(()=>ContractsCiCommand.Select(default,1,maximum)).ParamName);Assert.Equal("through",Assert.Throws<ArgumentOutOfRangeException>(()=>ContractsCiCommand.Select(default,72,71)).ParamName);}
    [Fact] public void S044_DomainBoundaryStillChecksCompleteSetAndDuplicateFutureNumbers()
    {var rows=Enumerable.Range(1,71).Reverse().Select(n=>($"Entry.R{n:D3}_Boundary",(ulong)n)).ToArray();var map=ContractsCiCommand.Select(Discovery(rows),71,71);Assert.Equal(71,map.Count);Assert.Equal(71UL,map["Entry.R071_Boundary"]);Assert.Throws<InvalidDataException>(()=>ContractsCiCommand.Select(Discovery(("Entry.R001_A",1),("Entry.R071_A",2),("Entry.R071_B",3)),1,71));}
}
