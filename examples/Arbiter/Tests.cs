using SharpTestBench;
using SharpTestBench.Fixtures;
using System.Runtime.InteropServices;
using System.Text.Json;
using static SharpTestBench.Triggers;

public static class Entry
{
    [UnmanagedCallersOnly] public static int Initialize(nint p, int n) => Regression.Initialize(typeof(Entry).Assembly);

    [SimTest(TimeoutSteps = 10000000)]
    public static async Task ArbiterScoreboard(TestContext context)
    {
        var config = JsonSerializer.Deserialize<ArbiterConfig>(File.ReadAllText(Environment.GetEnvironmentVariable("ARBITER_CONFIG")!))!;
        var plan = JsonSerializer.Deserialize<ArbiterRow[]>(File.ReadAllText(Environment.GetEnvironmentVariable("ARBITER_PLAN")!))!;
        var model = new ArbiterModel(config);
        var clk = context.Dut.Child("clk").Signal<W1>(); var rst = context.Dut.Child("rst").Signal<W1>();
        var fault = context.Dut.Child("fault").Signal<W1>();
        var request = context.Dut.Child("request").Signal<W8>(); var ack = context.Dut.Child("acknowledge").Signal<W8>();
        var grant = context.Dut.Child("grant").Signal<W8>(); var valid = context.Dut.Child("grant_valid").Signal<W1>();
        var encoded = context.Dut.Child("grant_encoded").Signal<W8>();
        var trace = new List<int[]>();
        for (int index = 0; index < plan.Length; index++)
        {
            var row = plan[index];
            clk.WriteBit(false); rst.WriteBit(row.Reset); fault.WriteBit(Environment.GetEnvironmentVariable("ARBITER_FAULT") == "1");
            request.Write(new UInt<W8>(row.Request)); ack.Write(new UInt<W8>(row.Acknowledge));
            await Timer(1); clk.WriteBit(true); await Timer(1); await ReadOnly();
            var actual = new ArbiterObservation((int)grant.ReadUInt().Value, valid.ReadBit(), (int)encoded.ReadUInt().Value);
            if (actual != model.Step(row)) throw new Exception($"ARBITER_MISMATCH cycle={index} actual={actual}");
            trace.Add([index, row.Reset ? 1 : 0, row.Request, row.Acknowledge, actual.Grant, actual.Valid ? 1 : 0, actual.Encoded]);
            await Timer(1);
        }
        int transitions = trace.Zip(trace.Skip(1)).Count(pair => pair.First[4] != pair.Second[4]);
        File.WriteAllText(Environment.GetEnvironmentVariable("ARBITER_REPORT")!, JsonSerializer.Serialize(new {
            trace, cycles = trace.Count, valid = trace.Count(row => row[5] != 0), idle = trace.Count(row => row[5] == 0),
            resets = trace.Count(row => row[1] != 0), transitions }));
    }
}
