using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;
/// <summary>Recipe attestation checks expected failures without relaxing required-pass parity contracts.</summary>
public sealed class SelectionScenarioTests
{
    static JsonArray Rows(string status="passed",ulong seed=83)=>new(new JsonObject{["Name"]="Case",["QualifiedName"]="Probe.Case",["Status"]=status,["Seed"]=seed,["Error"]=status=="passed"?null:"controlled reason"});
    static JsonElement Element(JsonArray rows)=>JsonSerializer.SerializeToElement(rows);
    static readonly SelectionScenarioEvidence.Expected[] Passing=[new("Probe.Case",83,"passed")];
    const string Body="SELECTION_BODY_ENTERED:Probe.Case\n";
    [Fact] public void S067_ControlledPassFailureAndSkipRecipesRequireTheirOwnStatusAndReason()
    {
        foreach(string status in new[]{"passed","failed","skipped"})
        {
            int code=status=="failed"?1:0;var expected=new[]{new SelectionScenarioEvidence.Expected("Probe.Case",83,status,status=="passed"?null:"controlled reason")};
            SelectionScenarioEvidence.Verify(Element(Rows(status)),expected,code,code,status=="skipped"?"":Body,status=="skipped"?[]:["Probe.Case"]);
            Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.Verify(Element(Rows(status)),new[]{new SelectionScenarioEvidence.Expected("Probe.Case",83,status,"wrong reason")},code,code,"",[]));
        }
    }
    [Fact] public void S068_ExactIndependentIdentityAndSeedInventoryRejectsMissingExtraAndDuplicateRows()
    {
        var missing=new JsonArray();Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.Verify(Element(missing),Passing,0,0,Body,["Probe.Case"]));
        var duplicate=Rows();duplicate.Add(duplicate[0]!.DeepClone());Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.Verify(Element(duplicate),Passing,0,0,Body,["Probe.Case"]));
        var unknown=Rows();unknown[0]!["QualifiedName"]="Other.Case";Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.Verify(Element(unknown),Passing,0,0,Body,["Probe.Case"]));
        foreach(JsonNode? seed in new JsonNode?[]{JsonValue.Create(84UL),JsonValue.Create(-1),JsonValue.Create("83"),null})
        {var rows=Rows();rows[0]!["Seed"]=seed;Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.Verify(Element(rows),Passing,0,0,Body,["Probe.Case"]));}
        var unqualified=Rows();unqualified[0]!.AsObject().Remove("QualifiedName");Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.Verify(Element(unqualified),Passing,0,0,Body,["Probe.Case"]));
    }
    [Fact] public void S069_PreflightHasNoBodiesAndDuplicateUnexpectedOrMissingBodyMarkersReject()
    {
        SelectionScenarioEvidence.VerifyBodies("compiler diagnostic\n",[]);
        SelectionScenarioEvidence.VerifyBodies(Body,["Probe.Case"]);
        const string second="SELECTION_BODY_ENTERED:Probe.Second\n";
        SelectionScenarioEvidence.VerifyBodies(Body+second,["Probe.Case","Probe.Second"]);
        Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.VerifyBodies(second+Body,["Probe.Case","Probe.Second"]));
        Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.VerifyBodies(Body,[]));
        Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.VerifyBodies("",["Probe.Case"]));
        Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.VerifyBodies(Body+Body,["Probe.Case"]));
        Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.VerifyBodies("SELECTION_BODY_ENTERED:Other.Case\n",["Probe.Case"]));
    }
    [Fact] public void S070_RecipeFailureAcceptanceCannotTurnRequiredSkipsIntoPassingParity()
    {
        var skipped=Element(Rows("skipped"));
        SelectionScenarioEvidence.Verify(skipped,[new("Probe.Case",83,"skipped","controlled reason")],1,1,"",[]);
        Assert.Throws<InvalidDataException>(()=>ParityContracts.Signature(skipped));
        Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.Verify(skipped,Passing,0,0,Body,["Probe.Case"]));
        Assert.Throws<InvalidDataException>(()=>SelectionScenarioEvidence.Verify(Element(Rows()),Passing,1,0,Body,["Probe.Case"]));
    }
}