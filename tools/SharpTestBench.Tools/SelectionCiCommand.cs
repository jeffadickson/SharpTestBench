using System.Globalization;
using System.Text.Json;
namespace SharpTestBench.Tools;

/// <summary>Executes source-reviewed selection recipes through both real Icarus front ends.</summary>
internal static class SelectionCiCommand
{
    internal const int CompletedRecipes=30;
    sealed record Scenario(int Number,string Name,ulong Seed,SelectionScenarioEvidence.Expected[] Cases,string[] Bodies,
        int LegacyExit=0,int NativeExit=0,bool Startup=false,bool List=false,bool NoExact=false);
    static readonly string[] ProbeNames=
    ["ProbeCases.A_Pass","ProbeCases.B_ExplicitSkip","ProbeCases.C_Backend","ProbeCases.D_Param[0]","ProbeCases.D_Param[1]",
     "ProbeCases.E_Expected","ProbeCases.F_Fail","ProbeCases.G_SkipExpectedLeak","ProbeCases.H_SkipExpected","ProbeCases.I_Case","ProbeCases.Ω_Unicode",
     "ProbeContainer+Nested.A_Same","ProbeSibling.A_Pass","ProbeLater.A_Skip","ProbeAdversarial."+new string('a',128)+"B"];
    static bool DeclaredSkip(string name)=>name is "ProbeCases.B_ExplicitSkip" or "ProbeCases.G_SkipExpectedLeak" or "ProbeCases.H_SkipExpected" or "ProbeLater.A_Skip";
    static int Ordinal(string name)
    {
        int protocol=ProtocolContractInventory.Names.ToList().IndexOf(name);if(protocol>=0)return protocol;
        int probe=Array.IndexOf(ProbeNames,name);if(probe<0)throw new InvalidDataException("Unknown source-reviewed scenario identity");return 70+probe;
    }
    static SelectionScenarioEvidence.Expected E(string name,ulong seed=83,string status="passed",string? error=null)=>new(name,unchecked(seed+(ulong)Ordinal(name)),status,error);
    static Scenario Recipe(int number)
    {
        const string a="ProbeCases.A_Pass",d0="ProbeCases.D_Param[0]",d1="ProbeCases.D_Param[1]",sibling="ProbeSibling.A_Pass",skip="ProbeCases.B_ExplicitSkip";
        return number switch
        {
            1=>new(number,"qualified substring search",83,[E(a),E(sibling)],[a,sibling]),
            2=>new(number,"anchored qualified identity",83,[E(a)],[a]),
            3=>new(number,"regex alternation",83,[E(a),E(sibling)],[a,sibling]),
            4=>new(number,"disjoint pattern OR",83,[E(a),E(d1)],[a,d1]),
            5=>new(number,"same short name in sibling type",83,[E(sibling)],[sibling]),
            6=>new(number,"parameter suffix selection",83,[E(d1)],[d1]),
            7=>new(number,"unsigned seed wrap including zero",ulong.MaxValue,[E(ProtocolContractInventory.Names[0],ulong.MaxValue),E(ProtocolContractInventory.Names[1],ulong.MaxValue),E(d0,ulong.MaxValue),E(d1,ulong.MaxValue)],[d0,d1]),
            8=>new(number,"invariant inline ignore-case under Turkish locale",83,[E("ProbeCases.I_Case")],["ProbeCases.I_Case"]),
            9=>new(number,"Unicode qualified identity",83,[E("ProbeCases.Ω_Unicode")],["ProbeCases.Ω_Unicode"]),
            10=>new(number,"qualified lookbehind",83,[E(a)],[a]),
            11=>new(number,"negative lookahead",83,[E(a),E(d0),E(d1)],[a,d0,d1]),
            12=>new(number,"escaped nested type separator",83,[E("ProbeContainer+Nested.A_Same")],["ProbeContainer+Nested.A_Same"]),
            13=>new(number,"Testcase overrides declared skip",83,[E(skip)],[skip]),
            14=>new(number,"Testcase preserves expected exception",83,[E("ProbeCases.H_SkipExpected")],["ProbeCases.H_SkipExpected"]),
            15=>new(number,"ordinary expected exception",83,[E("ProbeCases.E_Expected")],["ProbeCases.E_Expected"]),
            16=>new(number,"stage stop still dominates Testcase",83,[E("ProbeCases.F_Fail",status:"failed",error:"SELECTION unexpected failure"),E("ProbeLater.A_Skip",status:"skipped",error:"Earlier stage 100 failed")],["ProbeCases.F_Fail"],1,1),
            17=>new(number,"unmatched OR alternative",83,[E(a)],[a]),
            18=>new(number,"unrestricted list preflight",83,[],[],0,1,List:true,NoExact:true),
            19=>new(number,"selected parameter list",83,[],[],0,1,List:true),
            20=>new(number,"Testcase list retains declared skip",83,[],[],0,1,List:true),
            21=>new(number,"no match fails without bodies",83,[new("runtime",0,"failed","No tests matched")],[],1,1,NoExact:true),
            22=>new(number,"malformed regex rejects initialization",83,[],[],1,1,Startup:true,NoExact:true),
            23=>new(number,"invalid mode rejects initialization",83,[],[],1,1,Startup:true,NoExact:true),
            24=>new(number,"invalid timeout rejects initialization",83,[],[],1,1,Startup:true,NoExact:true),
            25=>new(number,"duplicate identity rejects complete plan",83,[new("runtime",0,"failed","Duplicate qualified test identity")],[],1,1,NoExact:true),
            26=>new(number,"late regex timeout precedes all bodies",83,[new("runtime",0,"failed","RegexMatchTimeoutException")],[],1,1,NoExact:true),
            27=>new(number,"ordinary declared skip remains required-pass rejection",83,[E(skip,status:"skipped",error:"Explicit skip")],[],0,1),
            28=>new(number,"Testcase cannot authorize unavailable backend",83,[E("ProbeCases.C_Backend",status:"skipped",error:"Requires simulator")],[],0,1),
            29=>new(number,"Testcase cannot waive unexpected failure",83,[E("ProbeCases.F_Fail",status:"failed",error:"SELECTION unexpected failure")],["ProbeCases.F_Fail"],1,1),
            30=>new(number,"Testcase expected error cannot waive leak",83,[E("ProbeCases.G_SkipExpectedLeak",status:"failed",error:"Test leaked tasks or callbacks")],["ProbeCases.G_SkipExpectedLeak"],1,1),
            _=>throw new ArgumentOutOfRangeException(nameof(number))
        };
    }
    static string[] DiscoveryNames(Scenario recipe)=>recipe.Number switch
    {
        18=>ProtocolContractInventory.Names.Concat(ProbeNames).ToArray(),19=>["ProbeCases.D_Param[1]"],20=>["ProbeCases.B_ExplicitSkip"],
        21 or 22 or 23 or 24 or 25 or 26=>[],_=>recipe.Cases.Select(item=>item.Identity).ToArray()
    };
    static void VerifyDiscovery(JsonElement document,Scenario recipe)
    {
        var names=DiscoveryNames(recipe);var rows=document.GetProperty("Tests");var seen=new HashSet<string>(StringComparer.Ordinal);
        if(rows.ValueKind!=JsonValueKind.Array||rows.GetArrayLength()!=names.Length)throw new InvalidDataException("Selection discovery inventory differs from source recipe");
        foreach(var row in rows.EnumerateArray())
        {
            string name=ParityContracts.Identity(row);
            if(!names.Contains(name,StringComparer.Ordinal)||!seen.Add(name)||row.GetProperty("Seed").GetUInt64()!=unchecked(recipe.Seed+(ulong)Ordinal(name))||row.GetProperty("Skip").GetBoolean()!=DeclaredSkip(name))
                throw new InvalidDataException("Selection discovery identity/seed/declared skip differs");
            int stage=name.StartsWith("Entry.",StringComparison.Ordinal)?0:name.StartsWith("ProbeLater.",StringComparison.Ordinal)?101:name.StartsWith("ProbeAdversarial.",StringComparison.Ordinal)?102:100;
            int index=name=="ProbeCases.D_Param[1]"?1:0;
            if(row.GetProperty("Stage").GetInt32()!=stage||row.GetProperty("CaseIndex").GetInt32()!=index)throw new InvalidDataException("Selection discovery stage/case coordinate differs");
            string[] tags=row.GetProperty("Simulators").EnumerateArray().Select(value=>value.GetString()!).ToArray();
            string[] expectedTags=name=="ProbeCases.C_Backend"?["UnavailableFixtureBackend"]:[];
            if(!tags.SequenceEqual(expectedTags,StringComparer.Ordinal))throw new InvalidDataException("Selection backend declarations changed");
        }
    }
    static (int Code,string Output) Run(ToolContext context,string work,string label,string[] command,Dictionary<string,string?> environment)
    {
        var result=context.Run(command,environment:environment,capture:true,check:false,timeout:TimeSpan.FromSeconds(300));
        string text=result.Output+"\n"+result.Error;File.WriteAllText(Path.Combine(work,label+".log"),text);return(result.ExitCode,text);
    }
    static void VerifySide(string work,string folder,string stem,Scenario recipe,int code,int expectedExit,string output)
    {
        string results=Path.Combine(work,folder,stem+".json"),discovery=Path.Combine(work,folder,stem+".discovery.json");
        output=SelectionScenarioEvidence.ReadCapturedOutput(Path.Combine(work,folder),folder=="native");
        if(folder=="native")
        {
            string? rejection=recipe.Number switch{16 or 18 or 19 or 20 or 27 or 28=>"configuration_error",22 or 23 or 24=>"simulator_failure",_=>null};
            string? reason=recipe.Number switch{16 or 27 or 28=>"Skipped test cases cannot satisfy the required CI manifest",18 or 19 or 20=>"No test results",22 or 23 or 24=>"Exited with 1",_=>null};
            SelectionScenarioEvidence.VerifyNativeCompletion(Path.Combine(work,folder),recipe.Cases,recipe.Seed,rejection,reason);
        }
        if(recipe.Startup)
        {
            string kind=recipe.Number==22?"RegexParseException":"ArgumentOutOfRangeException";
            if(code!=expectedExit||!output.Contains("SELECTION_INIT_REJECTED:"+kind,StringComparison.Ordinal)||File.Exists(results)||File.Exists(discovery))throw new InvalidDataException("Startup recipe did not reject before report/body publication");
            SelectionScenarioEvidence.VerifyBodies(output,[]);return;
        }
        using var rows=StrictJson.Read(results);SelectionScenarioEvidence.Verify(rows.RootElement,recipe.Cases,code,expectedExit,output,recipe.Bodies);
        using var metadata=StrictJson.Read(discovery);VerifyDiscovery(metadata.RootElement,recipe);
    }
    static void VerifyProcessBoundaries(string work)
    {
        using(var compiler=StrictJson.Read(Path.Combine(work,"legacy/legacy-compile-process.json")))
        {
            var row=compiler.RootElement;
            if(row.GetProperty("status").GetString()!="completed"||row.GetProperty("exitCode").GetInt32()!=0||row.GetProperty("timedOut").GetBoolean()||!row.GetProperty("diagnosticsComplete").GetBoolean()||!row.GetProperty("captureSettled").GetBoolean())throw new InvalidDataException("Legacy compilation did not complete before selection recipe");
        }
        using(var capture=StrictJson.Read(Path.Combine(work,"legacy/legacy-simulate-capture.json")))
        {
            var row=capture.RootElement;
            if(row.GetProperty("status").GetString()!="completed"||row.GetProperty("exitCode").GetInt32() is not (0 or 1)||!row.GetProperty("diagnosticsComplete").GetBoolean()||!row.GetProperty("captureSettled").GetBoolean()||row.GetProperty("stdoutLimited").GetBoolean()||row.GetProperty("stderrLimited").GetBoolean())throw new InvalidDataException("Legacy simulation was interrupted rather than reaching recipe boundary");
        }
        foreach(string phase in new[]{"compile","simulate"})
        {
            using var process=StrictJson.Read(Path.Combine(work,"native",phase+"-process.json"));var row=process.RootElement;
            if(!row.GetProperty("Started").GetBoolean()||row.GetProperty("TimedOut").GetBoolean()||row.GetProperty("Stdout").ValueKind!=JsonValueKind.Object||row.GetProperty("Stderr").ValueKind!=JsonValueKind.Object||row.GetProperty("Stdout").GetProperty("Truncated").GetBoolean()||row.GetProperty("Stderr").GetProperty("Truncated").GetBoolean()||(phase=="compile"?row.GetProperty("ExitCode").GetInt32()!=0:row.GetProperty("ExitCode").GetInt32() is not (0 or 1)))
                throw new InvalidDataException("Native process failed to reach the intended completed phase boundary");
        }
    }
    static void VerifyProducerInputs(string work,InputFingerprint[] expected,Scenario recipe)
    {
        foreach(bool legacy in new[]{true,false})
        {
            string path=legacy?Path.Combine(work,"legacy/legacy-input-manifest.json"):Path.Combine(work,"native/input-manifest.json");
            using var document=StrictJson.Read(path);var root=document.RootElement;
            if(root.GetProperty(legacy?"schemaVersion":"SchemaVersion").GetInt32()!=1)throw new InvalidDataException("Producer input evidence version differs");
            if(legacy)
            {
                string? status=root.GetProperty("status").GetString();
                if(status!="verified"&&!(recipe.LegacyExit!=0&&status=="captured"))throw new InvalidDataException("Legacy input verification did not reach its expected boundary");
            }
            var values=JsonSerializer.Deserialize<InputFingerprint[]>(root.GetProperty(legacy?"inputs":"Inputs").GetRawText())??throw new InvalidDataException("Producer input records missing");
            if(values.Any(value=>value is null||string.IsNullOrWhiteSpace(value.Role)||string.IsNullOrWhiteSpace(value.Path)||value.Bytes<0||value.Sha256 is not {Length:64}||!value.Sha256.All(Uri.IsHexDigit))||values.Count(value=>value.Role=="manifest")!=1)
                throw new InvalidDataException("Malformed producer input fingerprints");
            var canonical=values.Select(value=>value with{Path=RunnerManifestCommand.RealPath(value.Path)}).Where(value=>value.Role!="manifest").ToArray();
            InputEvidence.Verify(expected.Where(value=>value.Role!="manifest").ToArray(),canonical);
        }
    }
    internal static void Execute(ToolContext context,string[] arguments)
    {
        var options=new Options(arguments);options.Only("--through","--runner");
        if(!int.TryParse(options.Get("--through",CompletedRecipes.ToString(CultureInfo.InvariantCulture)),NumberStyles.None,CultureInfo.InvariantCulture,out int through)||through<1||through>CompletedRecipes)throw new ArgumentException("Invalid selection recipe milestone");
        string? supplied=options.Get("--runner");string runner=supplied is null?context.At("tools/SharpTestBench.Runner/bin/Release/net10.0/SharpTestBench.Runner.dll"):Path.GetFullPath(supplied,context.Root);
        // Retire default deployment completion even when its runner is missing.
        string? completion=supplied is null?context.At($"build/selection200-selection-{through}-managed.json"):null;
        if(completion is not null)File.Delete(completion);
        if(!File.Exists(runner))throw new FileNotFoundException("Selected native runner is missing",runner);
        string runnerHash=CiCommands.Hash(runner),deployment=supplied is null?"managed":"published-"+runnerHash[..12];
        completion??=context.At($"build/selection200-selection-{through}-{deployment}.json");
        File.Delete(completion);
        context.Run(["dotnet","build",context.At("tests/ProtocolContracts"),"-c","Release","-p:RestoreLockedMode=true","--disable-build-servers","-m:1"]);NativeBuildCommand.Build(context);
        string assembly=context.At("tests/ProtocolContracts/bin/Release/net10.0/ProtocolContracts.dll");string runtime=Path.Combine(Path.GetDirectoryName(assembly)!,"SharpTestBench.dll");string runtimeHash=CiCommands.Hash(runtime);
        if(runtimeHash!=CiCommands.Hash(context.At("src/SharpTestBench/bin/Release/net10.0/SharpTestBench.dll")))throw new InvalidDataException("Fixture runtime dependency differs from current production build");
        var reports=new List<Dictionary<string,object>>();
        for(int number=1;number<=through;number++)
        {
            var recipe=Recipe(number);
            var report=ParityCiCommand.InWorkspace(context,$"selection200-scenarios-{through}-{number:00}-{deployment}",(work,environment)=>
            {
                var manifest=RunnerManifestCommand.Make(context.Root,"counter",recipe.Seed,RunnerManifestCommand.InstalledAdapter(context),Path.Combine(work,"native"));
                manifest["top"]="protocol_top";manifest["sources"]=new[]{context.At("tests/ProtocolContracts/protocol_top.sv")};manifest["assembly"]=assembly;
                manifest["entryType"]=$"ScenarioEntry{number:00}, ProtocolContracts";
                string[] expected=recipe.List?DiscoveryNames(recipe):recipe.Startup?["runtime"]:recipe.Cases.Select(item=>item.Identity).ToArray();
                manifest["expectedNames"]=expected;manifest["expectedTests"]=expected.Length;
                manifest["expectedSeeds"]=expected.ToDictionary(name=>name,name=>name=="runtime"?0UL:unchecked(recipe.Seed+(ulong)Ordinal(name)),StringComparer.Ordinal);
                if(recipe.NoExact)manifest.Remove("testNames");else manifest["testNames"]=expected;
                var command=ParityLegacyCommand.Build(manifest,work,recipe.Seed,null);if(recipe.List)command=[..command,"--list"];var config=ParityLegacyCommand.Read(manifest);
                ParityLegacyCommand.ValidateAdapter(config,work,CocotbAdapter.Inspect(context));
                var left=new Dictionary<string,string?>(environment);var right=new Dictionary<string,string?>(environment);
                var bound=ParityLegacyCommand.BindTools(context,manifest,work,left,right);StrictJson.Write(Path.Combine(work,"manifest.json"),bound);config=ParityLegacyCommand.Read(bound);
                foreach(var (folder,stem) in new[]{("legacy","legacy-results"),("native","icarus-results")})foreach(string suffix in new[]{".json",".discovery.json",".coverage.json",".xml"})File.Delete(Path.Combine(work,folder,stem+suffix));
                var inputs=PairedInputs.Capture(work,config);
                var a=Run(context,work,"legacy",command,left);PairedInputs.Verify(inputs,work,config);
                string[] native=supplied is null?["dotnet",runner,"run",Path.Combine(work,"manifest.json")]:[runner,"run",Path.Combine(work,"manifest.json")];
                var b=Run(context,work,"native",native,right);PairedInputs.Verify(inputs,work,config);
                VerifyProcessBoundaries(work);
                VerifySide(work,"legacy","legacy-results",recipe,a.Code,recipe.LegacyExit,a.Output);VerifySide(work,"native","icarus-results",recipe,b.Code,recipe.NativeExit,b.Output);
                VerifyProducerInputs(work,inputs,recipe);
                if(CiCommands.Hash(runtime)!=runtimeHash||CiCommands.Hash(runner)!=runnerHash)throw new InvalidDataException("Selection runtime/runner changed during paired execution");
                PairedInputs.Publish(inputs,work,config);
                return new(){["schemaVersion"]=1,["matched"]=true,["negativeControl"]=recipe.LegacyExit!=0||recipe.NativeExit!=0,["qualification"]="selection-recipe",["recipe"]=number,["name"]=recipe.Name,["seed"]=recipe.Seed,
                    ["expectedNames"]=expected,["expectedSeeds"]=manifest["expectedSeeds"],["expectedBodies"]=recipe.Bodies,["exitCodes"]=new{legacy=a.Code,native=b.Code},["expectedExitCodes"]=new{legacy=recipe.LegacyExit,native=recipe.NativeExit},["listOnly"]=recipe.List,
                    ["processBoundariesMatched"]=true,["producerInputsMatched"]=true,["nativeCompletionMatched"]=true,["nativeRunnerSha256"]=runnerHash,["runtimeAssemblySha256"]=runtimeHash,["deployment"]=deployment,["note"]="Verified recipe outcomes include strict-attestation failure boundaries; this is not an all-passed or identical-exit-code claim."};
            });reports.Add(report);
        }
        var summary=new{schemaVersion=1,matched=true,recipes=through,deployment,nativeRunnerSha256=runnerHash,runtimeAssemblySha256=runtimeHash,scenarios=reports};
        StrictJson.Write(completion,summary);Console.WriteLine(JsonSerializer.Serialize(summary));
    }
}
