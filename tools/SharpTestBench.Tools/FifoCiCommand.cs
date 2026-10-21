using System.Globalization;
using System.Text.Json;

namespace SharpTestBench.Tools;

internal static class FifoCiCommand
{
    public static void Execute(ToolContext context, string[] arguments)
    {
        var options = new Options(arguments, "--output-fifo", "--disabled-sidebands", "--negative-control");
        options.Only("--depth", "--pipeline", "--output-fifo", "--disabled-sidebands", "--seed", "--count", "--stall", "--reset-at", "--negative-control");
        int Number(string key, int fallback) => int.TryParse(options.Get(key, fallback.ToString(CultureInfo.InvariantCulture)), System.Globalization.NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value : throw new ArgumentException("Invalid FIFO option: " + key);
        if (!ulong.TryParse(options.Get("--seed", "1"), NumberStyles.None, CultureInfo.InvariantCulture, out var seed)) throw new ArgumentException("Invalid FIFO seed");
        var config = FifoFixture.Configuration(Number("--depth", 8), Number("--pipeline", 1), options.Has("--output-fifo"), !options.Has("--disabled-sidebands"));
        int count = Number("--count", 128), stall = Number("--stall", 7);
        string reset = options.Get("--reset-at", count > 1 ? (count / 2).ToString(CultureInfo.InvariantCulture) : "none")!;
        int? resetAt = reset == "none" ? null : int.TryParse(reset, NumberStyles.None, CultureInfo.InvariantCulture, out int index) ? index : throw new ArgumentException("Invalid FIFO reset index");
        Run(context, config, seed, count, stall, resetAt, options.Has("--negative-control"));
    }
    internal static Dictionary<string, object> Run(ToolContext context, FifoConfiguration config, ulong seed, int count, int stall, int? resetAt, bool negative = false)
    {
        var rows = FifoFixture.Plan(config, seed, count, stall, resetAt);
        var stimulus = JsonSerializer.SerializeToElement(rows); FifoFixture.ValidatePlan(config, stimulus);
        return ParityCiCommand.InWorkspace(context, $"fifo-{config.Top}-{seed}-{count}-{stall}-{resetAt?.ToString(CultureInfo.InvariantCulture) ?? "none"}" + (negative ? "-negative" : ""), (work, environment) => {
            var provenance = FixtureContracts.VerifySources(context.Root, "fifo");
            var build = context.Run(["dotnet", "build", context.At("examples/AxisRegister"), "-c", "Release", "-p:RestoreLockedMode=true", "--disable-build-servers", "-m:1"], environment: environment, capture: true, check: false);
            File.WriteAllText(Path.Combine(work, "build.log"), build.Output + build.Error);
            if (build.ExitCode != 0) throw new IOException("FIFO fixture build failed; see build.log");
            NativeBuildCommand.Build(context);
            var planPath = Path.Combine(work, "plan.json"); var wrapper = Path.Combine(work, "fifo_fixture.sv");
            StrictJson.Write(planPath, rows); File.WriteAllText(wrapper, FifoFixture.Wrapper(config));
            var oracle = context.At("tests/Tooling/fifo_reference.py");
            var planHash = CiCommands.Hash(planPath); var wrapperHash = CiCommands.Hash(wrapper); var oracleHash = CiCommands.Hash(oracle);
            environment["AXIS_MODE"] = "2"; environment["AXIS_PLAN"] = planPath;
            environment["AXIS_SIDEBANDS"] = config.Sidebands ? "1" : "0"; environment["AXIS_FAULT"] = negative ? "1" : "0";
            var manifest = RunnerManifestCommand.Make(context.Root, "counter", seed, RunnerManifestCommand.InstalledAdapter(context), Path.Combine(work, "native"));
            manifest["top"] = config.Top; manifest["sources"] = new[] { wrapper, context.At("tests/Fixtures/verilog-fifo/axis_fifo.v") };
            manifest["assembly"] = context.At("examples/AxisRegister/bin/Release/net10.0/AxisRegister.dll"); manifest["entryType"] = "Entry, AxisRegister";
            RunnerManifestCommand.BindSingleCase(manifest, "Entry.StreamScoreboard", seed);
            StrictJson.Write(Path.Combine(work, "manifest.json"), manifest);
            StrictJson.Write(Path.Combine(work, "expected.json"), manifest["expectedNames"]);
            var report = ParityCiCommand.Compare(context, work, manifest, seed, environment, "Entry.StreamScoreboard", "AXIS_REPORT", negative ? "AXIS_MISMATCH" : null, (a, b) => {
                FifoFixture.Metrics(config, a, stimulus); FifoFixture.Metrics(config, b, stimulus);
                if (!JsonElement.DeepEquals(a, b)) throw new InvalidDataException("FIFO runner traces differ");
                foreach (var runner in new[] { "legacy", "native" })
                {
                    var command = new List<string> { Environment.GetEnvironmentVariable("CS_PYTHON") ?? "python", oracle, "--plan", planPath, "--metrics", Path.Combine(work, runner, "metrics.json") };
                    if (!config.Sidebands) command.Add("--disabled-sidebands");
                    var result = context.Run(command.ToArray(), capture: true, check: false, timeout: TimeSpan.FromSeconds(30));
                    File.WriteAllText(Path.Combine(work, runner + "-oracle.log"), result.Output + result.Error);
                    if (result.ExitCode != 0) throw new InvalidDataException("Independent FIFO oracle failed; see " + runner + "-oracle.log");
                }
            });
            if (CiCommands.Hash(planPath) != planHash || CiCommands.Hash(wrapper) != wrapperHash || CiCommands.Hash(oracle) != oracleHash)
                throw new InvalidDataException("FIFO stimulus, adapter or oracle changed during qualification");
            FixtureContracts.VerifySources(context.Root, "fifo");
            report["fixture"] = "fifo"; report["configuration"] = config; report["seed"] = seed;
            report["revision"] = provenance.GetProperty("revision").GetString()!;
            report["planSha256"] = planHash; report["wrapperSha256"] = wrapperHash; report["oracleSha256"] = oracleHash;
            report["fault"] = negative ? "payload-lsb" : "none";
            if (!negative) report["oracleMatched"] = true;
            return report;
        });
    }
}
