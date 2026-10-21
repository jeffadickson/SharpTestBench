using SharpTestBench;
using System.Runtime.InteropServices;
using System.Text.Json;

public static class Entry
{
    [UnmanagedCallersOnly]
    public static int Initialize(nint p, int n) => Regression.Initialize(typeof(Entry).Assembly);

    // Deliberately linear software oracle, independent of the HDL reduction tree.
    public static int Winner(uint mask, int width, bool lowFirst)
    {
        if (width is < 2 or > 32) throw new ArgumentOutOfRangeException(nameof(width));
        for (int step = 0; step < width; step++)
        {
            int bit = lowFirst ? step : width - step - 1;
            if ((mask & (1u << bit)) != 0) return bit;
        }
        return -1;
    }

    [SimTest(TimeoutSteps = 10000000)]
    public static async Task PriorityScoreboard(TestContext c)
    {
        var input = c.Dut.Child("requests").Signal<W32>();
        var fault = c.Dut.Child("fault").Signal<W1>();
        var rows = new List<long[]>();
        var masks = Enumerable.Range(0, 256).Select(x => (uint)x).ToList();
        masks.AddRange([uint.MaxValue, 0x80000000, 0x80000001, 0xaaaaaaaa, 0x55555555]);
        for (int bit = 0; bit < 32; bit++) { masks.Add(1u << bit); masks.Add(~(1u << bit)); }
        // A specified generator, rather than System.Random's versioned algorithm.
        ulong state = c.Seed;
        for (int n = 0; n < 128; n++)
        {
            state = unchecked(state * 6364136223846793005UL + 1442695040888963407UL);
            masks.Add((uint)(state >> 32));
        }
        var signals = (from width in new[] { 2, 3, 5, 8, 17, 32 }
                       from low in new[] { false, true }
                       let prefix = $"w{width}_{(low ? "lsb" : "msb")}_"
                       select (width, low, valid: c.Dut.Child(prefix + "valid").Signal<W1>(),
                           index: c.Dut.Child(prefix + "index").Signal<W32>(),
                           onehot: c.Dut.Child(prefix + "onehot").Signal<W32>())).ToArray();
        fault.WriteBit(Environment.GetEnvironmentVariable("PRIORITY_FAULT") == "1");
        foreach (uint mask in masks)
        {
            input.Write(new UInt<W32>(mask));
            await Triggers.Timer(1); await Triggers.ReadOnly();
            foreach (var s in signals)
            {
                int winner = Winner(mask, s.width, s.low);
                bool valid = s.valid.ReadBit();
                uint index = (uint)s.index.ReadUInt().Value, onehot = (uint)s.onehot.ReadUInt().Value;
                uint expected = winner < 0 ? 0 : 1u << winner;
                // Both encoded and onehot outputs are unspecified when invalid.
                if (valid != (winner >= 0) || (valid && (onehot != expected || index != winner)))
                    throw new Exception($"PRIORITY_MISMATCH width={s.width} low={s.low} mask={mask:X8} winner={winner} valid={valid} index={index} onehot={onehot:X8}");
                rows.Add([s.width, s.low ? 1 : 0, mask, valid ? 1 : 0, valid ? index : -1, valid ? onehot : 0]);
            }
            await Triggers.Timer(1);
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("PRIORITY_REPORT")!,
            JsonSerializer.Serialize(new { trace = rows, vectors = masks.Count, configurations = signals.Length }));
    }
}
