using System.Text.Json;
using System.Globalization;
namespace SharpTestBench.Tools;

// Local qualification command. It never dispatches hosted workflows.
internal static class ContractsCiCommand
{
    internal static Dictionary<string,ulong> Select(JsonElement discovery,int through,int maximum=100)
    {
        if(maximum is <1 or >100)throw new ArgumentOutOfRangeException(nameof(maximum));
        if(through<1||through>maximum)throw new ArgumentOutOfRangeException(nameof(through));
        if(discovery.ValueKind!=JsonValueKind.Object||!discovery.TryGetProperty("Tests",out var tests)||tests.ValueKind!=JsonValueKind.Array)
            throw new InvalidDataException("Contract discovery requires Tests.");
        var result=new Dictionary<string,ulong>(StringComparer.Ordinal);var numbers=new HashSet<int>();var identities=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in tests.EnumerateArray())
        {
            var name=ParityContracts.Identity(row);
            if(!identities.Add(name)||!row.TryGetProperty("Seed",out var seed)||seed.ValueKind!=JsonValueKind.Number||!seed.TryGetUInt64(out ulong value))throw new InvalidDataException("Invalid contract identity or seed.");
            if(!name.StartsWith("Entry.R",StringComparison.Ordinal)||name.Length<11||name[10]!='_'||!int.TryParse(name.AsSpan(7,3),NumberStyles.None,CultureInfo.InvariantCulture,out int number)||number<1||number>maximum)
                throw new InvalidDataException("Unknown contract discovery identity.");
            if(!numbers.Add(number))throw new InvalidDataException("Duplicate contract number.");
            if(number<=through)result.Add(name,value);
        }
        if(result.Count!=through)throw new InvalidDataException("Incomplete requested contract set.");
        return result;
    }
    internal static Dictionary<string,ulong> SelectRegressions(JsonElement discovery)
    {
        string[] names=["Tests.AO_PrimitiveFairnessStress","Tests.AP_MultipleFailureTeardownStress","Tests.AM_CancellationContract","Tests.AL_CompletionTriggers",
            "Tests.AB_AggregateAndNestedGroups","Tests.AA_TypedJobResults","TriggerExtensionTests.ExternalTriggerComposition","TriggerExtensionTests.ExternalTriggerLifecycle"];
        return names.ToDictionary(name=>name,name=>ManifestCheckCommand.SelectionSeed(discovery,name),StringComparer.Ordinal);
    }
    internal static (string Project,string Top,int Count) ContractSuite(string suite) => suite switch
    {
        "requirements" => ("RequirementContracts","contract_top",100),
        "closure" => ("ClosureContracts","closure_top",70),
        "conformance" => ("ConformanceContracts","conformance_top",100),
        _ => throw new ArgumentException("Unknown contract suite", nameof(suite))
    };
    public static void Execute(ToolContext context,string[] arguments)
    {
        var options=new Options(arguments,"--integration-regressions");options.Only("--through","--seed","--integration-regressions","--suite");
        bool integration=options.Has("--integration-regressions");
        if(integration&&options.Get("--through") is not null)throw new ArgumentException("Integration regression selection does not accept --through.");
        if(integration&&options.Has("--suite"))throw new ArgumentException("Integration regressions do not accept --suite.");
        var suite=ContractSuite(options.Get("--suite","requirements")!);
        bool closure=suite.Project=="ClosureContracts";
        bool conformance=suite.Project=="ConformanceContracts";
        if(!int.TryParse(options.Get("--through",suite.Count.ToString(CultureInfo.InvariantCulture)),NumberStyles.None,CultureInfo.InvariantCulture,out int through)||through<1||through>suite.Count)throw new ArgumentException("Invalid contract milestone.");
        if(!ulong.TryParse(options.Get("--seed","83"),NumberStyles.None,CultureInfo.InvariantCulture,out ulong seed))throw new ArgumentException("Invalid contract seed.");
        string project=integration?"Integration":suite.Project;
        string top=integration?"top":suite.Top;
        var name=integration?$"requirements100-integration-{seed}":conformance?$"conformance200-contracts-{through}-{seed}":closure?$"closure100-contracts-{through}-{seed}":$"requirements100-{through}-{seed}";
        ParityCiCommand.InWorkspace(context,name,(work,environment)=>{
            context.Run(["dotnet","build",context.At("tests/"+project),"-c","Release","-p:RestoreLockedMode=true","--disable-build-servers","-m:1"],environment:environment);
            NativeBuildCommand.Build(context);
            var manifest=RunnerManifestCommand.Make(context.Root,"counter",seed,RunnerManifestCommand.InstalledAdapter(context),Path.Combine(work,"native"));
            manifest["top"]=top;manifest["sources"]=new[]{context.At("tests/"+project+"/"+top+".sv")};
            manifest["assembly"]=context.At("tests/"+project+"/bin/Release/net10.0/"+project+".dll");manifest["entryType"]="Entry, "+project;
            StrictJson.Write(Path.Combine(work,"manifest.json"),manifest);
            context.Run([..ToolContext.Self,"run","--sim","icarus","--no-build","--assembly",(string)manifest["assembly"],"--source",((string[])manifest["sources"])[0],"--top",(string)manifest["top"],"--entry-type",(string)manifest["entryType"],"--seed",seed.ToString(CultureInfo.InvariantCulture),"--work-dir",Path.Combine(work,"discovery"),"--label","discovery","--list"],environment:environment);
            using var discovery=StrictJson.Read(Path.Combine(work,"discovery/discovery-results.discovery.json"));
            var selected=integration?SelectRegressions(discovery.RootElement):Select(discovery.RootElement,through,suite.Count);
            manifest["testNames"]=selected.Keys.ToArray();manifest["expectedNames"]=selected.Keys.ToArray();manifest["expectedTests"]=selected.Count;manifest["expectedSeeds"]=selected;
            StrictJson.Write(Path.Combine(work,"manifest.json"),manifest);
            var inputs=PairedInputs.Capture(work,ParityLegacyCommand.Read(manifest));
            using var producer=StrictJson.Read(Path.Combine(work,"discovery/discovery-input-manifest.json"));ManifestCheckCommand.VerifyDiscoveryInputs(producer.RootElement,inputs);
            var report=ParityCiCommand.Compare(context,work,manifest,seed,environment,null,null,null,null);
            report["contracts"]=selected.Count;report["fixture"]=integration?"integration-regressions":conformance?"conformance-contracts":closure?"closure-contracts":"requirement-contracts";return report;
        });
    }
}
