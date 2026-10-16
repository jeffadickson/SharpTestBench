using SharpTestBench;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using static SharpTestBench.Triggers;

/// <summary>Independent signed FIR scoreboard and cycle-level handshake comparison.</summary>
public static class Entry
{
    /// <summary>Native host entry point for this standalone example.</summary>
    [UnmanagedCallersOnly]
    public static int Initialize(nint p, int n) => Regression.Initialize(typeof(Entry).Assembly);

    /// <summary>Two-bit fault selector width.</summary>
    public readonly struct W2 : IWidth { public static int Bits => 2; }

    /// <summary>One deterministic stimulus opportunity; pending inputs survive backpressure.</summary>
    public sealed record Cycle(bool Reset, bool Ready, bool Offer, int Sample);

    /// <summary>Check every cycle, including stalls and resets, against a signed software model.</summary>
    [SimTest(TimeoutSteps = 10000000)]
    public static async Task FirScoreboard(TestContext c)
    {
        var plan = JsonSerializer.Deserialize<Cycle[]>(File.ReadAllText(Environment.GetEnvironmentVariable("DSP_PLAN")!))!;
        var clk = c.Dut.Child("clk").Signal<W1>();
        var reset = c.Dut.Child("reset").Signal<W1>();
        var valid = c.Dut.Child("in_valid").Signal<W1>();
        var ready = c.Dut.Child("out_ready").Signal<W1>();
        var sample = c.Dut.Child("sample").Signal<W16>();
        var fault = c.Dut.Child("fault").Signal<W2>();
        var accept = c.Dut.Child("in_ready").Signal<W1>();
        var output = c.Dut.Child("out_valid").Signal<W1>();
        var result = c.Dut.Child("result").Signal<W32>();
        int? pending = null;
        int[] history = new int[3];
        int expected = 0, accepted = 0, consumed = 0, stalls = 0;
        bool occupied = false;
        var trace = new List<long[]>();
        int mode = int.Parse(Environment.GetEnvironmentVariable("DSP_FAULT") ?? "0");
        long before = GC.GetTotalAllocatedBytes(true);
        int[] gc = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
        var timer = Stopwatch.StartNew();
        for (int cycle = 0; cycle < plan.Length; cycle++)
        {
            var row = plan[cycle];
            if (row.Reset) pending = null;
            else if (pending is null && row.Offer) pending = row.Sample;
            clk.WriteBit(false); reset.WriteBit(row.Reset); ready.WriteBit(row.Ready);
            valid.WriteBit(pending.HasValue); sample.Write(new UInt<W16>((ushort)(pending ?? 0)));
            fault.Write(new UInt<W2>(mode));
            await Timer(1); await ReadOnly();
            if (cycle > 0)
            {
                bool canAccept = !occupied || row.Ready;
                bool actualValid = output.ReadBit();
                int actual = actualValid ? unchecked((int)(uint)result.ReadUInt().Value) : 0;
                if (accept.ReadBit() != canAccept || actualValid != occupied || (occupied && actual != expected))
                    throw new Exception($"DSP_MISMATCH cycle={cycle} expected={expected} actual={actual}");
                trace.Add([cycle, canAccept ? 1 : 0, actualValid ? 1 : 0, actual]);
                if (!row.Reset)
                {
                    if (occupied && row.Ready) consumed++;
                    if (occupied && !row.Ready) stalls++;
                    if (canAccept)
                    {
                        occupied = pending.HasValue;
                        if (pending is int value)
                        {
                            // Int64 accumulation is intentionally wider than the HDL result.
                            expected = checked((int)(-3L * value + 7L * history[0] + 11L * history[1] - 2L * history[2]));
                            history[2] = history[1]; history[1] = history[0]; history[0] = value;
                            pending = null; accepted++;
                        }
                    }
                }
            }
            if (row.Reset) { occupied = false; expected = 0; Array.Clear(history); }
            await Timer(1); clk.WriteBit(true);
            await Timer(1); await ReadOnly(); await Timer(1);
        }
        if (occupied || pending.HasValue || accepted == 0 || stalls == 0)
            throw new Exception("DSP_DRAIN: incomplete or empty workload");
        timer.Stop();
        File.WriteAllText(Environment.GetEnvironmentVariable("DSP_REPORT")!, JsonSerializer.Serialize(new
        {
            trace, accepted, consumed, stalls, cycles = plan.Length, seconds = timer.Elapsed.TotalSeconds,
            allocated_bytes = GC.GetTotalAllocatedBytes(false) - before,
            gc_collections = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - gc[i]).ToArray()
        }));
    }
}
