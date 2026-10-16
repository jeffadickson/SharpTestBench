using SharpTestBench;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using static SharpTestBench.Triggers;

/// <summary>Explicit consumer of compiler metadata; does not modify runtime discovery behavior.</summary>
public static class Entry
{
    /// <summary>Native host entry point for the isolated metadata contract.</summary>
    [UnmanagedCallersOnly]
    public static int Initialize(nint p, int n) => Regression.Initialize(typeof(Entry).Assembly);
    /// <summary>Fixture vector width.</summary>
    public readonly struct W48 : IWidth { public static int Bits => 48; }

    /// <summary>Use exported dimensions to address fields and compare independent HDL selections.</summary>
    [SimTest]
    public static async Task CompilerPackedLayout(TestContext c)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("STB_LAYOUT_METADATA")!));
        var root = document.RootElement;
        if (root.GetProperty("schema").GetInt32() != 1) throw new Exception("LAYOUT_SCHEMA");
        foreach (var source in root.GetProperty("provenance").GetProperty("sources").EnumerateObject())
            if (!Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source.Name))).Equals(source.Value.GetString(), StringComparison.OrdinalIgnoreCase))
                throw new Exception("LAYOUT_SOURCE_CHANGED");
        PackedArray<W48, W8> Bind(string name)
        {
            var row = root.GetProperty("signals").EnumerateArray().Single(x =>
                x.GetProperty("path").EnumerateArray().Select(p => p.GetString()).SequenceEqual(new[] { "top", name }));
            var pairs = row.GetProperty("dimensions").EnumerateArray().ToArray();
            if (row.GetProperty("width").GetInt32() != 48 || pairs.Length != 3
                || pairs[^1][0].GetInt32() != 7 || pairs[^1][1].GetInt32() != 0) throw new Exception("LAYOUT_SCHEMA");
            var dims = pairs.Take(2).Select(p => new HdlRange(p[0].GetInt32(), p[1].GetInt32())).ToArray();
            var signal = c.Dut.Child(name).Signal<W48>();
            signal.Write(new UInt<W48>(0));
            return signal.Packed<W8>(dims);
        }
        var matrix = Bind("packed_matrix"); var mixed = Bind("packed_mixed");
        matrix[1, 1].Write(new UInt<W8>(0xA2)); matrix[0, 0].Write(new UInt<W8>(0xB3));
        mixed[-1, 3].Write(new UInt<W8>(0xD4)); mixed[0, 1].Write(new UInt<W8>(0xE5));
        await Timer(1); await ReadOnly();
        foreach (var (name, expected) in new[] { ("matrix_middle", 0xA2), ("matrix_right", 0xB3), ("mixed_first", 0xD4), ("mixed_last", 0xE5) })
            if (c.Dut.Child(name).Signal<W8>().ReadUInt().Value != expected) throw new Exception("LAYOUT_SELECTION_MISMATCH " + name);
    }
}
