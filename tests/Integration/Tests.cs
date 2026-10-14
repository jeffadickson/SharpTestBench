using SharpTestBench;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using static SharpTestBench.Triggers;

public static class Entry
{
    [UnmanagedCallersOnly]
    public static int Initialize(nint p, int n) => Regression.Initialize(typeof(Entry).Assembly);
}

public static class Tests
{
    [SimTest]
    public static async Task AP_MultipleFailureTeardownStress(TestContext c)
    {
        for (int round = 0; round < 256; round++)
        {
            int cleaned = 0;
            AggregateException? failure = null;
            try
            {
                await c.RunGroup(async group =>
                {
                    var jobs = Enumerable.Range(0, 4).Select(i =>
                    {
                        ulong delay = (ulong)c.Random.Next(3) + 1;
                        return group.StartSoon(async token =>
                        {
                            try
                            {
                                await Timer(delay);
                                throw new FormatException($"failure round={round} child={i}");
                            }
                            finally
                            {
                                cleaned++;
                                if (token.IsCancellationRequested)
                                    throw new ApplicationException($"cleanup round={round} child={i}");
                            }
                        });
                    }).ToArray();
                    await Task.WhenAll(jobs.Select(j => j.Task));
                });
            }
            catch (AggregateException e) { failure = e; }
            Check(cleaned == 4 && failure?.Flatten().InnerExceptions.Count == 4,
                $"aggregate retains each failure once seed={c.Seed} round={round}");
            await Timer(1);
            Check(c.Runtime.PendingCallbacks == 1, $"native teardown seed={c.Seed} round={round}");
        }
    }

    [SimTest]
    public static async Task AO_PrimitiveFairnessStress(TestContext c)
    {
        const int contenders = 24;
        for (int round = 0; round < 64; round++)
        {
            // The regression prints the seed; each assertion retains round and phase.
            var cancelled = Enumerable.Range(0, contenders).Select(_ => c.Random.Next(4) == 0).ToArray();
            var expected = Enumerable.Range(0, contenders).Where(i => !cancelled[i]).ToArray();
            var order = new List<int>();
            var semaphore = new SimSemaphore(0, 1);
            var jobs = Enumerable.Range(0, contenders).Select(i => c.StartSoon(async token =>
            {
                await semaphore.Acquire();
                order.Add(i);
                semaphore.Release();
            })).ToArray();
            await Yield();
            for (int i = 0; i < contenders; i++) if (cancelled[i]) jobs[i].Cancel();
            await Yield();
            semaphore.Release();
            await Combine(jobs.Select(j => j.Complete).ToArray());
            Check(order.SequenceEqual(expected) && semaphore.CurrentCount == 1, $"semaphore FIFO/cancellation round={round}");

            order.Clear();
            var mutex = new SimLock();
            var held = await mutex.Acquire();
            jobs = Enumerable.Range(0, contenders).Select(i => c.StartSoon(async token =>
            {
                using (await mutex.Acquire())
                {
                    order.Add(i);
                    await Yield(); // Hold across a continuation under contention.
                }
            })).ToArray();
            await Yield();
            for (int i = 0; i < contenders; i++) if (cancelled[i]) jobs[i].Cancel();
            await Yield();
            held.Dispose(); held.Dispose();
            await Combine(jobs.Select(j => j.Complete).ToArray());
            Check(order.SequenceEqual(expected), $"lock FIFO/cancellation round={round}");
            using (await mutex.Acquire()) { }

            var queue = new SimQueue<int>(1);
            await queue.Put(-1);
            jobs = Enumerable.Range(0, contenders).Select(i => c.StartSoon(async token => await queue.Put(i))).ToArray();
            await Yield();
            for (int i = 0; i < contenders; i++) if (cancelled[i]) jobs[i].Cancel();
            await Yield();
            Check(await queue.Get() == -1, "queue retains existing item");
            foreach (var i in expected)
                Check(await queue.Get() == i, $"queue blocked-producer FIFO round={round}");
            await Combine(jobs.Select(j => j.Complete).ToArray());
            Check(queue.Count == 0, "cancelled producers leave no phantom values");

            order.Clear();
            jobs = Enumerable.Range(0, contenders).Select(i => c.StartSoon(async token =>
            {
                int value = await queue.Get();
                Check(value == i, $"queue consumer assigned value round={round} index={i}");
                order.Add(i);
            })).ToArray();
            await Yield();
            for (int i = 0; i < contenders; i++) if (cancelled[i]) jobs[i].Cancel();
            await Yield();
            foreach (var i in expected) await queue.Put(i);
            await Combine(jobs.Select(j => j.Complete).ToArray());
            Check(order.SequenceEqual(expected) && queue.Count == 0, $"queue blocked-consumer FIFO round={round}");
            await Timer(1);
            Check(c.Runtime.PendingCallbacks == 1, $"primitive cleanup round={round}");
        }
    }

    [SimTest]
    public static async Task AN_TaskLocalsAndIntrospection(TestContext c)
    {
        var local = new SimTaskLocal<string?>();
        Check(SimJob.Current == null, "test body is not a job");
        Throws<InvalidOperationException>(() => local.Value = "outside");
        SimJob? parent = null;
        parent = c.StartSoon(async token =>
        {
            Check(ReferenceEquals(SimJob.Current, parent), "current job identity");
            Check(!local.TryGetValue(out _), "new job has empty locals");
            local.Value = "parent";
            var nested = c.StartSoon(async token =>
            {
                Check(!local.TryGetValue(out _), "nested job storage starts empty");
                local.Value = null;
                await Timer(2);
                Check(local.TryGetValue(out var value) && value == null, "null value survives await");
                Check(local.Clear() && !local.Clear(), "clear removes only current job value");
            });
            await nested;
            Check(local.Value == "parent" && ReferenceEquals(SimJob.Current, parent), "parent state survives nested job");
            Exception? error = null;
            var thread = new Thread(() => { try { local.Value = "illegal"; } catch (Exception e) { error = e; } });
            thread.Start(); thread.Join();
            Check(error is InvalidOperationException && local.Value == "parent", "locals reject foreign thread access");
        }, "local-parent");
        var sibling = c.StartSoon(async token =>
        {
            Check(!local.TryGetValue(out _), "sibling starts empty");
            local.Value = "sibling";
            await Timer(1);
            Check(local.Value == "sibling", "sibling state is isolated");
        });
        await SimTasks.Combine(parent.Task, sibling.Task);
        Check(SimJob.Current == null, "current job does not leak into test body");
        bool stackObserved = false;
        try
        {
            await c.RunGroup(async group =>
            {
                var failed = group.StartSoon(async token => { await Timer(1); throw new FormatException("introspection fixture"); });
                await failed.Complete.WithCancellation(CancellationToken.None);
                stackObserved = failed.Status == TaskStatus.Faulted &&
                    failed.Exception?.InnerException?.StackTrace?.Contains(nameof(AN_TaskLocalsAndIntrospection)) == true;
            });
        }
        catch (FormatException) { }
        Check(stackObserved, "fault introspection retains managed source stack");
    }

    [SimTest]
    public static async Task AL_CompletionTriggers(TestContext c)
    {
        var job = c.StartSoon(async _ => { await Timer(5); return 42; });
        var completion = job.Complete;
        await First(Timer(1), completion);
        Check(!job.IsCompleted, "losing completion subscription does not cancel job");
        using (var cancel = new CancellationTokenSource())
        {
            var wait = completion.AsTask(cancel.Token);
            cancel.Cancel();
            try { await wait; throw new Exception("wait did not cancel"); }
            catch (OperationCanceledException) { }
        }
        Check(!job.IsCompleted, "cancelled observer leaves job running");
        var a = completion.AsTask();
        var b = completion.AsTask();
        await Combine(completion, Timer(2));
        await Task.WhenAll(a, b);
        Check(job.Result == 42, "multiple observers and mixed combine");
        await First(completion, Timer(10000));
        await Combine(completion, completion);
        Check(c.Runtime.PendingCallbacks == 1, "completed job removes losing native timers");

        var cancelled = c.StartSoon(async _ => await Timer(10000));
        cancelled.Cancel();
        await cancelled.Complete;
        Check(cancelled.Task.IsCanceled, "completion observes cancellation without throwing");
        bool observedFailure = false;
        try
        {
            await c.RunGroup(async group =>
            {
                var failed = group.StartSoon(async _ => { await Timer(1); throw new FormatException("completion fixture"); });
                // Group failure cancels the body's default token; opt out only for this bounded observation.
                await failed.Complete.WithCancellation(CancellationToken.None);
                observedFailure = failed.Task.IsFaulted;
                Throws<FormatException>(() => failed.Task.GetAwaiter().GetResult());
            });
        }
        catch (FormatException) { }
        Check(observedFailure, "completion preserves failure and group supervision");
        for (int i = 0; i < 128; i++)
        {
            var late = c.StartSoon(async _ => await Timer(2));
            try { await WithTimeout(late.Complete, 1); throw new Exception("missing timeout"); }
            catch (TimeoutException) { }
            Check(!late.IsCompleted, "timeout does not own job");
            await late.Complete;
            Check(c.Runtime.PendingCallbacks == 1, "mixed timeout cleanup remains bounded");
        }
    }

    [SimTest]
    public static async Task AM_CancellationContract(TestContext c)
    {
        int cleaned = 0;
        var job = c.StartSoon(async _ =>
        {
            try { await Timer(10000); }
            finally { cleaned++; await Timer(2).WithCancellation(CancellationToken.None); }
        });
        await Yield();
        job.Cancel(); job.Cancel();
        await job.Complete;
        Check(job.Task.IsCanceled && cleaned == 1, "repeated cancellation joins cleanup exactly once");
        job.Cancel();
        var success = c.StartSoon(_ => Task.FromResult(7));
        Check(await success == 7, "successful job result");
        success.Cancel();
        Check(success.Task.IsCompletedSuccessfully && success.Result == 7, "late cancel cannot rewrite completed result");
        await Timer(1);
        Check(c.Runtime.PendingCallbacks == 1, "cancellation cleanup unregisters native waits");
    }

    [SimTest]
    public static async Task AK_TaskNames(TestContext c)
    {
        var job = c.StartSoon(async _ => { await Timer(2); return 42; }, "initial");
        job.SetName("renamed");
        Check(job.Name == "renamed" && !job.IsCompleted, "rename active typed task");
        Throws<ArgumentException>(() => job.SetName(" "));
        Check(job.Name == "renamed", "invalid rename leaves name unchanged");
        Check(await job == 42, "rename preserves result");
        job.SetName("completed");
        Check(job.Name == "completed", "rename completed task");
        var plain = c.StartSoon(async _ => { await Timer(1); }, "plain");
        plain.SetName("plain-renamed");
        await plain;
        Check(plain.Name == "plain-renamed", "rename untyped task");
        Exception? error = null;
        var thread = new Thread(() => { try { plain.SetName("wrong-thread"); } catch (Exception e) { error = e; } });
        thread.Start(); thread.Join();
        Check(error is InvalidOperationException && plain.Name == "plain-renamed", "rename enforces simulator thread");
    }

    [SimTest]
    public static async Task AI_NamedStruct(TestContext c)
    {
        var signal = c.Dut.Child("packed_packet").Signal<W16>();
        var layout = new PackedStructLayout(new PackedMember("tag", 4), new PackedMember("payload", 8), new PackedMember("flags", 4));
        var packet = signal.Struct(layout);
        signal.Write(new UInt<W16>(0));
        packet.Field<W4>("tag").Write(new UInt<W4>(0xA));
        packet.Field<W8>("payload").Write(new UInt<W8>(0xB7));
        packet.Field<W4>("flags").Write(new UInt<W4>(3));
        Throws<ArgumentException>(() => packet.Field<W4>("payload"));
        Throws<KeyNotFoundException>(() => packet.Field<W8>("missing"));
        Throws<ArgumentException>(() => signal.Struct(new PackedStructLayout(new PackedMember("bad", 8))));
        await ReadOnly();
        Check(signal.ReadUInt().Value == 0xAB73 && c.Dut.Child("packet_payload").Signal<W8>().ReadUInt().Value == 0xB7, "named fields match HDL packed struct");
    }

    [SimTest]
    public static async Task AJ_AdjustableClock(TestContext c)
    {
        var signal = c.Dut.Child("adjustable_clk").Signal<W1>();
        signal.Write(new UInt<W1>(0));
        await Timer(1);
        var clock = new SimClock(signal, 10);
        var job = c.StartSoon(clock.Run);
        ulong start = c.Runtime.Now;
        await RisingEdge(signal);
        Check(c.Runtime.Now == start + 5, "initial clock rise");
        clock.SetPeriod(6, 2);
        Throws<ArgumentOutOfRangeException>(() => clock.SetPeriod(1));
        Throws<ArgumentOutOfRangeException>(() => clock.SetPeriod(6, 6));
        await FallingEdge(signal);
        Check(c.Runtime.Now == start + 10, "current high pulse unchanged");
        await RisingEdge(signal);
        Check(c.Runtime.Now == start + 14, "new low duration");
        await FallingEdge(signal);
        Check(c.Runtime.Now == start + 16, "new high duration");
        job.Cancel();
    }

    [DllImport("libsharptestbench_host.so", CallingConvention = CallingConvention.Cdecl)]
    static extern int cs_v1_vpi_packed_ranges(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, uint bits, [Out] int[] pairs, uint capacity, out uint count);

    [SimTest]
    public static async Task AH_Discovery(TestContext c)
    {
        var roots = HdlObject.Roots();
        Check(roots.Status == DiscoveryStatus.Observed && roots.Objects.Any(o => o.Name == "top"), "discover actual top module");
        var packages = HdlObject.Packages();
        if (c.Runtime.Simulator.Contains("Verilator"))
            Check(packages.Objects.Any(o => o.Name.Contains("discovery_pkg")), "discover fixture package");
        foreach (var package in packages.Objects)
        {
            Check(package.Kind == ObjectKind.Package, "package kind");
            if (package.Name.Contains("discovery_pkg"))
            {
                var marker = package.Child("constant_marker");
                Check((marker.Kind == ObjectKind.Integer ? marker.Integer().Read() : (long)marker.Signal<W32>().ReadUInt().Value) == 37, "package member access");
                if (c.Runtime.Simulator.Contains("Verilator"))
                    Check(package.Child("marker").Signal<W32>().ReadUInt().Value == 37, "Verilator variable package member");
                else
                    Console.WriteLine("PACKAGE variable member access remains unverified on this backend; constant member verified.");
            }
        }
        var drivers = c.Dut.Child("doubled").Drivers();
        var loads = c.Dut.Child("data").Loads();
        foreach (var discovery in new[] { roots, packages, drivers, loads })
            Console.WriteLine($"DISCOVERY {discovery.Status}: {string.Join(",", discovery.Objects.Select(o => o.FullName))}; {discovery.Explanation}");
        var down = c.Dut.Child("data").Signal<W8>();
        var up = c.Dut.Child("ascending").Signal<W8>();
        var matrix = c.Dut.Child("packed_matrix").Signal<W48>();
        var inferred = matrix.DiscoverPackedLayout();
        Console.WriteLine($"LAYOUT {inferred.Source}: {string.Join(",", inferred.Layout!.Dimensions)}");
        if (c.Runtime.Simulator.Contains("Verilator"))
        {
            Check(inferred.Source == PackedLayoutSource.NativePackedRanges && inferred.Layout.Dimensions.Count == 3, "native packed rank");
            matrix.Write(new UInt<W48>(0));
            matrix.PackedInferred<W8>()[1, 1].Write(new UInt<W8>(0xA5));
            var mixed = c.Dut.Child("packed_mixed").Signal<W48>();
            mixed.Write(new UInt<W48>(0));
            mixed.PackedInferred<W8>()[-1, 3].Write(new UInt<W8>(0x3C));
            Throws<ArgumentException>(() => matrix.PackedInferred<W4>());
            int[] pairs = new int[128];
            Check(cs_v1_vpi_packed_ranges(matrix.FullName, 48, pairs, 1, out uint count) == 4 && count == 3, "native metadata capacity failure drains iterator");
            Check(cs_v1_vpi_packed_ranges(matrix.FullName, 47, pairs, 64, out _) == 3, "native metadata rejects width mismatch");
            Check(cs_v1_vpi_packed_ranges(matrix.FullName, 48, pairs, 0, out _) == 2, "native metadata rejects zero capacity");
            Check(cs_v1_vpi_packed_ranges("top.no_such_signal", 48, pairs, 64, out _) == 1, "native metadata missing handle");
            for (int i = 0; i < 100; i++)
                Check(matrix.DiscoverPackedLayout().Layout!.Dimensions.Count == 3, "repeated native iterator lifetime");
        }
        else Throws<NotSupportedException>(() => matrix.PackedInferred<W8>());
        Check(down.DiscoverPackedLayout().Layout!.BitOffset(7) == 7, "inferred descending layout");
        Check(up.DiscoverPackedLayout().Layout!.BitOffset(0) == 7, "inferred ascending layout");
        down.Write(new UInt<W8>(0));
        up.Write(new UInt<W8>(0));
        down.PackedBits()[7].Write(new UInt<W1>(1));
        up.PackedBits()[0].Write(new UInt<W1>(1));
        Check(!c.Dut.DiscoverPackedLayout().Available, "module is not packed");
        await ReadOnly();
        Check(down.ReadUInt().Value == 128 && up.ReadUInt().Value == 128, "inferred bit writes use HDL indices");
        if (c.Runtime.Simulator.Contains("Verilator"))
            Check(c.Dut.Child("matrix_middle").Signal<W8>().ReadUInt().Value == 0xA5 && c.Dut.Child("mixed_first").Signal<W8>().ReadUInt().Value == 0x3C, "native inferred dimensions match independent HDL selection");
    }

    static void Check(bool value, string why)
    {
        if (!value)
            throw new Exception(why);
    }

    static void Throws<T>(Action action)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }

        throw new Exception("Expected " + typeof(T).Name);
    }

    public readonly struct W73 : IWidth
    {
        public static int Bits => 73;
    }

    public readonly struct W4096 : IWidth
    {
        public static int Bits => 4096;
    }

    public readonly struct W48 : IWidth
    {
        public static int Bits => 48;
    }

    [SimTest]
    public static async Task AG_DeltaChain(TestContext c)
    {
        var source = c.Dut.Child("delta_source").Signal<W8>();
        var first = c.Dut.Child("delta1").Signal<W8>();
        var last = c.Dut.Child("delta3").Signal<W8>();
        source.Write(new UInt<W8>(0));
        await Timer(1);
        var trace = new List<string>();
        for (int i = 1; i <= 20; i++)
        {
            ulong now = c.Runtime.Now;
            source.Write(new UInt<W8>(i));
            source.Write(new UInt<W8>(i + 32));
            await WithTimeout(Combine(ValueChange(first), ValueChange(last)), 10);
            await ReadOnly();
            Check(c.Runtime.Now == now && last.ReadUInt().Value == ((i + 32) ^ 7), "multi-NBA settle at same timestamp");
            trace.Add($"{i}:{first.ReadUInt().Value}:{last.ReadUInt().Value}");
            await Timer(1);
        }

        File.WriteAllLines((Environment.GetEnvironmentVariable("CS_TRACE") ?? "trace.txt") + ".delta", trace);
    }

    [SimTest]
    public static async Task AF_PackedArrayAndStruct(TestContext c)
    {
        var raw = c.Dut.Child("packed_matrix").Signal<W48>();
        var matrix = raw.Packed<W8>(HdlRange.DownTo(1, 0), HdlRange.DownTo(2, 0));
        raw.Write(new UInt<W48>(0x112233445566));
        matrix[1, 1].Write(new UInt<W8>(0xA2));
        matrix[0, 0].Write(new UInt<W8>(0xB3));
        var mixedRaw = c.Dut.Child("packed_mixed").Signal<W48>();
        var mixed = mixedRaw.Packed<W8>(HdlRange.To(-1, 0), HdlRange.DownTo(3, 1));
        mixedRaw.WriteBits(new string ('0', 48));
        mixed[-1, 3].Write(new UInt<W8>(0xD4));
        mixed[0, 1].Write(new UInt<W8>(0xE5));
        var packet = c.Dut.Child("packed_packet").Signal<W16>();
        var tag = packet.Field<W4>(12);
        var payload = c.Dut.Child("packed_packet").Signal<W16>().Field<W8>(4);
        var flags = packet.Field<W4>(0);
        packet.Write(new UInt<W16>(0));
        tag.Write(new UInt<W4>(0xA));
        payload.Write(new UInt<W8>(0xBC));
        flags.Write(new UInt<W4>(0xD));
        await ReadOnly();
        Check(raw.ReadUInt().Value == 0x11A2334455B3, "packed sibling writes preserve whole deposit");
        Check(matrix[1, 2].ReadUInt().Value == 0x11 && matrix[0, 0].ReadUInt().Value == 0xB3, "packed HDL indices");
        Check(c.Dut.Child("matrix_middle").Signal<W8>().ReadUInt().Value == 0xA2 && c.Dut.Child("matrix_right").Signal<W8>().ReadUInt().Value == 0xB3, "HDL independent packed subscripts");
        Check(c.Dut.Child("mixed_first").Signal<W8>().ReadUInt().Value == 0xD4 && c.Dut.Child("mixed_last").Signal<W8>().ReadUInt().Value == 0xE5, "HDL mixed direction negative indices");
        Check(c.Dut.Child("packet_payload").Signal<W8>().ReadUInt().Value == 0xBC, "HDL struct member");
        Check(packet.ReadUInt().Value == 0xABCD && payload.ReadUInt().Value == 0xBC, "packed struct fields");
        Throws<InvalidOperationException>(() => flags.Write(new UInt<W4>(0)));
        Throws<IndexOutOfRangeException>(() => _ = matrix[2, 0]);
        Throws<ArgumentException>(() => _ = raw.Packed<W8>(HdlRange.To(0, 2)));
        await Timer(1);
        flags.Write(new UInt<W4>(1));
        packet.Write(new UInt<W16>(0x1234));
        await ReadOnly();
        Check(packet.ReadUInt().Value == 0x1234, "whole write replaces earlier field updates");
        await Timer(1);
        string prefix = c.Runtime.SupportsFourState ? "XXZZ" : "1100";
        packet.WriteBits(prefix + "000000001010");
        payload.Write(new UInt<W8>(0xBC));
        await ReadOnly();
        Check(packet.ReadBits() == prefix + "101111001010", "field update preserves untouched unknowns");
    }

    [SimTest]
    public static async Task AE_PackedPlanes(TestContext c)
    {
        await CheckPacked<W73>(c, "odd_wide");
        await CheckPacked<W256>(c, "wide");
        await CheckPacked<W4096>(c, "huge");
    }

    static async Task CheckPacked<W>(TestContext c, string name)
        where W : struct, IWidth
    {
        var signal = c.Dut.Child(name).Signal<W>();
        var random = new Random(5741 + W.Bits);
        string symbols = c.Runtime.SupportsFourState ? "01XZ" : "01";
        for (int sample = 0; sample < 24; sample++)
        {
            string text = new(Enumerable.Range(0, W.Bits).Select(_ => symbols[random.Next(symbols.Length)]).ToArray());
            var expected = LogicVector<W>.Parse(text);
            if (sample % 2 == 0)
                signal.Write(expected);
            else
                signal.WriteBits(text);
            await ReadOnly();
            Check(signal.ReadBits() == text, $"{name} text comparison {sample}");
            Check(signal.Read() == expected, $"{name} packed comparison {sample}");
            await Timer(1);
        }

        // Deposits still coalesce, and caller values remain immutable until the flush.
        signal.Write(new LogicVector<W>(1));
        signal.Write(new LogicVector<W>(2));
        await ReadOnly();
        Check(signal.ReadUInt().Value == 2, "packed deferred last write wins");
        await Timer(1);
    }

    [SimTest]
    public static Task AD_ObjectMetadata(TestContext c)
    {
        var obj = c.Dut.Child("unit0").Child("local_data");
        Check(obj.FullName.Contains("unit0") && obj.FullName.Contains("local_data"), "qualified nested name");
        Check(obj.TypeName.Length > 0, "native type name");
        Console.WriteLine($"CS_METADATA {obj.FullName} type={obj.TypeName} definition={c.Dut.DefinitionName} file={c.Dut.DefinitionFile}");
        return Task.CompletedTask;
    }

    [SimTest]
    public static async Task A_TypesAndSignals(TestContext c)
    {
        var typed = c.Dut.Child("data").Unsigned<W8>();
        Throws<ArgumentException>(() => c.Dut.Child("data").Signed<W8>());
        Throws<ArgumentException>(() => c.Dut.Child("signed_data").Unsigned<W16>());
        c.Dut.Child("signed_data").Signed<W16>().Write(new SInt<W16>(-2));
        var data = typed.Raw;
        Throws<ArgumentException>(() => c.Dut.Child("data").Signal<W16>());
        data.Write(new UInt<W8>(201));
        var wide = c.Dut.Child("wide").Signal<W256>();
        wide.Write(new UInt<W256>((BigInteger.One << 255) | 123));
        c.Dut.Child("signed_data").Signal<W16>().Write(new SInt<W16>(-32768));
        var logic = c.Dut.Child("logic_data").Signal<W4>();
        if (c.Runtime.SupportsFourState)
            logic.Write(LogicVector<W4>.Parse("10XZ"));
        else
        {
            Throws<NotSupportedException>(() => logic.Write(LogicVector<W4>.Parse("10XZ")));
            logic.Write(LogicVector<W4>.Parse("1010"));
        }

        c.Dut.Child("real_data").Real().Write(3.125);
        await ReadOnly();
        Check(data.ReadUInt().Value == 201, "uint read");
        Check(wide.ReadUInt().Value == ((BigInteger.One << 255) | 123), "wide read");
        Check(c.Dut.Child("signed_data").Signal<W16>().ReadSInt().Value == -32768, "signed read");
        if (c.Runtime.SupportsFourState)
        {
            Check(logic.Read().ToString() == "10XZ", "XZ roundtrip");
            Throws<InvalidOperationException>(() => logic.ReadUInt());
        }
        else
            Check(logic.Read().ToString() == "1010", "known vector on two-state simulator");
        Check(c.Dut.Child("real_data").Real().Read() == 3.125, "real value");
        Throws<InvalidOperationException>(() => data.Write(new UInt<W8>(1)));
        Throws<InvalidOperationException>(() => data.Write(new UInt<W8>(1), WriteAction.Immediate));
    }

    [SimTest]
    public static async Task B_TimingAndNba(TestContext c)
    {
        var clk = c.Dut.Child("clk").Signal<W1>();
        var data = c.Dut.Child("data").Signal<W8>();
        var sampled = c.Dut.Child("sampled").Signal<W8>();
        clk.WriteBit(false);
        data.Write(new UInt<W8>(42));
        await Timer(2);
        var seen = new List<string>();
        var monitor = c.StartSoon(async token =>
        {
            await RisingEdge(clk).WithCancellation(token);
            seen.Add("edge:" + sampled.ReadUInt().Value);
            await ReadOnly().WithCancellation(token);
            seen.Add("settled:" + sampled.ReadUInt().Value);
        });
        await Timer(1);
        clk.WriteBit(true);
        await monitor;
        Check(seen.SequenceEqual(new[] { "edge:0", "settled:42" }), string.Join(",", seen));
        File.WriteAllLines(Environment.GetEnvironmentVariable("CS_TRACE") ?? "trace.txt", seen);
    }

    [SimTest]
    public static async Task C_CancellationAndCombinators(TestContext c)
    {
        ulong now = c.Runtime.Now;
        await First(Timer(3), Timer(30));
        Check(c.Runtime.Now == now + 3, "first");
        Check(c.Runtime.PendingCallbacks == 1, "losing callback removed; only watchdog remains");
        now = c.Runtime.Now;
        await Combine(Timer(2), Timer(5));
        Check(c.Runtime.Now == now + 5, "combine");
        bool timeout = false;
        try
        {
            await WithTimeout(Timer(100), 3);
        }
        catch (TimeoutException)
        {
            timeout = true;
        }

        Check(timeout, "timeout");
        using var source = new CancellationTokenSource();
        bool cancelled = false;
        var job = c.StartSoon(async _ =>
        {
            try
            {
                await Timer(100).WithCancellation(source.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
        });
        await Timer(1);
        source.Cancel();
        await job;
        Check(cancelled, "cancelled await");
        using var already = new CancellationTokenSource();
        already.Cancel();
        try
        {
            await Timer(1).WithCancellation(already.Token);
            throw new Exception("missing cancellation");
        }
        catch (OperationCanceledException)
        {
        }
    }

    [SimTest]
    public static async Task D_ConcurrentTasks(TestContext c)
    {
        var order = new List<int>();
        var jobs = Enumerable.Range(1, 100).Select(i => c.StartSoon(async token =>
        {
            await Timer((ulong)i).WithCancellation(token);
            order.Add(i);
        })).ToArray();
        await Task.WhenAll(jobs.Select(j => j.Task));
        Check(order.SequenceEqual(Enumerable.Range(1, 100)), "task order");
    }

    [SimTest]
    public static async Task E_EventsAndQueues(TestContext c)
    {
        var queue = new SimQueue<int>(2);
        var mutex = new SimLock();
        var gate = new SimEvent();
        var output = new List<int>();
        var producer = c.StartSoon(async token =>
        {
            await gate.Wait().WithCancellation(token);
            for (int i = 0; i < 30; i++)
                await queue.Put(i, token);
        });
        var consumer = c.StartSoon(async token =>
        {
            for (int i = 0; i < 30; i++)
            {
                await Timer(1).WithCancellation(token);
                using (await mutex.Acquire(token))
                    output.Add(await queue.Get(token));
            }
        });
        await Timer(1);
        gate.Set();
        await Task.WhenAll(producer.Task, consumer.Task);
        Check(output.SequenceEqual(Enumerable.Range(0, 30)), "queue order");
        gate.Clear();
        Check(!gate.IsSet, "clear");
        gate.Set();
        await gate.Wait();
    }

    [SimTest]
    public static async Task F_ForceReleaseAndHierarchy(TestContext c)
    {
        var data = c.Dut.Child("data").Signal<W8>();
        data.Write(new UInt<W8>(11));
        await Timer(1);
        if (c.Runtime.SupportsForceRelease)
        {
            data.Force(LogicVector<W8>.From(new UInt<W8>(77)));
            await Timer(1);
            data.Write(new UInt<W8>(12));
            await Timer(1);
            Check(data.ReadUInt().Value == 77, "force holds");
            data.Release();
            data.Write(new UInt<W8>(13));
            await Timer(1);
            Check(data.ReadUInt().Value == 13, "release");
        }
        else
            Throws<NotSupportedException>(() => data.Force(LogicVector<W8>.From(new UInt<W8>(77))));
        c.Dut.Child("memory").At(2).Signal<W8>().Write(new UInt<W8>(91));
        await Timer(1);
        Check(c.Dut.Child("memory").At(2).Signal<W8>().ReadUInt().Value == 91, "memory indexing");
        Check(c.Dut.Child("ascending").Signal().Range.Ascending, "ascending range");
        string output = Environment.GetEnvironmentVariable("CS_BINDINGS") ?? "DutBindings.cs";
        var bindings = Bindings.Generate(c.Dut);
        Check(bindings.Contains("Width256"), "wide vector code generation");
        Check(bindings.Contains("SignedSignal"), "signed code generation");
        Check(bindings.Contains("local_data") && bindings.Contains("HdlArray<"), "recursive scope and array generation");
        var memory = c.Dut.Child("memory").Array(x => x.Unsigned<W8>());
        Check(memory.Count == 4 && ReferenceEquals(memory[2], memory[2]), "array caches typed handles");
        Throws<IndexOutOfRangeException>(() =>
        {
            _ = memory[4];
        });
        c.Dut.Child("unit0").Child("local_data").Signal<W8>().Write(new UInt<W8>(27));
        await Timer(1);
        Check(c.Dut.Child("unit0").Child("local_data").Signal<W8>().ReadUInt().Value == 27, "nested scope signal");
        File.WriteAllText(output, bindings);
        Check(c.Dut.Children().Any(x => x.Name == "wide"), "enumeration");
    }

    [SimTest]
    [SimCase(3)]
    [SimCase(7)]
    public static async Task G_ParameterizedClock(TestContext c, int cycles)
    {
        var clk = c.Dut.Child("clk").Signal();
        var job = c.StartSoon(new SimClock(clk, 10).Run);
        await ClockCycles(clk, cycles, c.Cancellation);
        job.Cancel();
        await Timer(1);
        Check(job.Task.IsCompleted, "clock stopped");
    }

    [SimTest]
    public static async Task H_WriteCoalescing(TestContext c)
    {
        var d = c.Dut.Child("data").Signal<W8>();
        d.Write(new UInt<W8>(1));
        d.Write(new UInt<W8>(2));
        d.Write(new UInt<W8>(3));
        await ReadWrite();
        await ReadOnly();
        Check(d.ReadUInt().Value == 3, "last deposit wins");
        await Timer(1);
        await ReadWrite();
        d.Write(new UInt<W8>(17));
        await ReadOnly();
        Check(c.Dut.Child("doubled").Signal<W8>().ReadUInt().Value == 34, "downstream settles");
    }

    [SimTest]
    public static async Task I_NextTimeStep(TestContext c)
    {
        var job = c.StartSoon(async token => await Timer(10).WithCancellation(token));
        await Timer(1);
        ulong t = c.Runtime.Now;
        await NextTimeStep();
        Check(c.Runtime.Now > t, "next step advances");
        await job;
    }

    [SimTest(ExpectedException = typeof(InvalidOperationException))]
    public static async Task J_BackgroundFailure(TestContext c)
    {
        _ = c.StartSoon(async token =>
        {
            await Timer(2).WithCancellation(token);
            throw new InvalidOperationException("intentional child failure");
        });
        await Timer(100);
    }

    [SimTest(TimeoutSteps = 5, ExpectedException = typeof(TimeoutException))]
    public static async Task K_Deadline(TestContext c)
    {
        await Timer(1000);
    }

    [SimTest]
    public static async Task L_StreamBackpressure(TestContext c)
    {
        var clk = c.Dut.Child("clk").Signal();
        var reset = c.Dut.Child("reset").Signal();
        var valid = c.Dut.Child("in_valid").Signal();
        var ready = c.Dut.Child("in_ready").Signal();
        var data = c.Dut.Child("in_data").Signal<W8>();
        var outValid = c.Dut.Child("out_valid").Signal();
        var outReady = c.Dut.Child("out_ready").Signal();
        var outData = c.Dut.Child("out_data").Signal<W8>();
        clk.WriteBit(false);
        reset.WriteBit(true);
        valid.WriteBit(false);
        outReady.WriteBit(false);
        var clock = c.StartSoon(new SimClock(clk, 10).Run);
        await ClockCycles(clk, 2, c.Cancellation);
        await FallingEdge(clk);
        reset.WriteBit(false);
        int sent = 0, received = 0;
        for (int cycle = 0; cycle < 1000 && received < 100; cycle++)
        {
            bool offer = sent < 100 && c.Random.Next(4) != 0;
            bool accept = c.Random.Next(3) != 0;
            valid.WriteBit(offer);
            outReady.WriteBit(accept);
            data.Write(new UInt<W8>(sent));
            await ReadOnly();
            bool transferIn = offer && ready.ReadBit();
            bool transferOut = outValid.ReadBit() && accept;
            int sample = (int)outData.ReadUInt().Value;
            await RisingEdge(clk);
            if (transferIn)
                sent++;
            if (transferOut)
            {
                Check(sample == received, $"stream mismatch {sample} != {received}");
                received++;
            }

            await FallingEdge(clk);
        }

        Check(sent == 100 && received == 100, "all transactions delivered");
        valid.WriteBit(false);
        clock.Cancel();
        await Timer(1);
    }

    [SimTest]
    public static async Task M_ThreadGuard(TestContext c)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                _ = c.Dut.Child("data");
            }
            catch (Exception e)
            {
                failure = e;
            }
        });
        thread.Start();
        thread.Join();
        Check(failure is InvalidOperationException, "cross-thread access rejected");
        await Timer(1);
    }

    [SimTest]
    public static async Task N_TimeUnits(TestContext c)
    {
        Check(c.Runtime.Steps(1, TimeUnit.Nanoseconds) == 1000, "precision conversion");
        Throws<ArgumentException>(() => c.Runtime.Steps(0.5m, TimeUnit.Picoseconds));
        Check(c.Runtime.Steps(0.5m, TimeUnit.Picoseconds, TimeRounding.Ceiling) == 1, "rounding");
        await Timer(1);
    }

    [SimTest]
    public static async Task P_ExitStatus(TestContext c)
    {
        await Timer(1);
        if (Environment.GetEnvironmentVariable("CS_INJECT_FAILURE") == "1")
            throw new Exception("intentional runner failure");
    }

    [SimTest(Skip = true)]
    public static Task O_Skip(TestContext c) => throw new Exception("must not execute");
    [SimTest]
    public static async Task Q_ValueChangeFanoutAndYield(TestContext c)
    {
        var signal = c.Dut.Child("data").Signal<W8>();
        signal.Write(new UInt<W8>(0));
        await Timer(1);
        int completed = 0;
        var jobs = Enumerable.Range(0, 10).Select(_ => c.StartSoon(async token =>
        {
            await ValueChange(signal).WithCancellation(token);
            await ReadOnly().WithCancellation(token);
            Check(signal.ReadUInt().Value == 83, "fanout settled value");
            completed++;
        })).ToArray();
        ulong step = c.Runtime.Now;
        await Yield();
        Check(c.Runtime.Now == step, "yield does not advance time");
        signal.Write(new UInt<W8>(83));
        await Task.WhenAll(jobs.Select(x => x.Task));
        Check(completed == 10, "all listeners resumed");
    }

    [SimTest]
    public static async Task R_CancelledQueueWait(TestContext c)
    {
        var queue = new SimQueue<int>(1);
        using var cancel = new CancellationTokenSource();
        bool caught = false;
        var job = c.StartSoon(async _ =>
        {
            try
            {
                await queue.Get(cancel.Token);
            }
            catch (OperationCanceledException)
            {
                caught = true;
            }
        });
        await Yield();
        cancel.Cancel();
        await job;
        await queue.Put(9);
        Check(await queue.Get() == 9, "cancel did not consume a permit");
        Check(caught, "queue cancellation delivered");
    }

    [SimTest]
    public static async Task S_ChildCancellationScope(TestContext c)
    {
        bool cleaned = false;
        var child = c.StartSoon(async _ =>
        {
            try
            {
                await Timer(10000);
            }
            finally
            {
                cleaned = true;
            }
        }, "monitor");
        await Yield();
        child.Cancel();
        try
        {
            await child;
            throw new Exception("not cancelled");
        }
        catch (OperationCanceledException)
        {
        }

        Check(cleaned && child.Task.IsCanceled && child.Name == "monitor", "child cleanup and status");
        await Timer(1); // Child cancellation must not bleed into its parent.
        Check(c.Runtime.PendingCallbacks == 1, "child native registration removed");
    }

    [SimTest]
    public static async Task T_NestedCancellationAndSuppression(TestContext c)
    {
        SimJob? grandchild = null;
        bool suppressed = false;
        var parent = c.StartSoon(async _ =>
        {
            grandchild = c.StartSoon(async _ => await new SimQueue<int>().Get());
            try
            {
                await Timer(10000);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is cooperative; bounded cleanup may opt out explicitly.
                await Timer(2).AsTask(CancellationToken.None);
                await new SimSemaphore(1).Acquire(CancellationToken.None);
                suppressed = true;
            }
        });
        await Yield();
        await Yield();
        parent.Cancel();
        await parent;
        Check(suppressed && parent.Task.IsCompletedSuccessfully, "suppressed cancellation returns normally");
        Check(grandchild != null && grandchild.Task.IsCanceled, "nested queue inherits parent token");
        bool ran = false;
        var beforeStart = c.StartSoon(_ =>
        {
            ran = true;
            return Task.CompletedTask;
        });
        beforeStart.Cancel();
        await Yield();
        Check(!ran && beforeStart.Task.IsCanceled, "cancel before start never invokes body");
    }

    [SimTest]
    public static async Task U_TaskCombinators(TestContext c)
    {
        var slow = Timer(7).AsTask();
        var fast = Timer(2).AsTask();
        var winner = await SimTasks.First(slow, fast);
        Check(winner == fast && !slow.IsCompleted, "first task identity; losers remain owned by caller");
        await SimTasks.Combine(slow, fast);
        Check(slow.IsCompletedSuccessfully, "combine joined loser");
        var done = Task.CompletedTask;
        Check(await SimTasks.First(done, Task.FromResult(42)) == done, "completed tie input order");
        bool caught = false;
        try
        {
            await SimTasks.First(Task.FromException(new FormatException("expected")), done);
        }
        catch (FormatException)
        {
            caught = true;
        }

        Check(caught, "first propagates failure");
        var remaining = Timer(3).AsTask();
        caught = false;
        try
        {
            await SimTasks.Combine(Task.FromException(new FormatException("expected")), remaining);
        }
        catch (FormatException)
        {
            caught = true;
        }

        Check(caught && remaining.IsCompleted, "combine joins all before propagating failure");
        using var cancel = new CancellationTokenSource();
        var survivor = Timer(2).AsTask();
        var cancelledWait = SimTasks.Combine(cancel.Token, survivor);
        cancel.Cancel();
        try
        {
            await cancelledWait;
            throw new Exception("combine was not cancelled");
        }
        catch (OperationCanceledException)
        {
        }

        Check(!survivor.IsCompleted, "combine cancellation leaves owned task running");
        await survivor;
    }

    [SimTest]
    public static async Task V_TaskTimeoutOwnership(TestContext c)
    {
        var owned = Timer(9).AsTask();
        bool expired = false;
        try
        {
            await SimTasks.WithTimeout(owned, 2);
        }
        catch (TimeoutException)
        {
            expired = true;
        }

        Check(expired && !owned.IsCompleted, "deadline leaves task running");
        await owned;
        Check(await SimTasks.WithTimeout(Task.FromResult(123), 10) == 123, "typed result");
        using var cancel = new CancellationTokenSource();
        var pending = Timer(5).AsTask();
        var wait = SimTasks.WithTimeout(pending, 100, cancel.Token);
        cancel.Cancel();
        try
        {
            await wait;
            throw new Exception("wait not cancelled");
        }
        catch (OperationCanceledException)
        {
        }

        Check(!pending.IsCompleted, "wait cancellation leaves task running");
        await pending;
        Check(c.Runtime.PendingCallbacks == 1, "deadline registrations cleaned");
    }

    [SimTest]
    public static async Task W_ClockCyclesDefaultCancellation(TestContext c)
    {
        var clock = c.Dut.Child("clk").Signal();
        var child = c.StartSoon(async _ => await ClockCycles(clock, 100));
        await Yield();
        child.Cancel();
        try
        {
            await child;
            throw new Exception("clock wait not cancelled");
        }
        catch (OperationCanceledException)
        {
        }

        Check(c.Runtime.PendingCallbacks == 1, "clock callback removed");
    }

    [SimTest(ExpectedException = typeof(InvalidOperationException))]
    public static async Task Z_LeakCannotMatchExpectedException(TestContext c)
    {
        if (Environment.GetEnvironmentVariable("CS_INJECT_LEAK") == "1")
            _ = c.StartSoon(async _ => await Timer(10000).WithCancellation(CancellationToken.None));
        await Yield();
        throw new InvalidOperationException("expected assertion, but leaked work must still fail");
    }

    [SimTest]
    public static async Task X_StructuredGroup(TestContext c)
    {
        bool cleaned = false, caught = false;
        SimTaskGroup? captured = null;
        try
        {
            await c.RunGroup(async group =>
            {
                captured = group;
                _ = group.StartSoon(async _ =>
                {
                    try
                    {
                        await Timer(10000);
                    }
                    finally
                    {
                        cleaned = true;
                    }
                });
                _ = group.StartSoon(async _ =>
                {
                    await Timer(2);
                    throw new FormatException("group failure");
                });
                await Timer(10000);
            });
        }
        catch (FormatException)
        {
            caught = true;
        }

        Check(caught && cleaned, "failure observed only after sibling cleanup");
        Throws<InvalidOperationException>(() => captured!.StartSoon(_ => Task.CompletedTask));
        await Timer(1); // A handled group failure must not cancel the enclosing test.
        Check(c.Runtime.PendingCallbacks == 1, "group removed pending native timers");
        bool normalCleanup = false;
        await c.RunGroup(async group =>
        {
            _ = group.StartSoon(async _ =>
            {
                try
                {
                    await Timer(10000);
                }
                finally
                {
                    normalCleanup = true;
                }
            });
            await Yield();
        });
        Check(normalCleanup, "normal scope exit also cancels and joins children");
        bool cleanupFailure = false;
        try
        {
            await c.RunGroup(async group =>
            {
                _ = group.StartSoon(async _ =>
                {
                    try
                    {
                        await Timer(10000);
                    }
                    finally
                    {
                        throw new FormatException("cleanup failure");
                    }
                });
                await Yield();
            });
        }
        catch (FormatException)
        {
            cleanupFailure = true;
        }

        Check(cleanupFailure, "failure during cancellation cleanup is not swallowed by join");
    }

    [SimTest]
    public static async Task Y_SameTimeCancellationStress(TestContext c)
    {
        for (int round = 0; round < 100; round++)
        {
            var semaphore = new SimSemaphore(0, 1);
            var waiter = c.StartSoon(async _ => await semaphore.Acquire());
            await Yield();
            semaphore.Release(); // Grant, then cancel before the continuation consumes it.
            waiter.Cancel();
            try
            {
                await waiter;
            }
            catch (OperationCanceledException)
            {
            }

            // Event completion already won this race: the waiter succeeds, consuming it.
            Check(waiter.Task.IsCompletedSuccessfully && semaphore.CurrentCount == 0, "completed trigger wins cancellation race");
            using var cancel = new CancellationTokenSource();
            var native = Timer(1).AsTask(cancel.Token);
            cancel.Cancel();
            try
            {
                await native;
                throw new Exception("native wait survived cancellation");
            }
            catch (OperationCanceledException)
            {
            }

            await Timer(1);
            Check(c.Runtime.PendingCallbacks == 1, "no registration buildup across races");
        }
    }

    [SimTest]
    public static async Task AA_TypedJobResults(TestContext c)
    {
        SimJob<UInt<W16>> job = c.StartSoon(async _ =>
        {
            await Timer(2);
            return new UInt<W16>(321);
        }, "typed");
        Throws<InvalidOperationException>(() =>
        {
            _ = job.Result;
        });
        UInt<W16> result = await job;
        Check(result.Value == 321 && job.Result == result, "typed job result survives awaiting");
        await c.RunGroup(async group =>
        {
            SimJob<string> text = group.StartSoon(async _ =>
            {
                await Timer(1);
                return "done";
            });
            Check(await text == "done", "group typed result");
        });
        var cancelled = c.StartSoon<int>(async _ =>
        {
            await Timer(1000);
            return 42;
        });
        cancelled.Cancel();
        try
        {
            _ = await cancelled;
            throw new Exception("not cancelled");
        }
        catch (OperationCanceledException)
        {
        }

        Throws<OperationCanceledException>(() =>
        {
            _ = cancelled.Result;
        });
    }

    [SimTest]
    public static async Task AB_AggregateAndNestedGroups(TestContext c)
    {
        AggregateException? failures = null;
        try
        {
            await c.RunGroup(async outer =>
            {
                _ = outer.StartSoon(async outerToken =>
                {
                    await c.RunGroup(async inner =>
                    {
                        _ = inner.StartSoon(async _ =>
                        {
                            try
                            {
                                await Timer(100);
                            }
                            finally
                            {
                                throw new FormatException("cleanup A");
                            }
                        });
                        _ = inner.StartSoon(async _ =>
                        {
                            try
                            {
                                await Timer(100);
                            }
                            finally
                            {
                                throw new ArgumentException("cleanup B");
                            }
                        });
                        await Yield();
                    });
                });
                await Timer(1000);
            });
        }
        catch (AggregateException e)
        {
            failures = e;
        }

        Check(failures != null && failures.InnerExceptions.Count == 2, "nested failures preserved once");
        Check(failures!.InnerExceptions.Any(e => e is FormatException) && failures.InnerExceptions.Any(e => e is ArgumentException), "both cleanup failures reported");
        bool single = false;
        try
        {
            await c.RunGroup(async group =>
            {
                var job = group.StartSoon<int>(async _ =>
                {
                    await Timer(1);
                    throw new FormatException("single");
                });
                try
                {
                    await job;
                }
                finally
                {
                    Check(job.Exception?.InnerExceptions.Count == 1, "typed exception available");
                }
            });
        }
        catch (FormatException)
        {
            single = true;
        }

        Check(single, "awaited child failure not duplicated as a body failure");
        await Timer(1);
    }

    [SimTest]
    public static async Task AC_Constants(TestContext c)
    {
        var constant = c.Dut.Child("PARAMETER").Signal<W8>();
        Check(constant.IsConstant && constant.ReadUInt().Value == 37, "parameter metadata/value");
        Throws<InvalidOperationException>(() => constant.Write(new UInt<W8>(5)));
        await Timer(1);
    }
}
