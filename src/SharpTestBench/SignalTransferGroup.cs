using System.Numerics;

namespace SharpTestBench;

/// <summary>Reusable typed four-state snapshots and deferred deposits for a fixed signal list.</summary>
/// <remarks>Capture requires ReadOnly. Set stages values; Commit submits only staged members
/// in group order through the normal last-write-wins scheduler. Captured and staged values
/// are independent. No operation promises HDL atomicity. Widths are 1..1048576 bits;
/// layouts are limited to 65536 distinct signals and 64 MiB. All calls belong to the simulator thread.</remarks>
public sealed class SignalTransferGroup
{
    readonly SimRuntime runtime;
    readonly Native.PlaneTransfer[] layout;
    readonly Dictionary<nint, int> indices = new();
    readonly byte[] captured, staged;
    readonly bool[] dirty;
    readonly Func<string>?[] previews;
    bool hasCapture;
    readonly bool native;

    /// <summary>Bind distinct live signals and allocate reusable packed storage once; duplicates are rejected.</summary>
    public SignalTransferGroup(params Signal[] signals)
    {
        ArgumentNullException.ThrowIfNull(signals);
        runtime = SimRuntime.Active ?? throw new InvalidOperationException("No runtime");
        runtime.CheckThread();
        if (signals.Length > 65536) throw new ArgumentException("Too many group members", nameof(signals));
        layout = new Native.PlaneTransfer[signals.Length];
        int length = 0;
        for (int i = 0; i < signals.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(signals[i]);
            if (signals[i].Runtime != runtime) throw new ArgumentException("Mixed runtimes", nameof(signals));
            int bits = signals[i].Width;
            if (bits is < 1 or > 1048576) throw new ArgumentException("Unsupported group width", nameof(signals));
            if (!indices.TryAdd(signals[i].Handle, i)) throw new ArgumentException("Duplicate group signal", nameof(signals));
            layout[i] = new() { Signal = signals[i].Handle, Bits = (uint)bits, Offset = (uint)length };
            length = checked(length + 3 * ((bits + 7) / 8));
            if (length > 67108864) throw new ArgumentException("Group exceeds 64 MiB", nameof(signals));
        }
        captured = new byte[length]; staged = new byte[length];
        dirty = new bool[signals.Length]; previews = new Func<string>?[signals.Length];
        native = (runtime.NativeFeatures & 32) != 0 && Environment.GetEnvironmentVariable("STB_TRANSFER_GROUPS") != "0";
    }

    /// <summary>Whether this group uses the negotiated native batch exports; false uses scalar packed calls.</summary>
    public bool UsesNativeBatch => native;
    /// <summary>Number of bound signals, including members with no staged write.</summary>
    public int Count => layout.Length;

    /// <summary>Capture all members in ReadOnly; a transport failure invalidates the entire previous snapshot.</summary>
    public unsafe void Capture()
    {
        runtime.CheckThread();
        if (runtime.Phase != SimPhase.ReadOnly) throw new InvalidOperationException("Capture requires ReadOnly");
        hasCapture = false;
        fixed (byte* buffer = captured)
        {
            if (native)
            {
                fixed (Native.PlaneTransfer* items = layout)
                    PackedTransport.Check(Native.cs_v1_read_plane_batch(items, (uint)Count, buffer, (uint)captured.Length));
            }
            else foreach (var item in layout)
                PackedTransport.Check(Native.cs_v1_read_planes(item.Signal, item.Bits, buffer + item.Offset, Size(item)));
        }
        hasCapture = true;
    }

    /// <summary>Return an immutable typed value from the last successful Capture; performs no simulator read.</summary>
    public LogicVector<W> Get<W>(Signal<W> signal) where W : struct, IWidth
    {
        int index = Index(signal);
        if (!hasCapture) throw new InvalidOperationException("No successful group capture");
        var item = layout[index]; int n = (int)((item.Bits + 7) / 8), offset = (int)item.Offset;
        return new(new BigInteger(captured.AsSpan(offset, n), true),
            new BigInteger(captured.AsSpan(offset + n, n), true), new BigInteger(captured.AsSpan(offset + 2 * n, n), true));
    }

    /// <summary>Stage a typed value without scheduling or changing HDL; repeated Set replaces the member.</summary>
    public void Set<W>(Signal<W> signal, LogicVector<W> value) where W : struct, IWidth
    {
        int index = Index(signal);
        if (!value.IsKnown) runtime.Capabilities.Require(SimulatorFeature.FourStateWrites);
        var item = layout[index]; int n = (int)((item.Bits + 7) / 8);
        Span<byte> bytes = staged.AsSpan((int)item.Offset, (int)Size(item)); bytes.Clear();
        value.Value.TryWriteBytes(bytes[..n], out _, true);
        value.Unknown.TryWriteBytes(bytes.Slice(n, n), out _, true);
        value.HighZ.TryWriteBytes(bytes.Slice(2 * n, n), out _, true);
        dirty[index] = true;
        previews[index] = () => value.ToString();
    }

    /// <summary>Stage a known unsigned value with compile-time width checking.</summary>
    public void Set<W>(Signal<W> signal, UInt<W> value) where W : struct, IWidth
        => Set(signal, LogicVector<W>.From(value));

    /// <summary>Discard staged values; already committed deposits retain their immutable payload.</summary>
    public void ClearStaged()
    {
        runtime.CheckThread(); Array.Clear(dirty); Array.Clear(previews);
    }

    /// <summary>Validate all staged members, freeze their values, and submit normal deferred deposits.</summary>
    /// <remarks>ReadOnly is rejected. Commit copies packed storage so later Set/ClearStaged cannot
    /// modify queued writes. Scalar writes may supersede individual members. In ReadWrite the usual
    /// immediate application rule is retained. Cancelling the submitting task does not retract
    /// committed deposits, matching Signal.Write. Backend failures do not roll back earlier writes.</remarks>
    public void Commit()
    {
        runtime.CheckWrite();
        for (int i = 0; i < Count; i++)
            if (dirty[i] && Native.gpi_is_constant(layout[i].Signal) != 0)
                throw new InvalidOperationException("Cannot write an HDL constant");
        if (!dirty.Contains(true)) return;
        var submission = new PackedSubmission(native, (byte[])staged.Clone());
        // Reserve the callback before mutating any per-signal queue entries.
        runtime.PrepareDeposits();
        for (int i = 0; i < Count; i++)
        {
            if (!dirty[i]) continue;
            var deposit = new PackedDeposit(submission, layout[i]);
            runtime.Write(layout[i].Signal, deposit.Apply, WriteAction.Deposit, previews[i], packed: deposit);
        }
        ClearStaged();
    }

    /// <summary>Resolve a typed group member, including equivalent wrappers for the same HDL handle.</summary>
    int Index<W>(Signal<W> signal) where W : struct, IWidth
    {
        runtime.CheckThread(); ArgumentNullException.ThrowIfNull(signal);
        if (signal.Runtime != runtime || !indices.TryGetValue(signal.Handle, out int index)
            || layout[index].Bits != Width<W>.Bits) throw new ArgumentException("Signal is not a matching group member", nameof(signal));
        return index;
    }
    /// <summary>Exact byte extent of one member's three packed planes.</summary>
    internal static uint Size(Native.PlaneTransfer item) => 3 * ((item.Bits + 7) / 8);
}

/// <summary>Immutable submission shared by surviving queue entries; no native pointer escapes a call.</summary>
internal sealed class PackedSubmission(bool native, byte[] bytes)
{
    /// <summary>Submit surviving members in queue order, using old scalar exports if batch support is absent.</summary>
    internal unsafe void Apply(ReadOnlySpan<Native.PlaneTransfer> items)
    {
        fixed (byte* buffer = bytes)
        {
            if (native)
            {
                fixed (Native.PlaneTransfer* descriptors = items)
                    PackedTransport.Check(Native.cs_v1_write_plane_batch(descriptors, (uint)items.Length, buffer, (uint)bytes.Length));
            }
            else foreach (var item in items)
                PackedTransport.Check(Native.cs_v1_write_planes(item.Signal, item.Bits, buffer + item.Offset,
                    SignalTransferGroup.Size(item), (int)WriteAction.Deposit));
        }
    }
}

/// <summary>Per-handle queue entry retaining its immutable submission and member descriptor.</summary>
internal sealed record PackedDeposit(PackedSubmission Owner, Native.PlaneTransfer Item)
{
    /// <summary>Apply this member alone when a scalar barrier or current ReadWrite phase requires it.</summary>
    internal void Apply() => Owner.Apply([Item]);
}
