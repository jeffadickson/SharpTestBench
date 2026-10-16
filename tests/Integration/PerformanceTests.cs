using SharpTestBench;
using static SharpTestBench.Triggers;

/// <summary>Semantic regressions for optimizations, independent of performance thresholds.</summary>
public static class PerformanceTests
{
    /// <summary>Fail with an explanation that survives simulator log capture.</summary>
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    [SimTest]
    public static async Task OrderedBatchesAndSnapshots(TestContext c)
    {
        var a = c.Dut.Child("perf_a").Signal<W32>();
        var b = c.Dut.Child("perf_b").Signal<W8>();
        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("STB_DIRECT_PACKED") != "0")
            Check(a.UsesDirectPackedTransport && b.UsesDirectPackedTransport, "Known Linux VPI handles must use direct packed access");
        var snapshot = new SignalReadBatch(a, b, a);
        var values = new uint[3];
        a.Write(new UInt<W32>(0), WriteAction.Immediate); b.Write(new UInt<W8>(0), WriteAction.Immediate);
        a.Write(new UInt<W32>(123)); b.Write(new UInt<W8>(17)); a.Write(new UInt<W32>(0xFEDCBA98));
        snapshot.Read(values);
        Check(values.SequenceEqual(new uint[] { 0, 0, 0 }), "Deferred batch became visible before ReadWrite");
        await Timer(1); await ReadOnly();
        snapshot.Read(values);
        Check(values.SequenceEqual(new uint[] { 0xFEDCBA98, 17, 0xFEDCBA98 }), "Batch order/coalescing/unsigned transport");
        try { a.Write(new UInt<W32>(1)); throw new Exception("ReadOnly batch write accepted"); }
        catch (InvalidOperationException) { }
        await Timer(1);
        a.Write(new UInt<W32>(8));
        // A string write is an ordering barrier; the final numeric deposit still wins.
        b.WriteBits("00100100"); a.Write(new UInt<W32>(9));
        await Timer(1); snapshot.Read(values);
        Check(values[0] == 9 && values[1] == 36, "Mixed write barrier");
        if (c.Runtime.SupportsFourState)
        {
            b.Write(LogicVector<W8>.Parse("01XZ01XZ"), WriteAction.Immediate);
            Check(b.Read().ToString() == "01XZ01XZ", "Direct aval/bval X/Z mapping");
            try { snapshot.Read(values); throw new Exception("Unresolved batch read accepted"); }
            catch (FormatException) { }
        }
        var empty = new SignalReadBatch(); empty.Read(Span<uint>.Empty);
        try { snapshot.Read(new uint[2]); throw new Exception("Short snapshot accepted"); }
        catch (ArgumentException) { }
    }

    /// <summary>Capture exact pulse times including an in-flight period change and cancellation.</summary>
    static async Task<ulong[]> ClockTrace(TestContext c, ClockImplementation implementation)
    {
        var signal = c.Dut.Child("perf_clock").Signal<W1>();
        signal.WriteBit(false, WriteAction.Immediate); await Timer(1);
        ulong start = c.Runtime.Now;
        var clock = new SimClock(signal, 9, 4, implementation);
        var job = c.StartSoon(clock.Run);
        var trace = new List<ulong>();
        await RisingEdge(signal); trace.Add(c.Runtime.Now - start);
        clock.SetPeriod(7, 2);
        await FallingEdge(signal); trace.Add(c.Runtime.Now - start);
        await RisingEdge(signal); trace.Add(c.Runtime.Now - start);
        await FallingEdge(signal); trace.Add(c.Runtime.Now - start);
        job.Cancel();
        try { await job; } catch (OperationCanceledException) { }
        await Timer(30);
        Check(!signal.ReadBit(), "Cancelled clock issued an extra edge");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await clock.Run(cancelled.Token); throw new Exception("Precancelled clock started"); }
        catch (OperationCanceledException) { }
        return trace.ToArray();
    }

    [SimTest]
    public static async Task NativeClockPulseEquivalence(TestContext c)
    {
        var managed = await ClockTrace(c, ClockImplementation.Managed);
        var native = await ClockTrace(c, ClockImplementation.Native);
        Check(managed.SequenceEqual(new ulong[] { 5, 9, 14, 16 }), "Managed pulse oracle");
        Check(native.SequenceEqual(managed), "Native pulse/update/cancellation differs from managed");
        // Cancel while still in the initial low pulse, before any edge can fire.
        var signal = c.Dut.Child("perf_clock").Signal<W1>();
        var job = c.StartSoon(new SimClock(signal, 100, implementation: ClockImplementation.Native).Run);
        await Timer(1); job.Cancel();
        try { await job; } catch (OperationCanceledException) { }
        await Timer(101); Check(!signal.ReadBit(), "Early cancellation left native timer armed");
    }
}
