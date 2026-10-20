using System.Numerics;
using System.Text;
using System.Text.Json;

namespace SharpTestBench.Tools;

internal sealed record LfsrConfig(int Width, ulong Polynomial, int DataWidth, string Topology, bool Reverse, bool FeedForward);
internal sealed record LfsrVector(ulong State, ulong Data, ulong ExpectedState, ulong ExpectedData);
internal sealed record LfsrPlan(LfsrConfig Config, ulong Seed, LfsrVector[] Vectors);
internal static class LfsrFixture
{
    public static readonly IReadOnlyDictionary<string, LfsrConfig> Cases = new Dictionary<string, LfsrConfig>(StringComparer.Ordinal) {
        ["crc32"] = new(32, 0x04c11db7, 8, "GALOIS", true, false), ["crc16"] = new(16, 0x1021, 8, "GALOIS", false, false),
        ["crc64"] = new(64, 0x42f0e1eba9ea3693, 8, "GALOIS", false, false), ["crc64xz"] = new(64, 0x42f0e1eba9ea3693, 8, "GALOIS", true, false),
        ["crc32c"] = new(32, 0x1edc6f41, 32, "GALOIS", true, false), ["prbs7"] = new(7, 0x41, 8, "FIBONACCI", false, false),
        ["scrambler"] = new(58, 0x8000000001, 64, "FIBONACCI", true, false), ["descrambler"] = new(58, 0x8000000001, 64, "FIBONACCI", true, true),
        ["galois_ff"] = new(16, 0x8005, 16, "GALOIS", true, true), ["minimal"] = new(1, 1, 1, "FIBONACCI", false, false) };
    public static readonly string[] Styles = ["LOOP", "REDUCTION"];
    public static LfsrConfig Validate(LfsrConfig value)
    {
        if (value.Width < 1 || value.Width > 64 || value.DataWidth < 1 || value.DataWidth > 64 || value.Polynomial == 0 || value.Polynomial > FixtureContracts.Mask(value.Width) || (value.Polynomial & 1) == 0 || value.Topology is not ("GALOIS" or "FIBONACCI")) throw new InvalidDataException("Invalid LFSR configuration");
        return value;
    }
    public static LfsrConfig Config(JsonElement value)
    {
        FixtureContracts.Fields(value, "Width", "Polynomial", "DataWidth", "Topology", "Reverse", "FeedForward");
        int width = (int)FixtureContracts.Integer(value.GetProperty("Width"), 1, 64, "width");
        int dataWidth = (int)FixtureContracts.Integer(value.GetProperty("DataWidth"), 1, 64, "data width");
        return Validate(new(width, FixtureContracts.Integer(value.GetProperty("Polynomial"), 1, FixtureContracts.Mask(width), "polynomial"), dataWidth, value.GetProperty("Topology").GetString() ?? "", FixtureContracts.Boolean(value.GetProperty("Reverse"), "reverse"), FixtureContracts.Boolean(value.GetProperty("FeedForward"), "feed forward")));
    }
    public static (string Top, LfsrConfig Config) Fixture(string name, string style)
    {
        if (!Cases.TryGetValue(name, out var config) || !Styles.Contains(style, StringComparer.Ordinal)) throw new InvalidDataException("Unknown LFSR fixture or style");
        return ("lfsr_" + name + "_" + style.ToLowerInvariant(), config);
    }
    public static ulong Reverse(ulong value, int width)
    {
        if (width < 1 || width > 64 || value > FixtureContracts.Mask(width)) throw new InvalidDataException("Invalid bit reversal");
        ulong result = 0; for (int i = 0; i < width; i++) { result = (result << 1) | (value & 1); value >>= 1; } return result;
    }
    public static (ulong State, ulong Data) Transition(LfsrConfig settings, ulong state, ulong data)
    {
        Validate(settings); int width = settings.Width, dataWidth = settings.DataWidth;
        ulong mask = FixtureContracts.Mask(width);
        if (state > mask || data > FixtureContracts.Mask(dataWidth)) throw new InvalidDataException("Invalid state or data");
        if (settings.Reverse) { state = Reverse(state, width); data = Reverse(data, dataWidth); }
        ulong result = 0;
        for (int index = dataWidth - 1; index >= 0; index--)
        {
            ulong bit = (data >> index) & 1;
            ulong feedback = ((state >> (width - 1)) & 1) ^ bit;
            if (settings.Topology == "FIBONACCI") feedback ^= (ulong)(BitOperations.PopCount(state & (settings.Polynomial >> 1)) & 1);
            result = (result << 1) | feedback;
            ulong injected = settings.FeedForward ? bit : feedback;
            state = ((state << 1) | injected) & mask;
            if (settings.Topology == "GALOIS" && injected != 0) state ^= settings.Polynomial & ~1UL;
        }
        if (settings.Reverse) { state = Reverse(state, width); result = Reverse(result, dataWidth); }
        return (state, result);
    }
    static List<(ulong State, ulong Data)> Prefix(LfsrConfig settings)
    {
        var pairs = new List<(ulong, ulong)> { (0, 0), (FixtureContracts.Mask(settings.Width), 0), (0, FixtureContracts.Mask(settings.DataWidth)), (FixtureContracts.Mask(settings.Width), FixtureContracts.Mask(settings.DataWidth)) };
        for (int bit = 0; bit < settings.Width; bit++) pairs.Add((1UL << bit, 0));
        for (int bit = 0; bit < settings.DataWidth; bit++) pairs.Add((0, 1UL << bit));
        return pairs;
    }
    public static LfsrPlan Plan(LfsrConfig settings, ulong seed, int count = 64)
    {
        Validate(settings); if (count < 1 || count > 4096) throw new InvalidDataException("Invalid vector count");
        var random = new PythonRandom(seed); var pairs = Prefix(settings);
        for (int index = 0; index < count; index++) pairs.Add((random.GetBits(settings.Width), random.GetBits(settings.DataWidth)));
        return new(settings, seed, pairs.Select(pair => { var result = Transition(settings, pair.State, pair.Data); return new LfsrVector(pair.State, pair.Data, result.State, result.Data); }).ToArray());
    }
    public static LfsrPlan ValidatePlan(JsonElement value)
    {
        FixtureContracts.Fields(value, "Config", "Seed", "Vectors"); var config = Config(value.GetProperty("Config"));
        ulong seed = FixtureContracts.Integer(value.GetProperty("Seed"), 0, ulong.MaxValue, "seed");
        var rows = value.GetProperty("Vectors");
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() < 5 + config.Width + config.DataWidth || rows.GetArrayLength() > 4228) throw new InvalidDataException("Invalid vector count");
        var prefix = Prefix(config); var vectors = new List<LfsrVector>();
        foreach (var row in rows.EnumerateArray())
        {
            FixtureContracts.Fields(row, "State", "Data", "ExpectedState", "ExpectedData");
            var item = new LfsrVector(FixtureContracts.Integer(row.GetProperty("State"), 0, FixtureContracts.Mask(config.Width), "State"), FixtureContracts.Integer(row.GetProperty("Data"), 0, FixtureContracts.Mask(config.DataWidth), "Data"), FixtureContracts.Integer(row.GetProperty("ExpectedState"), 0, FixtureContracts.Mask(config.Width), "ExpectedState"), FixtureContracts.Integer(row.GetProperty("ExpectedData"), 0, FixtureContracts.Mask(config.DataWidth), "ExpectedData"));
            if (vectors.Count < prefix.Count && prefix[vectors.Count] != (item.State, item.Data)) throw new InvalidDataException("Missing basis vectors");
            if (Transition(config, item.State, item.Data) != (item.ExpectedState, item.ExpectedData)) throw new InvalidDataException("Incorrect reference result");
            vectors.Add(item);
        }
        return new(config, seed, vectors.ToArray());
    }
    public static JsonElement Metrics(JsonElement value, JsonElement stimulus)
    {
        var plan = ValidatePlan(stimulus); FixtureContracts.Fields(value, "trace", "vectors");
        var rows = value.GetProperty("trace"); ulong count = FixtureContracts.Integer(value.GetProperty("vectors"), 1, 4228, "vector count");
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() != plan.Vectors.Length || count != (ulong)rows.GetArrayLength()) throw new InvalidDataException("Incomplete LFSR trace");
        for (int index = 0; index < plan.Vectors.Length; index++)
        {
            var row = rows[index]; var item = plan.Vectors[index];
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != 5) throw new InvalidDataException("Invalid LFSR trace row");
            var actual = row.EnumerateArray().Select(word => FixtureContracts.Integer(word, 0, ulong.MaxValue, "trace word")).ToArray();
            if (!actual.SequenceEqual(new[] { (ulong)index, item.State, item.Data, item.ExpectedState, item.ExpectedData })) throw new InvalidDataException("LFSR trace does not match stimulus");
        }
        return value.Clone();
    }
    public static string PlanDigest(string path)
    {
        using var stream = File.OpenRead(path); var raw = BoundedInput.Read(stream, JsonInput.MaximumBytes);
        using var document = StrictJson.Parse(new UTF8Encoding(false, true).GetString(raw)); ValidatePlan(document.RootElement);
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(raw));
    }
}
