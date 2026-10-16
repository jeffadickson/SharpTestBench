using System.Collections.Concurrent;

namespace SharpTestBench;
public enum SimPhase
{
    Beginning,
    ValueChange,
    ReadWrite,
    ReadOnly
}

public enum WriteAction
{
    Deposit = 0,
    Force = 1,
    Release = 2,
    Immediate = 3
}

/// <summary>Kind of native simulator callback dispatched by the runtime.</summary>
public enum CallbackKind
{
    Start,
    End,
    Timer,
    Edge,
    ReadWrite,
    ReadOnly,
    NextStep
}

/// <summary>An immutable snapshot of an actual native callback; Time is in simulator steps.</summary>
/// <param name="Kind">Native callback category.</param>
/// <param name="Time">Simulator time when the callback fired.</param>
/// <param name="Trigger">Original native trigger object, or null for an internal runtime callback.</param>
public readonly record struct SimulatorCallbackInfo(CallbackKind Kind, ulong Time, Trigger? Trigger);

public sealed class SimRuntime
{
    internal static SimRuntime? Active;
    readonly int owner = Environment.CurrentManagedThreadId;
    readonly ConcurrentQueue<Action> ready = new();
    readonly Dictionary<nint, Registration> registrations = new();
    readonly record struct PendingWrite(Action Apply, int? Numeric);
    readonly Dictionary<nint, PendingWrite> writes = new();
    readonly HashSet<IDisposable> nativeResources = new();
    internal readonly ulong NativeFeatures;
    internal bool BatchTransfers => PerformanceOptions.BatchTransfers && (NativeFeatures & 8) != 0;
    /// <summary>Root native owners until explicit cancellation/disposal; used in leak detection.</summary>
    internal void OwnNative(IDisposable resource) { CheckThread(); nativeResources.Add(resource); }
    internal void ReleaseNative(IDisposable resource) { CheckThread(); nativeResources.Remove(resource); }
    /// <summary>Dispatch a rare native clock failure in timer context without crossing an exception through C.</summary>
    internal void NativeNotification(Action action)
    {
        CheckThread(); Phase = SimPhase.Beginning; appliedPreviews.Clear(); action(); Drain();
    }
    /// <summary>Stop native timers and release owned VPI handles while the simulator is still alive.</summary>
    void ReleaseNativeResources()
    {
        foreach (var resource in nativeResources.ToArray()) resource.Dispose();
        if ((NativeFeatures & 4) != 0) Native.cs_v1_clear_direct();
    }
    readonly Dictionary<nint, Func<string>> writePreviews = new();
    readonly Dictionary<nint, Func<string>> appliedPreviews = new();
    readonly Native.Callback callback;
    readonly SimContext context;
    long nextId;
    bool draining;
    Registration? writeCallback;
    public SimPhase Phase { get; private set; } = SimPhase.Beginning;
    SimulatorCallbackInfo? lastSimulatorCallback;

    /// <summary>
    /// Last dispatched native callback, including internal deadline/write callbacks.
    /// Null before the first native callback. Managed events and Yield do not change
    /// this snapshot. Read it on the simulator thread; it is runtime-wide, not job-local.
    /// </summary>
    public SimulatorCallbackInfo? LastSimulatorCallback
    {
        get { CheckThread(); return lastSimulatorCallback; }
    }

    string? simulator;
    public string Simulator
    {
        get
        {
            CheckThread();
            return simulator ??= Native.Text(Native.gpi_get_simulator_product());
        }
    }

    SimulatorCapabilities? capabilities;
    public SimulatorCapabilities Capabilities
    {
        get { CheckThread(); return capabilities ??= SimulatorCapabilities.Describe(Simulator, Native.cs_abi_features()); }
    }
    public bool SupportsFourState => Capabilities[SimulatorFeature.FourStateWrites].IsSupported;
    public bool SupportsForceRelease => Capabilities[SimulatorFeature.ForceRelease].IsSupported;
    public bool SupportsNineState => Capabilities[SimulatorFeature.NineStateWrites].IsSupported;
    public int PendingCallbacks => registrations.Values.Count(r => r.Kind != CallbackKind.End) + nativeResources.Count;

    public int Precision
    {
        get
        {
            CheckThread();
            Native.gpi_get_sim_precision(out int p);
            return p;
        }
    }

    public Action<Exception>? FatalError { get; set; }

    readonly AsyncLocal<CancellationToken> cancellationScope = new();
    internal CancellationToken DefaultCancellation { get => cancellationScope.Value; set => cancellationScope.Value = value; }

    public SimRuntime()
    {
        if (Active != null)
            throw new InvalidOperationException("One simulator runtime per process");
        Native.VerifyAbi();
        NativeFeatures = Native.cs_abi_features();
        Active = this;
        callback = Dispatch;
        context = new(this);
    }

    public void CheckThread()
    {
        if (Environment.CurrentManagedThreadId != owner)
            throw new InvalidOperationException("Simulator access requires the simulator thread");
    }

    internal void CheckWrite()
    {
        CheckThread();
        if (Phase == SimPhase.ReadOnly)
            throw new InvalidOperationException("Writes are forbidden in ReadOnly");
    }

    public ulong Now
    {
        get
        {
            CheckThread();
            Native.gpi_get_sim_time(out uint h, out uint l);
            return ((ulong)h << 32) | l;
        }
    }

    public ulong Steps(decimal value, TimeUnit unit = TimeUnit.Nanoseconds, TimeRounding rounding = TimeRounding.Error)
        => ConvertSteps(value, unit, rounding, false);

    internal ulong TimerSteps(decimal value, TimeUnit unit, TimeRounding rounding)
        => ConvertSteps(value, unit, rounding, true);

    ulong ConvertSteps(decimal value, TimeUnit unit, TimeRounding rounding, bool clampTimer)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        decimal scaled = value;
        if (unit != TimeUnit.Step)
        {
            int shift = (int)unit - Precision;
            for (int i = 0; i < Math.Abs(shift); i++)
                scaled = shift > 0 ? checked(scaled * 10) : scaled / 10;
        }

        decimal integral = rounding switch
        {
            TimeRounding.Floor => decimal.Floor(scaled),
            TimeRounding.Ceiling => decimal.Ceiling(scaled),
            TimeRounding.Nearest => decimal.Round(scaled, 0, MidpointRounding.ToEven),
            _ => scaled == decimal.Truncate(scaled) ? scaled : throw new ArgumentException("Time is not an integral simulator step")};
        if (clampTimer && integral == 0) return 1;
        return integral >= 1 ? checked((ulong)integral) : throw new ArgumentOutOfRangeException(nameof(value), "Timer must consume at least one step");
    }

    internal void Enqueue(Action a) => ready.Enqueue(a);
    internal void Drain()
    {
        CheckThread();
        if (draining)
            return;
        draining = true;
        var old = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            int count = 0;
            while (ready.TryDequeue(out var a))
            {
                if (++count > 1000000)
                    throw new InvalidOperationException("Runaway scheduler: one million continuations without returning to simulator");
                a();
            }
        }
        catch (Exception e)
        {
            Fatal(e);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(old);
            draining = false;
        }
    }

    internal void Fatal(Exception e)
    {
        Console.Error.WriteLine(e);
        if (FatalError is { } f)
            f(e);
        else
            Native.gpi_finish();
    }

    int Dispatch(nint id)
    {
        try
        {
            CheckThread();
            if (!registrations.Remove(id, out var r))
                return 0;
            r.Handle = 0;
            lastSimulatorCallback = new(r.Kind, Now, r.Trigger);
            if (r.Kind == CallbackKind.End)
            {
                ReleaseNativeResources();
                r.Action();
                return 0;
            }

            if (r.Kind != CallbackKind.ReadWrite)
                appliedPreviews.Clear();
            Phase = r.Kind switch
            {
                CallbackKind.ReadOnly => SimPhase.ReadOnly,
                CallbackKind.ReadWrite => SimPhase.ReadWrite,
                CallbackKind.Edge => SimPhase.ValueChange,
                _ => SimPhase.Beginning
            };
            if (Phase == SimPhase.ReadWrite)
                FlushWrites();
            Enqueue(r.Action);
            Drain();
            return 0;
        }
        catch (Exception e)
        {
            Fatal(e);
            return -1;
        }
    }

    internal Registration Register(CallbackKind kind, Action action, ulong delay = 0, nint signal = 0, int edge = 0, Trigger? trigger = null)
    {
        CheckThread();
        if ((kind == CallbackKind.ReadOnly || kind == CallbackKind.ReadWrite) && Phase == SimPhase.ReadOnly)
            throw new InvalidOperationException("Cannot reenter this time step from ReadOnly");
        nint id = checked((nint)(++nextId));
        var r = new Registration(this, id, kind, action, trigger);
        registrations.Add(id, r);
        r.Handle = kind switch
        {
            CallbackKind.Start => Native.gpi_register_start_of_sim_time_callback(callback, id),
            CallbackKind.End => Native.gpi_register_end_of_sim_time_callback(callback, id),
            CallbackKind.Timer => Native.gpi_register_timed_callback(callback, id, delay),
            CallbackKind.Edge => Native.gpi_register_value_change_callback(callback, id, signal, edge),
            CallbackKind.ReadOnly => Native.gpi_register_readonly_callback(callback, id),
            CallbackKind.ReadWrite => Native.gpi_register_readwrite_callback(callback, id),
            CallbackKind.NextStep => Native.gpi_register_nexttime_callback(callback, id),
            _ => 0
        };
        if (r.Handle == 0)
        {
            registrations.Remove(id);
            throw new InvalidOperationException($"Cannot register {kind}");
        }

        return r;
    }

    internal string? PendingBits(nint handle)
    {
        CheckThread();
        return writePreviews.TryGetValue(handle, out var preview) || appliedPreviews.TryGetValue(handle, out preview) ? preview() : null;
    }

    internal void Write(nint handle, Action apply, WriteAction action, Func<string>? preview = null, int? numeric = null)
    {
        CheckWrite();
        if (Native.gpi_is_constant(handle) != 0)
            throw new InvalidOperationException("Cannot write an HDL constant");
        if (!Enum.IsDefined(action))
            throw new ArgumentOutOfRangeException(nameof(action));
        if (action == WriteAction.Force || action == WriteAction.Release)
            Capabilities.Require(SimulatorFeature.ForceRelease);
        if (action != WriteAction.Deposit || Phase == SimPhase.ReadWrite)
        {
            apply();
            appliedPreviews.Remove(handle);
            if (Phase == SimPhase.ReadWrite && action is WriteAction.Deposit or WriteAction.Immediate && preview != null)
                appliedPreviews.Add(handle, preview);
            return;
        }

        writeCallback ??= Register(CallbackKind.ReadWrite, () => writeCallback = null);
        writes.Remove(handle);
        writes.Add(handle, new(apply, numeric));
        writePreviews.Remove(handle);
        if (preview != null)
            writePreviews.Add(handle, preview);
    }

    /// <summary>Flush in last-write order, batching only consecutive eligible numeric deposits.</summary>
    unsafe void FlushWrites()
    {
        var items = writes.ToArray();
        var previews = writePreviews.ToArray();
        writes.Clear();
        writePreviews.Clear();
        // Fixed-size chunks bound stack use. Non-numeric writes remain ordering barriers.
        Span<Native.U32Write> batch = stackalloc Native.U32Write[64];
        int index = 0;
        while (index < items.Length)
        {
            if (!BatchTransfers || items[index].Value.Numeric is null)
            {
                items[index++].Value.Apply();
                continue;
            }
            int first = index, count = 0;
            while (index < items.Length && count < batch.Length && items[index].Value.Numeric is int raw)
            {
                batch[count++] = new() { Signal = items[index].Key, Value = unchecked((uint)raw), Action = (int)WriteAction.Deposit };
                index++;
            }
            if (count == 1) items[first].Value.Apply();
            else fixed (Native.U32Write* p = batch) PackedTransport.Check(Native.cs_v1_write_u32_batch(p, (uint)count));
        }
        foreach (var entry in previews) appliedPreviews[entry.Key] = entry.Value;
    }

    internal void DiscardWrites()
    {
        CheckThread();
        writes.Clear();
        writePreviews.Clear();
        appliedPreviews.Clear();
        writeCallback?.Dispose();
        writeCallback = null;
    }

    public void Finish()
    {
        CheckThread();
        ReleaseNativeResources();
        Native.gpi_finish();
    }

    internal sealed class Registration : IDisposable
    {
        readonly SimRuntime runtime;
        readonly nint id;
        internal readonly CallbackKind Kind;
        internal readonly Action Action;
        internal readonly Trigger? Trigger;
        internal nint Handle;
        internal Registration(SimRuntime r, nint i, CallbackKind k, Action a, Trigger? trigger)
        {
            runtime = r;
            id = i;
            Kind = k;
            Action = a;
            Trigger = trigger;
        }

        public void Dispose()
        {
            runtime.CheckThread();
            if (Handle == 0)
                return;
            var h = Handle;
            Handle = 0;
            runtime.registrations.Remove(id);
            if (Native.gpi_remove_cb(h) != 0)
                throw new InvalidOperationException("Native callback deregistration failed");
        }
    }

    sealed class SimContext(SimRuntime runtime) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => runtime.Enqueue(() => d(state));
        public override void Send(SendOrPostCallback d, object? state)
        {
            runtime.CheckThread();
            d(state);
        }
    }
}

public enum TimeUnit
{
    Step = 100,
    Seconds = 0,
    Milliseconds = -3,
    Microseconds = -6,
    Nanoseconds = -9,
    Picoseconds = -12,
    Femtoseconds = -15
}

public enum TimeRounding
{
    Error,
    Floor,
    Ceiling,
    Nearest
}
