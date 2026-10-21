using System.Text.Json;
namespace SharpTestBench.Tools;
internal static class ManifestCheckCommand
{
    static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    internal static ulong SelectionSeed(JsonElement discovery,string selected)
    {
        if (discovery.ValueKind!=JsonValueKind.Object || !discovery.TryGetProperty("Tests",out var tests) || tests.ValueKind!=JsonValueKind.Array)
            throw new InvalidDataException("Discovery must contain a Tests array.");
        ulong? found=null;var names=new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in tests.EnumerateArray())
        {
            if (row.ValueKind!=JsonValueKind.Object) throw new InvalidDataException("Invalid discovery descriptor.");
            var name=ParityContracts.Identity(row);
            if (!names.Add(name)) throw new InvalidDataException("Duplicate discovery identity.");
            if (!row.TryGetProperty("Seed",out var seed) || seed.ValueKind!=JsonValueKind.Number || !seed.TryGetUInt64(out var value))
                throw new InvalidDataException("Invalid discovery seed.");
            if (name==selected) found=value;
        }
        return found ?? throw new InvalidDataException("Selected test missing from discovery.");
    }
    public static void Execute(ToolContext context, string[] arguments)
    {
        if (arguments.Length != 0) throw new ArgumentException("runner-manifest-check accepts no arguments");
        var work = context.At("build/manifest-check"); Directory.CreateDirectory(work); using var workspace = new WorkspaceLock(work, ".manifest-check.lock"); File.Delete(Path.Combine(work, "summary.json"));
        var environment = ParityContracts.ChildEnvironment(ParityContracts.EnvironmentValues()); environment["STB_NATIVE_VPI_TIME"] = "1";
        void Run(string label, string[] command, int expected = 0) { var result = context.Run(command, environment: environment, capture: true, check: false, timeout: TimeSpan.FromSeconds(180)); File.WriteAllText(Path.Combine(work, label + ".log"), result.Output + result.Error); Require(result.ExitCode == expected, label + " did not produce expected exit code"); }
        Run("build-counter", ["dotnet", "build", context.At("examples/Counter"), "-c", "Release", "-v:q"]); NativeBuildCommand.Build(context);
        var include = Path.Combine(work, "include with spaces"); Directory.CreateDirectory(include); File.WriteAllText(Path.Combine(include, "counter_config.vh"), "`ifdef STB_BARE\n`define COUNTER_INCREMENT `STB_INCREMENT\n`else\n`define COUNTER_INCREMENT 2\n`endif\n");
        var source = Path.Combine(work, "parameterized counter.sv"); File.WriteAllText(source, """
`include "counter_config.vh"
module counter #(parameter WIDTH=4)(input logic clk, reset, enable, output logic [WIDTH-1:0] count);
    always @(posedge clk)
        if (reset) count <= 0;
        else if (enable) count <= count + `COUNTER_INCREMENT;
endmodule
""" + "\n");
        var runner = context.At("tools/SharpTestBench.Runner/bin/Release/net10.0/SharpTestBench.Runner.dll");
        var manifest = RunnerManifestCommand.Make(context.Root, "counter", 17, RunnerManifestCommand.InstalledAdapter(context), Path.Combine(work, "counter-native"));
        manifest["sources"] = new[] { source }; manifest["includeDirectories"] = new[] { include }; manifest["hdlStandard"]="2005-sv"; manifest["defines"] = new Dictionary<string, string?> { ["STB_INCREMENT"] = "1",["STB_BARE"]=null }; manifest["parameters"] = new Dictionary<string, string> { ["WIDTH"] = "8" }; manifest["testNames"] = new[] { "Entry.ResetAndCount" };
        var path = Path.Combine(work, "counter.json"); StrictJson.Write(path, manifest); Run("compile-options", ["dotnet", runner, "run", path]);
        using (var evidence = StrictJson.Read(Path.Combine(work, "counter-native/run-manifest.json"))) Require(evidence.RootElement.GetProperty("Status").GetString() == "passed" && evidence.RootElement.GetProperty("Inputs").EnumerateArray().Any(item => item.GetProperty("Role").GetString() == "include"), "Compiler options did not produce attested passing run");
        var invalid = new Dictionary<string, object>(manifest) { ["outputDirectory"] = Path.Combine(work, "wrong-width"), ["parameters"] = new Dictionary<string, string>() }; StrictJson.Write(Path.Combine(work, "wrong-width.json"), invalid); Run("parameter-negative", ["dotnet", runner, "run", Path.Combine(work, "wrong-width.json")], 1);
        using (var failure = StrictJson.Read(Path.Combine(work, "wrong-width/icarus-results.json"))) Require(failure.RootElement.GetArrayLength() == 1 && failure.RootElement[0].GetProperty("Status").GetString() == "failed" && failure.RootElement[0].GetProperty("Error").GetString()!.Contains("is 4 bits, expected 8", StringComparison.Ordinal), "Parameter negative control rejected for wrong reason");
        Dictionary<string,object> CompileParity(string name,bool negative) =>
            ParityCiCommand.InWorkspace(context,"manifest-check/"+name,(pair,childEnvironment) =>
        {
            var configured=new Dictionary<string,object>(manifest) {
                ["assembly"]=Path.GetRelativePath(pair,(string)manifest["assembly"]),
                ["sources"]=new[] { Path.GetRelativePath(pair,source) },
                ["includeDirectories"]=new[] { Path.GetRelativePath(pair,include) },
                ["outputDirectory"]=Path.Combine(pair,"native") };
            if (negative) configured["parameters"]=new Dictionary<string,string>();
            StrictJson.Write(Path.Combine(pair,"manifest.json"),configured);
            var report=ParityCiCommand.Compare(context,pair,configured,17,childEnvironment,null,null,negative ? "is 4 bits, expected 8" : null,null);
            Require((bool)report["matched"],"Compiler-option dual-runner parity failed: "+name);
            return report;
        });
        var compilerParity=CompileParity("compiler-parity",false);
        var compilerNegativeParity=CompileParity("compiler-parity-negative",true);
        const string selected = "Tests.A_TypesAndSignals"; manifest["assembly"] = context.At("tests/Integration/bin/Release/net10.0/Integration.dll"); manifest["sources"] = new[] { context.At("tests/Integration/top.sv") }; manifest["top"] = "top"; manifest["entryType"] = "Entry, Integration"; manifest["includeDirectories"] = Array.Empty<string>(); manifest["defines"] = new Dictionary<string, string>(); manifest["parameters"] = new Dictionary<string, string>(); manifest["outputDirectory"] = Path.Combine(work, "selection-native"); manifest["expectedNames"] = new[] { selected }; manifest["testNames"] = new[] { selected };
        Run("selection-discovery",[.. ToolContext.Self,"run","--sim","icarus","--no-build","--assembly",(string)manifest["assembly"],
            "--source",((string[])manifest["sources"])[0],"--top","top","--entry-type","Entry, Integration","--work-dir",Path.Combine(work,"selection-discovery"),"--label","discovered","--list","--seed","17"]);
        using var discovery=StrictJson.Read(Path.Combine(work,"selection-discovery/discovered-results.discovery.json"));
        var selectedSeed=SelectionSeed(discovery.RootElement,selected);
        RunnerManifestCommand.BindSingleCase(manifest,selected,selectedSeed);
        manifest["hdlStandard"]="2012";
        StrictJson.Write(Path.Combine(work, "selection.json"), manifest); Run("native-selection", ["dotnet", runner, "run", Path.Combine(work, "selection.json")]);
        Run("legacy-selection", [.. ToolContext.Self, "run", "--sim", "icarus", "--no-build", "--assembly", (string)manifest["assembly"], "--source", ((string[])manifest["sources"])[0], "--top", "top", "--entry-type", "Entry, Integration", "--work-dir", Path.Combine(work, "selection-legacy"), "--label", "selected", "--exact", selected, "--seed", "17"]);
        using var left = StrictJson.Read(Path.Combine(work, "selection-legacy/selected-results.json")); using var right = StrictJson.Read(Path.Combine(work, "selection-native/icarus-results.json")); var comparison = ParityContracts.Compare(left.RootElement, right.RootElement, JsonSerializer.SerializeToElement(new[] { selected })); Require((bool)comparison["matched"], "Selected runner cases differ");
        StrictJson.Write(Path.Combine(work, "summary.json"), new { schemaVersion = 1, compilerOptions = true, parameterNegativeControl = true, selectedCaseParity = comparison, selectedExpectedSeed=selectedSeed, compilerOptionParity=compilerParity, compilerOptionNegativeParity=compilerNegativeParity, inputAttestation = true }); Console.WriteLine("MANIFEST_CHECK_PASS compiler options, parameter negative, exact selection and provenance");
    }
}
