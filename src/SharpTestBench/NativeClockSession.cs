namespace SharpTestBench;

/// <summary>Owns a native clock handle and roots its rare failure callback until successful stop.</summary>
internal sealed class NativeClockSession : IDisposable
{
    readonly SimRuntime runtime;
    readonly Native.Callback failureCallback;
    readonly SimEvent failed = new();
    nint handle;
    /// <summary>Start native edge scheduling; no per-edge managed continuation is registered.</summary>
    internal NativeClockSession(Signal signal, ulong high, ulong low)
    {
        runtime = signal.Runtime;
        failureCallback = _ =>
        {
            try { runtime.NativeNotification(failed.Set); }
            catch (Exception error) { runtime.Fatal(error); }
            return 0;
        };
        handle = Native.cs_v1_clock_start(signal.Handle, high, low, failureCallback, 0, out int status);
        PackedTransport.Check(status);
        if (handle == 0) throw new InvalidOperationException("Native clock returned a null handle");
        runtime.OwnNative(this);
    }
    /// <summary>Wait for cancellation or callback-registration failure, keeping native ownership explicit.</summary>
    internal async Task Wait(CancellationToken token)
    {
        await failed.Wait().WithCancellation(token);
        throw new InvalidOperationException("Native clock callback registration failed");
    }
    /// <summary>Apply a timing change at the next full-cycle boundary.</summary>
    internal void Update(ulong high, ulong low)
    {
        runtime.CheckThread();
        if (handle == 0) throw new ObjectDisposedException(nameof(NativeClockSession));
        PackedTransport.Check(Native.cs_v1_clock_update(handle, high, low));
    }
    /// <summary>Cancel all native callbacks before unrooting the delegate; safe to call repeatedly.</summary>
    public void Dispose()
    {
        runtime.CheckThread();
        if (handle == 0) return;
        PackedTransport.Check(Native.cs_v1_clock_stop(handle));
        handle = 0;
        runtime.ReleaseNative(this);
        GC.KeepAlive(failureCallback);
    }
}
