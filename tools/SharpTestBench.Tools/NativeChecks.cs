using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SharpTestBench.Tools;

internal static class NativeChecks
{
    public static ushort PeMachine(string path)
    {
        using var stream = File.OpenRead(path); using var input = new BinaryReader(stream);
        if (stream.Length < 2 || input.ReadUInt16() != 0x5a4d) throw new InvalidDataException("Not a PE image: " + path);
        if (stream.Length < 64) throw new InvalidDataException("Truncated PE header: " + path);
        stream.Position = 0x3c; var offset = input.ReadUInt32();
        if (offset > stream.Length - 6) throw new InvalidDataException("Truncated PE signature: " + path);
        stream.Position = offset;
        if (input.ReadUInt32() != 0x4550) throw new InvalidDataException("Invalid PE signature: " + path);
        return input.ReadUInt16();
    }
    public static object Dependencies(ToolContext context, string native, string[]? simulatorHosts = null, string? platform = null)
    {
        native = Path.GetFullPath(native); platform ??= OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsMacOS() ? "darwin" : "linux";
        var hosts = (simulatorHosts ?? []).Select(name => name.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var reports = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(native).Order(StringComparer.Ordinal))
        {
            if (!new[] { ".so", ".dylib", ".dll", ".vpi" }.Contains(Path.GetExtension(path).ToLowerInvariant())) continue;
            string output;
            if (platform == "linux")
            {
                var result = context.Run(["ldd", path], environment: new Dictionary<string, string?> { ["LD_LIBRARY_PATH"] = native }, capture: true); output = result.Output + result.Error;
                if (output.Contains("not found", StringComparison.Ordinal)) throw new InvalidDataException("Missing native dependency: " + path);
                foreach (Match match in Regex.Matches(output, @"=> (/\S+)"))
                {
                    var resolved = match.Groups[1].Value; var relative = Path.GetRelativePath(native, resolved);
                    bool packaged = relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !Path.IsPathRooted(relative);
                    if (!packaged && !new[] { "/lib/", "/lib64/", "/usr/lib/" }.Any(prefix => resolved.StartsWith(prefix, StringComparison.Ordinal))) throw new InvalidDataException("Unexpected external native dependency: " + resolved);
                }
            }
            else if (platform == "darwin")
            {
                output = context.Run(["otool", "-L", path], capture: true).Output;
                var identifiers = context.Run(["otool", "-D", path], capture: true).Output.Split('\n').Skip(1).Select(line => line.Trim()).ToHashSet(StringComparer.Ordinal);
                foreach (var line in output.Split('\n').Skip(1).Where(line => line.Trim().Length != 0))
                {
                    var dependency = line.Trim().Split(" (", StringSplitOptions.None)[0];
                    if (identifiers.Contains(dependency) || dependency.StartsWith("/usr/lib/", StringComparison.Ordinal) || dependency.StartsWith("/System/Library/", StringComparison.Ordinal) || dependency == path || Path.GetFileName(dependency) == Path.GetFileName(path)) continue;
                    if (!File.Exists(Path.Combine(native, Path.GetFileName(dependency)))) throw new InvalidDataException("Unpackaged native dependency: " + dependency);
                }
            }
            else
            {
                output = context.Run([Environment.GetEnvironmentVariable("OBJDUMP") ?? "objdump", "-p", path], capture: true).Output;
                var dependencies = Regex.Matches(output, @"DLL Name:\s*(\S+)").Select(match => match.Groups[1].Value).ToArray();
                if (dependencies.Length == 0) throw new InvalidDataException("No PE import information for " + path);
                var files = Directory.EnumerateFiles(native).Select(path => Path.GetFileName(path).ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
                foreach (var dependency in dependencies)
                {
                    var lower = dependency.ToLowerInvariant();
                    if (hosts.Contains(lower)) continue;
                    bool system = new[] { "kernel32.dll", "msvcrt.dll", "ucrtbase.dll", "advapi32.dll", "user32.dll", "ws2_32.dll", "shell32.dll", "ole32.dll", "bcrypt.dll", "ntdll.dll" }.Contains(lower) || new[] { "api-ms-win-", "ext-ms-win-", "vcruntime140", "msvcp140" }.Any(prefix => lower.StartsWith(prefix, StringComparison.Ordinal));
                    if (!system && !files.Contains(lower)) throw new InvalidDataException("Unpackaged native dependency: " + dependency);
                }
                output = string.Join("\n", dependencies);
            }
            reports.Add(Path.GetFileName(path), output);
        }
        return new { platform, imports = reports, simulator_hosts = hosts.Order(StringComparer.Ordinal).ToArray(), system_prerequisites = "OS runtime libraries; Windows VC++ runtime matching the process architecture where imported. .NET 10 and matching Icarus installed separately." };
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint AbiVersion();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate ulong AbiFeatures();
    public static void BuildCheck(ToolContext context)
    {
        var host = context.At("build/" + NativeBuildCommand.HostName);
        if (OperatingSystem.IsWindows())
        {
            var imports = context.Run([Environment.GetEnvironmentVariable("OBJDUMP") ?? "objdump", "-p", host], capture: true).Output;
            var dependencies = Regex.Matches(imports, @"DLL Name:\s*(\S+)").Select(match => match.Groups[1].Value).ToArray();
            if (dependencies.Any(name => new[] { "libwinpthread", "libstdc++", "libgcc" }.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))) throw new InvalidDataException("Compiler runtime must be statically linked");
        }
        var exports = Regex.Matches(File.ReadAllText(context.At("native/abi.h")), @"^CS_EXPORT .*?\b(cs_\w+)\(", RegexOptions.Multiline).Select(match => match.Groups[1].Value).ToArray();
        if (exports.Length < 40) throw new InvalidDataException("ABI declaration scan missed exports");
        var library = NativeLibrary.Load(host);
        try
        {
            foreach (var name in exports.Append("sharptestbench_initialize")) _ = NativeLibrary.GetExport(library, name);
            if (Marshal.GetDelegateForFunctionPointer<AbiVersion>(NativeLibrary.GetExport(library, "cs_abi_version"))() != 1 || Marshal.GetDelegateForFunctionPointer<AbiFeatures>(NativeLibrary.GetExport(library, "cs_abi_features"))() != 95) throw new InvalidDataException("Native ABI contract differs");
        }
        finally { NativeLibrary.Free(library); }
        var folder = context.At("build/native-build-check"); Directory.CreateDirectory(folder);
        var source = Path.Combine(folder, "consumer.c"); File.WriteAllText(source, "#include \"abi.h\"\nint main(void) { return cs_abi_version() != 1 || cs_abi_features() != 95; }\n");
        var executable = Path.Combine(folder, OperatingSystem.IsWindows() ? "consumer.exe" : "consumer");
        context.Run([Environment.GetEnvironmentVariable("CC") ?? (OperatingSystem.IsMacOS() ? "clang" : "gcc"), "-std=c11", "-Wall", "-Wextra", "-Werror", "-I", context.At("native"), source, host, "-o", executable]);
        context.Run([executable], environment: new Dictionary<string, string?> { [NativeBuildCommand.LoaderPath] = context.At("build") + Path.PathSeparator + Environment.GetEnvironmentVariable(NativeBuildCommand.LoaderPath) });
        using var build = StrictJson.Read(context.At("build/native-build.json"));
        StrictJson.Write(context.At("build/native-build-check.json"), new { exports_resolved = exports.Length + 1, c11_consumer = "passed", build = build.RootElement.Clone() });
        Console.WriteLine($"PASS: {exports.Length + 1} unmangled exports; C11 consumer links and runs against optimized C++20 host");
    }
    public static void Arm64(ToolContext context)
    {
        var adapter = CocotbAdapter.Inspect(context); var entry = adapter.NativeEntry();
        var implementation = adapter.Implementation ?? throw new InvalidDataException("Missing native implementation");
        int index = implementation.LastIndexOf(':'); if (index <= 0) throw new InvalidDataException("Invalid native implementation entry point");
        var sdk = NativeBuildCommand.SdkRoot();
        var paths = new List<string> { adapter.Python, Path.Combine(sdk, "dotnet.exe"), NativeBuildCommand.Executable("vvp"), Path.Combine(Environment.GetEnvironmentVariable("STB_IVL_ROOT") ?? throw new InvalidOperationException("Missing STB_IVL_ROOT"), "ivl.exe"), context.At("build/sharptestbench_host.dll"), entry.Library, implementation[..index], adapter.Bootstrap };
        paths.AddRange(Directory.EnumerateFiles(Path.Combine(sdk, "host/fxr"), "hostfxr.dll", SearchOption.AllDirectories));
        var report = paths.ToDictionary(path => path, path => "0x" + PeMachine(path).ToString("x", System.Globalization.CultureInfo.InvariantCulture));
        if (report.Values.Any(value => value != "0xaa64")) throw new InvalidDataException("Expected ARM64 PE machine 0xaa64 throughout: " + JsonSerializer.Serialize(report));
        StrictJson.Write(context.At("build/windows-arm64-binaries.json"), report); Console.WriteLine("ARM64_PE_PASS " + report.Count + " native components");
    }
    public static void Doctor(ToolContext context)
    {
        var checks = new List<object>(); bool ok = true;
        void Check(string name, Func<string> action)
        {
            try { checks.Add(new { name, ok = true, detail = action() }); }
            catch (Exception error) { ok = false; checks.Add(new { name, ok = false, detail = error.Message }); }
        }
        Check("dotnet SDK", () => { var sdk = NativeBuildCommand.SdkRoot(); var version = context.Run([Path.Combine(sdk, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"), "--version"], capture: true).Output.Trim(); using var global = StrictJson.Read(context.At("global.json")); if (version != global.RootElement.GetProperty("sdk").GetProperty("version").GetString()) throw new InvalidDataException("Pinned .NET SDK differs"); return version; });
        Check("C++20 compiler", () => NativeBuildCommand.Executable(Environment.GetEnvironmentVariable("CXX") ?? (OperatingSystem.IsMacOS() ? "clang++" : "g++")));
        Check("Icarus compiler", () => NativeBuildCommand.Executable(Environment.GetEnvironmentVariable("CS_IVERILOG") ?? "iverilog"));
        Check("Icarus runtime", () => NativeBuildCommand.Executable(Environment.GetEnvironmentVariable("CS_VVP") ?? "vvp"));
        Check("native adapters", () => { var adapter = CocotbAdapter.Inspect(context); var entry = adapter.NativeEntry(); if (!File.Exists(entry.Library) || !File.Exists(adapter.Bootstrap)) throw new FileNotFoundException("Missing native adapter"); return adapter.GpiEntryPoint!; });
        Console.WriteLine(JsonSerializer.Serialize(new { platform = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), checks }));
        if (!ok) throw new InvalidDataException("Prerequisite checks failed");
    }
}
