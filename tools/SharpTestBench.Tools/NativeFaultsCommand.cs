using System.Security;
using System.Text.RegularExpressions;
namespace SharpTestBench.Tools;
internal static class NativeFaultsCommand
{
    public static string FakeSource(string declarations, string fixture)
    {
        var source = "#include \"abi.h\"\n#include <stdlib.h>\n" + fixture;
        var values = new Dictionary<string, string> { ["gpi_get_root_handle"] = "(void *)1", ["gpi_get_signal_fullname_str"] = "\"fake.signal\"", ["gpi_get_object_type"] = "10", ["gpi_get_signal_value_long"] = "-2147483649L", ["gpi_get_num_elems"] = "getenv(\"CS_FAKE_WIDTH\") ? atoi(getenv(\"CS_FAKE_WIDTH\")) : 9", ["gpi_get_simulator_product"] = "getenv(\"CS_FAKE_PRODUCT\") ? getenv(\"CS_FAKE_PRODUCT\") : \"Icarus Verilog fault fixture\"", ["gpi_get_signal_value_binstr"] = "getenv(\"CS_FAKE_LOGIC\") ? getenv(\"CS_FAKE_LOGIC\") : \"10XZ01XZ1\"", ["gpi_is_constant"] = "getenv(\"CS_FAKE_CONSTANT\") != NULL" };
        var bodies = new Dictionary<string, string> { ["gpi_register_timed_callback"] = "return fake_register(cb, data, delay, 1);", ["gpi_register_readwrite_callback"] = "return fake_register(cb, data, 0, 0);", ["gpi_remove_cb"] = "return fake_remove(cb);", ["gpi_set_signal_value_int"] = "++fake_write_count; fake_value=value; if(fake_on_write) fake_on_write(NULL);" };
        var matches = Regex.Matches(declarations, @"^CS_EXPORT (.+?) cs_v1_(\w+)\((.*?)\);\r?$", RegexOptions.Multiline);
        if (matches.Count == 0) throw new InvalidDataException("Native ABI declarations missing");
        foreach (Match match in matches)
        {
            var name = match.Groups[2].Value; var type = name == "gpi_get_signal_value_long" ? "long" : match.Groups[1].Value;
            var body = bodies.GetValueOrDefault(name) ?? (type == "void" ? "" : "return " + values.GetValueOrDefault(name, "0") + ";");
            source += type + " " + name + "(" + match.Groups[3].Value + ") {" + body + "}\n";
        }
        return source;
    }
    public static void Execute(ToolContext context, string[] arguments)
    {
        if (arguments.Length != 0) throw new ArgumentException("native-faults accepts no arguments");
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Native fault fixtures require Linux");
        var build = context.At("build/native-faults"); Directory.CreateDirectory(build); File.Delete(context.At("build/native-faults.json"));
        var host = context.At("build/libsharptestbench_host.so");
        var fake = FakeSource(File.ReadAllText(context.At("native/abi.h")), File.ReadAllText(context.At("tests/Native/clock_fixture.h")));
        foreach (var (name, source, file) in new[] { ("registration", fake, "libgpi.so"), ("missing", "void unrelated(void) {}", "libgpi.so"), ("legacy", "unsigned cs_abi_version(void) { return 1; }", "libsharptestbench_host.so"), ("version", "unsigned cs_abi_version(void) { return 99; }", "libsharptestbench_host.so") })
        {
            var folder = Path.Combine(build, name); Directory.CreateDirectory(folder); var input = Path.Combine(folder, "fake.c"); File.WriteAllText(input, source);
            context.Run(["gcc", "-shared", "-fPIC", "-I", context.At("native"), input, "-o", Path.Combine(folder, file)]);
            if (name is not ("version" or "legacy")) File.Copy(host, Path.Combine(folder, Path.GetFileName(host)), true);
        }
        var reference = SecurityElement.Escape(context.At("src/SharpTestBench/bin/Release/net10.0/SharpTestBench.dll"));
        File.WriteAllText(Path.Combine(build, "Faults.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><Reference Include=\"SharpTestBench\"><HintPath>" + reference + "</HintPath></Reference></ItemGroup></Project>");
        using (var resource = typeof(NativeFaultsCommand).Assembly.GetManifestResourceStream("SharpTestBench.Tools.NativeFaultProbe.cs.txt") ?? throw new InvalidDataException("Native fault probe resource missing")) using (var reader = new StreamReader(resource)) File.WriteAllText(Path.Combine(build, "Program.cs"), reader.ReadToEnd());
        context.Run(["dotnet", "build", Path.Combine(build, "Faults.csproj"), "-c", "Release"]);
        var results = new List<object>();
        void Probe(string name, string[] command, Dictionary<string, string?> environment)
        {
            var result = context.Run(command, environment: environment, capture: true, check: false, timeout: TimeSpan.FromSeconds(120));
            File.WriteAllText(Path.Combine(build, name + "-driver.log"), result.Output + result.Error);
            if (result.ExitCode != 0) throw new InvalidDataException("Native fault probe failed: " + name + "\n" + result.Output + result.Error);
            results.Add(new Dictionary<string, object> { ["case"] = name, ["stdout"] = result.Output.Trim(), ["stderr"] = result.Error.Trim(), ["exit"] = result.ExitCode }); Console.WriteLine(result.Output.Trim());
        }
        foreach (var name in new[] { "version", "legacy", "missing", "registration", "unknown" })
        {
            var environment = new Dictionary<string, string?> { ["LD_LIBRARY_PATH"] = Path.Combine(build, name == "unknown" ? "registration" : name) };
            if (name == "unknown") { environment["CS_FAKE_PRODUCT"] = "Unknown Simulator"; environment["CS_FAKE_WIDTH"] = "8"; }
            Probe(name, ["dotnet", Path.Combine(build, "bin/Release/net10.0/Faults.dll"), name], environment);
        }
        foreach (var name in new[] { "packed-boundary", "clock-boundary" }) Probe(name, ["dotnet", typeof(ToolContext).Assembly.Location, name, Path.Combine(build, "registration", Path.GetFileName(host))], new() { ["LD_LIBRARY_PATH"] = Path.Combine(build, "registration") });
        StrictJson.Write(context.At("build/native-faults.json"), results);
    }
}
