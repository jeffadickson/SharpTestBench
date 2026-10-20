using System.Text.Json;
namespace SharpTestBench.Tools;
internal static class ManifestCheckCommand
{
    static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    public static void Execute(ToolContext context, string[] arguments)
    {
        if (arguments.Length != 0) throw new ArgumentException("runner-manifest-check accepts no arguments");
        var work = context.At("build/manifest-check"); Directory.CreateDirectory(work); using var workspace = new WorkspaceLock(work, ".manifest-check.lock"); File.Delete(Path.Combine(work, "summary.json"));
        var environment = ParityContracts.ChildEnvironment(ParityContracts.EnvironmentValues()); environment["STB_NATIVE_VPI_TIME"] = "1";
        void Run(string label, string[] command, int expected = 0) { var result = context.Run(command, environment: environment, capture: true, check: false, timeout: TimeSpan.FromSeconds(180)); File.WriteAllText(Path.Combine(work, label + ".log"), result.Output + result.Error); Require(result.ExitCode == expected, label + " did not produce expected exit code"); }
        Run("build-counter", ["dotnet", "build", context.At("examples/Counter"), "-c", "Release", "-v:q"]); NativeBuildCommand.Build(context);
        var include = Path.Combine(work, "include with spaces"); Directory.CreateDirectory(include); File.WriteAllText(Path.Combine(include, "counter_config.vh"), "`define COUNTER_INCREMENT `STB_INCREMENT\n");
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
        manifest["sources"] = new[] { source }; manifest["includeDirectories"] = new[] { include }; manifest["defines"] = new Dictionary<string, string> { ["STB_INCREMENT"] = "1" }; manifest["parameters"] = new Dictionary<string, string> { ["WIDTH"] = "8" }; manifest["testNames"] = new[] { "Entry.ResetAndCount" };
        var path = Path.Combine(work, "counter.json"); StrictJson.Write(path, manifest); Run("compile-options", ["dotnet", runner, "run", path]);
        using (var evidence = StrictJson.Read(Path.Combine(work, "counter-native/run-manifest.json"))) Require(evidence.RootElement.GetProperty("Status").GetString() == "passed" && evidence.RootElement.GetProperty("Inputs").EnumerateArray().Any(item => item.GetProperty("Role").GetString() == "include"), "Compiler options did not produce attested passing run");
        var invalid = new Dictionary<string, object>(manifest) { ["outputDirectory"] = Path.Combine(work, "wrong-width"), ["parameters"] = new Dictionary<string, string>() }; StrictJson.Write(Path.Combine(work, "wrong-width.json"), invalid); Run("parameter-negative", ["dotnet", runner, "run", Path.Combine(work, "wrong-width.json")], 1);
        using (var failure = StrictJson.Read(Path.Combine(work, "wrong-width/icarus-results.json"))) Require(failure.RootElement.GetArrayLength() == 1 && failure.RootElement[0].GetProperty("Status").GetString() == "failed" && failure.RootElement[0].GetProperty("Error").GetString()!.Contains("is 4 bits, expected 8", StringComparison.Ordinal), "Parameter negative control rejected for wrong reason");
        const string selected = "Tests.A_TypesAndSignals"; manifest["assembly"] = context.At("tests/Integration/bin/Release/net10.0/Integration.dll"); manifest["sources"] = new[] { context.At("tests/Integration/top.sv") }; manifest["top"] = "top"; manifest["entryType"] = "Entry, Integration"; manifest["includeDirectories"] = Array.Empty<string>(); manifest["defines"] = new Dictionary<string, string>(); manifest["parameters"] = new Dictionary<string, string>(); manifest["outputDirectory"] = Path.Combine(work, "selection-native"); manifest["expectedNames"] = new[] { selected }; manifest["testNames"] = new[] { selected };
        StrictJson.Write(Path.Combine(work, "selection.json"), manifest); Run("native-selection", ["dotnet", runner, "run", Path.Combine(work, "selection.json")]);
        Run("legacy-selection", [.. ToolContext.Self, "run", "--sim", "icarus", "--no-build", "--assembly", (string)manifest["assembly"], "--source", ((string[])manifest["sources"])[0], "--top", "top", "--entry-type", "Entry, Integration", "--work-dir", Path.Combine(work, "selection-legacy"), "--label", "selected", "--exact", selected, "--seed", "17"]);
        using var left = StrictJson.Read(Path.Combine(work, "selection-legacy/selected-results.json")); using var right = StrictJson.Read(Path.Combine(work, "selection-native/icarus-results.json")); var comparison = ParityContracts.Compare(left.RootElement, right.RootElement, JsonSerializer.SerializeToElement(new[] { selected })); Require((bool)comparison["matched"], "Selected runner cases differ");
        StrictJson.Write(Path.Combine(work, "summary.json"), new { schemaVersion = 1, compilerOptions = true, parameterNegativeControl = true, selectedCaseParity = comparison, inputAttestation = true }); Console.WriteLine("MANIFEST_CHECK_PASS compiler options, parameter negative, exact selection and provenance");
    }
}
