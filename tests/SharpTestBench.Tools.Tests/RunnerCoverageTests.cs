using SharpTestBench.Tools;
using Xunit;

public sealed class RunnerCoverageTests
{
    sealed class CollectionRunner(string failure) : ICommandRunner
    {
        public bool ReachedRunner { get; private set; }
        public bool ReachedCli { get; private set; }
        public bool Merged { get; private set; }
        public CommandResult Run(IReadOnlyList<string> arguments, string directory,
            IReadOnlyDictionary<string, string?>? environment = null, bool capture = false,
            bool check = true, TimeSpan? timeout = null)
        {
            if (arguments.Contains("reportgenerator")) { Merged = true; throw new InvalidOperationException("Must not merge invalid evidence"); }
            var command = arguments.ToList();
            var output = command[command.IndexOf("--output") + 1];
            var pattern = command[command.IndexOf("--include") + 1];
            var assembly = pattern[1..pattern.IndexOf(']')];
            var runner = assembly == "SharpTestBench.Runner";
            var cli = output.EndsWith("/runner-cli", StringComparison.Ordinal);
            var relevant = runner && (failure.StartsWith("cli-", StringComparison.Ordinal) ? cli : !cli);
            var mode = failure.StartsWith("cli-", StringComparison.Ordinal) ? failure[4..] : failure;
            if (runner)
            {
                ReachedRunner = true;
                if (cli) ReachedCli = true;
                if (relevant && mode == "process") throw new IOException("Runner contract failed");
                if (relevant && mode == "missing") return new(0, "", "");
                if (relevant && mode == "assembly") assembly = "RunnerValidation";
            }
            var covered = relevant && mode == "empty" ? 0 : 1;
            File.WriteAllText(Path.Combine(directory, output + ".cobertura.xml"),
                $"<coverage lines-valid=\"2\" lines-covered=\"{covered}\" branches-valid=\"2\" branches-covered=\"1\" line-rate=\"{(covered == 0 ? "0" : "0.5")}\" branch-rate=\"0.5\"><packages><package name=\"{assembly}\"><classes><class name=\"Example\" filename=\"Example.cs\" /></classes></package></packages></coverage>");
            return new(0, "", "");
        }
    }

    [Theory]
    [InlineData("process")]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("assembly")]
    public void InvalidRunnerCollectionCannotPublishCompletion(string failure)
    {
        var root = Path.Combine(Path.GetTempPath(), "runner-coverage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "build/code-coverage/report"));
        try
        {
            var summary = Path.Combine(root, "build/code-coverage/summary.json");
            File.WriteAllText(summary, "stale success");
            File.WriteAllText(Path.Combine(root, "build/code-coverage/runner.cobertura.xml"),
                "<coverage lines-valid=\"2\" lines-covered=\"1\" branches-valid=\"2\" branches-covered=\"1\" line-rate=\"0.5\" branch-rate=\"0.5\"><packages><package name=\"SharpTestBench.Runner\"><classes><class name=\"Stale\" filename=\"Stale.cs\" /></classes></package></packages></coverage>");
            var runner = new CollectionRunner(failure);
            Assert.NotNull(Record.Exception(() => CoverageCommand.Execute(new(root, runner), [])));
            Assert.True(runner.ReachedRunner);
            Assert.False(runner.Merged);
            Assert.False(File.Exists(summary));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("cli-process")]
    [InlineData("cli-missing")]
    [InlineData("cli-empty")]
    [InlineData("cli-assembly")]
    public void InvalidCliCollectionCannotPublishCompletion(string failure)
    {
        var root = Path.Combine(Path.GetTempPath(), "runner-cli-coverage-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "tools/SharpTestBench.Runner/bin/Release/net10.0"); Directory.CreateDirectory(source);
        var output = Path.Combine(root, "build/code-coverage"); Directory.CreateDirectory(output);
        try
        {
            foreach (var name in new[] { "SharpTestBench.Runner.dll", "SharpTestBench.Runner.pdb", "SharpTestBench.Runner.deps.json", "SharpTestBench.Runner.runtimeconfig.json" }) File.WriteAllText(Path.Combine(source, name), "test copy boundary");
            File.WriteAllText(Path.Combine(output, "summary.json"), "stale success");
            File.WriteAllText(Path.Combine(output, "runner-cli.cobertura.xml"), "stale CLI coverage");
            var runner = new CollectionRunner(failure);
            Assert.NotNull(Record.Exception(() => CoverageCommand.Execute(new(root, runner), [])));
            Assert.True(runner.ReachedCli); Assert.False(runner.Merged); Assert.False(File.Exists(Path.Combine(output, "summary.json")));
            Assert.Equal("test copy boundary", File.ReadAllText(Path.Combine(output, "runner-cli-harness/SharpTestBench.Runner.runtimeconfig.json")));
        }
        finally
        {
            if (Path.GetDirectoryName(Path.GetFullPath(root)) != Path.TrimEndingDirectorySeparator(Path.GetTempPath())) throw new InvalidOperationException("Temporary path escaped its parent");
            Directory.Delete(root, true);
        }
    }
}
