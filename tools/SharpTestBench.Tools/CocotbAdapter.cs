using System.Text.Json;

namespace SharpTestBench.Tools;

internal sealed record CocotbAdapter(string Version, string Python, string LibsDir, string Bootstrap, string? GpiEntryPoint, string? Implementation, string? PyGpiEntryPoint, string? LibPython)
{
    const string Query = "import json, sys, cocotb\nfrom cocotb_tools import config\nfrom find_libpython import find_libpython\nsim, interface, mode = sys.argv[1:]\nnative = mode == \"native\"\nprint(json.dumps(dict(Version=cocotb.__version__, Python=sys.executable, LibsDir=str(config.libs_dir), Bootstrap=str(config.lib_entry(interface, sim) if native else config.lib_name_path(interface, sim)), GpiEntryPoint=config.gpi_entry_point() if native else None, Implementation=config.gpi_impl(sim, interface) if native else None, PyGpiEntryPoint=config.pygpi_entry_point(), LibPython=find_libpython())))\n";
    public static CocotbAdapter Inspect(ToolContext context, string simulator = "icarus", bool released = false, string? python = null)
    {
        if (!new[] { "icarus", "verilator", "ghdl", "nvc" }.Contains(simulator, StringComparer.Ordinal)) throw new ArgumentException("Unknown simulator");
        python ??= Environment.GetEnvironmentVariable("CS_PYTHON") ?? "python";
        var result = context.Run([python, "-c", Query, simulator, simulator == "nvc" ? "vhpi" : "vpi", released ? "released" : "native"], capture: true);
        using var document = StrictJson.Parse(result.Output);
        var adapter = document.RootElement.Deserialize<CocotbAdapter>() ?? throw new InvalidDataException("Missing adapter configuration");
        if (released && adapter.Version != "2.1.0") throw new InvalidDataException("Released oracle requires cocotb 2.1.0");
        return adapter;
    }
    public (string Library, string Entry) NativeEntry()
    {
        int index = GpiEntryPoint?.LastIndexOf(',') ?? -1;
        if (GpiEntryPoint is null || index <= 0 || index == GpiEntryPoint.Length - 1) throw new InvalidDataException("Invalid GPI entry point");
        return (GpiEntryPoint[..index], GpiEntryPoint[(index + 1)..]);
    }
    public (string Library, string Entry) ImplementationEntry()
    {
        int index = Implementation?.LastIndexOf(':') ?? -1;
        if (Implementation is null || index <= 0 || index == Implementation.Length - 1) throw new InvalidDataException("Invalid implementation entry point");
        return (Implementation[..index], Implementation[(index + 1)..]);
    }

}
