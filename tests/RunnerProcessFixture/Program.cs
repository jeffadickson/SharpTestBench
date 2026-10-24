// Controlled child executable for production CLI contracts. Does not simulate HDL.
using System.Text.Json;
using System.Diagnostics;
using System.Reflection;

// A live root process with both streams published. Its PID and creation time
// let the caller prove termination and avoid killing a reused/unrelated PID.
if (args is ["--capture-deadline", var ownershipFile])
{
    using var self=Process.GetCurrentProcess();
    File.WriteAllText(ownershipFile,Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n"+self.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
    Console.WriteLine("before stdout");Console.Out.Flush();
    Console.Error.WriteLine("before stderr");Console.Error.Flush();
    await Task.Delay(TimeSpan.FromSeconds(60));
    return 0;
}
// Capture tests need a readiness handshake and a descendant lifetime independent
// of the command deadline. These controlled modes bypass simulator-fixture JSON.
if (args is ["--capture-descendant", var descendantGate])
{
    Console.Out.Flush();
    File.WriteAllText(descendantGate + ".pid", Environment.ProcessId.ToString());
    File.WriteAllText(descendantGate + ".ready", "inherited streams open");
    var fallback = Stopwatch.StartNew();
    while (!File.Exists(descendantGate) && fallback.Elapsed < TimeSpan.FromSeconds(60))
        await Task.Delay(25);
    return 0;
}
if (args is ["--capture-parent", var parentGate])
{
    var childStart = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
    if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        childStart.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    childStart.ArgumentList.Add("--capture-descendant");
    childStart.ArgumentList.Add(parentGate);
    using var descendant = Process.Start(childStart) ?? throw new IOException("Capture descendant did not start");
    var ready = Stopwatch.StartNew();
    while (!File.Exists(parentGate + ".ready") && ready.Elapsed < TimeSpan.FromSeconds(30))
        await Task.Delay(10);
    if (!File.Exists(parentGate + ".ready")) return 75;
    Console.WriteLine("parent exited");
    Console.Out.Flush();
    return 0;
}
var behaviorPath = Environment.GetEnvironmentVariable("STB_PROCESS_FIXTURE_CONFIG") ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "process-fixture.json");
using var behavior = JsonDocument.Parse(File.ReadAllText(behaviorPath));
var config = behavior.RootElement;
if (args.Length>0 && args[0]=="-c" && config.TryGetProperty("adapter",out var adapter)) { Console.WriteLine(adapter.GetRawText());return 0; }
bool Flag(string key) => config.TryGetProperty(key, out var value) && value.GetBoolean();
int Number(string key) => config.TryGetProperty(key, out var value) ? value.GetInt32() : 0;
bool compile = args.Contains("-o");
string phase = compile ? "compile" : "simulate";
Console.WriteLine("Controlled process fixture: " + phase);
if (config.TryGetProperty("mutatePhase", out var mutate) && mutate.GetString() == phase)
    File.AppendAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(behaviorPath))!, "input.sv"), "\n// controlled mutation\n");
if (compile)
{
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(behaviorPath))!,"fixture-compile-argv.json"),JsonSerializer.Serialize(args));
    int index = Array.IndexOf(args, "-o");
    if (index < 0 || index + 1 >= args.Length) return 64;
    if (!Flag("omitCompiled"))
        File.WriteAllText(args[index + 1], Flag("emptyCompiled") ? "" : "Controlled compiler output; not executable HDL.");
    if (Flag("blockCompileStdoutLog")) Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(args[index+1])!,"guard-compile.stdout.log"));
    if (Flag("blockCompileProcess")) Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(args[index+1])!,"guard-compile-process.json"));
    if (Number("compileStderrBytes")>0) Console.Error.Write(new string('E',Number("compileStderrBytes")));
    if (Flag("blockCompileLog")) Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(args[index+1])!,"guard-compile.stderr.log"));
    if (Number("compileDelayMs")>0) await Task.Delay(Number("compileDelayMs"));
    Console.Error.WriteLine("controlled compiler diagnostic");
    return Number("compileExit");
}
if (args.Length < 3 || args[0] != "-m") return 64;
if (Flag("writeLog")) File.WriteAllText(Environment.GetEnvironmentVariable("CS_LOG_JSON") ?? throw new InvalidOperationException("Missing log path"),"controlled runtime log\n");
if (Flag("mutateWaveHelper")) File.AppendAllText(Path.Combine(Directory.GetCurrentDirectory(),"guard-wave-dump.sv"),"// mutation");
if (Flag("mutateBootstrap")) File.AppendAllText(args[1],"\ncontrolled bootstrap mutation\n");
if (Flag("mutateCompiled")) File.AppendAllText(args[2], "\ncontrolled image mutation\n");
if (Flag("earlyResults")) File.WriteAllText(Environment.GetEnvironmentVariable("CS_RESULTS")!,config.GetProperty("resultText").GetString());
int stderrBytes=Number("stderrBytes");if(stderrBytes>0) Console.Error.Write(new string('E',stderrBytes));
int logBytes = Number("stdoutBytes");
if (logBytes > 0) Console.Write(new string('L', logBytes));
var names = new[] { "GPI_IMPL", "CS_LOG_JSON", "CS_SEED", "CS_TEST_FILTER", "CS_TEST_NAMES", "CS_TEST_STAGES", "CS_LIST_TESTS", "CS_STAGE_FAILURE_POLICY" };
File.WriteAllText("fixture-environment.json", JsonSerializer.Serialize(names.ToDictionary(key => key, Environment.GetEnvironmentVariable)));
if (Flag("blockRunLog")) Directory.CreateDirectory((Environment.GetEnvironmentVariable("CS_RESULTS") ?? throw new InvalidOperationException("Missing results")).Replace("-results.json","-run.log",StringComparison.Ordinal));
if (Flag("blockFailureArtifact")) Directory.CreateDirectory((Environment.GetEnvironmentVariable("CS_RESULTS") ?? throw new InvalidOperationException("Missing results")).Replace("-results.json","-failure.json",StringComparison.Ordinal));
if (Flag("blockSimulateCapture")) Directory.CreateDirectory((Environment.GetEnvironmentVariable("CS_RESULTS") ?? throw new InvalidOperationException("Missing results")).Replace("-results.json","-simulate-capture.json",StringComparison.Ordinal));
int delay = Number("simulateDelayMs");
if (delay != 0) await Task.Delay(delay);
if (!Flag("omitResults"))
{
    string resultPath = Environment.GetEnvironmentVariable("CS_RESULTS") ?? throw new InvalidOperationException("Missing results destination");
    if (config.TryGetProperty("resultHex", out var hex)) File.WriteAllBytes(resultPath, Convert.FromHexString(hex.GetString()!));
    else File.WriteAllText(resultPath, config.GetProperty("resultText").GetString());
}
if (config.TryGetProperty("reportFailureStream", out var report))
{
    var stream = report.GetString() == "stderr" ? Console.Error : Console.Out;
    stream.WriteLine("CS_REPORT_FAILURE controlled publication failure");
}
return Number("simulateExit");
