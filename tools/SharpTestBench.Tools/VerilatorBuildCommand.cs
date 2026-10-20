using System.Runtime.InteropServices;
using System.Text.Json;

namespace SharpTestBench.Tools;

internal static class VerilatorBuildCommand
{
    public static string Build(ToolContext context, string[] sources, string top, string directory, bool waves = false)
    {
        var compiler = Environment.GetEnvironmentVariable("CXX") ?? "c++";
        var version = context.Run([compiler, "--version"], capture: true).Output;
        var vectors = version.Contains("clang", StringComparison.OrdinalIgnoreCase) ? "-fvectorize -fslp-vectorize" : "-ftree-loop-vectorize -ftree-slp-vectorize";
        string[] flags = ["--Wno-fatal", "--timing", "-CFLAGS", "-std=c++20 -O3 -flto " + vectors, "-LDFLAGS", "-O3 -flto"];
        var settings = JsonSerializer.Serialize(new { sources, hdl_toplevel = top, build_dir = directory, always = true, waves, build_args = flags });
        // Upstream exposes its model builder as a Python API; orchestration and provenance remain managed.
        context.Run([Environment.GetEnvironmentVariable("CS_PYTHON") ?? "python", "-c", "import json,sys; from cocotb_tools.runner import get_runner; get_runner('verilator').build(**json.loads(sys.argv[1]))", settings]);
        var executable = Path.Combine(directory, top);
        StrictJson.Write(Path.Combine(directory, "model-build.json"), new { architecture = RuntimeInformation.ProcessArchitecture.ToString(), platform = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "Darwin" : "Linux", compiler = version.Split('\n')[0].TrimEnd('\r'), build_args = flags, simulator = context.Run(["verilator", "--version"], capture: true).Output.Trim(), waves, top, sources = sources.ToDictionary(path => path, CiCommands.Hash), executable_sha256 = CiCommands.Hash(executable) });
        return executable;
    }
}
