using System.Text;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;
/// <summary>Simulator evidence retains UTF-8 markers and rejects incomplete, truncated or inconsistent streams.</summary>
public sealed class SelectionCaptureTests
{
    sealed class Capture:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"stb-selection-capture-"+Guid.NewGuid().ToString("N"));
        internal readonly bool Native;internal JsonObject Header;
        internal string Output=>Path.Combine(Root,Native?"simulate.stdout.log":"legacy-simulate.stdout.log");
        internal Capture(bool native)
        {
            Native=native;Directory.CreateDirectory(Root);byte[] bytes=Encoding.UTF8.GetBytes("SELECTION_BODY_ENTERED:Probe.Ω\n");File.WriteAllBytes(Output,bytes);File.WriteAllText(Path.Combine(Root,native?"simulate.stderr.log":"legacy-simulate.stderr.log"),"");
            Header=native?new JsonObject{["SchemaVersion"]=1,["Phase"]="simulate",["MaxLogBytes"]=1024,["Stdout"]=new JsonObject{["ObservedBytes"]=bytes.Length,["RetainedBytes"]=bytes.Length,["Truncated"]=false},["Stderr"]=new JsonObject{["ObservedBytes"]=0,["RetainedBytes"]=0,["Truncated"]=false}}:new JsonObject{["schemaVersion"]=1,["maximumCaptureBytes"]=1024,["status"]="completed",["captureSettled"]=true,["diagnosticsComplete"]=true,["stdoutLimited"]=false,["stderrLimited"]=false};
        }
        internal string Read(){File.WriteAllText(Path.Combine(Root,Native?"simulate-process.json":"legacy-simulate-capture.json"),Header.ToJsonString());return SelectionScenarioEvidence.ReadCapturedOutput(Root,Native);}
        public void Dispose(){if(Path.GetDirectoryName(Root)!=Path.TrimEndingDirectorySeparator(Path.GetTempPath()))throw new InvalidOperationException("Invalid owned capture root");Directory.Delete(Root,true);}
    }
    [Fact] public void BothFrontEndsUseRetainedUtf8SimulatorMarkers()
    {foreach(bool native in new[]{false,true}){using var capture=new Capture(native);SelectionScenarioEvidence.VerifyBodies(capture.Read(),["Probe.Ω"]);}}
    [Fact] public void TruncationCounterDriftMissingInvalidAndOversizedStreamsReject()
    {
        using(var c=new Capture(true)){c.Header["SchemaVersion"]=2;Assert.Throws<InvalidDataException>(()=>c.Read());}
        using(var c=new Capture(true)){c.Header["Phase"]="compile";Assert.Throws<InvalidDataException>(()=>c.Read());}
        using(var c=new Capture(true)){c.Header["Stdout"]!["Truncated"]=true;Assert.Throws<InvalidDataException>(()=>c.Read());}
        using(var c=new Capture(true)){c.Header["Stdout"]!["RetainedBytes"]=0;Assert.Throws<InvalidDataException>(()=>c.Read());}
        using(var c=new Capture(true)){c.Header["Stdout"]!["ObservedBytes"]=0;Assert.Throws<InvalidDataException>(()=>c.Read());}
        using(var c=new Capture(false)){c.Header["captureSettled"]=false;Assert.Throws<InvalidDataException>(()=>c.Read());}
        using(var c=new Capture(false)){c.Header["stdoutLimited"]=true;Assert.Throws<InvalidDataException>(()=>c.Read());}
        using(var c=new Capture(false)){c.Header["maximumCaptureBytes"]=1;Assert.Throws<InvalidDataException>(()=>c.Read());}
        using(var c=new Capture(false)){File.WriteAllBytes(c.Output,[0xff]);Assert.Throws<DecoderFallbackException>(()=>c.Read());}
        using(var c=new Capture(false)){File.Delete(c.Output);Assert.Throws<FileNotFoundException>(()=>c.Read());}
    }
}