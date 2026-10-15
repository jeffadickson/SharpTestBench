using SharpTestBench;
using static SharpTestBench.Triggers;

/// <summary>These subclasses compile outside the runtime assembly, exercising the public extension contract.</summary>
public static class TriggerExtensionTests
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    /// <summary>Wrap a child subscription with independent lifetime accounting for each waiter.</summary>
    sealed class CountedTrigger(Trigger child) : Trigger
    {
        public int Active { get; private set; }
        public int Detached { get; private set; }
        protected override IDisposable Subscribe(SimRuntime runtime, Action<Exception?> complete)
        {
            var subscription = SubscribeTo(child, runtime, complete);
            Active++;
            return new Detach(() => { subscription.Dispose(); Active--; Detached++; });
        }
    }

    sealed class ImmediateTrigger(Exception? error = null) : Trigger
    {
        public int Detached { get; private set; }
        protected override IDisposable Subscribe(SimRuntime runtime, Action<Exception?> complete)
        {
            runtime.CheckThread();
            complete(error);
            complete(error); // Duplicate completion must never resume the awaiter twice.
            return new Detach(() => Detached++);
        }
    }

    sealed class BrokenRegistration : Trigger
    {
        protected override IDisposable Subscribe(SimRuntime runtime, Action<Exception?> complete)
            => throw new FormatException("registration marker");
    }

    sealed class Detach(Action action) : IDisposable
    {
        Action? pending = action;
        public void Dispose() => Interlocked.Exchange(ref pending, null)?.Invoke();
    }

    [SimTest]
    public static async Task ExternalTriggerLifecycle(TestContext c)
    {
        int baseline = c.Runtime.PendingCallbacks;
        var gate = new SimEvent();
        var custom = new CountedTrigger(gate.Wait());
        var first = custom.AsTask();
        var second = custom.AsTask();
        Check(custom.Active == 2, "each external wait owns its subscription");
        gate.Set();
        await Task.WhenAll(first, second);
        Check(custom.Active == 0 && custom.Detached == 2, "fanout detaches each subscription once");
        gate.Clear();
        using var cancellation = new CancellationTokenSource();
        var cancelled = custom.AsTask(cancellation.Token);
        cancellation.Cancel();
        try { await cancelled; throw new Exception("missing cancellation"); }
        catch (OperationCanceledException) { }
        Check(custom.Active == 0 && custom.Detached == 3, "cancellation detaches custom wait");
        var reused = custom.AsTask();
        gate.Set();
        await reused;
        Check(custom.Active == 0 && custom.Detached == 4, "custom trigger reusable after cancellation");

        var immediate = new ImmediateTrigger();
        ulong before = c.Runtime.Now;
        await immediate;
        Check(immediate.Detached == 1 && c.Runtime.Now == before, "synchronous completion disposes returned handle");
        var failure = new ImmediateTrigger(new FormatException("completion marker"));
        try { await failure; throw new Exception("missing failure"); }
        catch (FormatException e) { Check(e.Message == "completion marker", "completion failure preserved"); }
        Check(failure.Detached == 1, "failed completion detaches once");
        try { await new BrokenRegistration(); throw new Exception("missing registration failure"); }
        catch (FormatException e) { Check(e.Message == "registration marker", "registration failure preserved"); }
        Check(c.Runtime.PendingCallbacks == baseline, "custom lifecycle leaks no native callbacks");
    }

    [SimTest]
    public static async Task ExternalTriggerComposition(TestContext c)
    {
        int baseline = c.Runtime.PendingCallbacks;
        var slow = new CountedTrigger(Timer(20));
        await First(Timer(1), slow);
        Check(slow.Active == 0 && slow.Detached == 1, "First detaches losing custom trigger");
        try { await WithTimeout(slow, 1); throw new Exception("missing timeout"); }
        catch (TimeoutException) { }
        Check(slow.Active == 0 && slow.Detached == 2, "timeout detaches custom trigger");
        var fast = new CountedTrigger(Timer(1));
        await Combine(fast, Timer(2));
        Check(fast.Active == 0 && fast.Detached == 1, "Combine releases completed custom child");
        var duplicate = new ImmediateTrigger();
        ulong start = c.Runtime.Now;
        await Combine(duplicate, Timer(3));
        Check(c.Runtime.Now == start + 3 && duplicate.Detached == 1,
            "duplicate custom completion cannot satisfy another Combine child");
        // Failure during the second registration must roll back the already registered first child.
        try { await Combine(slow, new BrokenRegistration()); throw new Exception("missing registration failure"); }
        catch (FormatException) { }
        Check(slow.Active == 0 && slow.Detached == 3, "partial composite registration rolls back");
        Check(c.Runtime.PendingCallbacks == baseline, "custom composition leaks no native callbacks");
    }

    [SimTest]
    public static async Task SimulatorCallbackIntrospection(TestContext c)
    {
        var timer = Timer(2);
        await timer;
        var snapshot = c.Runtime.LastSimulatorCallback!.Value;
        Check(snapshot.Kind == CallbackKind.Timer && snapshot.Time == c.Runtime.Now
            && ReferenceEquals(snapshot.Trigger, timer), "timer callback retains concrete trigger identity");
        await Yield();
        var gate = new SimEvent(); gate.Set(); await gate.Wait();
        Check(c.Runtime.LastSimulatorCallback == snapshot, "managed waits preserve last native callback");
        using var cancellation = new CancellationTokenSource();
        var cancelled = Timer(100).AsTask(cancellation.Token);
        cancellation.Cancel();
        try { await cancelled; } catch (OperationCanceledException) { }
        Check(c.Runtime.LastSimulatorCallback == snapshot, "cancelled registration never becomes last callback");
        var readWrite = ReadWrite(); await readWrite;
        Check(ReferenceEquals(c.Runtime.LastSimulatorCallback!.Value.Trigger, readWrite)
            && c.Runtime.LastSimulatorCallback.Value.Kind == CallbackKind.ReadWrite, "read-write identity");
        var readOnly = ReadOnly(); await readOnly;
        Check(ReferenceEquals(c.Runtime.LastSimulatorCallback!.Value.Trigger, readOnly)
            && c.Runtime.LastSimulatorCallback.Value.Kind == CallbackKind.ReadOnly, "read-only identity");
        var readonlySnapshot = c.Runtime.LastSimulatorCallback;
        try { await ReadWrite(); throw new Exception("missing phase rejection"); }
        catch (InvalidOperationException) { }
        Check(c.Runtime.LastSimulatorCallback == readonlySnapshot, "rejected registration does not alter callback history");
        var future = Timer(2).AsTask();
        var next = NextTimeStep(); await next;
        Check(ReferenceEquals(c.Runtime.LastSimulatorCallback!.Value.Trigger, next)
            && c.Runtime.LastSimulatorCallback.Value.Kind == CallbackKind.NextStep, "next step is distinct from timer");
        await future;
        var data = c.Dut.Child("data").Signal<W8>();
        data.Write(new UInt<W8>(10));
        await Timer(1);
        var change = ValueChange(data);
        var wait = change.AsTask();
        data.Write(new UInt<W8>(25));
        await wait;
        Check(ReferenceEquals(c.Runtime.LastSimulatorCallback!.Value.Trigger, change)
            && c.Runtime.LastSimulatorCallback.Value.Kind == CallbackKind.Edge, "value-change identity");
        // A custom wrapper must expose the actual native child, not substitute itself.
        var child = Timer(1); await new CountedTrigger(child);
        Check(ReferenceEquals(c.Runtime.LastSimulatorCallback!.Value.Trigger, child), "custom wrapper preserves native origin");
        Check(snapshot.Trigger == timer && snapshot.Kind == CallbackKind.Timer, "captured snapshot remains immutable");
    }

    /// <summary>The test deadline is a native runtime callback, with no user Trigger object.</summary>
    [SimTest(TimeoutSteps = 3, ExpectedException = typeof(TimeoutException))]
    public static async Task InternalDeadlineCallbackIntrospection(TestContext c)
    {
        ulong expectedTime = c.Runtime.Now + 3;
        using var registration = c.Cancellation.Register(() =>
        {
            var snapshot = c.Runtime.LastSimulatorCallback!.Value;
            Check(snapshot.Kind == CallbackKind.Timer && snapshot.Time == expectedTime && snapshot.Trigger is null,
                "internal deadline is identified without inventing a public trigger");
        });
        await Timer(1000);
    }
}
