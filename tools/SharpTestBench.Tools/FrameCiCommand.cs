using SharpTestBench.Fixtures;
using System.Globalization;
using System.Text.Json;

namespace SharpTestBench.Tools;

internal static class FrameCiCommand
{
    public static void Execute(ToolContext context, string[] arguments)
    {
        var options=new Options(arguments,"--output-fifo","--negative-control");
        options.Only("--depth","--pipeline","--output-fifo","--seed","--count","--negative-control");
        int Number(string key,int fallback)=>int.TryParse(options.Get(key,fallback.ToString(CultureInfo.InvariantCulture)),NumberStyles.None,CultureInfo.InvariantCulture,out int value)?value:throw new ArgumentException("Invalid frame option: "+key);
        if(!ulong.TryParse(options.Get("--seed","1"),NumberStyles.None,CultureInfo.InvariantCulture,out ulong seed))throw new ArgumentException("Invalid frame seed");
        var config=new FrameConfig(Number("--depth",8),Number("--pipeline",1),options.Has("--output-fifo"));
        config.Validate();Run(context,config,seed,Number("--count",16),options.Has("--negative-control"));
    }
    internal static Dictionary<string, object> Run(ToolContext context, FrameConfig config, ulong seed, int count, bool negative)
    {
        var planValue = FrameFixture.Plan(config, seed, count); var stimulus = JsonSerializer.SerializeToElement(planValue);
        FrameFixture.ReadPlan(config, stimulus);
        return ParityCiCommand.InWorkspace(context, $"frame-{config.Top}-{seed}-{count}" + (negative ? "-negative" : ""), (work, environment) => {
            var provenance = FixtureContracts.VerifySources(context.Root, "fifo");
            var build = context.Run(["dotnet", "build", context.At("examples/FrameFifo"), "-c", "Release", "-p:RestoreLockedMode=true", "--disable-build-servers", "-m:1"], environment: environment, capture: true, check: false);
            File.WriteAllText(Path.Combine(work, "build.log"), build.Output + build.Error);
            if (build.ExitCode != 0) throw new IOException("FrameFifo fixture build failed; see build.log");
            NativeBuildCommand.Build(context);
            var plan = Path.Combine(work, "plan.json"); var configuration = Path.Combine(work, "configuration.json");
            var wrapper = Path.Combine(work, "frame_fixture.sv"); var oracle = context.At("tests/Tooling/frame_reference.py");
            StrictJson.Write(plan, planValue); StrictJson.Write(configuration, config); File.WriteAllText(wrapper, FrameFixture.Wrapper(config));
            string[] oracleFiles=["tests/Tooling/frame_reference.py","tests/Tooling/fifo_reference.py"];
            var oracleInputs=OracleEvidence.Capture(context.Root,oracleFiles);
            var hashes = new[] { plan, configuration, wrapper, oracle }.ToDictionary(path => path, CiCommands.Hash);
            environment["FRAME_PLAN"] = plan; environment["FRAME_CONFIG"] = configuration; environment["FRAME_FAULT"] = negative ? "1" : "0";
            var manifest = RunnerManifestCommand.Make(context.Root, "counter", seed, RunnerManifestCommand.InstalledAdapter(context), Path.Combine(work, "native"));
            manifest["top"] = config.Top; manifest["sources"] = new[] { wrapper, context.At("tests/Fixtures/verilog-fifo/axis_fifo.v") };
            manifest["assembly"] = context.At("examples/FrameFifo/bin/Release/net10.0/FrameFifo.dll"); manifest["entryType"] = "Entry, FrameFifo";
            RunnerManifestCommand.BindSingleCase(manifest, "Entry.FrameTransactions", seed);
            StrictJson.Write(Path.Combine(work, "manifest.json"), manifest); StrictJson.Write(Path.Combine(work, "expected.json"), manifest["expectedNames"]);
            var report = ParityCiCommand.Compare(context, work, manifest, seed, environment, "Entry.FrameTransactions", "FRAME_REPORT", negative ? "FRAME_MISMATCH" : null, (a, b) => {
                if(FrameFixture.Metrics(config,a,stimulus)<1 || FrameFixture.Metrics(config,b,stimulus)<1) throw new InvalidDataException("Incomplete-frame visibility was not witnessed.");
                if (!JsonElement.DeepEquals(a, b)) throw new InvalidDataException("FrameFifo runner traces differ");
                foreach (var runner in new[] { "legacy", "native" })
                {
                    var result = context.Run([Environment.GetEnvironmentVariable("CS_PYTHON") ?? "python", oracle, "--configuration", configuration, "--plan", plan, "--metrics", Path.Combine(work, runner, "metrics.json")], capture: true, check: false, timeout: TimeSpan.FromSeconds(30));
                    File.WriteAllText(Path.Combine(work, runner + "-oracle.log"), result.Output + result.Error);
                    if (result.ExitCode != 0) throw new InvalidDataException("Independent frame oracle failed; see " + runner + "-oracle.log");
                }
            });
            if (hashes.Any(pair => CiCommands.Hash(pair.Key) != pair.Value)) throw new InvalidDataException("FrameFifo stimulus, wrapper, configuration or oracle changed.");
            OracleEvidence.Verify(context.Root,oracleFiles,oracleInputs);
            FixtureContracts.VerifySources(context.Root, "fifo");
            report["fixture"] = "frame-fifo"; report["configuration"] = config; report["top"] = config.Top; report["seed"] = seed;
            report["revision"] = provenance.GetProperty("revision").GetString()!;
            report["planSha256"] = hashes[plan]; report["configurationSha256"] = hashes[configuration]; report["wrapperSha256"] = hashes[wrapper]; report["oracleSha256"] = hashes[oracle];
            StrictJson.Write(Path.Combine(work,OracleEvidence.FileName),new { schemaVersion=1,status="source-verified",hashes=oracleInputs });
            report["fault"] = negative ? "payload-lsb" : "none";
            if (!negative) report["oracleMatched"] = true;
            return report;
        });
    }
}
