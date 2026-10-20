using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SharpTestBench.Tools;

internal static class CiCommands
{
    internal sealed record DownloadSpec(string Url, string Sha256, string Filename);
    public static readonly IReadOnlyDictionary<string, DownloadSpec> Downloads = new Dictionary<string, DownloadSpec> {
        ["nvc"] = new("https://github.com/nickg/nvc/releases/download/r1.23.0/nvc_1.23.0-1_amd64_ubuntu-24.04.deb", "33d65eafb83d83352716895e44c116002f94d8a198a9486a3cee6df3e4fdfb89", "nvc.deb"),
        ["debugger"] = new("https://github.com/Samsung/netcoredbg/releases/download/3.2.0-1092/netcoredbg-linux-amd64.tar.gz", "080eb3b2d2152465f599d3b33d1ee6e747794e11cc0a3773ec689f5e5f2c5afa", "netcoredbg.tar.gz"),
        ["verilator"] = new("https://github.com/verilator/verilator/archive/refs/tags/v5.048.tar.gz", "02d934b3f972c6d9b792350634d81eadfc9e61f347e3f3bdcaad40960b9fcb53", "verilator.tar.gz") };
    public static string Hash(string path) { using var input = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(input)); }
    public static void GithubValue(string fileVariable, string name, string value)
    {
        if (!Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$") || value.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new ArgumentException("Invalid GitHub command-file value");
        File.AppendAllText(Environment.GetEnvironmentVariable(fileVariable) ?? throw new InvalidOperationException("Missing " + fileVariable), name + "=" + value + "\n", new UTF8Encoding(false));
    }
    public static void GithubPath(string value)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new ArgumentException("Invalid GitHub PATH entry");
        File.AppendAllText(Environment.GetEnvironmentVariable("GITHUB_PATH") ?? throw new InvalidOperationException("Missing GITHUB_PATH"), value + "\n", new UTF8Encoding(false));
    }
    public static string Download(ToolContext context, DownloadSpec spec, HttpClient? supplied = null)
    {
        var target = context.At("build/" + spec.Filename);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var pending = target + "." + Guid.NewGuid().ToString("N") + ".download";
        using var owned = supplied is null ? new HttpClient { Timeout = TimeSpan.FromSeconds(60) } : null;
        var client = supplied ?? owned!;
        try
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    using var response = client.GetAsync(spec.Url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                    using var input = response.Content.ReadAsStream();
                    using var output = File.Create(pending);
                    var buffer = new byte[1024 * 1024];
                    long size = 0;
                    int read;
                    while ((read = input.Read(buffer)) != 0)
                    {
                        size += read;
                        if (size > 256L * 1024 * 1024) throw new InvalidDataException("Download exceeds size limit");
                        output.Write(buffer, 0, read);
                    }
                    break;
                }
                catch (Exception error) when (attempt < 3 && error is HttpRequestException or TaskCanceledException)
                { Thread.Sleep(TimeSpan.FromSeconds(attempt + 1)); }
            }
            if (Hash(pending) != spec.Sha256) throw new InvalidDataException("Pinned download checksum mismatch");
            File.Move(pending, target, true);
            return target;
        }
        finally { File.Delete(pending); }
    }
    public static void Extract(string archive, string destination)
    {
        Directory.CreateDirectory(destination);
        using var compressed = File.OpenRead(archive);
        using var stream = new GZipStream(compressed, CompressionMode.Decompress);
        TarFile.ExtractToDirectory(stream, destination, true);
    }
    public static void InstallDependencies(ToolContext context, string profile)
    {
        if (profile == "macos") { context.Run(["brew", "install", "icarus-verilog"]); return; }
        string[] packages = profile switch {
            "linux" => ["iverilog", "gcc", "g++", "gdb"], "parity" => ["iverilog", "gcc", "g++"],
            "verilator" => ["autoconf", "flex", "bison", "libfl-dev", "g++", "make"], "documentation" => ["doxygen"],
            _ => throw new ArgumentException("Unknown dependency profile") };
        context.Run(["sudo", "apt-get", "update"]);
        context.Run(["sudo", "apt-get", "install", "-y", .. packages]);
    }
    public static void WindowsToolchain(ToolContext context, string root, string variant)
    {
        if (variant is not ("ucrt64" or "clangarm64")) throw new ArgumentException("Unknown MSYS2 toolchain");
        root = Path.GetFullPath(root);
        var prefix = Path.Combine(root, variant);
        foreach (var relative in new[] { "bin", "lib/ivl", "include/iverilog" })
            if (!Directory.Exists(Path.Combine(prefix, relative))) throw new DirectoryNotFoundException("Missing MSYS2 directory: " + relative);
        var pacman = Path.Combine(root, "usr/bin/pacman.exe");
        if (!File.Exists(pacman)) throw new FileNotFoundException("Missing MSYS2 package inventory tool", pacman);
        var inventory = context.Run([pacman, "-Q"], capture: true);
        File.WriteAllText(context.At(variant == "clangarm64" ? "arm64-toolchain.txt" : "windows-toolchain.txt"), inventory.Output);
        GithubPath(Path.Combine(prefix, "bin"));
        GithubValue("GITHUB_ENV", "STB_IVL_ROOT", Path.Combine(prefix, "lib/ivl"));
        GithubValue("GITHUB_ENV", "STB_IVL_INCLUDE", Path.Combine(prefix, "include/iverilog"));
        if (variant == "clangarm64")
            foreach (var (name, value) in new Dictionary<string, string> { ["Platform"] = "", ["CC"] = "clang", ["CXX"] = "clang++", ["DLLTOOL"] = "llvm-dlltool", ["OBJDUMP"] = "llvm-objdump" }) GithubValue("GITHUB_ENV", name, value);
    }
    public static string VerifyReleaseRun(JsonElement data, string repository)
    {
        foreach (var (key, value) in new Dictionary<string, string> { ["status"] = "completed", ["conclusion"] = "success", ["head_branch"] = "main", ["path"] = ".github/workflows/ci.yml", ["event"] = "push" })
            if (!data.TryGetProperty(key, out var actual) || actual.ValueKind != JsonValueKind.String || actual.GetString() != value) throw new InvalidDataException("Requires a successful main push from the native CI workflow");
        if (!data.TryGetProperty("head_repository", out var head) || head.ValueKind != JsonValueKind.Object || !head.TryGetProperty("full_name", out var fullName) || fullName.GetString() != repository) throw new InvalidDataException("Release source repository differs");
        var revision = data.GetProperty("head_sha").GetString() ?? "";
        if (!Regex.IsMatch(revision, "^[0-9a-f]{40}$")) throw new InvalidDataException("Invalid validated revision");
        return revision;
    }
    public static void VerifyReleaseRunCommand()
    {
        var id = Environment.GetEnvironmentVariable("RUN_ID") ?? "";
        var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? "";
        if (!Regex.IsMatch(id, "^[0-9]{1,19}$") || !long.TryParse(id, out var runId) || runId <= 0) throw new ArgumentException("Invalid run ID");
        if (!Regex.IsMatch(repository, "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")) throw new ArgumentException("Invalid GitHub repository");
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("GH_TOKEN") ?? throw new InvalidOperationException("Missing GitHub credential"));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SharpTestBench-CI/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = client.GetStreamAsync($"https://api.github.com/repos/{repository}/actions/runs/{runId}").GetAwaiter().GetResult();
        var text = new UTF8Encoding(false, true).GetString(BoundedInput.Read(response, 1024 * 1024));
        using var document = StrictJson.Parse(text);
        GithubValue("GITHUB_OUTPUT", "revision", VerifyReleaseRun(document.RootElement, repository));
    }
    public static void PublishPackages(ToolContext context, string directory)
    {
        var bundle = Path.GetFullPath(directory);
        using var document = StrictJson.Read(Path.Combine(bundle, "release-manifest.json"));
        var packages = document.RootElement.GetProperty("packages").EnumerateArray().Select(item => (name: item.GetProperty("file").GetString() ?? "", hash: item.GetProperty("sha256").GetString() ?? "")).ToArray();
        if (packages.Length != 5 || packages.Select(item => item.name).Distinct(StringComparer.Ordinal).Count() != 5 || packages.Any(item => item.name.Contains('/') || item.name.Contains('\\') || !item.name.EndsWith(".nupkg", StringComparison.Ordinal) || item.name != Path.GetFileName(item.name))) throw new InvalidDataException("Release requires five distinct safe package names");
        if (!packages.Select(item => item.name).ToHashSet(StringComparer.Ordinal).SetEquals(Directory.EnumerateFiles(bundle, "*.nupkg").Select(Path.GetFileName)!)) throw new InvalidDataException("Unlisted or missing release package");
        foreach (var item in packages)
        {
            var path = Path.Combine(bundle, item.name);
            if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint) || Hash(path) != item.hash) throw new InvalidDataException("Release checksum mismatch");
        }
        var key = Environment.GetEnvironmentVariable("NUGET_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("NuGet credential is required");
        foreach (var item in packages.OrderBy(item => item.name, StringComparer.Ordinal)) context.Run(["dotnet", "nuget", "push", Path.Combine(bundle, item.name), "--api-key", key, "--source", "https://api.nuget.org/v3/index.json"]);
    }
    public static void Execute(ToolContext context, string[] arguments)
    {
        if (arguments.Length == 0) throw new ArgumentException("ci requires a command");
        var command = arguments[0];
        if (command == "install-deps")
        {
            if (arguments.Length != 2) throw new ArgumentException("install-deps requires a profile");
            InstallDependencies(context, arguments[1]); return;
        }
        var options = new Options(arguments[1..], "--gnu");
        switch (command)
        {
            case "install-adapter":
                options.Only("--gnu");
                context.Tool("prepare-upstream");
                var environment = new Dictionary<string, string?>();
                if (options.Has("--gnu")) { environment["CC"] = "gcc"; environment["CXX"] = "g++"; }
                context.Run([Environment.GetEnvironmentVariable("CS_PYTHON") ?? "python", "-m", "pip", "install", "./build/upstream/cocotb-source", "pytest"], environment: environment); break;
            case "windows-toolchain": options.Only("--root", "--variant"); WindowsToolchain(context, options.Require("--root"), options.Require("--variant")); break;
            case "release-bundle":
                options.Only("--artifacts", "--output");
                var revision = Environment.GetEnvironmentVariable("REVISION") ?? "";
                if (!Regex.IsMatch(revision, "^[0-9a-f]{40}$")) throw new ArgumentException("Invalid release revision");
                context.Tool("release-bundle", "--artifacts", Path.GetFullPath(options.Require("--artifacts")), "--output", Path.GetFullPath(options.Require("--output")), "--revision", revision); break;
            case "publish-packages": options.Only("--bundle"); PublishPackages(context, options.Require("--bundle")); break;
            case "install-nvc": options.Only(); context.Run(["sudo", "apt-get", "install", "-y", Download(context, Downloads["nvc"])]); break;
            case "install-verilator":
                options.Only(); Extract(Download(context, Downloads["verilator"]), context.At("build"));
                var source = context.At("build/verilator-5.048");
                context.Run(["autoconf"], directory: source); context.Run([Path.Combine(source, "configure")], directory: source); context.Run(["make", "-C", "src", "-j2", "opt"], directory: source);
                GithubValue("GITHUB_ENV", "VERILATOR_ROOT", source); GithubPath(Path.Combine(source, "bin")); break;
            case "debugger-check":
                options.Only(); Extract(Download(context, Downloads["debugger"]), context.At("build/debugger"));
                context.Run(["dotnet", "build", "tests/DebugIntegration", "-c", "Debug"]);
                context.Tool("debugger-tests", "--debugger", context.At("build/debugger/netcoredbg/netcoredbg")); break;
            case "require-arm64":
                options.Only(); if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64) throw new PlatformNotSupportedException("Native ARM64 runtime is required"); context.Run(["dotnet", "--info"]); break;
            case "verify-release-run": options.Only(); VerifyReleaseRunCommand(); break;
            case "layouts":
                options.Only(); string root, include;
                if (OperatingSystem.IsMacOS()) { var prefix = context.Run(["brew", "--prefix", "icarus-verilog"], capture: true).Output.Trim(); if (string.IsNullOrEmpty(prefix) || prefix.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new InvalidDataException("Invalid simulator prefix"); root = Path.Combine(prefix, "lib/ivl"); include = Path.Combine(prefix, "include/iverilog"); }
                else { root = Environment.GetEnvironmentVariable("STB_IVL_ROOT") ?? throw new InvalidOperationException("Missing STB_IVL_ROOT"); include = Environment.GetEnvironmentVariable("STB_IVL_INCLUDE") ?? throw new InvalidOperationException("Missing STB_IVL_INCLUDE"); }
                context.Tool("icarus-layouts-check", "--ivl-root", root, "--include", include); break;
            case "lfsr-matrix":
                options.Only("--fixture"); var fixture = options.Require("--fixture");
                if (!new[] { "crc32", "crc16", "crc32c", "prbs7", "scrambler", "descrambler", "galois_ff", "minimal" }.Contains(fixture, StringComparer.Ordinal)) throw new ArgumentException("Unknown LFSR fixture");
                foreach (var style in new[] { "LOOP", "REDUCTION" })
                {
                    foreach (var seed in new[] { "1", "17" }) context.Tool("lfsr-ci", "--fixture", fixture, "--style", style, "--seed", seed);
                    foreach (var fault in new[] { "1", "2" }) context.Tool("lfsr-ci", "--fixture", fixture, "--style", style, "--seed", "1", "--fault", fault);
                }
                break;
            default: throw new ArgumentException("Unknown CI command: " + command);
        }
    }
}
