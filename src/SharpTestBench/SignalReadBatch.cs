using System.Globalization;
using System.Buffers;

namespace SharpTestBench;

/// <summary>Reusable ordered snapshot of known unsigned signals of at most 32 bits.</summary>
/// <remarks>Reads occur in one simulator phase and do not advance time. This is not
/// an HDL-atomic transaction. Call in ReadOnly for stable bus sampling. Discard all
/// destination values if Read throws; X/Z values are rejected rather than coerced.</remarks>
public sealed class SignalReadBatch
{
    readonly SimRuntime runtime;
    readonly Signal[] signals;
    readonly nint[] handles;
    readonly uint[] widths;
    /// <summary>Bind an immutable signal list once; all signals must belong to the same runtime.</summary>
    public SignalReadBatch(params Signal[] signals)
    {
        ArgumentNullException.ThrowIfNull(signals);
        runtime = SimRuntime.Active ?? throw new InvalidOperationException("No runtime");
        runtime.CheckThread();
        this.signals = (Signal[])signals.Clone();
        handles = new nint[signals.Length]; widths = new uint[signals.Length];
        for (int i = 0; i < signals.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(signals[i]);
            if (signals[i].Runtime != runtime) throw new ArgumentException("Mixed runtimes");
            int width = signals[i].Width;
            if (width is < 1 or > 32) throw new ArgumentException("Batch values require widths 1..32");
            handles[i] = signals[i].Handle; widths[i] = (uint)width;
        }
    }
    /// <summary>Number of values written by each snapshot.</summary>
    public int Count => handles.Length;
    /// <summary>Read into an exactly sized caller-owned span; no buffers are retained.</summary>
    public unsafe void Read(Span<uint> destination)
    {
        runtime.CheckThread();
        if (destination.Length != Count) throw new ArgumentException("Snapshot destination length mismatch");
        if (runtime.BatchTransfers)
        {
            fixed (nint* h = handles)
            fixed (uint* w = widths)
            fixed (uint* v = destination)
                PackedTransport.Check(Native.cs_v1_read_u32_batch(h, w, v, (uint)Count));
        }
        else
        {
            // Match the native batch all-or-nothing contract even when batching is
            // disabled: never expose a partially updated destination after X/Z.
            uint[]? rented = null;
            Span<uint> scratch = Count <= 32 ? stackalloc uint[Count] :
                (rented = ArrayPool<uint>.Shared.Rent(Count)).AsSpan(0, Count);
            try
            {
                for (int i = 0; i < Count; i++)
                    scratch[i] = uint.Parse(signals[i].ReadBits(), NumberStyles.AllowBinarySpecifier, CultureInfo.InvariantCulture);
                scratch.CopyTo(destination);
            }
            finally
            {
                if (rented is not null) ArrayPool<uint>.Shared.Return(rented);
            }
        }
    }
}
