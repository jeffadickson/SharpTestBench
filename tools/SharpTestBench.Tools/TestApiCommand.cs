using System.Text;
using System.Text.Json;
namespace SharpTestBench.Tools;
internal static class TestApiCommand
{
    public static void Execute()
    {
        string? line;
        while ((line = Console.ReadLine()) is not null)
        {
            try { using var request = StrictJson.Parse(line); var result = Dispatch(request.RootElement); Console.WriteLine(JsonSerializer.Serialize(new { ok = true, result })); }
            catch (Exception error) { Console.WriteLine(JsonSerializer.Serialize(new { ok = false, kind = error.GetType().Name, message = error.Message })); }
        }
    }
    static object? Dispatch(JsonElement request)
    {
        var module = request.GetProperty("module").GetString(); var name = request.GetProperty("name").GetString(); var args = request.GetProperty("args"); var kwargs = request.GetProperty("kwargs");
        JsonElement Value(int index, string key, object? fallback = null) => index < args.GetArrayLength() ? args[index] : kwargs.TryGetProperty(key, out var value) ? value : JsonSerializer.SerializeToElement(fallback);
        string Text(int index, string key, string? fallback = null) { var value = Value(index, key, fallback); if (value.ValueKind != JsonValueKind.String) throw new InvalidDataException("Expected text: " + key); return value.GetString()!; }
        ulong Number(int index, string key, ulong minimum = 0, ulong maximum = ulong.MaxValue, ulong fallback = 0) => FixtureContracts.Integer(Value(index, key, fallback), minimum, maximum, key);
        bool Flag(int index, string key, bool fallback = true) => FixtureContracts.Boolean(Value(index, key, fallback), key);
        if (module == "lfsr_fixture")
        {
            LfsrConfig Config() => LfsrFixture.Config(Value(0, "settings"));
            return name switch {
                "constants" => new { CASES = LfsrFixture.Cases.ToDictionary(pair => pair.Key, pair => new object[] { pair.Value.Width, pair.Value.Polynomial, pair.Value.DataWidth, pair.Value.Topology, pair.Value.Reverse, pair.Value.FeedForward }), STYLES = LfsrFixture.Styles },
                "config" => LfsrFixture.Config(JsonSerializer.SerializeToElement(new { Width = Value(0, "width", 32), Polynomial = Value(1, "polynomial", 0x04c11db7), DataWidth = Value(2, "data_width", 8), Topology = Value(3, "topology", "GALOIS"), Reverse = Value(4, "reverse", true), FeedForward = Value(5, "feed_forward", false) })),
                "validate_config" => LfsrFixture.Config(Value(0, "value")),
                "fixture" => Fixture(Text(0, "name"), Text(1, "style")),
                "reverse_bits" => LfsrFixture.Reverse(Number(0, "value", maximum: FixtureContracts.Mask((int)Number(1, "width", 1, 64))), (int)Number(1, "width", 1, 64)),
                "transition" => Transition(Config(), Number(1, "state", maximum: FixtureContracts.Mask(Config().Width)), Number(2, "data", maximum: FixtureContracts.Mask(Config().DataWidth))),
                "plan" => LfsrFixture.Plan(Config(), Number(1, "seed"), (int)Number(2, "count", 1, 4096, 64)),
                "validate_plan" => LfsrFixture.ValidatePlan(Value(0, "value")),
                "metrics" => LfsrFixture.Metrics(Value(0, "value"), Value(1, "stimulus")),
                "verify_sources" => FixtureContracts.VerifySources(Text(0, "root"), "lfsr"), "plan_digest" => LfsrFixture.PlanDigest(Text(0, "path")), _ => throw new ArgumentException("Unknown LFSR contract") };
        }
        if (module == "axis_fixture") return name switch {
            "integer" => Number(0, "value", Number(1, "low"), Number(2, "high")),
            "plan" => AxisFixture.Plan(Number(0, "seed"), (int)Number(1, "count", 1, 4096, 64), (int)Number(2, "stall", 0, 32, 3), Value(3, "reset_at").ValueKind == JsonValueKind.Null ? null : (int)Number(3, "reset_at", 1, 4095)),
            "validate_plan" => AxisFixture.ValidatePlan(Value(0, "rows")), "configuration" => AxisFixture.Configuration((int)Number(0, "mode", 0, 2), Flag(1, "sidebands")), "project" => AxisFixture.Project(Value(0, "row"), Flag(1, "sidebands")), "metrics" => AxisFixture.Metrics(Value(0, "data")), "verify_sources" => FixtureContracts.VerifySources(Text(0, "root"), "axis"), _ => throw new ArgumentException("Unknown AXIS contract") };
        if (module == "arbiter_fixture")
        {
            var config = ArbiterFixture.Configuration(Value(0, "configuration"));
            return name switch {
                "plan" => ArbiterFixture.Plan(config, Number(1, "seed"), (int)Number(2, "count", 1, 2048, 128)),
                "metrics" => ArbiterFixture.Metrics(config, Value(1, "metrics"), Value(2, "plan")),
                _ => throw new ArgumentException("Unknown arbiter contract") };
        }
        if (module == "frame_fixture")
        {
            var config=FrameFixture.Configuration(Value(0,"config"));
            return name switch {
                "plan"=>FrameFixture.Plan(config,Number(1,"seed"),(int)Number(2,"count",1,32,16)),
                "metrics"=>FrameFixture.Metrics(config,Value(1,"metrics"),Value(2,"plan")),
                "wrapper"=>FrameFixture.Wrapper(config),
                _=>throw new ArgumentException("Unknown frame contract") };
        }
        if (module == "fifo_fixture")
        {
            FifoConfiguration Config()
            {
                var value = Value(0, "configuration"); FixtureContracts.Fields(value, "Depth", "Pipeline", "OutputFifo", "Sidebands");
                return FifoFixture.Configuration((int)FixtureContracts.Integer(value.GetProperty("Depth"), 2, 64, "depth"),
                    (int)FixtureContracts.Integer(value.GetProperty("Pipeline"), 0, 3, "pipeline"),
                    FixtureContracts.Boolean(value.GetProperty("OutputFifo"), "output_fifo"), FixtureContracts.Boolean(value.GetProperty("Sidebands"), "sidebands"));
            }
            return name switch {
                "plan" => FifoFixture.Plan(Config(), Number(1, "seed"), (int)Number(2, "count", 1, 2048, 128), (int)Number(3, "stall", 0, 32, 7), Value(4, "reset_at").ValueKind == JsonValueKind.Null ? null : (int)Number(4, "reset_at", 1, 2047)),
                "saturation_plan" => FifoFixture.Plan(Config(), Number(1, "seed"), (int)Number(2, "count", 1, 2048, 256), 0, Value(3, "reset_at").ValueKind == JsonValueKind.Null ? null : (int)Number(3, "reset_at", 1, 2047), "saturation"),
                "saturation_witness" => FifoFixture.SaturationWitness(Config(), Value(1, "metrics"), Value(2, "plan")),
                "metrics" => FifoFixture.Metrics(Config(), Value(1, "metrics"), Value(2, "plan")),
                "verify_sources" => FixtureContracts.VerifySources(Text(0, "root"), "fifo"),
                _ => throw new ArgumentException("Unknown FIFO contract") };
        }
        if (module == "dual_runner_manifest") return name switch { "constants" => RunnerManifestCommand.Fixtures, "make_manifest" => RunnerManifestCommand.Make(Text(0, "root"), Text(1, "fixture"), Number(2, "seed"), Value(3, "adapter").EnumerateObject().ToDictionary(pair => pair.Name, pair => (object)(pair.Value.ValueKind == JsonValueKind.String ? pair.Value.GetString()! : "")), Text(4, "output_directory")), _ => throw new ArgumentException("Unknown manifest contract") };
        if (module == "dual_runner_parity")
        {
            if (name == "write_report") { StrictJson.Write(Text(0, "path"), Value(1, "value")); return null; }
            return name switch { "signature" => ParityContracts.Signature(Value(0, "results")), "expected_identities" => ParityContracts.Expected(Value(0, "value")), "compare" => ParityContracts.Compare(Value(0, "legacy"), Value(1, "native"), Value(2, "expected")), "main" => Cli(Value(0, "args").EnumerateArray().Select(value => value.GetString()!).ToArray()), _ => throw new ArgumentException("Unknown parity contract") };
        }
        if (module == "dual_runner_ci")
        {
            if (name == "clear_case") { ParityContracts.ClearCase(Text(0, "work")); return null; }
            return name switch { "clean_environment" => ParityContracts.CleanEnvironment(Value(0, "environment").EnumerateObject().ToDictionary(pair => pair.Name, pair => pair.Value.GetString())), "metrics_contract" => ParityContracts.Metrics(Text(0, "fixture"), Value(1, "data")), "fault_detected" => ParityContracts.FaultDetected(Value(0, "results"), (int)Number(1, "returncode", 0, int.MaxValue), Value(2, "expected_names"), Text(3, "marker")), _ => throw new ArgumentException("Unknown CI contract") };
        }
        if (module == "strict_json")
        {
            var maximum = request.TryGetProperty("maximumBytes", out var selected) ? selected.GetInt32() : 16 * 1024 * 1024;
            string text;
            if (name == "load") { using var stream = File.OpenRead(Text(0, "path")); text = new UTF8Encoding(false, true).GetString(BoundedInput.Read(stream, maximum)).TrimStart('\uFEFF'); }
            else text = Text(0, "text");
            if (Encoding.UTF8.GetByteCount(text) > maximum) throw new InvalidDataException("JSON exceeds byte limit"); using var document = StrictJson.Parse(text); return document.RootElement.Clone();
        }
        if (module == "code_coverage_report")
        {
            var path = Text(0, "path"); var maximum = request.TryGetProperty("maximumBytes", out var selected) ? selected.GetInt32() : CoverageCommand.MaximumBytes;
            if (new FileInfo(path).Length > maximum) throw new InvalidDataException("Coverage exceeds size limit"); return CoverageCommand.Validate(path, Value(1, "expected").EnumerateArray().Select(value => value.GetString()!).ToArray());
        }
        if (module == "windows_arm64_check") return NativeChecks.PeMachine(Text(0, "path"));
        if (module == "prepare_upstream") return PrepareUpstreamCommand.ConfigureNativeBuild(Text(0, "source"));
        if (module == "icarus_layouts") return IcarusLayoutsCommand.TargetFlags(Text(0, "platform"), Text(1, "compiler"));
        if (module == "release_bundle") return name switch { "assemble" => ReleaseCommands.Assemble(Text(0, "artifacts"), Text(1, "output"), Text(2, "revision")), "constants" => ReleaseCommands.Rids, _ => throw new ArgumentException("Unknown release contract") };
        throw new ArgumentException("Unknown managed test API");
    }
    static object[] Fixture(string name, string style) { var fixture = LfsrFixture.Fixture(name, style); return [fixture.Top, fixture.Config]; }
    static ulong[] Transition(LfsrConfig config, ulong state, ulong data) { var next = LfsrFixture.Transition(config, state, data); return [next.State, next.Data]; }
    static int Cli(string[] args)
    {
        var original = Console.Out;
        try { Console.SetOut(TextWriter.Null); ParityContracts.Execute(args); return 0; }
        catch { return 1; }
        finally { Console.SetOut(original); }
    }
}
