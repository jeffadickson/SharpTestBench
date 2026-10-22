using SharpTestBench.Tools;
using System.Text.Json;
using Xunit;

public sealed class ContractSelectionTests
{
    static JsonElement Discovery(params (string Name,ulong Seed)[] rows)=>JsonSerializer.SerializeToElement(new {Tests=rows.Select(row=>new {row.Name,row.Seed})});
    [Fact] public void SelectionPreservesDiscoveredEffectiveSeeds()
    {
        var map=ContractsCiCommand.Select(Discovery(("Entry.R002_B",17),("Entry.R001_A",83)),2);
        Assert.Equal(83UL,map["Entry.R001_A"]);Assert.Equal(17UL,map["Entry.R002_B"]);
    }
    [Fact] public void FutureContractsAreExcludedWithoutInventingSeeds()
    {
        var map=ContractsCiCommand.Select(Discovery(("Entry.R001_A",ulong.MaxValue),("Entry.R002_B",0)),1);
        Assert.Single(map);Assert.Equal(ulong.MaxValue,map["Entry.R001_A"]);
    }
    [Fact] public void MissingRequestedNumberCannotQualify()
    {Assert.Throws<InvalidDataException>(()=>ContractsCiCommand.Select(Discovery(("Entry.R001_A",1),("Entry.R003_C",3)),2));}
    [Fact] public void DuplicateIdentityCannotQualify()
    {Assert.Throws<InvalidDataException>(()=>ContractsCiCommand.Select(Discovery(("Entry.R001_A",1),("Entry.R001_A",2)),1));}
    [Fact] public void DuplicateNumberOutsideSelectionStillRejectsProducer()
    {Assert.Throws<InvalidDataException>(()=>ContractsCiCommand.Select(Discovery(("Entry.R001_A",1),("Entry.R002_A",2),("Entry.R002_B",3)),1));}
    [Fact] public void MalformedDiscoveryShapeFailsBeforeSelection()
    {
        foreach(var raw in new[]{"null","[]","{}","{\"Tests\":{}}"})
            Assert.Throws<InvalidDataException>(()=>ContractsCiCommand.Select(JsonSerializer.Deserialize<JsonElement>(raw),1));
    }
    [Fact] public void InvalidSeedKindsAndRangesAreDataFailures()
    {
        foreach(var seed in new[]{"null","true","\"83\"","-1","1.5","18446744073709551616"})
            Assert.Throws<InvalidDataException>(()=>ContractsCiCommand.Select(JsonSerializer.Deserialize<JsonElement>("{\"Tests\":[{\"Name\":\"Entry.R001_A\",\"Seed\":"+seed+"}]}"),1));
    }
    [Fact] public void InvalidContractNamesCannotAliasMilestoneNumbers()
    {
        foreach(var name in new[]{"Other.R001_A","Entry.R000_A","Entry.R101_A","Entry.R01_A","Entry.Rxxx_A"})
            Assert.Throws<InvalidDataException>(()=>ContractsCiCommand.Select(Discovery((name,1)),1));
    }
    [Fact] public void InvalidMilestonesRejectBeforeReadingDiscovery()
    {
        Assert.Throws<ArgumentOutOfRangeException>(()=>ContractsCiCommand.Select(default,0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>ContractsCiCommand.Select(default,101));
    }
}
