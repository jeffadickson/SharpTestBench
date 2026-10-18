// Standalone .NET 10 runner for prebuilt SharpTestBench native host/GPI adapter.
// This initial slice supports Icarus only. No cocotb Python process is launched.
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 2 || args[0] != "run")
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/SharpTestBench.Runner -- run <manifest.json>");
    return 2;
}

string? failureArtifactDirectory = null;
try
{
    var manifestPath = Path.GetFullPath(args[1]);
    var root = Path.GetDirectoryName(manifestPath)!;
    var config = JsonSerializer.Deserialize<RunManifest>(File.ReadAllText(manifestPath),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("Empty manifest");
    ManifestValidation.Validate(config);
    var expectedTests = config.ExpectedTests ?? throw new InvalidDataException("expectedTests is required.");
    string Resolve(string name) => Path.GetFullPath(Path.Combine(root, name));
    var sources = config.Sources.Select(Resolve).ToArray();
    var assembly = Resolve(config.Assembly);
    var host = Resolve(config.NativeHost);
    var bootstrap = Resolve(config.IcarusBootstrap);
    var gpi = Resolve(config.GpiLibrary);
    if (string.IsNullOrWhiteSpace(config.GpiImpl))
        throw new InvalidDataException("gpiImpl must specify the simulator-specific GPI implementation string.");
    var sdk = Resolve(config.DotnetRoot);
    var work = ManifestValidation.ResolveOutput(root, config.OutputDirectory);
    Directory.CreateDirectory(work);
    failureArtifactDirectory = work;
    foreach (var required in sources.Concat([assembly, host, bootstrap, gpi]))
        if (!File.Exists(required)) throw new FileNotFoundException("Required input not found", required);
    var runtimeConfig = Path.Combine(Path.GetDirectoryName(assembly)!, Path.GetFileNameWithoutExtension(assembly) + ".runtimeconfig.json");
    if (!File.Exists(runtimeConfig)) throw new FileNotFoundException("Missing runtimeconfig", runtimeConfig);
    var fxrDir = Path.Combine(sdk, "host", "fxr");
    if (!Directory.Exists(fxrDir)) throw new DirectoryNotFoundException(fxrDir);
    var fxr = Directory.GetDirectories(fxrDir).OrderByDescending(x =>
        Version.TryParse(Path.GetFileName(x), out var v) ? v : new Version(0, 0)).FirstOrDefault()
        ?? throw new InvalidDataException("No hostfxr found");
    var fxrFile = Directory.EnumerateFiles(fxr, OperatingSystem.IsWindows() ? "hostfxr.dll" : OperatingSystem.IsMacOS() ? "libhostfxr.dylib" : "libhostfxr.so").FirstOrDefault()
        ?? throw new InvalidDataException("No hostfxr library found");
    var compiled = Path.Combine(work, "top.vvp");
    await Run(config.Iverilog ?? "iverilog", ["-g2012", "-s", config.Top, "-o", compiled, ..sources], work, null, "compile");
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
    var searchPath = Path.GetDirectoryName(host)! + Path.PathSeparator + Path.GetDirectoryName(gpi);
    var loaderKey = OperatingSystem.IsWindows() ? "PATH" : OperatingSystem.IsMacOS() ? "DYLD_LIBRARY_PATH" : "LD_LIBRARY_PATH";
    env[loaderKey] = searchPath + Path.PathSeparator + Environment.GetEnvironmentVariable(loaderKey);
    var command = config.Vvp ?? "vvp";
    await Run(command, ["-m", bootstrap, compiled], work, env, "simulate");
    if (!File.Exists(results)) throw new InvalidDataException("Simulator did not produce a result JSON file");
    using var doc = JsonDocument.Parse(File.ReadAllText(results));
    var attestation = ResultAttestation.Verify(doc.RootElement, config);
    int failed = attestation.Failed;
    var evidence = new
    {
        SchemaVersion = 1, config.Simulator, config.Top, config.Seed,
        Sources = sources.Select(s => new { Path = s, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(s))) }),
        AssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),
        Results = results, Passed = attestation.Passed, Failed = failed,
        TestNames = attestation.Names,
        Status = failed == 0 ? "passed" : "failed"
    };
    File.WriteAllText(Path.Combine(work, "run-manifest.json"),
        JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"SharpTestBench: {doc.RootElement.GetArrayLength()} tests; {failed} failed. {results}");
    return failed == 0 ? 0 : 1;
}
catch (Exception ex)
{
    var kind = ex switch
    {
        TimeoutException => "timeout",
        FileNotFoundException or DirectoryNotFoundException or InvalidDataException or JsonException => "configuration_error",
        InvalidOperationException when ex.Message.Contains("iverilog", StringComparison.OrdinalIgnoreCase) => "build_failure",
        InvalidOperationException when ex.Message.Contains("vvp", StringComparison.OrdinalIgnoreCase) => "simulator_failure",
        _ => "infrastructure_failure"
    };
    Console.Error.WriteLine($"SharpTestBench runner {kind}: {ex.GetType().Name}: {ex.Message}");
    if (failureArtifactDirectory is not null)
    {
        try
        {
            var evidence = new
            {
                SchemaVersion = 1, Status = "failed", Kind = kind,
                ErrorType = ex.GetType().FullName, Error = ex.Message,
                RecordedAtUtc = DateTimeOffset.UtcNow,
                LogFiles = new[] { "compile.stdout.log", "compile.stderr.log", "simulate.stdout.log", "simulate.stderr.log" }
                    .Where(p => File.Exists(Path.Combine(failureArtifactDirectory, p))).ToArray()
            };
            var target = Path.Combine(failureArtifactDirectory, "failure-manifest.json");
            File.WriteAllText(target, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
            Console.Error.WriteLine($"Failure evidence: {target}");
        }
        catch (Exception evidenceError)
        {
            Console.Error.WriteLine($"Could not write failure evidence: {evidenceError.Message}");
        }
    }
    return 1;
}

static async Task Run(string executable, IEnumerable<string> arguments, string cwd, Dictionary<string,string>? environment, string phase)
{
    var psi = new ProcessStartInfo(executable) { WorkingDirectory = cwd, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in arguments) psi.ArgumentList.Add(argument);
    if (environment != null)
        foreach (var (key, value) in environment) psi.Environment[key] = value;
    using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Cannot start {executable}");
    var stdoutPath = Path.Combine(cwd, phase + ".stdout.log");
    var stderrPath = Path.Combine(cwd, phase + ".stderr.log");
    using var stdoutFile = File.Create(stdoutPath);
    using var stderrFile = File.Create(stderrPath);
    var outTask = p.StandardOutput.BaseStream.CopyToAsync(stdoutFile);
    var errTask = p.StandardError.BaseStream.CopyToAsync(stderrFile);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
    try { await p.WaitForExitAsync(timeout.Token); }
    catch (OperationCanceledException)
    {
        if (!p.HasExited) p.Kill(entireProcessTree: true);
        throw new TimeoutException($"Process exceeded 120 seconds: {executable}");
    }
    await Task.WhenAll(outTask, errTask);
    if (p.ExitCode != 0) throw new InvalidOperationException($"{executable} exited with {p.ExitCode}. Logs: {stdoutPath}, {stderrPath}");
}
