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

public sealed class SimQueue<T>
{
    readonly Queue<T> queue = new();
    readonly SimSemaphore items = new(0);
    readonly SimSemaphore slots;
    public SimQueue(int capacity = int.MaxValue)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        slots = new(capacity, capacity);
    }

    public int Count => queue.Count;

    public async Task Put(T value, CancellationToken? token = null)
    {
        await slots.Acquire(token);
        queue.Enqueue(value);
        items.Release();
    }

    public async Task<T> Get(CancellationToken? token = null)
    {
        await items.Acquire(token);
        var result = queue.Dequeue();
        slots.Release();
        return result;
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
