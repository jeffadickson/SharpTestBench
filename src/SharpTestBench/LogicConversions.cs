using System.Numerics;

namespace SharpTestBench;

/// <summary>Explicit treatment of nonbinary logic; never applied implicitly to integer conversions.</summary>
public enum LogicResolution
{
    /// <summary>Map L/H to 0/1 and W to X; preserve other unknown symbols (pinned cocotb behavior).</summary>
    Weak,
    /// <summary>Map L/H to 0/1 and all remaining unknowns to zero.</summary>
    Zeros,
    /// <summary>Map L/H to 0/1 and all remaining unknowns to one.</summary>
    Ones,
    /// <summary>Map L/H to 0/1 and resolve remaining unknowns using a caller-supplied Random.</summary>
    Random
}

/// <summary>Byte order for logic-vector serialization, independent of HDL index direction.</summary>
public enum ByteOrder
{
    /// <summary>Most significant byte first.</summary>
    BigEndian,
    /// <summary>Least significant byte first.</summary>
    LittleEndian
}

public static partial class LogicOps
{
    /// <summary>True for 0, 1, L and H, which have an unambiguous binary value.</summary>
    public static bool IsResolvable(this Logic value) => value is Logic.Zero or Logic.One or Logic.L or Logic.H;

    internal static void ValidateResolution(LogicResolution resolution, Random? random)
    {
        if (!Enum.IsDefined(resolution)) throw new ArgumentOutOfRangeException(nameof(resolution));
        if (resolution == LogicResolution.Random && random is null) throw new ArgumentNullException(nameof(random));
    }

    /// <summary>Resolve one valid nine-state value using an explicit policy.</summary>
    /// <param name="value">Logic state to resolve.</param>
    /// <param name="resolution">Weak, zeros, ones or random policy.</param>
    /// <param name="random">Required for random resolution; one Next(2) call per unresolved state.</param>
    /// <returns>A new state; weak resolution can still contain unknowns.</returns>
    public static Logic Resolve(this Logic value, LogicResolution resolution, Random? random = null)
    {
        ValidateResolution(resolution, random);
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (value.IsResolvable()) return value.ToBool() ? Logic.One : Logic.Zero;
        return resolution switch
        {
            LogicResolution.Weak => value == Logic.W ? Logic.X : value,
            LogicResolution.Zeros => Logic.Zero,
            LogicResolution.Ones => Logic.One,
            LogicResolution.Random => random!.Next(2) == 0 ? Logic.Zero : Logic.One,
            _ => throw new ArgumentOutOfRangeException(nameof(resolution))
        };
    }
}

public sealed partial class LogicArray
{
    /// <summary>True when every element is 0, 1, L or H; true for the empty vector.</summary>
    public bool IsResolvable => bits.All(c => LogicOps.Parse(c).IsResolvable());

    /// <summary>Return an independently ranged immutable value without changing its displayed bit order.</summary>
    /// <exception cref="ArgumentException">The new range length differs from this vector's length.</exception>
    public LogicArray WithRange(HdlRange range) => new(bits, range);

    /// <summary>Resolve each element from left to right, preserving range and leaving this value unchanged.</summary>
    /// <remarks>Random resolution requires an explicit generator; no Python PRNG stream equivalence is implied.</remarks>
    public LogicArray Resolve(LogicResolution resolution, Random? random = null)
    {
        LogicOps.ValidateResolution(resolution, random);
        return new(new string(bits.Select(c => LogicOps.Parse(c).Resolve(resolution, random).Symbol()).ToArray()), Range);
    }

    static int NumericLength(HdlRange range)
    {
        int length = range.Length;
        if (length == 0) throw new ArgumentException("Numeric logic vectors must not be empty", nameof(range));
        return length;
    }

    static LogicArray FromBits(BigInteger value, HdlRange range)
    {
        byte[] bytes = value.ToByteArray(isUnsigned: true, isBigEndian: false);
        char[] text = new char[range.Length];
        for (int i = 0; i < text.Length; i++)
            text[text.Length - 1 - i] = i / 8 < bytes.Length && (bytes[i / 8] & (1 << (i % 8))) != 0 ? '1' : '0';
        return new(new string(text), range);
    }

    /// <summary>Construct an unsigned, leftmost-MSB value with the given HDL bounds.</summary>
    /// <param name="value">Nonnegative integer; negative values always fail, even with wrapping.</param>
    /// <param name="range">Nonempty inclusive HDL range.</param>
    /// <param name="wrap">True to discard excess significant bits; false to reject overflow.</param>
    /// <exception cref="OverflowException">The value is negative or does not fit without wrapping.</exception>
    public static LogicArray FromUnsigned(BigInteger value, HdlRange range, bool wrap = false)
    {
        int length = NumericLength(range);
        BigInteger modulus = BigInteger.One << length;
        if (value < 0 || (!wrap && value >= modulus)) throw new OverflowException("Unsigned value does not fit logic range");
        return FromBits(value & (modulus - 1), range);
    }

    /// <summary>Construct a two's-complement, leftmost-MSB value with the given HDL bounds.</summary>
    /// <param name="value">Signed integer.</param>
    /// <param name="range">Nonempty inclusive HDL range.</param>
    /// <param name="wrap">True to retain low bits modulo 2^Length; false to reject signed overflow.</param>
    /// <exception cref="OverflowException">The value does not fit the signed range without wrapping.</exception>
    public static LogicArray FromSigned(BigInteger value, HdlRange range, bool wrap = false)
    {
        int length = NumericLength(range);
        BigInteger limit = BigInteger.One << (length - 1);
        if (!wrap && (value < -limit || value >= limit)) throw new OverflowException("Signed value does not fit logic range");
        return FromBits(value & ((limit << 1) - 1), range);
    }

    /// <summary>Interpret the leftmost bit as the MSB; L/H map to 0/1 and other unresolved states throw.</summary>
    /// <exception cref="InvalidOperationException">The value is empty or contains unresolved states.</exception>
    public BigInteger ToUnsigned()
    {
        if (Length == 0) throw new InvalidOperationException("Cannot convert an empty logic vector to integer");
        byte[] bytes = new byte[(Length - 1) / 8 + 1];
        for (int i = 0; i < Length; i++)
            if (LogicOps.Parse(bits[Length - 1 - i]).ToBool()) bytes[i / 8] |= (byte)(1 << (i % 8));
        return new BigInteger(bytes, isUnsigned: true, isBigEndian: false);
    }

    /// <summary>Interpret the leftmost bit as the two's-complement sign bit; reject empty/unresolved values.</summary>
    public BigInteger ToSigned()
    {
        BigInteger value = ToUnsigned();
        return LogicOps.Parse(bits[0]).ToBool() ? value - (BigInteger.One << Length) : value;
    }

    static bool BigEndian(ByteOrder byteOrder)
    {
        if (!Enum.IsDefined(byteOrder)) throw new ArgumentOutOfRangeException(nameof(byteOrder));
        return byteOrder == ByteOrder.BigEndian;
    }

    /// <summary>Serialize ceil(Length/8) bytes, retaining leading zero bytes and zero-padding the high partial byte.</summary>
    /// <param name="byteOrder">Required byte order; independent of HDL ascending/descending direction.</param>
    /// <exception cref="InvalidOperationException">The vector is empty or contains unresolved states.</exception>
    public byte[] ToBytes(ByteOrder byteOrder)
    {
        bool bigEndian = BigEndian(byteOrder);
        byte[] raw = ToUnsigned().ToByteArray(isUnsigned: true, isBigEndian: bigEndian);
        byte[] result = new byte[(Length - 1) / 8 + 1];
        raw.CopyTo(result, bigEndian ? result.Length - raw.Length : 0);
        return result;
    }

    /// <summary>Construct a binary value from explicit-endian bytes; optional bounds must match exactly 8 bits per byte.</summary>
    /// <param name="value">Nonempty bytes interpreted as an unsigned integer.</param>
    /// <param name="byteOrder">Required byte order.</param>
    /// <param name="range">Optional HDL range; defaults to descending Length*8-1 downto 0.</param>
    /// <exception cref="ArgumentException">Input is empty or the supplied bounds have the wrong length.</exception>
    public static LogicArray FromBytes(ReadOnlySpan<byte> value, ByteOrder byteOrder, HdlRange? range = null)
    {
        bool bigEndian = BigEndian(byteOrder);
        int length = checked(value.Length * 8);
        HdlRange actualRange = range ?? HdlRange.DownTo(length - 1, 0);
        if (length == 0 || actualRange.Length != length) throw new ArgumentException("Byte length must match a nonempty logic range", nameof(value));
        return FromUnsigned(new BigInteger(value, isUnsigned: true, isBigEndian: bigEndian), actualRange);
    }
}
