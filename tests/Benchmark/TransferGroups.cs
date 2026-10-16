using SharpTestBench;
using System.Diagnostics;
using System.Numerics;
using static SharpTestBench.Triggers;

/// <summary>Mixed-width transfers with identical scalar and grouped values and phases.</summary>
public static class TransferGroups
{
    [SimTest(TimeoutSteps = 10000000)]
    public static async Task TransferGroupBenchmark(TestContext c)
    {
        var a = c.Dut.Child("wide").Signal<W256>();
        var b = c.Dut.Child("huge").Signal<Entry.W4096>();
        var av = new LogicVector<W256>((BigInteger.One << 255) | 123);
        var bv = new LogicVector<Entry.W4096>((BigInteger.One << 4095) | 321);
        var group = new SignalTransferGroup(a, b);
        a.Write(av, WriteAction.Immediate); b.Write(bv, WriteAction.Immediate);
        await Timer(1); await ReadOnly();
        void Read(bool grouped)
        {
            if (grouped) group.Capture();
            if ((grouped ? group.Get(a) : a.Read()) != av || (grouped ? group.Get(b) : b.Read()) != bv)
                throw new Exception("Snapshot benchmark mismatch");
        }
        foreach (bool grouped in new[] { false, true })
        {
            for (int i = 0; i < 1000; i++) Read(grouped);
            long bytes = GC.GetAllocatedBytesForCurrentThread(); var sw = Stopwatch.StartNew();
            for (int i = 0; i < 50000; i++) Read(grouped);
            sw.Stop(); Report("read", grouped, 50000, sw, GC.GetAllocatedBytesForCurrentThread() - bytes, group.UsesNativeBatch);
        }
        foreach (bool grouped in new[] { false, true })
        {
            await Timer(1);
            long bytes = GC.GetAllocatedBytesForCurrentThread(); var sw = Stopwatch.StartNew();
            for (int i = 0; i < 5000; i++)
            {
                if (grouped) { group.Set(a, av); group.Set(b, bv); group.Commit(); }
                else { a.Write(av); b.Write(bv); }
                await Timer(1); await ReadOnly();
                if (a.Read() != av || b.Read() != bv) throw new Exception("Deposit benchmark mismatch");
                await Timer(1);
            }
            sw.Stop(); Report("write", grouped, 5000, sw, GC.GetAllocatedBytesForCurrentThread() - bytes, group.UsesNativeBatch);
        }
    }

    static void Report(string operation, bool grouped, int count, Stopwatch sw, long bytes, bool native)
        => Console.WriteLine("GROUP_MEASURE " + System.Text.Json.JsonSerializer.Serialize(new
            { operation, grouped, native, count, seconds = sw.Elapsed.TotalSeconds, managedBytes = bytes }));
}
