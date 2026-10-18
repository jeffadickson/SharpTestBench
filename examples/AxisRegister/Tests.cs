using SharpTestBench;
using System.Runtime.InteropServices;
using System.Text.Json;
using static SharpTestBench.Triggers;

public static class Entry
{
    [UnmanagedCallersOnly]
    public static int Initialize(nint p, int n) => Regression.Initialize(typeof(Entry).Assembly);
    public readonly struct W2 : IWidth { public static int Bits => 2; }
    public readonly struct W4 : IWidth { public static int Bits => 4; }
    public sealed record Cycle(bool Reset, bool Ready, bool Offer, int Data, int Keep, bool Last, int Id, int Dest, int User);
    public sealed record Word(int Data, int Keep, bool Last, int Id, int Dest, int User);

    [SimTest(TimeoutSteps = 10000000)]
    public static async Task StreamScoreboard(TestContext c)
    {
        var rows = JsonSerializer.Deserialize<Cycle[]>(File.ReadAllText(Environment.GetEnvironmentVariable("AXIS_PLAN")!))!;
        bool enabled = Environment.GetEnvironmentVariable("AXIS_SIDEBANDS") != "0";
        bool inject = Environment.GetEnvironmentVariable("AXIS_FAULT") == "1";
        bool bypass = Environment.GetEnvironmentVariable("AXIS_MODE") == "0";
        var clk = c.Dut.Child("clk").Signal<W1>();
        var rst = c.Dut.Child("rst").Signal<W1>();
        var fault = c.Dut.Child("fault").Signal<W1>();
        var valid = c.Dut.Child("s_axis_tvalid").Signal<W1>();
        var ready = c.Dut.Child("m_axis_tready").Signal<W1>();
        var accept = c.Dut.Child("s_axis_tready").Signal<W1>();
        var output = c.Dut.Child("m_axis_tvalid").Signal<W1>();
        var data = c.Dut.Child("s_axis_tdata").Signal<W16>();
        var keep = c.Dut.Child("s_axis_tkeep").Signal<W2>();
        var last = c.Dut.Child("s_axis_tlast").Signal<W1>();
        var id = c.Dut.Child("s_axis_tid").Signal<W4>();
        var dest = c.Dut.Child("s_axis_tdest").Signal<W4>();
        var user = c.Dut.Child("s_axis_tuser").Signal<W2>();
        var outData = c.Dut.Child("m_axis_tdata").Signal<W16>();
        var outKeep = c.Dut.Child("m_axis_tkeep").Signal<W2>();
        var outLast = c.Dut.Child("m_axis_tlast").Signal<W1>();
        var outId = c.Dut.Child("m_axis_tid").Signal<W4>();
        var outDest = c.Dut.Child("m_axis_tdest").Signal<W4>();
        var outUser = c.Dut.Child("m_axis_tuser").Signal<W2>();
        var queue = new Queue<Word>();
        Word? pending = null, held = null;
        int accepted = 0, consumed = 0, flushed = 0, stalls = 0, resets = 0;
        var trace = new List<long[]>();
        for (int cycle = 0; cycle < rows.Length; cycle++)
        {
            var row = rows[cycle];
            if (row.Reset) { pending = null; held = null; }
            else if (pending is null && row.Offer) pending = new(row.Data,row.Keep,row.Last,row.Id,row.Dest,row.User);
            var input = pending ?? new Word(0,0,false,0,0,0);
            clk.WriteBit(false); rst.WriteBit(row.Reset); fault.WriteBit(inject);
            valid.WriteBit(pending is not null); ready.WriteBit(row.Ready);
            data.Write(new UInt<W16>(input.Data)); keep.Write(new UInt<W2>(input.Keep));
            last.WriteBit(input.Last); id.Write(new UInt<W4>(input.Id));
            dest.Write(new UInt<W4>(input.Dest)); user.Write(new UInt<W2>(input.User));
            await Timer(1); await ReadOnly();
            bool take = pending is not null && accept.ReadBit() && !row.Reset;
            bool hasOutput = output.ReadBit() && !row.Reset;
            Word? actual = hasOutput ? new((int)outData.ReadUInt().Value,(int)outKeep.ReadUInt().Value,
                outLast.ReadBit(),(int)outId.ReadUInt().Value,(int)outDest.ReadUInt().Value,(int)outUser.ReadUInt().Value) : null;
            if (row.Reset) { flushed += queue.Count; queue.Clear(); resets++; }
            else
            {
                if (take)
                {
                    queue.Enqueue(enabled ? input : input with { Keep=3, Last=true, Id=0, Dest=0, User=0 });
                    pending = null; accepted++;
                }
                if (held is not null && actual != held) throw new Exception($"AXIS_MISMATCH unstable output cycle={cycle}");
                Word? expected = queue.Count > 0 ? queue.Peek() : bypass ? pending : null;
                if (!enabled && expected is not null) expected = expected with { Keep=3, Last=true, Id=0, Dest=0, User=0 };
                if (hasOutput && (expected is null || actual != expected))
                    throw new Exception($"AXIS_MISMATCH payload cycle={cycle} actual={actual}");
                if (hasOutput && row.Ready) { queue.Dequeue(); consumed++; }
                if (hasOutput && !row.Ready) stalls++;
                held = hasOutput && !row.Ready ? actual : null;
            }
            trace.Add([cycle,row.Reset?1:0,take?1:0,hasOutput?1:0,row.Ready?1:0,
                actual?.Data??0,actual?.Keep??0,actual?.Last==true?1:0,actual?.Id??0,actual?.Dest??0,actual?.User??0]);
            await Timer(1); clk.WriteBit(true); await Timer(1); await ReadOnly(); await Timer(1);
        }
        if (queue.Count != 0 || pending is not null || accepted == 0 || consumed == 0 || accepted != consumed+flushed)
            throw new Exception("AXIS_DRAIN incomplete or empty stream");
        File.WriteAllText(Environment.GetEnvironmentVariable("AXIS_REPORT")!,JsonSerializer.Serialize(new
        { trace, accepted, consumed, flushed, stalls, resets, cycles=rows.Length }));
    }
}
