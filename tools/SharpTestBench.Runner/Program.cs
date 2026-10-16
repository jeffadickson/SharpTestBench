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

try
{
    var manifestPath = Path.GetFullPath(args[1]);
    var root = Path.GetDirectoryName(manifestPath)!;
    var config = JsonSerializer.Deserialize<RunManifest>(File.ReadAllText(manifestPath),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("Empty manifest");
    if (config.SchemaVersion != 1 || config.Simulator != "icarus")
        throw new InvalidDataException("Runner v1 supports schemaVersion=1 and simulator=icarus only.");
    if (config.Sources is not { Length: > 0 } || string.IsNullOrWhiteSpace(config.Top))
        throw new InvalidDataException("At least one source and a top module are required.");
    if (!System.Text.RegularExpressions.Regex.IsMatch(config.Top, @"^[A-Za-z_][A-Za-z0-9_$]*$"))
        throw new InvalidDataException("Top module must be a simple HDL identifier.");
    string Resolve(string name) => Path.GetFullPath(Path.Combine(root, name));
    var sources = config.Sources.Select(Resolve).ToArray();
    var assembly = Resolve(config.Assembly);
    var host = Resolve(config.NativeHost);
    var bootstrap = Resolve(config.IcarusBootstrap);
    var gpi = Resolve(config.GpiLibrary);
    if (string.IsNullOrWhiteSpace(config.GpiImpl))
        throw new InvalidDataException("gpiImpl must specify the simulator-specific GPI implementation string.");
    var sdk = Resolve(config.DotnetRoot);
    var work = Resolve(config.OutputDirectory);
    foreach (var required in sources.Concat([assembly, host, bootstrap, gpi]))
        if (!File.Exists(required)) throw new FileNotFoundException("Required input not found", required);
    var runtimeConfig = Path.ChangeExtension(assembly, ".runtimeconfig.json");
    if (!File.Exists(runtimeConfig)) throw new FileNotFoundException("Missing runtimeconfig", runtimeConfig);
    var fxrDir = Path.Combine(sdk, "host", "fxr");
    if (!Directory.Exists(fxrDir)) throw new DirectoryNotFoundException(fxrDir);
    var fxr = Directory.GetDirectories(fxrDir).OrderByDescending(x =>
        Version.TryParse(Path.GetFileName(x), out var v) ? v : new Version(0, 0)).FirstOrDefault()
        ?? throw new InvalidDataException("No hostfxr found");
    var fxrFile = Directory.EnumerateFiles(fxr, OperatingSystem.IsWindows() ? "hostfxr.dll" : OperatingSystem.IsMacOS() ? "libhostfxr.dylib" : "libhostfxr.so").FirstOrDefault()
        ?? throw new InvalidDataException("No hostfxr library found");
    Directory.CreateDirectory(work);
    var compiled = Path.Combine(work, "top.vvp");
    await Run(config.Iverilog ?? "iverilog", ["-g2012", "-s", config.Top, "-o", compiled, ..sources], work, null);
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
    await Run(command, ["-m", bootstrap, compiled], work, env);
    if (!File.Exists(results)) throw new InvalidDataException("Simulator did not produce a result JSON file");
    using var doc = JsonDocument.Parse(File.ReadAllText(results));
    if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        throw new InvalidDataException("No test results");
    int failed = 0;
    int passed = 0;
    foreach (var item in doc.RootElement.EnumerateArray())
    {
        if (!item.TryGetProperty("Status", out var status) || status.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Test result without Status.");
        switch (status.GetString())
        {
            case "passed": passed++; break;
            case "failed": failed++; break;
            case "skipped": break;
            default: throw new InvalidDataException($"Unknown test status: {status.GetString()}");
        }
    }
    if (passed == 0) throw new InvalidDataException("No passing tests; all-skipped regressions cannot qualify CI.");
    if (config.ExpectedTests is null || config.ExpectedTests <= 0)
        throw new InvalidDataException("expectedTests must be a positive count for CI attestation.");
    if (doc.RootElement.GetArrayLength() != config.ExpectedTests.Value)
        throw new InvalidDataException($"Expected {config.ExpectedTests} test results, observed {doc.RootElement.GetArrayLength()}.");
    if (passed + failed != config.ExpectedTests.Value)
        throw new InvalidDataException("Skipped test cases cannot satisfy the required CI manifest.");
    var evidence = new
    {
        SchemaVersion = 1, config.Simulator, config.Top, config.Seed,
        Sources = sources.Select(s => new { Path = s, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(s))) }),
        AssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),
        Results = results, Failed = failed,
        Status = failed == 0 ? "passed" : "failed"
    };
    File.WriteAllText(Path.Combine(work, "run-manifest.json"),
        JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"SharpTestBench: {doc.RootElement.GetArrayLength()} tests; {failed} failed. {results}");
    return failed == 0 ? 0 : 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"SharpTestBench runner failed: {ex.GetType().Name}: {ex.Message}");
    return 1;
}

static async Task Run(string executable, IEnumerable<string> arguments, string cwd, Dictionary<string,string>? environment)
{
    var psi = new ProcessStartInfo(executable) { WorkingDirectory = cwd, UseShellExecute = false };
    foreach (var argument in arguments) psi.ArgumentList.Add(argument);
    if (environment != null)
        foreach (var (key, value) in environment) psi.Environment[key] = value;
    using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Cannot start {executable}");
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
    try { await p.WaitForExitAsync(timeout.Token); }
    catch (OperationCanceledException)
    {
        if (!p.HasExited) p.Kill(entireProcessTree: true);
        throw new TimeoutException($"Process exceeded 120 seconds: {executable}");
    }
    if (p.ExitCode != 0) throw new InvalidOperationException($"{executable} exited with {p.ExitCode}");
}

sealed record RunManifest(
    int SchemaVersion, string Simulator, string Top, string[] Sources,
    string Assembly, string NativeHost, string IcarusBootstrap, string GpiLibrary,
    string DotnetRoot, string OutputDirectory, int Seed = 1,
    string? EntryType = null, string GpiEntryPoint = "entry_point", string? GpiImpl = null,
    string? Iverilog = null, string? Vvp = null, int? ExpectedTests = null);
