namespace SharpTestBench.Tools;

internal sealed record WorkerPublish(string Name, string Rid, string Deployment, string Directory, string[] Command);
internal static class WorkerPublishCommand
{
    public static readonly string[] SupportedRids = ["linux-x64", "linux-arm64", "win-x64", "win-arm64", "osx-x64", "osx-arm64"];
    public static WorkerPublish[] Plan(string root, string rid, string mode, string hostRid, string? output = null)
    {
        if (rid == "auto") rid = hostRid;
        if (!SupportedRids.Contains(rid, StringComparer.Ordinal) || !SupportedRids.Contains(hostRid, StringComparer.Ordinal)) throw new ArgumentException("Unsupported native worker RID");
        if (rid != hostRid) throw new ArgumentException("Publish native workers on a matching OS and architecture");
        if (mode is not ("all" or "tools" or "runner")) throw new ArgumentException("Unknown worker publish mode");
        root = RunnerManifestCommand.RealPath(root);
        var build = Path.Combine(root, "build");
        if (new DirectoryInfo(build).LinkTarget is not null) throw new ArgumentException("Native build root must not be a symbolic link");
        output = RunnerManifestCommand.RealPath(Path.GetFullPath(output ?? Path.Combine(build, "workers"), root));
        var relative = Path.GetRelativePath(build, output);
        if (relative is "." or ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative)) throw new ArgumentException("Worker output must be below the checkout build directory");
        var plans = new List<WorkerPublish>();
        foreach (var name in new[] { "tools", "runner" })
        {
            if (mode != "all" && mode != name) continue;
            var project = name == "tools" ? "SharpTestBench.Tools" : "SharpTestBench.Runner";
            var folder = Path.Combine(output, rid, name);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(RunnerManifestCommand.RealPath(folder), folder, comparison)) throw new ArgumentException("Worker RID and executable output directories must not be symbolic links");
            string[] profile = name == "tools" ? ["-p:PublishAot=false", "-p:PublishReadyToRun=true", "-p:PublishTrimmed=false"] : ["-p:PublishAot=true", "-p:IlcTreatWarningsAsErrors=true"];
            plans.Add(new(name, rid, name == "tools" ? "self-contained-readytorun" : "native-aot", folder,
                ["dotnet", "publish", Path.Combine(root, "tools", project, project + ".csproj"), "-c", "Release", "-r", rid, "--self-contained", "true", "-p:UseAppHost=true", "-p:PublishSingleFile=false", .. profile, "--output", folder, "--disable-build-servers", "-m:1"]));
        }
        return plans.ToArray();
    }
    public static void Execute(ToolContext context, string[] arguments)
    {
        var options = new Options(arguments); options.Only("--rid", "--mode", "--output");
        var plans = Plan(context.Root, options.Get("--rid", "auto")!, options.Get("--mode", "all")!, NativeBuildCommand.Rid, options.Get("--output"));
        foreach (var plan in plans)
        {
            context.Run(plan.Command);
            var executable = Path.Combine(plan.Directory, WorkerArtifact.EntryPoint(plan));
            if (!File.Exists(executable)) throw new InvalidDataException("Native worker executable was not produced: " + executable);
            context.Run([executable, "--help"]);
            WorkerArtifact.Write(plan);
            WorkerArtifact.Verify(plan);
        }
    }
    public static void Verify(ToolContext context, string[] arguments)
    {
        var options = new Options(arguments); options.Only("--rid", "--mode", "--output");
        foreach (var plan in Plan(context.Root, options.Get("--rid", "auto")!, options.Get("--mode", "all")!, NativeBuildCommand.Rid, options.Get("--output"))) WorkerArtifact.Verify(plan);
    }

}
