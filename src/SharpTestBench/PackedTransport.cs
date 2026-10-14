using System.Numerics;

namespace SharpTestBench;
internal static class PackedTransport
{
    internal static unsafe LogicVector<W> Read<W>(nint handle)
        where W : struct, IWidth
    {
        int n = (Width<W>.Bits + 7) / 8;
        Span<byte> buffer = n <= 256 ? stackalloc byte[3 * n] : new byte[3 * n];
        fixed (byte* p = buffer)
            Check(Native.cs_v1_read_planes(handle, (uint)Width<W>.Bits, p, (uint)buffer.Length));
        return new(new BigInteger(buffer[..n], true), new BigInteger(buffer.Slice(n, n), true), new BigInteger(buffer.Slice(2 * n, n), true));
    }

    internal static byte[] Encode<W>(LogicVector<W> value)
        where W : struct, IWidth
    {
        int n = (Width<W>.Bits + 7) / 8;
        var buffer = new byte[3 * n];
        if (!value.Value.TryWriteBytes(buffer.AsSpan(0, n), out _, true) || !value.Unknown.TryWriteBytes(buffer.AsSpan(n, n), out _, true) || !value.HighZ.TryWriteBytes(buffer.AsSpan(2 * n, n), out _, true))
            throw new InvalidOperationException("Vector does not fit transport buffer");
        return buffer;
    }

    internal static unsafe void Write<W>(nint handle, byte[] buffer, WriteAction action)
        where W : struct, IWidth
    {
        fixed (byte* p = buffer)
            Check(Native.cs_v1_write_planes(handle, (uint)Width<W>.Bits, p, (uint)buffer.Length, (int)action));
    }

    static void Check(int status)
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
            _ => new InvalidOperationException($"Unknown packed transport status {status}")};
    }
}
