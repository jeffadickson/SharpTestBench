using SharpTestBench.Fixtures;
using System.Globalization;
using System.Text.Json;

namespace SharpTestBench.Tools;

internal static class ArbiterCiCommand
{
    public static void Execute(ToolContext context, string[] arguments)
    {
        var options = new Options(arguments, "--round-robin", "--msb", "--negative-control");
        options.Only("--ports", "--round-robin", "--msb", "--block", "--seed", "--count", "--negative-control");
        int Number(string key, int fallback) => int.TryParse(options.Get(key, fallback.ToString(CultureInfo.InvariantCulture)), NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value : throw new ArgumentException("Invalid arbiter option: " + key);
        if (!ulong.TryParse(options.Get("--seed", "1"), NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed)) throw new ArgumentException("Invalid arbiter seed");
        int block = options.Get("--block", "none") switch { "none" => 0, "request" => 1, "ack" => 2, _ => throw new ArgumentException("Arbiter block must be none/request/ack") };
        var config = new ArbiterConfig(Number("--ports", 4), options.Has("--round-robin"), block, !options.Has("--msb"));
        config.Validate(); Run(context, config, seed, Number("--count", 128), options.Has("--negative-control"));
    }
    internal static Dictionary<string, object> Run(ToolContext context, ArbiterConfig config, ulong seed, int count, bool negative)
    {
        var rows = ArbiterFixture.Plan(config, seed, count); var stimulus = JsonSerializer.SerializeToElement(rows);
        ArbiterFixture.ValidatePlan(config, stimulus);
        return ParityCiCommand.InWorkspace(context, $"arbiter-{config.Top}-{seed}-{count}" + (negative ? "-negative" : ""), (work, environment) => {
            var provenance = FixtureContracts.VerifySources(context.Root, "arbiter");
            var build = context.Run(["dotnet", "build", context.At("examples/Arbiter"), "-c", "Release", "-p:RestoreLockedMode=true", "--disable-build-servers", "-m:1"], environment: environment, capture: true, check: false);
            File.WriteAllText(Path.Combine(work, "build.log"), build.Output + build.Error);
            if (build.ExitCode != 0) throw new IOException("Arbiter fixture build failed; see build.log");
            NativeBuildCommand.Build(context);
            var plan = Path.Combine(work, "plan.json"); var configuration = Path.Combine(work, "configuration.json");
            var wrapper = Path.Combine(work, "arbiter_fixture.sv"); var oracle = context.At("tests/Tooling/arbiter_reference.py");
            StrictJson.Write(plan, rows); StrictJson.Write(configuration, config); File.WriteAllText(wrapper, ArbiterFixture.Wrapper(config));
            string[] oracleFiles=["tests/Tooling/arbiter_reference.py","tests/Tooling/fifo_reference.py"];
            var oracleInputs=OracleEvidence.Capture(context.Root,oracleFiles);
            var hashes = new[] { plan, configuration, wrapper, oracle }.ToDictionary(path => path, CiCommands.Hash);
            environment["ARBITER_PLAN"] = plan; environment["ARBITER_CONFIG"] = configuration; environment["ARBITER_FAULT"] = negative ? "1" : "0";
            var manifest = RunnerManifestCommand.Make(context.Root, "counter", seed, RunnerManifestCommand.InstalledAdapter(context), Path.Combine(work, "native"));
            manifest["top"] = config.Top; manifest["sources"] = new[] { wrapper, context.At("tests/Fixtures/verilog-arbiter/arbiter.v"), context.At("tests/Fixtures/verilog-arbiter/priority_encoder.v") };
            manifest["assembly"] = context.At("examples/Arbiter/bin/Release/net10.0/Arbiter.dll"); manifest["entryType"] = "Entry, Arbiter";
            RunnerManifestCommand.BindSingleCase(manifest, "Entry.ArbiterScoreboard", seed);
            StrictJson.Write(Path.Combine(work, "manifest.json"), manifest); StrictJson.Write(Path.Combine(work, "expected.json"), manifest["expectedNames"]);
            var report = ParityCiCommand.Compare(context, work, manifest, seed, environment, "Entry.ArbiterScoreboard", "ARBITER_REPORT", negative ? "ARBITER_MISMATCH" : null, (a, b) => {
                ArbiterFixture.Metrics(config, a, stimulus); ArbiterFixture.Metrics(config, b, stimulus);
                if (!JsonElement.DeepEquals(a, b)) throw new InvalidDataException("Arbiter runner traces differ");
                foreach (var runner in new[] { "legacy", "native" })
                {
                    var result = context.Run([Environment.GetEnvironmentVariable("CS_PYTHON") ?? "python", oracle, "--configuration", configuration, "--plan", plan, "--metrics", Path.Combine(work, runner, "metrics.json")], capture: true, check: false, timeout: TimeSpan.FromSeconds(30));
                    File.WriteAllText(Path.Combine(work, runner + "-oracle.log"), result.Output + result.Error);
                    if (result.ExitCode != 0) throw new InvalidDataException("Independent arbiter oracle failed; see " + runner + "-oracle.log");
                }
            });
            if (hashes.Any(pair => CiCommands.Hash(pair.Key) != pair.Value)) throw new InvalidDataException("Arbiter stimulus, wrapper, configuration or oracle changed.");
            OracleEvidence.Verify(context.Root,oracleFiles,oracleInputs);
            FixtureContracts.VerifySources(context.Root, "arbiter");
            report["fixture"] = "arbiter"; report["configuration"] = config; report["top"] = config.Top; report["seed"] = seed;
            report["revision"] = provenance.GetProperty("revision").GetString()!;
            report["planSha256"] = hashes[plan]; report["configurationSha256"] = hashes[configuration]; report["wrapperSha256"] = hashes[wrapper]; report["oracleSha256"] = hashes[oracle];
            StrictJson.Write(Path.Combine(work,OracleEvidence.FileName),new { schemaVersion=1,status="source-verified",hashes=oracleInputs });
            report["fault"] = negative ? "grant-lsb" : "none";
            if (!negative) report["oracleMatched"] = true;
            return report;
        });
    }
}
