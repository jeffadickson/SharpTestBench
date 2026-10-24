using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;
/// <summary>Protocol qualification expectations come from reviewed source identities and full-order arithmetic, not producer rows.</summary>
public sealed class SelectionProtocolInventoryTests
{
    static JsonObject Discovery(ulong seed=83)
    {
        var rows=new JsonArray();for(int index=0;index<ProtocolContractInventory.Names.Count;index++)rows.Add((JsonNode)new JsonObject
        { ["Name"]=ProtocolContractInventory.Names[index][6..],["QualifiedName"]=ProtocolContractInventory.Names[index],["Seed"]=unchecked(seed+(ulong)index),["Stage"]=0,["CaseIndex"]=0,["Skip"]=false,["Simulators"]=new JsonArray() });
        return new(){["Tests"]=rows};
    }
    static Dictionary<string,ulong> Select(JsonObject value,int through=70,ulong seed=83)=>ProtocolContractInventory.Select(JsonSerializer.SerializeToElement(value),through,seed);
    [Fact] public void ReviewedSourceInventoryHasSeventyDistinctContractsAndSelectablePrefixes()
    {
        Assert.Equal(70,ProtocolContractInventory.Names.Count);Assert.Equal(70,ProtocolContractInventory.Names.Distinct(StringComparer.Ordinal).Count());
        var partial=Discovery();while(partial["Tests"]!.AsArray().Count>10)partial["Tests"]!.AsArray().RemoveAt(partial["Tests"]!.AsArray().Count-1);
        Assert.Equal(10,ProtocolContractInventory.Select(JsonSerializer.SerializeToElement(partial),10,83,10).Count);
        Assert.Throws<InvalidDataException>(()=>Select(partial,10));
        Assert.Throws<InvalidDataException>(()=>ProtocolContractInventory.Select(JsonSerializer.SerializeToElement(Discovery()),10,83,10));
        var selected=Select(Discovery(),10);Assert.Equal(10,selected.Count);Assert.Equal(83UL,selected[ProtocolContractInventory.Names[0]]);Assert.Equal(92UL,selected[ProtocolContractInventory.Names[9]]);
    }
    [Fact] public void ProducerCannotRenameANumberedContractOrChangeStageAndCaseCoordinates()
    {
        foreach(string field in new[]{"QualifiedName","Stage","CaseIndex"})
        {
            var value=Discovery();var row=value["Tests"]![0]!;
            if(field=="QualifiedName")row[field]="Entry.R001_ProducerInvented";else row[field]=1;
            Assert.Throws<InvalidDataException>(()=>Select(value));
        }
    }
    [Fact] public void FullOrderSeedDriftRejectsEvenWhenOutsideTheRequestedPrefix()
    {var value=Discovery();value["Tests"]![69]!["Seed"]=0UL;Assert.Throws<InvalidDataException>(()=>Select(value,10));}
    [Fact] public void IndependentSeedArithmeticPreservesUnsignedWraparound()
    {var selected=Select(Discovery(ulong.MaxValue),70,ulong.MaxValue);Assert.Equal(ulong.MaxValue,selected[ProtocolContractInventory.Names[0]]);Assert.Equal(0UL,selected[ProtocolContractInventory.Names[1]]);Assert.Equal(68UL,selected[ProtocolContractInventory.Names[69]]);}
}