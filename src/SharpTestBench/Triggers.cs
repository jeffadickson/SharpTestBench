using System.Runtime.CompilerServices;

namespace SharpTestBench;
public abstract class Trigger
{
    internal abstract IDisposable Subscribe(SimRuntime runtime, Action<Exception?> complete);
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
            subscription = trigger.Subscribe(runtime, Complete);
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
        internal override IDisposable Subscribe(SimRuntime runtime, Action<Exception?> complete)
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

    public static Trigger Timer(decimal value, TimeUnit unit, TimeRounding rounding = TimeRounding.Error) => Timer((SimRuntime.Active ?? throw new InvalidOperationException("No runtime")).Steps(value, unit, rounding));
    public static Trigger ReadOnly() => new NativeTrigger(CallbackKind.ReadOnly);
    public static Trigger ReadWrite() => new NativeTrigger(CallbackKind.ReadWrite);
    public static Trigger NextTimeStep() => new NativeTrigger(CallbackKind.NextStep);
    public static Trigger RisingEdge(Signal signal) => Edge(signal, 1);
    public static Trigger FallingEdge(Signal signal) => Edge(signal, 2);
    public static Trigger ValueChange(Signal signal) => Edge(signal, 0);
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
        internal override IDisposable Subscribe(SimRuntime r, Action<Exception?> complete) => r.Register(kind, () => complete(null), steps, signal, edge);
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

        internal override IDisposable Subscribe(SimRuntime r, Action<Exception?> complete)
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
                    bag.Add(t.Subscribe(r, e =>
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
        internal override IDisposable Subscribe(SimRuntime r, Action<Exception?> complete)
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
                bag.Add(trigger.Subscribe(r, Finish));
                if (!done)
                    bag.Add(timer.Subscribe(r, e => Finish(e ?? new TimeoutException("Simulation deadline expired"))));
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
