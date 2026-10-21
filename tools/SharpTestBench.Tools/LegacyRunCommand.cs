using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SharpTestBench.Tools;

internal sealed class WorkspaceLock : IDisposable
{
    readonly string path; readonly FileStream stream;
    public WorkspaceLock(string directory, string name = ".sharptestbench-run.lock")
    {
        Directory.CreateDirectory(directory); path = Path.Combine(directory, name);
        try { stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read); }
        catch (IOException error) when (File.Exists(path)) { throw new IOException("Workspace is already in use: " + directory, error); }
        var bytes = System.Text.Encoding.UTF8.GetBytes(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)); stream.Write(bytes); stream.Flush(true);
    }
    public void Dispose() { stream.Dispose(); File.Delete(path); }
}
internal static class LegacyRunCommand
{
    internal static Dictionary<string, string?> SimulatorEnvironment(IReadOnlyDictionary<string, string?> inherited, string hostDirectory, string adapterDirectory, string? logPath = null)
    {
        var environment = new Dictionary<string, string?>(inherited, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        string Prefix(string variable) => hostDirectory + Path.PathSeparator + adapterDirectory + Path.PathSeparator + environment.GetValueOrDefault(variable);
        environment["LD_LIBRARY_PATH"] = Prefix("LD_LIBRARY_PATH");
        if (NativeBuildCommand.LoaderPath != "LD_LIBRARY_PATH") environment[NativeBuildCommand.LoaderPath] = Prefix(NativeBuildCommand.LoaderPath);
        if (logPath is not null) environment["CS_LOG_JSON"] = Path.GetFullPath(logPath);
        return environment;
    }
    public static void Execute(ToolContext context, string[] arguments)
    {
        var options = new Options(arguments, "--no-build", "--list", "--timings");
        options.Only("--hdl-standard","--include-dir","--define","--parameter","--timings", "--sim-executable", "--work-dir", "--waves", "--host", "--assembly", "--source", "--top", "--entry-type", "--no-build", "--project", "--stage", "--stage-failure-policy", "--log-json", "--list", "--exact", "--seed", "--filter", "--label", "--sim");
        var simulator = options.Get("--sim", "icarus")!; var project = options.Get("--project", "Integration")!;
        if (!new[] { "icarus", "verilator", "ghdl", "nvc" }.Contains(simulator) || project is not ("Integration" or "Benchmark")) throw new ArgumentException("Unknown simulator or project");
        bool noBuild = options.Has("--no-build");
        if (options.Has("--timings") && (simulator != "icarus" || !noBuild || options.Has("--list"))) throw new ArgumentException("--timings requires Icarus --no-build without --list");
        if (options.Has("--sim-executable") && (simulator != "verilator" || !noBuild)) throw new ArgumentException("--sim-executable requires --sim verilator --no-build");
        if (options.Has("--work-dir") && !noBuild) throw new ArgumentException("--work-dir requires --no-build");
        var work = options.Get("--work-dir") is string selectedWork ? Path.GetFullPath(selectedWork) : context.At("build");
        using var workspace = new WorkspaceLock(work);
        var host = options.Get("--host") is string selectedHost ? Path.GetFullPath(selectedHost) : context.At("build/" + NativeBuildCommand.HostName);
        var top = options.Get("--top", "top")!; var label = options.Get("--label", simulator)!;
        if (label.Length == 0 || label.Any(c => !char.IsLetterOrDigit(c) && c is not ('-' or '_'))) throw new ArgumentException("Label must be alphanumeric");
        var timingPath = Path.Combine(work, label + "-timings.json");
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (options.Has("--timings") && options.All("--source").Concat(options.All("--assembly")).Concat(options.All("--host")).Select(Path.GetFullPath).Contains(timingPath, pathComparer)) throw new ArgumentException("Timing output must not overwrite an input");
        double compileMilliseconds = 0;
        string? wave = options.Get("--waves") is string selectedWave ? Path.GetFullPath(selectedWave) : null;
        if (wave is not null)
        {
            if (!wave.EndsWith(".vcd", StringComparison.OrdinalIgnoreCase) || options.Has("--list") || Environment.GetEnvironmentVariable("CS_LIST_TESTS") == "1" || wave.Any(c => c < 32 || c == 127)) throw new ArgumentException("Invalid waveform request");
        }
        var selectedSources = options.All("--source").Select(Path.GetFullPath).ToArray();
        if (selectedSources.Length == 0) selectedSources = [context.At(simulator is "ghdl" or "nvc" ? "tests/VhdlIntegration/top.vhd" : "tests/Integration/top.sv")];
        var compileConfig = LegacyCompileOptions.Create(options,simulator,top,selectedSources);
        var selectedName = simulator is "ghdl" or "nvc" ? "VhdlIntegration" : project;
        var selectedDll = options.Get("--assembly") is string requestedAssembly ? Path.ChangeExtension(Path.GetFullPath(requestedAssembly),".dll") : context.At($"tests/{selectedName}/bin/Release/net10.0/{selectedName}.dll");
        var inputPaths = selectedSources.Concat(new[] { selectedDll, Path.ChangeExtension(selectedDll,".runtimeconfig.json"), Path.ChangeExtension(selectedDll,".deps.json"), host }).ToList();
        if (options.Get("--sim-executable") is string selectedSimulator) inputPaths.Add(Path.GetFullPath(selectedSimulator));
        string? logPath = options.Get("--log-json") is string selectedLog ? Path.GetFullPath(selectedLog) : Environment.GetEnvironmentVariable("CS_LOG_JSON") is string inheritedLog ? Path.GetFullPath(inheritedLog,work) : null;
        var outputPaths = LegacyArtifacts.Outputs(work,label).Concat(wave is null ? [] : new[] { wave }).Concat(logPath is null ? [] : new[] { logPath }).ToArray();
        string[] generatedDirectories = simulator == "verilator" && !options.Has("--sim-executable") ? [Path.Combine(work,"verilator")] : simulator == "nvc" ? [Path.Combine(work,"nvc-work")] : simulator == "ghdl" ? [Path.Combine(work,"ghdl-work")] : [];
        LegacyArtifacts.Validate(inputPaths,outputPaths,generatedDirectories,compileConfig.IncludeDirectories);
        if (!noBuild)
        {
            var nativeOutput=Path.Combine(work,NativeBuildCommand.HostName);
            var buildInputs=selectedSources.Concat(new[] { selectedDll,Path.ChangeExtension(selectedDll,".runtimeconfig.json"),Path.ChangeExtension(selectedDll,".deps.json") }).ToList();
            if (!pathComparer.Equals(RunnerManifestCommand.RealPath(host),RunnerManifestCommand.RealPath(nativeOutput))) buildInputs.Add(host);
            LegacyArtifacts.Validate(buildInputs,[nativeOutput,Path.Combine(work,"native-vectorization.txt"),Path.Combine(work,"native-build.json")],includeDirectories:compileConfig.IncludeDirectories);
            if (!options.Has("--assembly")) LegacyArtifacts.Validate(selectedSources,[],[context.At($"tests/{selectedName}/bin"),context.At($"tests/{selectedName}/obj")],compileConfig.IncludeDirectories);
        }
        var sdk = NativeBuildCommand.SdkRoot();
        var adapter = CocotbAdapter.Inspect(context, simulator); var entry = adapter.NativeEntry();
        var hostFxr = NativeBuildCommand.HostFxr(sdk);
        LegacyArtifacts.Validate(inputPaths.Concat(new[] { adapter.Bootstrap,entry.Library,hostFxr }),outputPaths,generatedDirectories,compileConfig.IncludeDirectories);
        LegacyArtifacts.RetireReports(work,label);
        if (wave is not null) { Directory.CreateDirectory(Path.GetDirectoryName(wave)!); File.Delete(wave); }
        var assemblyName = simulator is "ghdl" or "nvc" ? "VhdlIntegration" : project;
        if (!noBuild)
        {
            if (!options.Has("--assembly")) context.Run([Path.Combine(sdk, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"), "build", context.At($"tests/{assemblyName}/{assemblyName}.csproj"), "-c", "Release", "--nologo"]);
            NativeBuildCommand.Build(context, sdk, work);
        }
        var binary = context.At($"tests/{assemblyName}/bin/Release/net10.0");
        if (options.Get("--assembly") is string selectedAssembly) { var full = Path.GetFullPath(selectedAssembly); binary = Path.GetDirectoryName(full)!; assemblyName = Path.GetFileNameWithoutExtension(full); }
        var assembly = Path.Combine(binary, assemblyName + ".dll"); var config = Path.Combine(binary, assemblyName + ".runtimeconfig.json");
        if (!File.Exists(assembly) || !File.Exists(config)) throw new FileNotFoundException("Test DLL and runtimeconfig.json are required");
        var sources = options.All("--source").Select(Path.GetFullPath).ToArray();
        if (sources.Length == 0) sources = [context.At(simulator is "ghdl" or "nvc" ? "tests/VhdlIntegration/top.vhd" : "tests/Integration/top.sv")];
        var environment = SimulatorEnvironment(ParityContracts.EnvironmentValues(), Path.GetDirectoryName(host)!, adapter.LibsDir, logPath);
        void Set(string name, string value) => environment[name] = value;
        Set("CS_HOSTFXR", hostFxr); Set("CS_CONFIG", config); Set("CS_ASSEMBLY", assembly); Set("CS_TOPLEVEL", top);
        Set("CS_ENTRY_TYPE", options.Get("--entry-type", "Entry, " + assemblyName)!); Set("CS_RESULTS", Path.Combine(work, label + "-results.json")); Set("CS_TRACE", Path.Combine(work, label + "-trace.txt")); Set("CS_BINDINGS", Path.Combine(work, "DutBindings.cs")); Set("DOTNET_ROOT", sdk);
        Set("CS_GPI_LIBRARY", entry.Library);
        if (options.Get("--filter") is string filter) Set("CS_TEST_FILTER", filter);
        if (options.Has("--list")) Set("CS_LIST_TESTS", "1");
        if (options.Get("--log-json") is string log) Set("CS_LOG_JSON", Path.GetFullPath(log));
        if (options.Has("--exact")) Set("CS_TEST_NAMES", JsonSerializer.Serialize(options.All("--exact")));
        if (options.Get("--seed") is string seed) { if (!ulong.TryParse(seed, out _)) throw new ArgumentException("Invalid seed"); Set("CS_SEED", seed); }
        if (options.Has("--stage")) { var stages = options.All("--stage"); if (stages.Any(stage => !int.TryParse(stage, out _))) throw new ArgumentException("Invalid stage"); Set("CS_TEST_STAGES", string.Join(',', stages)); }
        if (options.Get("--stage-failure-policy") is string policy) { if (policy is not ("continue" or "stop-after-stage")) throw new ArgumentException("Unknown stage failure policy"); Set("CS_STAGE_FAILURE_POLICY", policy); }
        Set("GPI_IMPL", adapter.Implementation ?? throw new InvalidDataException("Missing GPI implementation"));
        Set("COCOTB_BOOTSTRAP", adapter.GpiEntryPoint + Path.PathSeparator + host + ",sharptestbench_initialize");
        string[] ivl = environment.GetValueOrDefault("CS_IVL_ROOT") is string ivlRoot ? ["-B", ivlRoot] : [];
        string[] command;
        LegacyImage? compiledImage=null;
        var simulationDirectory = work;
        if (simulator == "icarus")
        {
            var compiledPath = Path.Combine(work,"top.vvp");
            File.Delete(compiledPath);
            var waveOptions = new List<string>();
            if (wave is not null)
            {
                if (!Regex.IsMatch(top, @"\A[A-Za-z_][A-Za-z0-9_$]*\z")) throw new ArgumentException("Icarus wave capture requires a simple top-level identifier");
                var helper = Path.Combine(work, label + "-wave-dump.sv");
                var literal = wave.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
                File.WriteAllText(helper, "module __sharptestbench_wave_dump; initial if ($test$plusargs(\"SHARPTESTBENCH_WAVES\")) begin $dumpfile(\"" + literal + "\"); $dumpvars(0," + top + "); end endmodule\n");
                waveOptions.AddRange(["-s", "__sharptestbench_wave_dump", helper]);
            }
            var compileWatch = Stopwatch.StartNew();
            context.Run([environment.GetValueOrDefault("CS_IVERILOG") ?? "iverilog", .. ivl, .. CompilePlan.Arguments(context.Root,compileConfig,compiledPath), .. waveOptions]);
            if (!File.Exists(compiledPath) || new FileInfo(compiledPath).Length == 0) throw new IOException("Icarus compiler did not produce a nonempty output image.");
            compiledImage=LegacyImage.Capture(compiledPath);
            StrictJson.Write(Path.Combine(work,label+"-compile.json"),new { schemaVersion=1,status="compiled",image=compiledImage });
            compileMilliseconds = compileWatch.Elapsed.TotalMilliseconds;
            var bootstrap = adapter.Bootstrap;
            if (OperatingSystem.IsWindows()) { var staged = Path.Combine(work, "cocotb_bootstrap.vpi"); File.Copy(bootstrap, staged, true); bootstrap = staged; }
            command = [environment.GetValueOrDefault("CS_VVP") ?? "vvp", "-m", bootstrap, Path.Combine(work, "top.vvp"), .. (wave is null ? Array.Empty<string>() : ["+SHARPTESTBENCH_WAVES"])];
        }
        else if (simulator == "ghdl")
        {
            var ghdl = environment.GetValueOrDefault("CS_GHDL") ?? "ghdl";
            simulationDirectory = Path.Combine(work,"ghdl-work");
            Directory.CreateDirectory(simulationDirectory);
            context.Run([ghdl, "-a", "--std=08", .. sources], directory: simulationDirectory, environment: environment); context.Run([ghdl, "-e", "--std=08", top], directory: simulationDirectory, environment: environment);
            command = [ghdl, "-r", "--std=08", top, "--vpi=" + adapter.Bootstrap, .. (wave is null ? Array.Empty<string>() : ["--vcd=" + wave])];
        }
        else if (simulator == "nvc")
        {
            var nvc = environment.GetValueOrDefault("CS_NVC") ?? "nvc"; var nvcWork = "--work=work:" + Path.Combine(work, "nvc-work");
            context.Run([nvc, "--std=2008", nvcWork, "-a", .. sources, "--preserve-case"], directory: work, environment: environment);
            command = [nvc, "--std=2008", nvcWork, "-e", top, "--no-save", "--jit", "-r", "--load=" + adapter.Bootstrap, .. (wave is null ? Array.Empty<string>() : ["--wave=" + wave, "--format=vcd"])];
        }
        else
        {
            var executable = options.Get("--sim-executable") is string selected ? Path.GetFullPath(selected) : VerilatorBuildCommand.Build(context, sources, top, Path.Combine(work, "verilator"), wave is not null);
            if (!File.Exists(executable)) throw new FileNotFoundException("Prebuilt Verilator executable is missing", executable);
            command = [executable, .. (wave is null ? Array.Empty<string>() : ["--trace", "--trace-file", wave])];
        }
        if (environment.GetValueOrDefault("CS_INJECT_EARLY") == "1" && simulator is "icarus" or "verilator") command = [.. command, "+CS_EARLY_FINISH"];
        var resultPath = environment["CS_RESULTS"]!; File.Delete(resultPath);
        if (!int.TryParse(environment.GetValueOrDefault("CS_WALL_TIMEOUT") ?? "120", out var seconds) || seconds < 1 || seconds > 3600) throw new ArgumentException("Invalid simulator wall timeout");
        var simulationWatch = Stopwatch.StartNew();
        var result = context.Run(command, directory: simulationDirectory, environment: environment, capture: true, check: false, timeout: TimeSpan.FromSeconds(seconds));
        var simulationMilliseconds = simulationWatch.Elapsed.TotalMilliseconds;
        var output = result.Output + result.Error; File.WriteAllText(Path.Combine(work, label + "-run.log"), output); Console.WriteLine(output);
        SharpTestBench.Internal.ReportFailureEvidence.ThrowIfReported(result.Output.Split('\n'));
        SharpTestBench.Internal.ReportFailureEvidence.ThrowIfReported(result.Error.Split('\n'));
        compiledImage?.Verify();
        if (wave is not null) RecordWave(work, label, simulator, resultPath, wave);
        if (result.ExitCode != 0 || !File.Exists(resultPath)) throw new IOException("Simulator failed or produced no result file");
        using var rows = StrictJson.Read(resultPath);
        LegacyResults.Validate(rows.RootElement,options.Has("--list") || environment.GetValueOrDefault("CS_LIST_TESTS") == "1");
        var failed = rows.RootElement.EnumerateArray().Where(row => row.GetProperty("Status").GetString() == "failed").ToArray();
        if (failed.Length != 0) { Console.WriteLine(JsonSerializer.Serialize(failed, new JsonSerializerOptions { WriteIndented = true })); throw new InvalidDataException("Simulator test failed"); }
        if (options.Has("--list") || environment.GetValueOrDefault("CS_LIST_TESTS") == "1") { Console.WriteLine(File.ReadAllText(Path.ChangeExtension(resultPath, ".discovery.json"))); return; }
        if (rows.RootElement.GetArrayLength() == 0) throw new InvalidDataException("No results");
        if (options.Has("--timings")) StrictJson.Write(timingPath, new { schemaVersion = 1, compileMilliseconds, simulationMilliseconds });
    }
    static void RecordWave(string work, string label, string simulator, string resultPath, string wave)
    {
        var artifact = new { Kind = "waveform", Format = "vcd", Path = System.IO.Path.GetRelativePath(System.IO.Path.GetDirectoryName(resultPath)!, wave), Exists = File.Exists(wave), Bytes = File.Exists(wave) ? new FileInfo(wave).Length : 0 };
        StrictJson.Write(System.IO.Path.Combine(work, label + "-artifacts.json"), new { SchemaVersion = 1, Simulator = simulator, Results = System.IO.Path.GetFileName(resultPath), Artifacts = new[] { artifact } });
        if (File.Exists(resultPath))
        {
            using var document = StrictJson.Read(resultPath); var rows = JsonNode.Parse(document.RootElement.GetRawText())!.AsArray();
            foreach (var row in rows) row!["Artifacts"] = JsonSerializer.SerializeToNode(new[] { artifact });
            StrictJson.Write(resultPath, rows);
            var junit = System.IO.Path.ChangeExtension(resultPath, ".xml");
            if (File.Exists(junit))
            {
                var xml = XDocument.Load(junit);
                foreach (var test in xml.Descendants("testcase")) { var properties = test.Element("properties"); if (properties is null) { properties = new XElement("properties"); test.Add(properties); } properties.Add(new XElement("property", new XAttribute("name", "waveform"), new XAttribute("value", artifact.Path))); }
                xml.Save(junit);
            }
        }
        if (!artifact.Exists || artifact.Bytes == 0) throw new InvalidDataException("Requested waveform was not generated or is empty");
    }
}
