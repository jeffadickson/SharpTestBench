using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
namespace SharpTestBench.Tools;
internal static class ReferenceCommand
{
    static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    public static string OraclePython(ToolContext context) => Path.Combine(context.At("build/reference-python"), OperatingSystem.IsWindows() ? "Scripts/python.exe" : "bin/python");
    public static string PrepareOracle(ToolContext context)
    {
        var target = context.At("build/reference-python"); var python = OraclePython(context);
        if (!File.Exists(python)) context.Run([Environment.GetEnvironmentVariable("CS_PYTHON") ?? "python", "-m", "venv", target]);
        context.Run([python, "-m", "pip", "install", "cocotb==2.1.0", "find_libpython==0.5.1"]); return python;
    }
    static Dictionary<string, string?> EnvironmentFor(CocotbAdapter adapter, string modulePath, string module, string top, string report, bool released)
    {
        var environment = new Dictionary<string, string?> { ["COCOTB_BOOTSTRAP"] = null, ["GPI_IMPL"] = null, ["GPI_EXTRA"] = null, ["COCOTB_TEST_FILTER"] = null, ["COCOTB_TESTCASE"] = null, ["PYTHONPATH"] = modulePath, ["PYGPI_PYTHON_BIN"] = adapter.Python, ["LIBPYTHON_LOC"] = adapter.LibPython, ["COCOTB_TOPLEVEL"] = top, ["COCOTB_TEST_MODULES"] = module, ["COCOTB_RESULTS_FILE"] = report };
        if (released) environment["GPI_USERS"] = adapter.LibPython + ";" + adapter.PyGpiEntryPoint;
        else { environment["GPI_IMPL"] = adapter.Implementation; environment["COCOTB_BOOTSTRAP"] = string.Join(Path.PathSeparator, adapter.GpiEntryPoint, adapter.LibPython, adapter.PyGpiEntryPoint); }
        if (OperatingSystem.IsWindows()) environment["PATH"] = adapter.LibsDir + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"); return environment;
    }
    static string Bootstrap(CocotbAdapter adapter)
    {
        if (!OperatingSystem.IsWindows()) return adapter.Bootstrap;
        var path = Path.ChangeExtension(adapter.Bootstrap, ".vpi"); if (path != adapter.Bootstrap) File.Copy(adapter.Bootstrap, path, true); return path;
    }
    public static void ValidateReport(string path, bool exactlyOne)
    {
        var document = XDocument.Load(path); var count = document.Descendants("testcase").Count();
        Require(count > 0 && (!exactlyOne || count == 1) && !document.Descendants("failure").Any() && !document.Descendants("error").Any(), "Python oracle failed or reported no tests");
    }
    public static void Workload(ToolContext context, string kind, string work, string? python = null)
    {
        var definition = kind switch { "axi" => (Project: "AxiMemory", Top: "axi_memory", Module: "test_axi"), "dsp" => (Project: "DspFir", Top: "dsp_fir", Module: "test_dsp"), "cpu" => (Project: "CpuCore", Top: "cpu_core", Module: "test_cpu"), _ => throw new ArgumentException("Unknown workload oracle") };
        work = Path.GetFullPath(work); Directory.CreateDirectory(work); var report = Path.Combine(work, "results.xml"); File.Delete(report);
        var adapter = CocotbAdapter.Inspect(context, released: true, python: python ?? OraclePython(context)); var environment = EnvironmentFor(adapter, context.At("examples/" + definition.Project), definition.Module, definition.Top, report, true);
        var ivl = System.Environment.GetEnvironmentVariable("CS_IVL_ROOT");
        context.Run([System.Environment.GetEnvironmentVariable("CS_IVERILOG") ?? "iverilog", .. (ivl is null ? Array.Empty<string>() : ["-B", ivl]), "-g2012", "-s", definition.Top, "-o", Path.Combine(work, "top.vvp"), context.At("examples/" + definition.Project + "/" + definition.Top + ".sv")]);
        var result = context.Run([System.Environment.GetEnvironmentVariable("CS_VVP") ?? "vvp", "-m", Bootstrap(adapter), Path.Combine(work, "top.vvp")], directory: work, environment: environment, capture: true, check: false, timeout: TimeSpan.FromSeconds(150));
        File.WriteAllText(Path.Combine(work, "oracle-driver.log"), result.Output + result.Error); Console.WriteLine(result.Output + result.Error); Require(result.ExitCode == 0 && File.Exists(report), "Python oracle simulator failed"); ValidateReport(report, true);
    }
    public static void CompareStructured(JsonElement actual, JsonElement expected)
    {
        var differences = new Dictionary<string, (JsonElement Managed, JsonElement Python)> {
            ["selection"] = (JsonSerializer.SerializeToElement(new[] { 0, 11, 5, 1 }), JsonSerializer.SerializeToElement(new[] { 0, 11, 2, 0 })),
            ["failure_after_cleanup"] = (JsonSerializer.SerializeToElement(5), JsonSerializer.SerializeToElement(2)), ["parent_cleanup"] = (JsonSerializer.SerializeToElement(4), JsonSerializer.SerializeToElement(2)), ["cleanup_failure"] = (JsonSerializer.SerializeToElement("propagated"), JsonSerializer.SerializeToElement("ignored")) };
        foreach (var pair in differences) Require(actual.TryGetProperty(pair.Key, out var left) && expected.TryGetProperty(pair.Key, out var right) && JsonElement.DeepEquals(left, pair.Value.Managed) && JsonElement.DeepEquals(right, pair.Value.Python), "Changed cancellation contract: " + pair.Key);
        var a = JsonNode.Parse(actual.GetRawText())!.AsObject(); var b = JsonNode.Parse(expected.GetRawText())!.AsObject(); foreach (var name in differences.Keys) { a.Remove(name); b.Remove(name); }
        Require(JsonNode.DeepEquals(a, b), "Structured trace mismatch");
    }
    public static void Execute(ToolContext context, string command, string[] arguments)
    {
        var options = new Options(arguments);
        if (command is "axi-python" or "dsp-python" or "cpu-python") { options.Only("--work-dir", "--python"); Workload(context, command.Split('-')[0], options.Require("--work-dir"), options.Get("--python")); return; }
        if (command == "structured-reference-env") { options.Only("--label"); PrepareOracle(context); Execute(context, "structured-reference", ["--label", options.Require("--label")]); return; }
        options.Only("--label", "--sim", "--python"); var label = options.Get("--label", command == "parity" ? "icarus" : null) ?? throw new ArgumentException("Missing --label");
        if (label.Length == 0 || label.Any(c => !char.IsLetterOrDigit(c) && c is not ('-' or '_'))) throw new ArgumentException("Invalid reference label");
        var simulator = options.Get("--sim", "icarus")!; bool released = command == "structured-reference";
        var adapter = CocotbAdapter.Inspect(context, simulator, released, options.Get("--python", released ? OraclePython(context) : null));
        var build = context.At("build"); Directory.CreateDirectory(build);
        var reference = Path.Combine(build, command == "parity" ? "python-trace.txt" : released ? "structured-python.json" : label + "-python-order");
        var report = command == "parity" ? Path.Combine(build, "python-results.xml") : released ? Path.Combine(build, "structured-python.xml") : Path.ChangeExtension(reference, ".xml");
        var module = command switch { "parity" => "test_parity", "structured-reference" => "test_structured_reference", "timing-parity" => "test_timing_order", _ => throw new ArgumentException("Unknown reference command") };
        var environment = EnvironmentFor(adapter, context.At("tests/Integration"), module, "top", report, released); environment[released ? "STRUCTURED_TRACE" : "PARITY_TRACE"] = reference; environment["TIMING_SIM"] = simulator;
        string[] suffixes = command == "parity" ? ["", ".delta"] : released ? [""] : simulator == "icarus" ? [".scheduled", ".force"] : [".scheduled"];
        File.Delete(report); foreach (var suffix in suffixes) File.Delete(reference + suffix);
        var bootstrap = Bootstrap(adapter);
        string[] invocation = simulator switch { "icarus" => [System.Environment.GetEnvironmentVariable("CS_VVP") ?? "vvp", "-m", bootstrap, Path.Combine(build, "top.vvp")], "verilator" => [Path.Combine(build, "verilator/top")], "ghdl" => [System.Environment.GetEnvironmentVariable("CS_GHDL") ?? "ghdl", "-r", "--std=08", "top", "--vpi=" + bootstrap], "nvc" => [System.Environment.GetEnvironmentVariable("CS_NVC") ?? "nvc", "--std=2008", "--work=work:" + Path.Combine(build, "nvc-work"), "-e", "top", "--no-save", "--jit", "-r", "--load=" + bootstrap], _ => throw new ArgumentException("Unknown simulator") };
        var result = context.Run(invocation, directory: build, environment: environment, capture: true, check: false, timeout: TimeSpan.FromSeconds(120));
        File.WriteAllText(Path.Combine(build, command == "parity" ? "python-parity.log" : released ? "structured-python.log" : label + "-python-order.log"), result.Output + result.Error); Require(result.ExitCode == 0 && File.Exists(report), "Reference simulator failed"); ValidateReport(report, released);
        if (released)
        {
            using var expected = StrictJson.Read(reference); using var actual = StrictJson.Read(Path.Combine(build, label + "-trace.txt.structured.json")); CompareStructured(actual.RootElement, expected.RootElement);
            StrictJson.Write(Path.Combine(build, "structured-comparison.json"), new { cocotb_version = adapter.Version, managed = actual.RootElement.Clone(), python = expected.RootElement.Clone(), intentional_differences = new[] { "selection", "failure_after_cleanup", "parent_cleanup", "cleanup_failure" } });
        }
        else foreach (var suffix in suffixes) Require(File.ReadAllText(reference + suffix) == File.ReadAllText(Path.Combine(build, label + "-trace.txt" + suffix)), "Reference trace mismatch: " + suffix);
        Console.WriteLine(command.ToUpperInvariant().Replace('-', '_') + "_PASS " + simulator);
    }
}
