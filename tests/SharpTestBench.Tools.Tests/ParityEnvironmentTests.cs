using System.Text.Json;
using SharpTestBench.Tools;
using Xunit;

[CollectionDefinition("Parity process environment", DisableParallelization = true)]
public sealed class ParityEnvironmentCollection;

[Collection("Parity process environment")]
public sealed class ParityEnvironmentTests
{
    [Fact]
    public void ChildOverridesRemoveInheritedControlsAndAllowExplicitFixtureValues()
    {
        var previous = ParityContracts.SelectionVariables.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var name in ParityContracts.SelectionVariables) Environment.SetEnvironmentVariable(name, "poison");
            var environment = ParityContracts.ChildEnvironment(ParityContracts.EnvironmentValues());
            foreach (var name in ParityContracts.SelectionVariables) Assert.Null(environment[name]);
            environment["DSP_FAULT"] = "1";
            var result = new CommandRunner().Run([
                Environment.GetEnvironmentVariable("CS_PYTHON") ?? "python", "-c",
                "import os,json; print(json.dumps(dict(os.environ)))"
            ], Environment.CurrentDirectory, environment, capture: true, timeout: TimeSpan.FromSeconds(20));
            using var actual = JsonDocument.Parse(result.Output);
            foreach (var name in ParityContracts.SelectionVariables.Where(name => name != "DSP_FAULT"))
                Assert.False(actual.RootElement.TryGetProperty(name, out _), name);
            Assert.Equal("1", actual.RootElement.GetProperty("DSP_FAULT").GetString());
            Assert.Equal(Environment.GetEnvironmentVariable("PATH"), actual.RootElement.GetProperty("PATH").GetString());
        }
        finally
        {
            foreach (var (name, value) in previous) Environment.SetEnvironmentVariable(name, value);
        }
    }

    [Fact]
    public void CleanEnvironmentRetainsItsCompleteEnvironmentContract()
    {
        var original = new Dictionary<string, string?> { ["CS_TEST_FILTER"] = "poison", ["PATH"] = "tool-path" };
        var clean = ParityContracts.CleanEnvironment(original);
        Assert.False(clean.ContainsKey("CS_TEST_FILTER"));
        Assert.Equal("tool-path", clean["PATH"]);
        Assert.Equal("poison", original["CS_TEST_FILTER"]);
    }

    [Fact]
    public void SimulatorEnvironmentPreservesInheritedSearchPathWithPlatformCasing()
    {
        var loader = NativeBuildCommand.LoaderPath;
        var inheritedKey = OperatingSystem.IsWindows() ? "Path" : loader;
        var original = new Dictionary<string, string?> { [inheritedKey] = "existing-tools", ["UNRELATED"] = "keep" };
        var environment = LegacyRunCommand.SimulatorEnvironment(original, "host-dir", "adapter-dir");
        Assert.Equal(string.Join(Path.PathSeparator, "host-dir", "adapter-dir", "existing-tools"), environment[loader]);
        Assert.Equal("keep", environment["UNRELATED"]);
        Assert.Equal("existing-tools", original[inheritedKey]);
        Assert.Single(environment.Keys, key => string.Equals(key, loader, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
    }
}
