using System.Text.Json;
using SharpTestBench.Fixtures;
using SharpTestBench.Tools;
using Xunit;

public sealed class FrameOrchestrationTests
{
    // Controlled execution only. Replay payload was separately qualified against RTL at160.
    sealed class Fixture:IDisposable,ICommandRunner
    {
        public string Root{get;}=Path.Combine(Path.GetTempPath(),"stb-frame-parent-"+Guid.NewGuid().ToString("N"));
        public string Work=>Path.Combine(Root,"build/frame-frame_d4_p0_o0-83-16");
        public string Mode{get;set;}="oracle";
        public int Workers{get;private set;}
        public int Oracles{get;private set;}
        readonly JsonDocument golden;
        public Fixture()
        {
            var repo=ToolContext.FindRoot();
            golden=JsonDocument.Parse(File.ReadAllText(Path.Combine(repo,"tests/Tooling/data/frame_fifo_recorded.json")));
            foreach(var name in new[]{"native/host.cpp","native/abi.cpp","native/transport.inc","native/clocks.inc","native/abi.h","native/platform.h",
                "tests/Fixtures/verilog-fifo/axis_fifo.v","tests/Fixtures/verilog-fifo/COPYING","tests/Fixtures/verilog-fifo/provenance.json",
                "tests/Tooling/frame_reference.py","tests/Tooling/fifo_reference.py"})
            { var target=Path.Combine(Root,name);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(Path.Combine(repo,name),target); }
            foreach(var name in new[]{"examples/FrameFifo/bin/Release/net10.0/FrameFifo.dll","examples/FrameFifo/bin/Release/net10.0/FrameFifo.runtimeconfig.json","build/adapter/boot","build/adapter/gpi","build/adapter/impl"})
            { var target=Path.Combine(Root,name);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.WriteAllText(target,"controlled bytes; not executable"); }
            Directory.CreateDirectory(Work);File.WriteAllText(Path.Combine(Work,"unrelated.keep"),"preserve");
            StrictJson.Write(Path.Combine(Work,"parity.json"),new {schemaVersion=1,matched=true});
            Assert.True(JsonElement.DeepEquals(golden.RootElement.GetProperty("plan"),JsonSerializer.SerializeToElement(FrameFixture.Plan(new(4,0,false),83,16))));
        }
        public CommandResult Run(IReadOnlyList<string> args,string directory,IReadOnlyDictionary<string,string?>? environment=null,bool capture=false,bool check=true,TimeSpan? timeout=null)
        {
            if(args.Count>1&&args[1]=="build")return new(Mode=="managed"?7:0,"controlled build","");
            if(args.Contains("--version"))return new(0,"controlled GNU compiler","");
            if(args.Contains("-DSTB_HOST_BUILD"))
            {
                if(Mode=="native-build")return new(7,"","controlled native build failure");
                File.WriteAllText(args[^1],"controlled native host");return new(0,"","");
            }
            if(args.Count>1&&args[1]=="-c")
            {
                var adapter=Path.Combine(Root,"build/adapter");
                return new(0,JsonSerializer.Serialize(new CocotbAdapter("controlled","python",adapter,Path.Combine(adapter,"boot"),Path.Combine(adapter,"gpi")+",entry",Path.Combine(adapter,"impl")+":entry",null,null)),"");
            }
            if(args.Count>1&&args[1].EndsWith("frame_reference.py",StringComparison.Ordinal))
            {
                Oracles++;
                if(Mode=="oracle-dependency")File.AppendAllText(Path.Combine(Root,"tests/Tooling/fifo_reference.py"),"\n# controlled mutation\n");
                return new(Mode=="oracle"?7:0,"controlled oracle status","");
            }
            Workers++;bool legacy=args.Contains("--label");
            if(Mode==(legacy?"legacy":"native"))throw new IOException("controlled "+Mode+" launch failure");
            var folder=Path.Combine(Work,legacy?"legacy":"native");Directory.CreateDirectory(folder);
            StrictJson.Write(Path.Combine(folder,legacy?"legacy-results.json":"icarus-results.json"),new[]{new {QualifiedName="Entry.FrameTransactions",Name="FrameTransactions",Status="passed",Seed=83UL}});
            if(Mode!="missing-metrics")File.WriteAllText(Path.Combine(folder,"metrics.json"),Mode=="malformed-metrics"?"{}":golden.RootElement.GetProperty("metrics").GetRawText());
            if(!legacy&&Mode=="plan")File.AppendAllText(Path.Combine(Work,"plan.json")," ");
            return new(0,"controlled worker","");
        }
        public Exception Fail()
        {
            var error=Record.Exception(()=>FrameCiCommand.Run(new ToolContext(Root,this),new FrameConfig(4,0,false),83,16,false));
            Assert.NotNull(error);Assert.False(File.Exists(Path.Combine(Work,".parity.lock")));
            if(File.Exists(Path.Combine(Work,"parity.json")))
            {using var report=StrictJson.Read(Path.Combine(Work,"parity.json"));Assert.False(report.RootElement.GetProperty("matched").GetBoolean());}
            using var failure=StrictJson.Read(Path.Combine(Work,"case-failure.json"));Assert.Equal("failed",failure.RootElement.GetProperty("status").GetString());
            Assert.Equal("preserve",File.ReadAllText(Path.Combine(Work,"unrelated.keep")));return error;
        }
        public void Dispose(){golden.Dispose();if(Path.GetDirectoryName(Root)!=Path.TrimEndingDirectorySeparator(Path.GetTempPath()))throw new InvalidOperationException("Invalid parent fixture root");Directory.Delete(Root,true);}
    }
    [Fact] public void ManagedBuildFailureCannotLeaveSuccessfulParityCompletion(){using var f=new Fixture{Mode="managed"};Assert.Contains("fixture build failed",f.Fail().Message);Assert.Equal(0,f.Workers);}
    [Fact] public void NativeBuildFailureStopsBeforeWorkerLaunch(){using var f=new Fixture{Mode="native-build"};Assert.Contains("Native build failed",f.Fail().Message);Assert.Equal(0,f.Workers);}
    [Fact] public void LegacyLaunchFailurePreventsNativeLaunch(){using var f=new Fixture{Mode="legacy"};Assert.Contains("legacy launch failure",f.Fail().Message);Assert.Equal(1,f.Workers);}
    [Fact] public void NativeLaunchFailureCannotAcceptPriorPassingLegacyRows(){using var f=new Fixture{Mode="native"};Assert.Contains("native launch failure",f.Fail().Message);Assert.Equal(2,f.Workers);}
    [Fact] public void PassingResultRowsCannotQualifyWithoutMetrics(){using var f=new Fixture{Mode="missing-metrics"};Assert.IsType<FileNotFoundException>(f.Fail());Assert.Equal(0,f.Oracles);}
    [Fact] public void MatchingMalformedMetricsCannotReachOracleOrSuccess(){using var f=new Fixture{Mode="malformed-metrics"};Assert.IsType<InvalidDataException>(f.Fail());Assert.Equal(0,f.Oracles);}
    [Fact] public void IndependentOracleFailureRejectsOtherwiseQualifiedReplay(){using var f=new Fixture{Mode="oracle"};Assert.Contains("Independent frame oracle failed",f.Fail().Message);Assert.Equal(1,f.Oracles);}
    [Fact] public void PlanByteMutationAfterWorkersCannotHideBehindEquivalentJson(){using var f=new Fixture{Mode="plan"};Assert.Contains("stimulus, wrapper, configuration or oracle changed",f.Fail().Message);Assert.Equal(2,f.Oracles);}
    [Fact] public void ImportedOracleDependencyMutationCannotQualify(){using var f=new Fixture{Mode="oracle-dependency"};Assert.Contains("Oracle source or declared dependency changed",f.Fail().Message);Assert.Equal(2,f.Oracles);}
}
