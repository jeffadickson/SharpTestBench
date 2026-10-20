using System.Runtime.InteropServices;

namespace SharpTestBench.Tools;

internal static class NativeBuildCommand
{
    public static string HostName => OperatingSystem.IsWindows() ? "sharptestbench_host.dll" : OperatingSystem.IsMacOS() ? "libsharptestbench_host.dylib" : "libsharptestbench_host.so";
    public static string FxrName => OperatingSystem.IsWindows() ? "hostfxr.dll" : OperatingSystem.IsMacOS() ? "libhostfxr.dylib" : "libhostfxr.so";
    public static string LoaderPath => OperatingSystem.IsWindows() ? "PATH" : OperatingSystem.IsMacOS() ? "DYLD_LIBRARY_PATH" : "LD_LIBRARY_PATH";
    public static string Rid => (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" + (RuntimeInformation.ProcessArchitecture switch { Architecture.X64 => "x64", Architecture.Arm64 => "arm64", _ => throw new PlatformNotSupportedException("Unsupported native architecture") });
    public static string Executable(string name)
    {
        string[] extensions = OperatingSystem.IsWindows() ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';') : [""];
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            foreach (var extension in extensions.Prepend(""))
            {
                var candidate = Path.Combine(folder, name + extension);
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
        throw new FileNotFoundException("Missing executable: " + name);
    }
    public static string SdkRoot()
    {
        var configured = Environment.GetEnvironmentVariable("CS_DOTNET_ROOT");
        if (!string.IsNullOrEmpty(configured)) return Path.GetFullPath(configured);
        var path = Executable("dotnet");
        path = new FileInfo(path).ResolveLinkTarget(true)?.FullName ?? path;
        return Path.GetDirectoryName(path)!;
    }
    public static string HighestVersion(IEnumerable<string> paths, Func<string, string> name)
    {
        return paths.OrderBy(path => Version.Parse(name(path).Split('-')[0])).LastOrDefault() ?? throw new DirectoryNotFoundException("Missing .NET native host headers/runtime");
    }
    public static string HostFxr(string sdk)
    {
        var folder = HighestVersion(Directory.EnumerateDirectories(Path.Combine(sdk, "host/fxr")), path => Path.GetFileName(path));
        var path = Path.Combine(folder, FxrName);
        if (!File.Exists(path)) throw new FileNotFoundException("Missing hostfxr", path);
        return path;
    }
    public static string Build(ToolContext context, string? sdk = null, string? directory = null)
    {
        sdk ??= SdkRoot(); directory ??= context.At("build"); directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)); Directory.CreateDirectory(directory);
        var packs = Path.Combine(sdk, "packs/Microsoft.NETCore.App.Host." + Rid);
        var candidates = Directory.EnumerateDirectories(packs).Select(path => Path.Combine(path, "runtimes/" + Rid + "/native")).Where(Directory.Exists);
        var headers = HighestVersion(candidates, path => new DirectoryInfo(path).Parent!.Parent!.Parent!.Name);
        var staging = Path.Combine(directory, "native-build-" + Guid.NewGuid().ToString("N"));
        var temporary = Path.Combine(staging, HostName);
        var compiler = Environment.GetEnvironmentVariable("CXX") ?? (OperatingSystem.IsMacOS() ? "clang++" : "g++");
        var version = context.Run([compiler, "--version"], capture: true).Output.Split('\n')[0].TrimEnd('\r');
        string[] flags = OperatingSystem.IsWindows() ? ["-shared", "-static"] : OperatingSystem.IsMacOS() ? ["-dynamiclib", "-fPIC", "-Wl,-install_name,@rpath/" + HostName] : ["-shared", "-fPIC", "-Wl,-soname," + HostName];
        string[] vectors = version.Contains("clang", StringComparison.OrdinalIgnoreCase) ? ["-fvectorize", "-fslp-vectorize"] : ["-ftree-loop-vectorize", "-ftree-slp-vectorize"];
        string[] diagnostics = version.Contains("clang", StringComparison.OrdinalIgnoreCase) ? ["-Rpass=loop-vectorize", "-Rpass=slp-vectorizer"] : ["-fopt-info-vec-optimized"];
        string[] profile = ["-std=c++20", "-O3", "-flto", "-g", "-DNDEBUG", "-DSTB_HOST_BUILD", "-fvisibility=hidden", .. vectors];
        string[] sources = [context.At("native/host.cpp"), context.At("native/abi.cpp")];
        string[] command = [compiler, .. flags, .. profile, .. diagnostics, "-Wall", "-Wextra", "-Werror", .. sources, "-I", context.At("native/upstream"), "-I", headers, .. (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? Array.Empty<string>() : ["-ldl"]), "-o", temporary];
        Directory.CreateDirectory(staging);
        CommandResult result;
        try
        {
            result = context.Run(command, capture: true, check: false);
            File.WriteAllText(Path.Combine(directory, "native-vectorization.txt"), result.Output + result.Error);
            if (result.ExitCode != 0) throw new IOException("Native build failed: " + result.Output + result.Error);
            File.Move(temporary, Path.Combine(directory, HostName), true);
        }
        finally
        {
            if (Path.GetDirectoryName(staging) != directory) throw new IOException("Native staging escaped its build directory");
            Directory.Delete(staging, true);
        }
        var output = Path.Combine(directory, HostName);
        var hashes = new[] { "native/host.cpp", "native/abi.cpp", "native/transport.inc", "native/clocks.inc", "native/abi.h", "native/platform.h" }.ToDictionary(path => path, path => CiCommands.Hash(context.At(path)));
        StrictJson.Write(Path.Combine(directory, "native-build.json"), new { rid = Rid, language = "C++20", profile, command, compiler = version, vectorization = vectors, vectorization_diagnostics = result.Error, sources = hashes, output_sha256 = CiCommands.Hash(output) });
        return output;
    }
}
