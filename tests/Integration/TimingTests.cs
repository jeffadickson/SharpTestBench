using SharpTestBench;
using static SharpTestBench.Triggers;

public static class TimingTests
{
    static string Trace => Environment.GetEnvironmentVariable("CS_TRACE") ?? "trace.txt";
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    [SimTest]
    public static async Task CombinatorBorrowedCancellation(TestContext c)
    {
        using var cancellation = new CancellationTokenSource();
        var callbacks = c.Runtime.PendingCallbacks;
        var borrowed = Timer(5).AsTask(CancellationToken.None);
        var wait = SimTasks.First(cancellation.Token, borrowed);
        cancellation.Cancel();
        bool cancelled = false;
        try { await wait; } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && !borrowed.IsCompleted, "cancelled First detaches without cancelling borrowed task");
        await borrowed;
        Check(c.Runtime.PendingCallbacks == callbacks, "borrowed timer joined without callback leak");
    }

    [SimTest]
    public static async Task CombinatorFailureIsNotFailFast(TestContext c)
    {
        var start = c.Runtime.Now;
        var delayed = Timer(7).AsTask();
        var failure = new FormatException("borrowed failure");
        try
        {
            await SimTasks.Combine(Task.FromException(failure), delayed);
            throw new Exception("Combine did not propagate failure");
        }
        catch (FormatException error)
        {
            Check(ReferenceEquals(error, failure), "original failure retained");
        }
        Check(c.Runtime.Now == start + 7 && delayed.IsCompletedSuccessfully,
            "Combine waits for borrowed success despite an earlier failure");
        start = c.Runtime.Now;
        delayed = Timer(3).AsTask();
        bool cancelled = false;
        try { await SimTasks.Combine(Task.FromCanceled(new CancellationToken(true)), delayed); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && c.Runtime.Now == start + 3 && delayed.IsCompletedSuccessfully,
            "cancelled child also waits for other borrowed tasks");
    }

    [SimTest]
    public static async Task CombinatorGroupJoinsAsyncCleanup(TestContext c)
    {
        var start = c.Runtime.Now;
        var callbacks = c.Runtime.PendingCallbacks;
        bool cleaned = false, caught = false;
        try
        {
            await c.RunGroup(async group =>
            {
                var sibling = group.StartSoon(async _ =>
                {
                    try { await Timer(100); }
                    finally
                    {
                        await Timer(3).WithCancellation(CancellationToken.None);
                        cleaned = true;
                    }
                });
                var failing = group.StartSoon(async _ =>
                {
                    await Timer(2);
                    throw new FormatException("owned failure");
                });
                await SimTasks.Combine(sibling.Task, failing.Task);
            });
        }
        catch (FormatException) { caught = true; }
        Check(caught && cleaned && c.Runtime.Now == start + 5,
            "group cancels sibling then joins asynchronous cleanup before propagating failure");
        Check(c.Runtime.PendingCallbacks == callbacks, "owned group leaves no native registrations");
    }

    [SimTest]
    public static Task CapabilityCatalogue(TestContext c)
    {
        var caps = c.Runtime.Capabilities;
        Check(ReferenceEquals(caps, c.Runtime.Capabilities), "stable runtime policy");
        Check(caps.Family != SimulatorFamily.Unknown, "validated backend recognized");
        Check(caps.All.Count == Enum.GetValues<SimulatorFeature>().Length, "complete catalogue");
        Check(c.Runtime.SupportsFourState == caps[SimulatorFeature.FourStateWrites].IsSupported, "legacy flag agrees");
        File.WriteAllText(Trace + ".capabilities.json", System.Text.Json.JsonSerializer.Serialize(caps.All,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));
        return Task.CompletedTask;
    }

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

public static class FixtureTests
{
    static readonly List<int> order = new();
    [SimTest(TimeoutSteps = 10, CleanupTimeoutSteps = 4)]
    public static async Task Cleanup_A_Run(TestContext c)
    {
        order.Clear();
        string? mode = Environment.GetEnvironmentVariable("CS_CLEANUP_MODE");
        c.Cleanup.Defer(() => order.Add(1));
        var nested = c.Cleanup.CreateScope();
        nested.Defer(() => order.Add(2));
        c.Cleanup.Defer(async token =>
        {
            order.Add(3);
            if (mode == "hang") await new TaskCompletionSource().Task;
            await Timer(mode == "cleanup-timeout" ? 20UL : 1UL).WithCancellation(token);
            if (mode == "both") throw new FormatException("cleanup marker");
        });
        if (mode == "cancel-error") c.Cancellation.Register(() => throw new FormatException("cancellation marker"));
        if (mode == "both") throw new InvalidOperationException("body marker");
        if (mode == "timeout") await Timer(20);
    }

    [SimTest]
    public static Task Cleanup_B_Verify(TestContext c)
    {
        if (!order.SequenceEqual(new[] { 3, 2, 1 })) throw new Exception("Cleanup ordering or continuation failed");
        return Task.CompletedTask;
    }
}
