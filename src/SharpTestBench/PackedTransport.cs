using System.Numerics;
using System.Buffers;

namespace SharpTestBench;
/// <summary>Transfers four-state planes using buffers owned only for the duration of a native call.</summary>
internal static class PackedTransport
{
    /// <summary>Reads into stack or pooled storage and copies planes into immutable integers before returning it.</summary>
    internal static unsafe LogicVector<W> Read<W>(nint handle)
        where W : struct, IWidth
    {
        int n = (Width<W>.Bits + 7) / 8;
        int length = checked(3 * n);
        byte[]? rented = null;
        Span<byte> buffer = length <= 1536 ? stackalloc byte[length]
            : (rented = ArrayPool<byte>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            fixed (byte* p = buffer)
                Check(Native.cs_v1_read_planes(handle, (uint)Width<W>.Bits, p, (uint)length));
            return new(new BigInteger(buffer[..n], true), new BigInteger(buffer.Slice(n, n), true), new BigInteger(buffer.Slice(2 * n, n), true));
        }
        finally
        {
            if (rented is not null) ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Encodes an immutable value at execution time; no rented buffer survives a scheduler turn.</summary>
    /// <remarks>Deferred writes capture the value, so replacement/cancellation cannot leak or reuse a queued buffer.</remarks>
    internal static unsafe void Write<W>(nint handle, LogicVector<W> value, WriteAction action)
        where W : struct, IWidth
    {
        int n = (Width<W>.Bits + 7) / 8;
        int length = checked(3 * n);
        byte[]? rented = null;
        Span<byte> buffer = length <= 1536 ? stackalloc byte[length]
            : (rented = ArrayPool<byte>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            // BigInteger writes only significant bytes; zero all padding and unused planes.
            buffer.Clear();
            if (!value.Value.TryWriteBytes(buffer[..n], out _, true)
                || !value.Unknown.TryWriteBytes(buffer.Slice(n, n), out _, true)
                || !value.HighZ.TryWriteBytes(buffer.Slice(2 * n, n), out _, true))
                throw new InvalidOperationException("Vector does not fit transport buffer");
            fixed (byte* p = buffer)
                Check(Native.cs_v1_write_planes(handle, (uint)Width<W>.Bits, p, (uint)length, (int)action));
        }
        finally
        {
            if (rented is not null) ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Translates the frozen native packed-transport status codes into managed exceptions.</summary>
    internal static void Check(int status)
    {
        if (status == 0)
            return;
        throw status switch
        {
            1 => new ArgumentException("Invalid packed-plane buffer or action"),
            2 => new ArgumentException("HDL width does not match packed-plane width"),
            3 => new FormatException("Four-state vector accepts only 01XZ; use ReadLogic for nine-state values"),
            4 => new OutOfMemoryException("Native vector conversion allocation failed"),
            5 => new InvalidOperationException("Native vector value is null or has an unexpected length"),
            6 => new InvalidOperationException("Cannot write an HDL constant"),
            7 => new InvalidOperationException("Simulator VPI vector operation failed"),
            8 => new InvalidOperationException("Native clock callback registration/removal failed"),
            _ => new InvalidOperationException($"Unknown packed transport status {status}")};
    }
}
