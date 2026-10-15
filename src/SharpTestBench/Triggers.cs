using System.Runtime.CompilerServices;

namespace SharpTestBench;
public abstract class Trigger
{
    /// <summary>
    /// Register one wait on the simulator thread. Complete with null for success or
    /// an exception for failure; return a non-null, idempotent, nonthrowing detach
    /// handle. Completion may be synchronous. Each call owns independent state.
    /// Completion and disposal must run on the simulator thread. If registration
    /// throws, the implementation must undo any resources it already acquired.
    /// </summary>
    protected internal abstract IDisposable Subscribe(SimRuntime runtime, Action<Exception?> complete);

    /// <summary>Subscribe to a child trigger when implementing a custom trigger.</summary>
    /// <remarks>The caller owns the returned subscription, including on failure or cancellation.</remarks>
    protected static IDisposable SubscribeTo(Trigger trigger, SimRuntime runtime, Action<Exception?> complete)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(complete);
        runtime.CheckThread();
        return trigger.SubscribeOnce(runtime, complete);
    }

    // Enforce one completion per subscription for custom triggers in every consumer,
    // including combinators. Disposal also silences a producer's stale notification.
    internal IDisposable SubscribeOnce(SimRuntime runtime, Action<Exception?> complete)
    {
        runtime.CheckThread();
        bool finished = false;
        try
        {
            var subscription = Subscribe(runtime, error =>
            {
                runtime.CheckThread();
                if (finished) return;
                finished = true;
                complete(error);
            }) ?? throw new InvalidOperationException("Trigger returned a null subscription");
            return new ActionDisposable(() => { runtime.CheckThread(); finished = true; subscription.Dispose(); });
        }
        catch
        {
            finished = true;
            throw;
        }
    }
    public TriggerAwaiter GetAwaiter() => new(this, SimRuntime.Active ?? throw new InvalidOperationException("No runtime"), null);
    public TriggerAwaiter WithCancellation(CancellationToken token) => new(this, SimRuntime.Active ?? throw new InvalidOperationException("No runtime"), token);
    public async Task AsTask(CancellationToken? token = null)
    {
        await WithCancellation(token ?? (SimRuntime.Active?.DefaultCancellation ?? default));
    }
}

public sealed class TriggerAwaiter : INotifyCompletion
{
    readonly Trigger trigger;
    readonly SimRuntime runtime;
    readonly CancellationToken? requested;
    IDisposable? subscription;
    CancellationTokenRegistration cancellation;
    Action? continuation;
    Exception? error;
    bool done;
    internal TriggerAwaiter(Trigger t, SimRuntime r, CancellationToken? c)
    {
        trigger = t;
        runtime = r;
        requested = c;
    }

    public TriggerAwaiter GetAwaiter() => this;
    public bool IsCompleted => false;

    public void GetResult()
    {
        if (error != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    public void OnCompleted(Action action)
    {
        runtime.CheckThread();
        continuation = action;
        var token = requested ?? runtime.DefaultCancellation;
        if (token.IsCancellationRequested)
        {
            Complete(new OperationCanceledException(token));
            return;
        }

        try
        {
            subscription = trigger.SubscribeOnce(runtime, Complete);
            if (done)
            {
                subscription.Dispose();
                subscription = null;
            }
            else if (token.CanBeCanceled)
                cancellation = token.Register(() =>
                {
                    if (Environment.CurrentManagedThreadId == ThreadId)
                        Complete(new OperationCanceledException(token));
                    else
                        runtime.Enqueue(() => Complete(new OperationCanceledException(token)));
                });
        }
        catch (Exception e)
        {
            Complete(e);
        }
    }

    readonly int ThreadId = Environment.CurrentManagedThreadId;
    void Complete(Exception? e)
    {
        runtime.CheckThread();
        if (done)
            return;
        done = true;
        error = e;
        subscription?.Dispose();
        subscription = null;
        cancellation.Dispose();
        runtime.Enqueue(continuation!);
    }
}

public static class Triggers
{
    public static Trigger Yield() => new YieldTrigger();
    sealed class YieldTrigger : Trigger
    {
        protected internal override IDisposable Subscribe(SimRuntime runtime, Action<Exception?> complete)
        {
            bool cancelled = false;
            runtime.Enqueue(() =>
            {
                if (!cancelled)
                    complete(null);
            });
            return new ActionDisposable(() => cancelled = true);
        }
    }

    public static Trigger Timer(ulong steps)
    {
        if (steps == 0)
            throw new ArgumentOutOfRangeException(nameof(steps));
        return new NativeTrigger(CallbackKind.Timer, steps);
    }

    public static Trigger Timer(decimal value, TimeUnit unit, TimeRounding rounding = TimeRounding.Error) => Timer((SimRuntime.Active ?? throw new InvalidOperationException("No runtime")).TimerSteps(value, unit, rounding));
    public static Trigger ReadOnly() => new NativeTrigger(CallbackKind.ReadOnly);
    public static Trigger ReadWrite() => new NativeTrigger(CallbackKind.ReadWrite);
    public static Trigger NextTimeStep() => new NativeTrigger(CallbackKind.NextStep);
    public static Trigger RisingEdge(Signal signal) => Edge(signal, 1);
    public static Trigger FallingEdge(Signal signal) => Edge(signal, 2);
    /// <summary>Wait for a logic or integer signal to change; preserves the original overload.</summary>
    public static Trigger ValueChange(Signal signal) => ValueChange((HdlObject)signal);

    /// <summary>
    /// Wait for a scalar value object, including real and HDL string signals, to change.
    /// Availability of native change notifications depends on the simulator adapter.
    /// Scopes, arrays and aggregate structures are not value-change targets.
    /// </summary>
    public static Trigger ValueChange(HdlObject signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        if (signal.Kind is not (ObjectKind.Logic or ObjectKind.LogicArray or ObjectKind.Packed
            or ObjectKind.Integer or ObjectKind.Enum or ObjectKind.Real or ObjectKind.String or ObjectKind.FixedString))
            throw new ArgumentException("ValueChange requires a scalar value object", nameof(signal));
        return new NativeTrigger(CallbackKind.Edge, signal: signal.Handle);
    }
    static Trigger Edge(Signal s, int edge)
    {
        if (edge != 0 && s.Width != 1)
            throw new ArgumentException("Rising/falling edge requires a one-bit signal");
        return new NativeTrigger(CallbackKind.Edge, signal: s.Handle, edge: edge);
    }

    public static Trigger First(params Trigger[] triggers) => new CompositeTrigger(false, triggers);
    public static Trigger Combine(params Trigger[] triggers) => new CompositeTrigger(true, triggers);
    public static Trigger WithTimeout(Trigger trigger, ulong steps) => new TimeoutTrigger(trigger, Timer(steps));
    public static async Task ClockCycles(Signal clock, int count, CancellationToken? cancellation = null)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        var token = cancellation ?? (SimRuntime.Active ?? throw new InvalidOperationException("No runtime")).DefaultCancellation;
        token.ThrowIfCancellationRequested();
        for (int i = 0; i < count; i++)
            await RisingEdge(clock).WithCancellation(token);
    }

    sealed class NativeTrigger(CallbackKind kind, ulong steps = 0, nint signal = 0, int edge = 0) : Trigger
    {
        protected internal override IDisposable Subscribe(SimRuntime r, Action<Exception?> complete) => r.Register(kind, () => complete(null), steps, signal, edge, this);
    }

    sealed class CompositeTrigger : Trigger
    {
        readonly bool all;
        readonly Trigger[] items;
        internal CompositeTrigger(bool all, Trigger[] items)
        {
            if (items.Length == 0)
                throw new ArgumentException("At least one trigger required");
            this.all = all;
            this.items = (Trigger[])items.Clone();
            if (items.Any(x => x == null))
                throw new ArgumentException("Null trigger");
        }

        protected internal override IDisposable Subscribe(SimRuntime r, Action<Exception?> complete)
        {
            var bag = new SubscriptionBag();
            int left = items.Length;
            bool done = false;
            foreach (var t in items)
            {
                if (done)
                    break;
                try
                {
                    bag.Add(t.SubscribeOnce(r, e =>
                    {
                        if (done)
                            return;
                        if (e != null || !all || --left == 0)
                        {
                            done = true;
                            bag.Dispose();
                            complete(e);
                        }
                    }));
                }
                catch
                {
                    bag.Dispose();
                    throw;
                }
            }

            return bag;
        }
    }

    sealed class TimeoutTrigger(Trigger trigger, Trigger timer) : Trigger
    {
        protected internal override IDisposable Subscribe(SimRuntime r, Action<Exception?> complete)
        {
            var bag = new SubscriptionBag();
            bool done = false;
            void Finish(Exception? e)
            {
                if (done)
                    return;
                done = true;
                bag.Dispose();
                complete(e);
            }

            try
            {
                bag.Add(trigger.SubscribeOnce(r, Finish));
                if (!done)
                    bag.Add(timer.SubscribeOnce(r, e => Finish(e ?? new TimeoutException("Simulation deadline expired"))));
                return bag;
            }
            catch
            {
                bag.Dispose();
                throw;
            }
        }
    }
}

internal sealed class SubscriptionBag : IDisposable
{
    readonly List<IDisposable> items = new();
    bool disposed;
    public void Add(IDisposable d)
    {
        if (disposed)
            d.Dispose();
        else
            items.Add(d);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        foreach (var d in items)
            d.Dispose();
        items.Clear();
    }
}

internal sealed class ActionDisposable(Action action) : IDisposable
{
    Action? dispose = action;
    public void Dispose() => Interlocked.Exchange(ref dispose, null)?.Invoke();
}
