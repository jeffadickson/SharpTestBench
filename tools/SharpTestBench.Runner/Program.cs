// Standalone .NET 10 runner for prebuilt SharpTestBench native host/GPI adapter.
// This initial slice supports Icarus only. No cocotb Python process is launched.
using System.Security.Cryptography;
using System.Text.Json;

if (args is ["--help"] or ["-h"])
{
    Console.WriteLine("SharpTestBench standalone runner: run <manifest.json>. Icarus orchestration; .NET runtime required by managed simulation tests.");
    return 0;
}
if (args.Length != 2 || args[0] != "run")
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/SharpTestBench.Runner -- run <manifest.json>");
    return 2;
}

string? failureArtifactDirectory = null;
RunWorkspace? workspace = null;
var runId = Guid.NewGuid().ToString("N");
try
{
    var manifestPath = Path.GetFullPath(args[1]);
    var root = Path.GetDirectoryName(manifestPath)!;
    var config = JsonInput.Manifest(JsonInput.Read(manifestPath));
    ManifestValidation.Validate(config);
    string Resolve(string name) => Path.GetFullPath(Path.Combine(root, name));
    var sources = CompilePlan.Sources(root, config);
    var assembly = Resolve(config.Assembly);
    var host = Resolve(config.NativeHost);
    var bootstrap = Resolve(config.IcarusBootstrap);
    var gpi = Resolve(config.GpiLibrary);
    if (string.IsNullOrWhiteSpace(config.GpiImpl))
        throw new InvalidDataException("gpiImpl must specify the simulator-specific GPI implementation string.");
    var implementationSeparator = config.GpiImpl.LastIndexOf(':');
    if (implementationSeparator <= 0 || implementationSeparator == config.GpiImpl.Length - 1)
        throw new InvalidDataException("gpiImpl must specify library:symbol.");
    var implementation = Resolve(config.GpiImpl[..implementationSeparator]);
    var sdk = Resolve(config.DotnetRoot);
    var work = ManifestValidation.ResolveOutput(root, config.OutputDirectory);
    Directory.CreateDirectory(work);
    var artifactPaths = RunWorkspace.Artifacts.Append(".sharptestbench-run.lock").Select(name => Path.Combine(work, name)).ToHashSet(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    if (sources.Concat([assembly, host, bootstrap, gpi, implementation]).Any(artifactPaths.Contains))
        throw new InvalidDataException("An input path collides with a generated runner artifact.");
    var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    foreach (var include in config.IncludeDirectories ?? [])
    {
        var directory = Resolve(include);
        var relative = Path.GetRelativePath(directory, work);
        if (relative == "." || (!Path.IsPathRooted(relative) && relative != ".." &&
            !relative.StartsWith(".." + Path.DirectorySeparatorChar, pathComparison)))
            throw new InvalidDataException("An include directory collides with the runner output directory.");
    }
    workspace = RunWorkspace.Acquire(work);
    failureArtifactDirectory = work;
    workspace.ClearArtifacts();
    foreach (var required in sources.Concat([assembly, host, bootstrap, gpi]))
        if (!File.Exists(required)) throw new FileNotFoundException("Required input not found: " + required, required);
    var runtimeConfig = Path.Combine(Path.GetDirectoryName(assembly)!, Path.GetFileNameWithoutExtension(assembly) + ".runtimeconfig.json");
    if (!File.Exists(runtimeConfig)) throw new FileNotFoundException("Missing runtimeconfig", runtimeConfig);
    var fxrDir = Path.Combine(sdk, "host", "fxr");
    if (!Directory.Exists(fxrDir)) throw new DirectoryNotFoundException(fxrDir);
    var fxr = Directory.GetDirectories(fxrDir).OrderByDescending(x =>
        Version.TryParse(Path.GetFileName(x), out var v) ? v : new Version(0, 0)).FirstOrDefault()
        ?? throw new InvalidDataException("No hostfxr found");
    var fxrFile = Directory.EnumerateFiles(fxr, OperatingSystem.IsWindows() ? "hostfxr.dll" : OperatingSystem.IsMacOS() ? "libhostfxr.dylib" : "libhostfxr.so").FirstOrDefault()
        ?? throw new InvalidDataException("No hostfxr library found");
    foreach (var include in config.IncludeDirectories ?? [])
        if (!Directory.Exists(Resolve(include))) throw new DirectoryNotFoundException("Missing include directory: " + Resolve(include));
    var inputs = InputEvidence.Capture(root, manifestPath, config, fxrFile);
    EvidenceWriter.Write(Path.Combine(work, "input-manifest.json"), JsonSerializer.SerializeToElement(new InputManifestEvidence(1, runId, inputs), EvidenceJsonContext.Default.InputManifestEvidence));
    var compiled = Path.Combine(work, "top.vvp");
    await ProcessExecution.Run(config.Iverilog ?? "iverilog", CompilePlan.Arguments(root, config, compiled), work, null, "compile", config.TimeoutSeconds, config.MaxLogBytes);
    if (!File.Exists(compiled) || new FileInfo(compiled).Length == 0)
        throw new RunnerProcessException("compile", config.Iverilog ?? "iverilog", 0, "Compiler did not produce a nonempty output image.");
    var compiledInput = InputEvidence.File("compiled-image", compiled);
    var results = Path.Combine(work, "icarus-results.json");
    if (File.Exists(results)) File.Delete(results);
    var env = new Dictionary<string, string>
    {
        ["CS_HOSTFXR"] = fxrFile,
        ["CS_CONFIG"] = runtimeConfig,
        ["CS_ASSEMBLY"] = assembly,
        ["CS_TOPLEVEL"] = config.Top,
        ["CS_ENTRY_TYPE"] = config.EntryType ?? "Entry, " + Path.GetFileNameWithoutExtension(assembly),
        ["CS_RESULTS"] = results,
        ["CS_TRACE"] = Path.Combine(work, "icarus-trace.txt"),
        ["CS_BINDINGS"] = Path.Combine(work, "DutBindings.cs"),
        ["CS_GPI_LIBRARY"] = gpi,
        ["COCOTB_BOOTSTRAP"] = gpi + "," + config.GpiEntryPoint + Path.PathSeparator + host + ",sharptestbench_initialize",
        ["GPI_IMPL"] = config.GpiImpl,
        ["DOTNET_ROOT"] = sdk,
        ["CS_SEED"] = config.Seed.ToString(),
    };
    foreach (var (key, value) in CompilePlan.Environment(config)) env[key] = value;
    var searchPath = Path.GetDirectoryName(host)! + Path.PathSeparator + Path.GetDirectoryName(gpi);
    var loaderKey = OperatingSystem.IsWindows() ? "PATH" : OperatingSystem.IsMacOS() ? "DYLD_LIBRARY_PATH" : "LD_LIBRARY_PATH";
    env[loaderKey] = searchPath + Path.PathSeparator + Environment.GetEnvironmentVariable(loaderKey);
    var command = config.Vvp ?? "vvp";
    await ProcessExecution.Run(command, ["-m", bootstrap, compiled], work, env, "simulate", config.TimeoutSeconds, config.MaxLogBytes);
    foreach (var stream in new[] { "stdout", "stderr" })
        SharpTestBench.Internal.ReportFailureEvidence.ThrowIfReported(File.ReadLines(Path.Combine(work, "simulate." + stream + ".log")));
    if (!File.Exists(results)) throw new InvalidDataException("Simulator did not produce a result JSON file");
    using var doc = JsonInput.Parse(JsonInput.Read(results));
    InputEvidence.Verify(inputs, InputEvidence.Capture(root, manifestPath, config, fxrFile));
    InputEvidence.Verify([compiledInput], [InputEvidence.File("compiled-image", compiled)]);
    var attestation = ResultAttestation.Verify(doc.RootElement, config);
    int failed = attestation.Failed;
    var evidence = new RunEvidence
    {
        SchemaVersion = 1, RunId = runId, Simulator = config.Simulator, Top = config.Top, Seed = config.Seed, Inputs = inputs, Compiled = compiledInput,
        Platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
        Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        Sources = sources.Select(s => new SourceEvidence(s, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(s))))).ToArray(),
        AssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),
        Results = results, Passed = attestation.Passed, Failed = failed,
        TestNames = attestation.Names,
        Status = failed == 0 ? "passed" : "failed"
    };
    EvidenceWriter.Write(Path.Combine(work, "run-manifest.json"), JsonSerializer.SerializeToElement(evidence, EvidenceJsonContext.Default.RunEvidence));
    Console.WriteLine($"SharpTestBench: {doc.RootElement.GetArrayLength()} tests; {failed} failed. {results}");
    return failed == 0 ? 0 : 1;
}
catch (Exception ex)
{
    var kind = ex switch
    {
        TimeoutException => "timeout",
        RunnerProcessException processError => processError.Kind,
        WorkspaceInUseException => "workspace_in_use",
        FileNotFoundException or DirectoryNotFoundException or InvalidDataException or JsonException or System.Text.DecoderFallbackException => "configuration_error",
        InvalidOperationException when ex.Message.Contains("iverilog", StringComparison.OrdinalIgnoreCase) => "build_failure",
        InvalidOperationException when ex.Message.Contains("vvp", StringComparison.OrdinalIgnoreCase) => "simulator_failure",
        _ => "infrastructure_failure"
    };
    Console.Error.WriteLine($"SharpTestBench runner {kind}: {ex.GetType().Name}: {ex.Message}");
    if (failureArtifactDirectory is not null)
    {
        try
        {
            var evidence = new FailureEvidence
            {
                SchemaVersion = 1, RunId = runId, Status = "failed", Kind = kind,
                ErrorType = ex.GetType().FullName, Error = ex.Message,
                RecordedAtUtc = DateTimeOffset.UtcNow,
                LogFiles = new[] { "compile.stdout.log", "compile.stderr.log", "simulate.stdout.log", "simulate.stderr.log", "compile-process.json", "simulate-process.json" }
                    .Where(p => File.Exists(Path.Combine(failureArtifactDirectory, p))).ToArray()
            };
            var target = Path.Combine(failureArtifactDirectory, "failure-manifest.json");
            EvidenceWriter.Write(target, JsonSerializer.SerializeToElement(evidence, EvidenceJsonContext.Default.FailureEvidence));
            Console.Error.WriteLine($"Failure evidence: {target}");
        }
        catch (Exception evidenceError)
        {
            Console.Error.WriteLine($"Could not write failure evidence: {evidenceError.Message}");
        }
    }
    return 1;
}
finally
{
    workspace?.Dispose();
}
