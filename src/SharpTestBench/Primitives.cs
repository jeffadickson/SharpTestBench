namespace SharpTestBench;
/// <summary>Manual-reset simulation event. Set wakes existing and future waiters until Clear.</summary>
public sealed class SimEvent
{
    readonly Dictionary<long, Action<Exception?>> waiters = new();
    long next;
    bool set;
    public bool IsSet => set;
    static SimRuntime Runtime => SimRuntime.Active ?? throw new InvalidOperationException("No runtime");

    public void Set()
    {
        Runtime.CheckThread();
        set = true;
        var pending = waiters.Values.ToArray();
        waiters.Clear();
        foreach (var w in pending)
            w(null);
    }

    public void Clear()
    {
        Runtime.CheckThread();
        set = false;
    }

    public Trigger Wait() => new EventTrigger(this);
    sealed class EventTrigger(SimEvent ev) : Trigger
    {
        protected internal override IDisposable Subscribe(SimRuntime runtime, Action<Exception?> complete)
        {
            runtime.CheckThread();
            if (ev.set)
            {
                complete(null);
                return new ActionDisposable(() =>
                {
                });
            }

            long id = ++ev.next;
            ev.waiters.Add(id, complete);
            return new ActionDisposable(() => ev.waiters.Remove(id));
        }
    }
}

/// <summary>FIFO semaphore. Cancellation returns granted-but-unconsumed permits.</summary>
public sealed class SimSemaphore
{
    int count;
    readonly int maximum;
    readonly Queue<Waiter> waiting = new();
    sealed class Waiter
    {
        internal readonly SimEvent Ready = new();
        internal bool Granted, Cancelled;
    }

    public SimSemaphore(int initial, int maximum = int.MaxValue)
    {
        if (initial < 0 || maximum < 1 || initial > maximum)
            throw new ArgumentOutOfRangeException();
        count = initial;
        this.maximum = maximum;
    }

    public int CurrentCount => count;

    // Reserved permits belong to queued acquirers; immediate callers cannot steal them.
    internal bool TryAcquire()
    {
        (SimRuntime.Active ?? throw new InvalidOperationException("No runtime")).CheckThread();
        if (count == 0) return false;
        count--;
        return true;
    }

    public async Task Acquire(CancellationToken? token = null)
    {
        var r = SimRuntime.Active ?? throw new InvalidOperationException("No runtime");
        r.CheckThread();
        var effectiveToken = token ?? r.DefaultCancellation;
        effectiveToken.ThrowIfCancellationRequested();
        if (count > 0)
        {
            count--;
            return;
        }

        var w = new Waiter();
        waiting.Enqueue(w);
        try
        {
            await w.Ready.Wait().WithCancellation(effectiveToken);
        }
        catch
        {
            w.Cancelled = true;
            if (w.Granted)
                Release();
            throw;
        }
    }

    public void Release()
    {
        (SimRuntime.Active ?? throw new InvalidOperationException("No runtime")).CheckThread();
        while (waiting.TryDequeue(out var w))
        {
            if (w.Cancelled)
                continue;
            w.Granted = true;
            w.Ready.Set();
            return;
        }

        if (count == maximum)
            throw new InvalidOperationException("Semaphore over-release");
        count++;
    }
}

public sealed class SimLock
{
    readonly SimSemaphore semaphore = new(1, 1);
    /// <summary>True while held or reserved for the next queued acquirer.</summary>
    public bool IsLocked => semaphore.CurrentCount == 0;
    public async Task<IDisposable> Acquire(CancellationToken? token = null)
    {
        await semaphore.Acquire(token);
        return new ActionDisposable(semaphore.Release);
    }
}

/// <summary>FIFO simulation queue with reserved permits for pending producers and consumers.</summary>
/// <typeparam name="T">Type of item transferred between simulation tasks.</typeparam>
public sealed class SimQueue<T>
{
    readonly Queue<T> queue = new();
    readonly SimSemaphore items = new(0);
    readonly SimSemaphore slots;
    /// <summary>Create a queue with a strictly positive capacity; the default is int.MaxValue.</summary>
    /// <param name="capacity">Maximum number of stored items.</param>
    public SimQueue(int capacity = int.MaxValue)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        slots = new(capacity, capacity);
        Capacity = capacity;
    }

    /// <summary>Maximum number of stored items.</summary>
    public int Capacity { get; }
    /// <summary>Stored item count, including items reserved for pending consumers.</summary>
    public int Count => queue.Count;
    /// <summary>True when no items are physically stored; does not describe reserved permits.</summary>
    public bool IsEmpty => Count == 0;
    /// <summary>True when stored item count reaches Capacity; pending producers may reserve free slots.</summary>
    public bool IsFull => Count == Capacity;

    /// <summary>Wait for an available slot, then append an item in FIFO order.</summary>
    /// <param name="value">Item to append.</param>
    /// <param name="token">Cancellation token, or null to inherit the simulation task's token.</param>
    public async Task Put(T value, CancellationToken? token = null)
    {
        await slots.Acquire(token);
        queue.Enqueue(value);
        items.Release();
    }

    /// <summary>Wait for an available item, then remove the oldest stored item.</summary>
    /// <param name="token">Cancellation token, or null to inherit the simulation task's token.</param>
    /// <returns>The oldest available item.</returns>
    public async Task<T> Get(CancellationToken? token = null)
    {
        await items.Acquire(token);
        var result = queue.Dequeue();
        slots.Release();
        return result;
    }

    /// <summary>Append immediately if an unreserved slot exists; never waits or advances simulation time.</summary>
    /// <param name="value">Item to append.</param>
    /// <returns>False when all slots are occupied or reserved by pending producers.</returns>
    public bool TryPut(T value)
    {
        if (!slots.TryAcquire()) return false;
        queue.Enqueue(value);
        items.Release();
        return true;
    }

    /// <summary>Remove immediately if an unreserved item exists; never steals from a pending consumer.</summary>
    /// <param name="value">The removed item, or default when no item is available.</param>
    /// <returns>True when an item was removed. False may occur with Count greater than zero during handoff.</returns>
    public bool TryGet([System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T value)
    {
        if (!items.TryAcquire()) { value = default!; return false; }
        value = queue.Dequeue();
        slots.Release();
        return true;
    }
}

public sealed class SimClock
{
    readonly Signal signal;
    ulong high, low;
    public SimClock(Signal signal, ulong period, ulong? highSteps = null)
    {
        if (signal.Width != 1)
            throw new ArgumentException("Clock must be one bit");
        this.signal = signal;
        SetPeriod(period, highSteps);
    }

    /// <summary>Set timing for the next full cycle; the current pulse pair keeps its original timing.</summary>
    public void SetPeriod(ulong period, ulong? highSteps = null)
    {
        signal.Runtime.CheckThread();
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        ulong nextHigh = highSteps ?? period / 2;
        if (nextHigh == 0 || nextHigh >= period)
            throw new ArgumentOutOfRangeException(nameof(highSteps));
        high = nextHigh;
        low = period - high;
    }

    public async Task Run(CancellationToken token)
    {
        signal.WriteBit(false);
        while (true)
        {
            ulong cycleLow = low, cycleHigh = high;
            await Triggers.Timer(cycleLow).WithCancellation(token);
            signal.WriteBit(true);
            await Triggers.Timer(cycleHigh).WithCancellation(token);
            signal.WriteBit(false);
        }
    }
}
