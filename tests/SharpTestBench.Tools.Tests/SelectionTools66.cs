using System.Security.Cryptography;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

/// <summary>Export-review rejection boundaries use isolated literal source, never imported simulator packages.</summary>
public sealed class SelectionExportTests
{
    sealed class Fixture:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"stb-export-review-"+Guid.NewGuid().ToString("N"));
        internal string Source=>Path.Combine(Root,"cocotb/__init__.py");
        internal string Review=>Path.Combine(Root,"review.json");
        internal JsonObject Document;
        internal Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Root,"cocotb"));Directory.CreateDirectory(Path.Combine(Root,"third_party"));
            File.WriteAllText(Source,"__all__ = ['Alpha', 'Beta']\nraise RuntimeError('must not import')\n");File.WriteAllText(Path.Combine(Root,"evidence.cs"),"public class Alpha {} // reviewed exposure\n");
            File.WriteAllText(Path.Combine(Root,"third_party/UPSTREAM.json"),"{\"commit\":\""+new string('a',40)+"\"}");
            Document=new(){["schema_version"]=1,["source_revision"]=new string('a',40),["source_hashes"]=new JsonObject{["cocotb"]=Hash()},["reviews"]=new JsonArray(new JsonObject{
                ["module"]="cocotb",["export"]="Alpha",["classification"]="intentional-difference",["reason"]="Controlled public exposure",["evidence"]=new JsonArray(new JsonObject{["path"]="evidence.cs",["contains"]="reviewed exposure"})})};
        }
        internal string Hash()=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Source))).ToLowerInvariant();
        internal JsonObject Entry=>Document["reviews"]![0]!.AsObject();
        internal void Check(){File.WriteAllText(Review,Document.ToJsonString());_ = ExportAuditCommand.Validate(Root,Root,Review);}
        public void Dispose(){if(Path.GetDirectoryName(Root)!=Path.TrimEndingDirectorySeparator(Path.GetTempPath()))throw new InvalidOperationException("Invalid owned temp root");Directory.Delete(Root,true);}
    }
    [Fact] public void S063_PinnedLiteralExportsAreParsedWithoutImportAndHashesAreMandatory()
    {
        using(var f=new Fixture())f.Check();
        using(var f=new Fixture()){File.AppendAllText(f.Source,"\nfor name in __all__:\n pass\n");f.Document["source_hashes"]!["cocotb"]=f.Hash();f.Check();}
        using(var f=new Fixture()){f.Document["source_revision"]=new string('b',40);Assert.Throws<InvalidDataException>(f.Check);}
        using(var f=new Fixture()){File.AppendAllText(f.Source,"# changed pinned source\n");Assert.Throws<InvalidDataException>(f.Check);}
    }
    [Fact] public void S064_UnknownDuplicateAndUnclassifiedPublicExposuresReject()
    {
        using(var f=new Fixture()){f.Entry["export"]="Unknown";Assert.Throws<InvalidDataException>(f.Check);}
        using(var f=new Fixture()){f.Document["reviews"]!.AsArray().Add(f.Entry.DeepClone());Assert.Throws<InvalidDataException>(f.Check);}
        using(var f=new Fixture()){f.Entry["classification"]="parity-proven";Assert.Throws<InvalidDataException>(f.Check);}
        using(var f=new Fixture()){f.Entry["reason"]=" ";Assert.Throws<InvalidDataException>(f.Check);}
    }
    [Fact] public void S065_StaleMissingAndEscapedEvidenceCannotValidateAnExposure()
    {
        foreach(string path in new[]{"missing.cs","../outside.cs",Path.GetFullPath("outside.cs")}){using var f=new Fixture();f.Entry["evidence"]![0]!["path"]=path;Assert.Throws<InvalidDataException>(f.Check);}
        using(var f=new Fixture()){f.Entry["evidence"]![0]!["contains"]="stale anchor";Assert.Throws<InvalidDataException>(f.Check);}
        using(var f=new Fixture()){f.Entry["evidence"]![0]!["contains"]=" ";Assert.Throws<InvalidDataException>(f.Check);}
        using(var f=new Fixture()){f.Entry["module"]="cocotb/../../escape";f.Document["source_hashes"]=new JsonObject{["cocotb/../../escape"]=f.Hash()};Assert.Throws<InvalidDataException>(f.Check);}
    }
    [Fact] public void S066_MalformedDynamicDuplicateAndEmptyExportDeclarationsReject()
    {
        foreach(string text in new[]{"__all__ = ['Alpha'", "__all__ = make_exports()", "__all__=['Alpha','Alpha']", "__all__=[]", "__all__=['Alpha']\n__all__=['Beta']", "__all__=[7]", "__all__=alias=[\"Alpha\"]", "__all__=[\"Alpha\"]\n__all__.append(\"Beta\")", "__all__=[\"Alpha\"]\n__all__[0]=\"Beta\"", "__all__=[\"Alpha\"]\nalias=__all__", "__all__=['Alpha']\n__all__ += ['Beta']", "__all__=['Alpha']\nif True: __all__=['Beta']", "__all__=['Alpha']\ndel __all__"})
        {using var f=new Fixture();File.WriteAllText(f.Source,text);f.Document["source_hashes"]!["cocotb"]=f.Hash();Assert.Throws<InvalidDataException>(f.Check);}
        using(var f=new Fixture()){f.Document["schema_version"]=2;Assert.Throws<InvalidDataException>(f.Check);}
    }
}