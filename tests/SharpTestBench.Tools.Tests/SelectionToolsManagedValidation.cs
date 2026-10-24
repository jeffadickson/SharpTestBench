using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using SharpTestBench.Tools;
using Xunit;
/// <summary>Managed validation accepts discovered inventories and rejects missing cases or misattributed mutation failures.</summary>
public sealed class SelectionManagedValidationTests
{
    static readonly ManagedTestsCommand.Case[] Cases=[new("ManagedTests","IntegerBoundaries","ManagedTests.IntegerBoundaries"),new("ManagedTests","FunctionalCoverage","ManagedTests.FunctionalCoverage"),new("AddedCases","NewContract","AddedCases.NewContract")];
    static string Discovery(IEnumerable<ManagedTestsCommand.Case>? cases=null)
    {
        var rows=new JsonArray();int index=0;foreach(var item in cases??Cases)rows.Add((JsonNode)new JsonObject{["ID"]="id-"+index++,["Class"]=item.Class,["Method"]=item.Method,["DisplayName"]=item.Name});return rows.ToJsonString();
    }
    static XDocument Report(IEnumerable<(ManagedTestsCommand.Case Case,string Status)> values)
    {
        var rows=values.ToArray();
        var collection=new XElement("collection",rows.Select(row=>new XElement("test",
            new XAttribute("type",row.Case.Class),new XAttribute("method",row.Case.Method),
            new XAttribute("name",row.Case.Name),new XAttribute("result",row.Status))));
        var assembly=new XElement("assembly",new XAttribute("errors",0),new XAttribute("not-run",0),
            new XAttribute("total",rows.Length),new XAttribute("passed",rows.Count(row=>row.Status=="Pass")),
            new XAttribute("failed",rows.Count(row=>row.Status=="Fail")),new XAttribute("skipped",rows.Count(row=>row.Status=="Skip")),collection);
        return new XDocument(new XElement("assemblies",assembly));
    }
    static void Check(XDocument report,bool mutation=false,ManagedTestsCommand.Case[]? cases=null)
    {
        string file=Path.Combine(Path.GetTempPath(),"stb-managed-validation-"+Guid.NewGuid().ToString("N")+".xml");
        try{report.Save(file);ManagedTestsCommand.Verify(file,ManagedTestsCommand.Inventory(Discovery(cases)),mutation);}finally{File.Delete(file);}
    }
    [Fact] public void DiscoveryBackedValidationDoesNotAssumeHistoricalCaseCount()
    {
        var expected=ManagedTestsCommand.Inventory("\u001b[0m"+Discovery()+"\n\u001b[0m");Assert.Equal(3,expected.Count);
        var theories=Cases.Concat(new[]{new ManagedTestsCommand.Case("TheoryFixture","Boundary","TheoryFixture.Boundary(width: 1)"),new ManagedTestsCommand.Case("TheoryFixture","Boundary","TheoryFixture.Boundary(width: 7)")});
        Assert.Equal(5,ManagedTestsCommand.Inventory(Discovery(theories)).Count);
        Check(Report(Cases.Select(item=>(item,"Pass"))));Check(Report(Cases.Select((item,index)=>(item,index==0?"Fail":"Pass"))),true);
    }
    [Fact] public void MissingExtraAndDuplicateManagedResultsReject()
    {
        Assert.Throws<InvalidDataException>(()=>Check(Report(Cases.Take(2).Select(item=>(item,"Pass")))));
        Assert.Throws<InvalidDataException>(()=>Check(Report(Cases.Append(Cases[0]).Select(item=>(item,"Pass")))));
        var extra=new ManagedTestsCommand.Case("Other","Injected","Other.Injected");Assert.Throws<InvalidDataException>(()=>Check(Report(Cases.Append(extra).Select(item=>(item,"Pass")))));
    }
    [Fact] public void MutationMustFailOnlyTheNamedBoundaryAndRetainEveryOtherPass()
    {
        Assert.Throws<InvalidDataException>(()=>Check(Report(Cases.Select(item=>(item,"Pass"))),true));
        Assert.Throws<InvalidDataException>(()=>Check(Report(Cases.Select((item,index)=>(item,index==1?"Fail":"Pass"))),true));
        Assert.Throws<InvalidDataException>(()=>Check(Report(Cases.Select((item,index)=>(item,index<2?"Fail":"Pass"))),true));
        Assert.Throws<InvalidDataException>(()=>Check(Report(Cases.Select((item,index)=>(item,index==2?"Skip":"Pass")))));
    }
    [Fact] public void DiscoveryRejectsEmptyDuplicateAndMissingTargetInventories()
    {
        Assert.Throws<InvalidDataException>(()=>ManagedTestsCommand.Inventory("[]"));Assert.Throws<InvalidDataException>(()=>ManagedTestsCommand.Inventory(Discovery(Cases.Skip(1))));
        Assert.Throws<InvalidDataException>(()=>ManagedTestsCommand.Inventory(Discovery(Cases.Append(Cases[0]))));
        var rows=JsonNode.Parse(Discovery())!.AsArray();rows[1]!["ID"]=rows[0]!["ID"]!.GetValue<string>();Assert.Throws<InvalidDataException>(()=>ManagedTestsCommand.Inventory(rows.ToJsonString()));
    }
    [Fact] public void AssemblyErrorsCounterDriftAndDtdCannotProduceManagedSuccess()
    {
        foreach(string field in new[]{"errors","not-run","total","passed","failed","skipped"})
        {var report=Report(Cases.Select(item=>(item,"Pass")));report.Root!.Element("assembly")!.SetAttributeValue(field,999);Assert.Throws<InvalidDataException>(()=>Check(report));}
        var orphaned=Report(Cases.Select(item=>(item,"Pass")));var misplaced=orphaned.Descendants("test").First();misplaced.Remove();orphaned.Root!.Add(misplaced);
        Assert.Throws<InvalidDataException>(()=>Check(orphaned));
        var nested=Report(Cases.Select(item=>(item,"Pass")));var row=nested.Descendants("test").First();row.ReplaceWith(new XElement("wrapper",new XElement(row)));
        Assert.Throws<InvalidDataException>(()=>Check(nested));
        string path=Path.Combine(Path.GetTempPath(),"stb-managed-dtd-"+Guid.NewGuid().ToString("N")+".xml");
        try{File.WriteAllText(path,"<!DOCTYPE assemblies [<!ENTITY x 'not allowed'>]><assemblies/>");Assert.Throws<XmlException>(()=>ManagedTestsCommand.Verify(path,ManagedTestsCommand.Inventory(Discovery()),false));}finally{File.Delete(path);}
    }
    [Fact] public void XmlDisplayNamesRequireThePinnedXunitEscapingExactlyOnce()
    {
        var raw=new ManagedTestsCommand.Case("TheoryFixture","Quoted","TheoryFixture.Quoted(value: \"\\n\")");
        var encoded=raw with{Name="TheoryFixture.Quoted(value: \\\"\\\\n\\\")"};
        var inventory=Cases.Append(raw).ToArray();
        Check(Report(Cases.Append(encoded).Select(item=>(item,"Pass"))),cases:inventory);
        Assert.Throws<InvalidDataException>(()=>Check(Report(inventory.Select(item=>(item,"Pass"))),cases:inventory));
        var doubleEncoded=raw with{Name="TheoryFixture.Quoted(value: \\\\\\\"\\\\\\\\n\\\\\\\")"};
        Assert.Throws<InvalidDataException>(()=>Check(Report(Cases.Append(doubleEncoded).Select(item=>(item,"Pass"))),cases:inventory));
    }
}