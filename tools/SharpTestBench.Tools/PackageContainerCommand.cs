using System.Text.Json;
using System.Text.RegularExpressions;
namespace SharpTestBench.Tools;
internal static class PackageContainerCommand
{
    const string Sdk = "mcr.microsoft.com/dotnet/sdk:10.0.402@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa";
    const string Runtime = "mcr.microsoft.com/dotnet/runtime:10.0.13@sha256:939555726cae4c916d212323ac3c0792996dcac2614f00e82cc40fcf059e1af3";
    public static void Execute(ToolContext context, string[] arguments)
    {
        var options = new Options(arguments); options.Only("--artifacts"); var artifacts = Path.GetFullPath(options.Require("--artifacts"));
        var evidence = Directory.EnumerateFiles(artifacts, "packages.json", SearchOption.AllDirectories).Where(path => Path.GetFileName(Path.GetDirectoryName(path)) == "package-evidence-linux-x64").ToArray(); if (evidence.Length != 1) throw new InvalidDataException("Expected one Linux package evidence set");
        using var indexDocument = StrictJson.Read(evidence[0]); var index = indexDocument.RootElement; var version = index.GetProperty("version").GetString()!; var revision = index.GetProperty("source_revision").GetString()!;
        if (index.GetProperty("source_dirty").GetBoolean()) throw new InvalidDataException("Container gate requires clean CI packages");
        if (!Regex.IsMatch(version, "^[0-9A-Za-z.+-]+$") || !Regex.IsMatch(revision, "^[0-9a-f]{40}$")) throw new InvalidDataException("Invalid package version or revision");
        var directory = context.At("build/container-consumer"); var feed = Path.Combine(directory, "packages"); Directory.CreateDirectory(feed);
        foreach (var old in Directory.EnumerateFiles(feed, "*.nupkg")) File.Delete(old);
        foreach (var pair in index.GetProperty("sha256").EnumerateObject())
        {
            if (Path.GetFileName(pair.Name) != pair.Name || pair.Name.IndexOfAny(['/', '\\']) >= 0) throw new InvalidDataException("Invalid package file name");
            var matches = Directory.EnumerateFiles(artifacts, pair.Name, SearchOption.AllDirectories).Where(path => Path.GetFileName(Path.GetDirectoryName(path)) == "packages-linux-x64").ToArray();
            if (matches.Length != 1 || CiCommands.Hash(matches[0]) != pair.Value.GetString()) throw new InvalidDataException("Missing/corrupt package: " + pair.Name); File.Copy(matches[0], Path.Combine(feed, pair.Name), true);
        }
        foreach (var name in new[] { "Tests.cs", "Counter.stb.xml", "counter.sv" }) File.Copy(context.At("examples/Counter/" + name), Path.Combine(directory, name), true);
        PackageCommand.ConsumerProject(Path.Combine(directory, "Counter.csproj"), version, "linux-x64", "/cache");
        var entry = new[] { "dotnet", "/package/tools/SharpTestBench.Tools.dll", "installed-run", "--native-dir", "/package/runtimes/linux-x64/native", "--assembly", "/test/Counter.dll", "--source", "/test/counter.sv", "--top", "counter", "--entry-type", "Entry, Counter", "--work-dir", "/results" };
        File.WriteAllText(Path.Combine(directory, "Dockerfile"), "FROM " + Sdk + " AS compile\nWORKDIR /consumer\nCOPY packages /feed\nCOPY Counter.csproj Tests.cs Counter.stb.xml counter.sv ./\nRUN dotnet build -c Release --source /feed --disable-build-servers -m:1\nFROM " + Runtime + "\nRUN apt-get update && apt-get install -y --no-install-recommends iverilog && rm -rf /var/lib/apt/lists/*\nCOPY --from=compile /consumer/bin/Release/net10.0/ /test/\nCOPY --from=compile /cache/sharptestbench.native.linux-x64/" + version + "/ /package/\nCOPY counter.sv /test/counter.sv\nENV CS_DOTNET_ROOT=/usr/share/dotnet\nWORKDIR /results\nENTRYPOINT " + JsonSerializer.Serialize(entry) + "\n");
        var resultDirectory = context.At("build/container-evidence"); Directory.CreateDirectory(resultDirectory); File.Delete(Path.Combine(resultDirectory, "results.json")); File.Delete(Path.Combine(resultDirectory, "environment.json")); var tag = "sharptestbench-consumer:" + revision[..12];
        void Run(string[] invocation, string log, int seconds) { var result = context.Run(invocation, capture: true, check: false, timeout: TimeSpan.FromSeconds(seconds)); File.WriteAllText(Path.Combine(resultDirectory, log), result.Output + result.Error); if (result.ExitCode != 0) throw new InvalidDataException("Container consumer failed; see " + log); }
        Run(["docker", "build", "--platform", "linux/amd64", "-t", tag, directory], "build.log", 600);
        Run(["docker", "run", "--rm", "--platform", "linux/amd64", "--network", "none", "--read-only", "--tmpfs", "/tmp", "--mount", "type=bind,source=" + resultDirectory + ",target=/results", tag], "driver.log", 120);
        using var rows = StrictJson.Read(Path.Combine(resultDirectory, "results.json")); if (rows.RootElement.GetArrayLength() != 1 || rows.RootElement[0].GetProperty("Name").GetString() != "ResetAndCount" || rows.RootElement[0].GetProperty("Status").GetString() != "passed") throw new InvalidDataException("Wrong/missing installed test result");
        StrictJson.Write(Path.Combine(resultDirectory, "environment.json"), new { sdk = Sdk, runtime = Runtime, source_revision = revision, version, network = "disabled during simulation", repository_mount = false, cocotb_python_installed = false }); Console.WriteLine("CONTAINER_CONSUMER_PASS");
    }
}
