using SharpTestBench;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static SharpTestBench.Triggers;

public static class Entry
{
    [UnmanagedCallersOnly]
    public static int Initialize(nint p, int n) => Regression.Initialize(typeof(Entry).Assembly);
    public readonly struct W4096 : IWidth
    {
        public static int Bits => 4096;
    }

    [SimTest]
    public static Task PackedVectorBenchmark(TestContext c)
    {
        MeasurePacked<W256>(c.Dut.Child("wide").Signal<W256>());
        MeasurePacked<W4096>(c.Dut.Child("huge").Signal<W4096>());
        return Task.CompletedTask;
    }

    static void MeasurePacked<W>(Signal<W> signal)
        where W : struct, IWidth
    {
        // Longer native A/B samples can be requested without changing normal CI cost.
        int count = int.TryParse(Environment.GetEnvironmentVariable("CS_PACKED_COUNT"), out int requested)
            && requested > 0 ? requested : 500;
        var value = new LogicVector<W>((System.Numerics.BigInteger.One << (W.Bits - 1)) | 123);
        void RoundTrip(bool packed)
        {
            if (packed)
                signal.Write(value, WriteAction.Immediate);
            else
                signal.WriteBits(value.ToString(), WriteAction.Immediate);
            var actual = packed ? signal.Read() : LogicVector<W>.Parse(signal.ReadBits());
            if (actual != value)
                throw new Exception("Packed benchmark mismatch");
        }

        for (int i = 0; i < 100; i++)
        {
            RoundTrip(false);
            RoundTrip(true);
        }

        bool reverse = Environment.GetEnvironmentVariable("CS_PACKED_REVERSE") == "1";
        foreach (bool packed in reverse ? new[]
        {
            true,
            false
        }

        : new[]
        {
            false,
            true
        }

        )
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
                RoundTrip(packed);
            sw.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Console.WriteLine("PACKED_MEASURE " + System.Text.Json.JsonSerializer.Serialize(new { width = W.Bits, mode = packed ? "packed" : "string", count, seconds = sw.Elapsed.TotalSeconds, managedBytes = allocated }));
        }
    }

    /// <summary>Measure a clock both with only an HDL counter and with a managed edge observer.</summary>
    [SimTest(TimeoutSteps = 10000000)]
    public static async Task ClockBenchmark(TestContext c)
    {
        if (Environment.GetEnvironmentVariable("CS_BENCH_CLOCKS") == "0") return;
        const int cycles = 10000;
        var signal = c.Dut.Child("perf_clock").Signal<W1>();
        var counter = c.Dut.Child("perf_clock_count").Signal<W32>();
        foreach (string mode in new[] { "idle", "observed" })
        {
            signal.WriteBit(false, WriteAction.Immediate); await Timer(1);
            counter.Write(new UInt<W32>(0), WriteAction.Immediate);
            ulong start = c.Runtime.Now;
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            var job = c.StartSoon(new SimClock(signal, 4, 2).Run);
            if (mode == "idle") await Timer((ulong)(cycles * 4 + 1));
            else { await ClockCycles(signal, cycles); await Timer(1); }
            job.Cancel();
            try { await job; } catch (OperationCanceledException) { }
            sw.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
            ulong expected = (ulong)(mode == "idle" ? cycles * 4 + 1 : cycles * 4 - 1);
            if (counter.ReadUInt().Value != cycles || c.Runtime.Now - start != expected)
                throw new Exception("Clock benchmark edge-count/timing mismatch");
            Console.WriteLine("CLOCK_MEASURE " + System.Text.Json.JsonSerializer.Serialize(new
                { mode, cycles, seconds = sw.Elapsed.TotalSeconds, managedBytes = allocated }));
        }
    }

    [SimTest(TimeoutSteps = 1000000)]
    public static async Task Benchmark(TestContext c)
    {
        const int n = 100000;
        var data = c.Dut.Child("bench32").Unsigned<W32>();
        for (int i = 0; i < 10000; i++)
            await Timer(1);
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < n; i++)
            await Timer(1);
        sw.Stop();
        Console.WriteLine($"MEASURE timer {sw.Elapsed.TotalSeconds:R}");
        uint x = 1;
        sw.Restart();
        for (int i = 0; i < n; i++)
        {
            x = unchecked(x * 1664525u + 1013904223u);
            data.Write(new UInt<W32>(x));
            await Timer(1);
            if (data.Read().Value != x)
                throw new Exception("Round-trip mismatch");
        }

        sw.Stop();
        Console.WriteLine($"MEASURE rw32 {sw.Elapsed.TotalSeconds:R} checksum={x}");
    }
}
