using System.Buffers;
using System.Numerics;

namespace SharpTestBench;

/// <summary>Linear four-state text conversion without per-bit BigInteger temporaries.</summary>
internal static class VectorText
{
    /// <summary>Decode MSB-first text into three little-endian byte planes, then copy into immutable integers.</summary>
    internal static LogicVector<W> Parse<W>(string text) where W : struct, IWidth
    {
        int n = (Width<W>.Bits + 7) / 8, length = 3 * n;
        byte[]? rented = null;
        Span<byte> bytes = length <= 1536 ? stackalloc byte[length]
            : (rented = ArrayPool<byte>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            bytes.Clear();
            for (int pos = 0; pos < text.Length; pos++)
            {
                int bit = text.Length - 1 - pos, offset = bit >> 3;
                byte mask = (byte)(1 << (bit & 7));
                switch (text[pos])
                {
                    case '0': break;
                    case '1': bytes[offset] |= mask; break;
                    case 'x': case 'X': bytes[n + offset] |= mask; break;
                    case 'z': case 'Z': bytes[n + offset] |= mask; bytes[2 * n + offset] |= mask; break;
                    default: throw new FormatException("Four-state vector accepts only 01XZ");
                }
            }
            return new(new BigInteger(bytes[..n], true), new BigInteger(bytes.Slice(n, n), true),
                new BigInteger(bytes.Slice(2 * n, n), true));
        }
        finally { if (rented is not null) ArrayPool<byte>.Shared.Return(rented); }
    }

    /// <summary>Allocate the final string once; inspect each encoded byte rather than shifting arbitrary-precision values per bit.</summary>
    internal static string Format<W>(LogicVector<W> value) where W : struct, IWidth
        => string.Create(Width<W>.Bits, value, static (text, state) =>
        {
            int n = (text.Length + 7) / 8, length = 3 * n;
            byte[]? rented = null;
            Span<byte> bytes = length <= 1536 ? stackalloc byte[length]
                : (rented = ArrayPool<byte>.Shared.Rent(length)).AsSpan(0, length);
            try
            {
                bytes.Clear();
                state.Value.TryWriteBytes(bytes[..n], out _, true);
                state.Unknown.TryWriteBytes(bytes.Slice(n, n), out _, true);
                state.HighZ.TryWriteBytes(bytes.Slice(2 * n, n), out _, true);
                for (int bit = 0; bit < text.Length; bit++)
                {
                    int offset = bit >> 3, mask = 1 << (bit & 7);
                    text[text.Length - 1 - bit] = (bytes[2 * n + offset] & mask) != 0 ? 'Z'
                        : (bytes[n + offset] & mask) != 0 ? 'X' : (bytes[offset] & mask) != 0 ? '1' : '0';
                }
            }
            finally { if (rented is not null) ArrayPool<byte>.Shared.Return(rented); }
        });
}
