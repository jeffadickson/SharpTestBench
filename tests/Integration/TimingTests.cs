using SharpTestBench;
using static SharpTestBench.Triggers;

public static class TimingTests
{
    static string Trace => Environment.GetEnvironmentVariable("CS_TRACE") ?? "trace.txt";
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    [SimTest]
    public static async Task ScheduledWriteOrdering(TestContext c)
    {
        var data = c.Dut.Child("data").Signal<W8>();
        var trace = new List<string>();
        data.Write(new UInt<W8>(10));
        await ReadOnly();
        await Timer(1);
        data.Write(new UInt<W8>(20));
        data.Write(new UInt<W8>(30), WriteAction.Immediate);
        trace.Add($"immediate:{data.ReadUInt().Value}");
        await ReadOnly();
        trace.Add($"deferred:{data.ReadUInt().Value}");
        Check(data.ReadUInt().Value == 20, "pending deposit follows immediate write at ReadWrite");
        await Timer(1);
        await ReadWrite();
        data.Write(new UInt<W8>(40));
        data.Write(new UInt<W8>(50));
        await ReadOnly();
        trace.Add($"readwrite:{data.ReadUInt().Value}");
        Check(data.ReadUInt().Value == 50, "last ReadWrite deposit wins after settling");
        await Timer(1);
        ulong start = c.Runtime.Now;
        await First(Timer(2), Timer(2));
        trace.Add($"first:{c.Runtime.Now - start}");
        Check(c.Runtime.Now == start + 2, "same-time First tie consumes one timer interval");
        start = c.Runtime.Now;
        await Combine(Timer(2), Timer(2));
        trace.Add($"combine:{c.Runtime.Now - start}");
        Check(c.Runtime.Now == start + 2 && c.Runtime.PendingCallbacks == 1, "same-time Combine clears both registrations");
        File.WriteAllLines(Trace + ".scheduled", trace);
    }

    [SimTest(Simulators = new[] { "Icarus" })]
    public static async Task ForceDepositOrdering(TestContext c)
    {
        var data = c.Dut.Child("data").Signal<W8>();
        var trace = new List<string>();
        data.Write(new UInt<W8>(10));
        await Timer(1);
        data.Write(new UInt<W8>(20));
        data.Force(LogicVector<W8>.From(new UInt<W8>(77)));
        await ReadOnly();
        trace.Add($"force-before-flush:{data.ReadUInt().Value}");
        Check(data.ReadUInt().Value == 77, "force blocks queued deposit");
        await Timer(1);
        data.Write(new UInt<W8>(30));
        data.Release();
        await ReadOnly();
        trace.Add($"release-before-flush:{data.ReadUInt().Value}");
        Check(data.ReadUInt().Value == 30, "release permits pending deposit");
        await Timer(1);
        await ReadWrite();
        data.Write(new UInt<W8>(40));
        data.Force(LogicVector<W8>.From(new UInt<W8>(88)));
        data.Write(new UInt<W8>(50));
        await ReadOnly();
        trace.Add($"forced-readwrite:{data.ReadUInt().Value}");
        Check(data.ReadUInt().Value == 88, "force holds against ReadWrite deposit");
        await Timer(1);
        data.Release();
        data.Write(new UInt<W8>(60));
        await ReadOnly();
        trace.Add($"released:{data.ReadUInt().Value}");
        Check(data.ReadUInt().Value == 60, "release restores subsequent deposit");
        File.WriteAllLines(Trace + ".force", trace);
    }
}
