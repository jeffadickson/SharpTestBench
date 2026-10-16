using SharpTestBench;
using static SharpTestBench.Triggers;

/// <summary>Group API semantics shared by native and older-host scalar transport.</summary>
public static class TransferGroupTests
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}");
    }

    [SimTest]
    public static async Task TransferGroupChunkBoundary(TestContext c)
    {
        var slots = Enumerable.Range(0, 70).Select(i => c.Dut.Child("group_slots").At(i).Signal<W8>()).ToArray();
        var group = new SignalTransferGroup(slots);
        for (int i = 0; i < slots.Length; i++) group.Set(slots[i], new UInt<W8>(i));
        group.Commit();
        slots[33].Write(new UInt<W8>(200));
        group.Set(slots[65], new UInt<W8>(201)); group.Commit();
        await Timer(1); await ReadOnly(); group.Capture();
        for (int i = 0; i < slots.Length; i++)
            Check(group.Get(slots[i]).ToUInt().Value == (i == 33 ? 200 : i == 65 ? 201 : i), "Chunk/order mismatch");
    }

    [SimTest]
    public static async Task TypedTransferGroups(TestContext c)
    {
        var a = c.Dut.Child("wide").Signal<W256>();
        var b = c.Dut.Child("perf_b").Signal<W8>();
        var other = c.Dut.Child("data").Signal<W8>();
        var group = new SignalTransferGroup(a, b);
        Reject<ArgumentException>(() => new SignalTransferGroup(a, a));
        Reject<ArgumentException>(() => group.Get(other));
        Reject<InvalidOperationException>(() => group.Get(a));
        Reject<InvalidOperationException>(group.Capture);
        a.Write(new UInt<W256>(0), WriteAction.Immediate);
        b.Write(new UInt<W8>(0), WriteAction.Immediate);
        a.Write(new UInt<W256>(9));
        group.Set(a, new UInt<W256>(123)); group.Set(b, new UInt<W8>(17)); group.Commit();
        group.Set(a, new UInt<W256>(999)); group.ClearStaged();
        b.Write(new UInt<W8>(42));
        await Timer(1); await ReadOnly(); group.Capture();
        Check(group.Get(a).ToUInt().Value == 123 && group.Get(b).ToUInt().Value == 42, "Frozen payload or scalar override");
        group.Set(b, new UInt<W8>(7));
        Reject<InvalidOperationException>(group.Commit);
        await Timer(1); group.Commit();
        await Timer(1); await ReadOnly();
        Check(group.Get(b).ToUInt().Value == 42, "Snapshot changed without Capture");
        group.Capture(); Check(group.Get(b).ToUInt().Value == 7, "Partial staging");
        var empty = new SignalTransferGroup(); empty.Capture();
        await Timer(1); empty.Commit();
        var constant = c.Dut.Child("PARAMETER").Signal<W8>();
        var invalid = new SignalTransferGroup(b, constant);
        invalid.Set(b, new UInt<W8>(99)); invalid.Set(constant, new UInt<W8>(1));
        Reject<InvalidOperationException>(invalid.Commit);
        await Timer(1); Check(b.ReadUInt().Value == 7, "Constant rejection partially queued writes");
        var unknown = LogicVector<W8>.Parse("01XZ01XZ");
        if (c.Runtime.SupportsFourState)
        {
            group.Set(b, unknown); group.Commit();
            await Timer(1); await ReadOnly(); group.Capture();
            Check(group.Get(b) == unknown, "Grouped X/Z round trip");
        }
        else Reject<NotSupportedException>(() => group.Set(b, unknown));
    }
}
