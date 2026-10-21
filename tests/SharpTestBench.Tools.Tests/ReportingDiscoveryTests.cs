using SharpTestBench.Tools;
using Xunit;

public sealed class ReportingDiscoveryTests
{
    sealed class Fixture:IDisposable
    {
        public string Root{get;}=Path.Combine(Path.GetTempPath(),"stb-report-discovery-"+Guid.NewGuid().ToString("N"));
        public RunManifest Config{get;}=new(1,"icarus","top",["input.sv"],"fixture.dll","host","boot","gpi","sdk","native",GpiImpl:"impl:entry",GpiEntryPoint:"entry");
        public Fixture()
        {
            Directory.CreateDirectory(Root);
            foreach(var name in new[]{"manifest.json","input.sv","fixture.dll","fixture.runtimeconfig.json","host","boot","gpi","impl"})File.WriteAllText(Path.Combine(Root,name),"controlled bytes; no native execution");
            var fxr=Path.Combine(Root,"sdk/host/fxr/10.0.0");Directory.CreateDirectory(fxr);File.WriteAllText(Path.Combine(fxr,NativeBuildCommand.FxrName),"controlled hostfxr");
        }
        public InputFingerprint[] Capture()=>PairedInputs.Capture(Root,Config);
        public void Discovery(InputFingerprint[] inputs,string status="verified")=>StrictJson.Write(Path.Combine(Root,"discovery-input-manifest.json"),new {schemaVersion=1,status,inputs});
        public void Dispose(){if(Path.GetDirectoryName(Root)!=Path.TrimEndingDirectorySeparator(Path.GetTempPath()))throw new InvalidOperationException("Invalid report-discovery temp root");Directory.Delete(Root,true);}
    }
    [Fact] public void ReportingSeedProducerRejectsAssemblyMutationBeforePairCapture()
    {
        using var f=new Fixture();f.Discovery(f.Capture());File.AppendAllText(Path.Combine(f.Root,"fixture.dll"),"changed assembly");
        Assert.Throws<InvalidDataException>(()=>ReportingCheckCommand.VerifyDiscovery(f.Root,f.Capture()));
    }
    [Fact] public void ReportingSeedProducerRejectsNewManagedDependencyBeforePairCapture()
    {
        using var f=new Fixture();f.Discovery(f.Capture());File.WriteAllText(Path.Combine(f.Root,"new dependency.dll"),"changed dependency membership");
        Assert.Throws<InvalidDataException>(()=>ReportingCheckCommand.VerifyDiscovery(f.Root,f.Capture()));
    }
    [Fact] public void ReportingProducerAllowsDifferentInvocationButRequiresVerifiedInputs()
    {
        using var f=new Fixture();var pair=f.Capture();
        f.Discovery(pair.Select(item=>item.Role=="manifest"?item with {Path="discovery invocation",Sha256=new string('A',64)}:item).ToArray());
        ReportingCheckCommand.VerifyDiscovery(f.Root,pair);
        f.Discovery(pair,"captured");Assert.Throws<InvalidDataException>(()=>ReportingCheckCommand.VerifyDiscovery(f.Root,pair));
    }
}
