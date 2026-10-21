// Controlled child executable for production CLI contracts. Does not simulate HDL.
using System.Text.Json;

var behaviorPath = Environment.GetEnvironmentVariable("STB_PROCESS_FIXTURE_CONFIG") ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "process-fixture.json");
using var behavior = JsonDocument.Parse(File.ReadAllText(behaviorPath));
var config = behavior.RootElement;
bool Flag(string key) => config.TryGetProperty(key, out var value) && value.GetBoolean();
int Number(string key) => config.TryGetProperty(key, out var value) ? value.GetInt32() : 0;
bool compile = args.Contains("-o");
string phase = compile ? "compile" : "simulate";
Console.WriteLine("Controlled process fixture: " + phase);
if (config.TryGetProperty("mutatePhase", out var mutate) && mutate.GetString() == phase)
    File.AppendAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(behaviorPath))!, "input.sv"), "\n// controlled mutation\n");
if (compile)
{
    int index = Array.IndexOf(args, "-o");
    if (index < 0 || index + 1 >= args.Length) return 64;
    if (!Flag("omitCompiled"))
        File.WriteAllText(args[index + 1], Flag("emptyCompiled") ? "" : "Controlled compiler output; not executable HDL.");
    return Number("compileExit");
}
if (args.Length != 3 || args[0] != "-m") return 64;
if (Flag("mutateCompiled")) File.AppendAllText(args[2], "\ncontrolled image mutation\n");
int logBytes = Number("stdoutBytes");
if (logBytes > 0) Console.Write(new string('L', logBytes));
var names = new[] { "CS_SEED", "CS_TEST_FILTER", "CS_TEST_NAMES", "CS_TEST_STAGES", "CS_LIST_TESTS", "CS_STAGE_FAILURE_POLICY" };
File.WriteAllText("fixture-environment.json", JsonSerializer.Serialize(names.ToDictionary(key => key, Environment.GetEnvironmentVariable)));
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
