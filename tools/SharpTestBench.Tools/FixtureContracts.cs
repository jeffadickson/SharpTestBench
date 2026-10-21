using System.Text.Json;
using System.Text.RegularExpressions;

namespace SharpTestBench.Tools;

internal static class FixtureContracts
{
    public static void Fields(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(names)) throw new InvalidDataException("Invalid fields");
    }
    public static ulong Integer(JsonElement value, ulong minimum, ulong maximum, string name)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt64(out var number) || number < minimum || number > maximum) throw new InvalidDataException("Invalid " + name);
        return number;
    }
    public static bool Boolean(JsonElement value, string name) => value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : throw new InvalidDataException("Invalid boolean: " + name);
    public static ulong Mask(int width) => width == 64 ? ulong.MaxValue : (1UL << width) - 1;
    public static JsonElement VerifySources(string root, string kind)
    {
        string source, revision;
        if (kind == "lfsr") { source = "lfsr.v"; revision = "c1f86d036745935f3812ec6ddc7821c70ef4c9de"; }
        else if (kind == "axis") { source = "axis_register.v"; revision = "48ff7a7e2ef782cf778d47910cf85835c64b1bce"; }
        else if (kind == "fifo") { source = "axis_fifo.v"; revision = "48ff7a7e2ef782cf778d47910cf85835c64b1bce"; }
        else if (kind == "priority") { source = "priority_encoder.v"; revision = "48ff7a7e2ef782cf778d47910cf85835c64b1bce"; }
        else if (kind == "arbiter") { source = "arbiter.v"; revision = "48ff7a7e2ef782cf778d47910cf85835c64b1bce"; }
        else throw new ArgumentException("Unknown fixture kind");
        var folder = Path.Combine(root, "tests/Fixtures/verilog-" + kind);
        if (File.GetAttributes(folder).HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Linked fixture directory");
        using var document = StrictJson.Read(Path.Combine(folder, "provenance.json"));
        var meta = document.RootElement;
        Fields(meta, "repository", "revision", "license", "files");
        if (meta.GetProperty("repository").GetString() != "https://github.com/alexforencich/verilog-" + (kind is "priority" or "fifo" or "arbiter" ? "axis" : kind) || meta.GetProperty("revision").GetString() != revision || meta.GetProperty("license").GetString() != "MIT") throw new InvalidDataException("Invalid upstream provenance");
        var files = meta.GetProperty("files");
        Fields(files, kind == "arbiter" ? [source, "priority_encoder.v", "COPYING"] : [source, "COPYING"]);
        foreach (var file in files.EnumerateObject())
        {
            Fields(file.Value, "upstream", "sha256");
            if (file.Value.GetProperty("upstream").GetString() != (file.Name == "COPYING" ? "" : "rtl/") + file.Name) throw new InvalidDataException("Invalid upstream path");
            var digest = file.Value.GetProperty("sha256").GetString() ?? "";
            var path = Path.Combine(folder, file.Name);
            if (!Regex.IsMatch(digest, "^[0-9a-f]{64}$") || File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint) || new FileInfo(path).Length > 1024 * 1024 || CiCommands.Hash(path) != digest) throw new InvalidDataException("Upstream source changed or invalid");
        }
        return meta.Clone();
    }
}
/// <summary>Integer-seeded MT19937 stream compatible with CPython's getrandbits/randrange, for fixture stability.</summary>
internal sealed class PythonRandom
{
    readonly uint[] state = new uint[624];
    int index = 624;
    public PythonRandom(ulong seed)
    {
        unchecked
        {
            state[0] = 19650218;
            for (int n = 1; n < 624; n++) state[n] = 1812433253 * (state[n - 1] ^ (state[n - 1] >> 30)) + (uint)n;
            uint[] key = seed > uint.MaxValue ? [(uint)seed, (uint)(seed >> 32)] : [(uint)seed];
            int i = 1, j = 0;
            for (int k = Math.Max(624, key.Length); k > 0; k--)
            {
                state[i] = (state[i] ^ ((state[i - 1] ^ (state[i - 1] >> 30)) * 1664525)) + key[j] + (uint)j;
                if (++i >= 624) { state[0] = state[623]; i = 1; }
                if (++j >= key.Length) j = 0;
            }
            for (int k = 623; k > 0; k--)
            {
                state[i] = (state[i] ^ ((state[i - 1] ^ (state[i - 1] >> 30)) * 1566083941)) - (uint)i;
                if (++i >= 624) { state[0] = state[623]; i = 1; }
            }
            state[0] = 0x80000000;
        }
    }
    uint Word()
    {
        if (index >= 624)
        {
            for (int i = 0; i < 624; i++) { uint y = (state[i] & 0x80000000) | (state[(i + 1) % 624] & 0x7fffffff); state[i] = state[(i + 397) % 624] ^ (y >> 1) ^ ((y & 1) != 0 ? 0x9908b0dfu : 0); }
            index = 0;
        }
        uint value = state[index++];
        value ^= value >> 11; value ^= (value << 7) & 0x9d2c5680; value ^= (value << 15) & 0xefc60000; value ^= value >> 18;
        return value;
    }
    public ulong GetBits(int width)
    {
        if (width < 0 || width > 64) throw new ArgumentOutOfRangeException(nameof(width));
        if (width == 0) return 0;
        if (width <= 32) return Word() >> (32 - width);
        return Word() | ((ulong)(Word() >> (64 - width)) << 32);
    }
    public int Below(int bound)
    {
        if (bound <= 0) throw new ArgumentOutOfRangeException(nameof(bound));
        int width = System.Numerics.BitOperations.Log2((uint)bound) + 1;
        ulong value; do { value = GetBits(width); } while (value >= (ulong)bound);
        return (int)value;
    }
}
