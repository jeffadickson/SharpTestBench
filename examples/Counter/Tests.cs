using SharpTestBench;
using System.Runtime.InteropServices;

public static class Entry
{
    [UnmanagedCallersOnly]
    public static int Initialize(nint p, int n) => Regression.Initialize(typeof(Entry).Assembly);

    [SimTest]
    public static async Task ResetAndCount(TestContext c)
    {
        var clock = c.Dut.Child("clk").Signal<W1>();
        var reset = c.Dut.Child("reset").Signal<W1>();
        var enable = c.Dut.Child("enable").Signal<W1>();
        var count = c.Dut.Child("count").Signal<W8>();
        clock.Write(new UInt<W1>(0));
        reset.Write(new UInt<W1>(1));
        enable.Write(new UInt<W1>(1));
        for (int expected = 0; expected <= 4; expected++)
        {
            await Triggers.Timer(1);
            clock.Write(new UInt<W1>(1));
            await Triggers.ReadOnly();
            if (count.ReadUInt().Value != expected)
                throw new Exception($"Expected {expected}, got {count.ReadUInt().Value}");
            c.Log($"Counter = {expected}");
            await Triggers.Timer(1);
            clock.Write(new UInt<W1>(0));
            reset.Write(new UInt<W1>(0));
        }
    }
}
