using SharpTestBench.Tools;
using Xunit;

public sealed class RunnerCoverageTests
{
    sealed class CollectionRunner(string failure) : ICommandRunner
    {
        public bool ReachedRunner { get; private set; }
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
            if (runner)
            {
                ReachedRunner = true;
                if (failure == "process") throw new IOException("Runner contract failed");
                if (failure == "missing") return new(0, "", "");
                if (failure == "assembly") assembly = "RunnerValidation";
            }
            var covered = runner && failure == "empty" ? 0 : 1;
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
}
