using System.Numerics;

namespace SharpTestBench;
public interface IWidth
{
    static abstract int Bits { get; }
}

public readonly struct W1 : IWidth
{
    public static int Bits => 1;
}

public readonly struct W4 : IWidth
{
    public static int Bits => 4;
}

public readonly struct W8 : IWidth
{
    public static int Bits => 8;
}

public readonly struct W16 : IWidth
{
    public static int Bits => 16;
}

public readonly struct W32 : IWidth
{
    public static int Bits => 32;
}

public readonly struct W64 : IWidth
{
    public static int Bits => 64;
}

public readonly struct W128 : IWidth
{
    public static int Bits => 128;
}

public readonly struct W256 : IWidth
{
    public static int Bits => 256;
}

public readonly struct W1024 : IWidth
{
    public static int Bits => 1024;
}

internal static class Width<W>
    where W : struct, IWidth
{
    public static readonly int Bits = Validate();
    public static readonly BigInteger Modulus = BigInteger.One << Bits;
    public static readonly BigInteger Mask = Modulus - 1;
    static int Validate()
    {
        // A user-defined getter must not change between validation and caching.
        int bits = W.Bits;
        return bits > 0 && bits <= 1048576 ? bits : throw new ArgumentOutOfRangeException(nameof(W), "Width must be 1..1048576");
    }
}

public readonly record struct UInt<W>
    where W : struct, IWidth
{
    public BigInteger Value { get; }
    public static int Bits => Width<W>.Bits;

    public UInt(BigInteger value)
    {
        if (value < 0 || value > Width<W>.Mask)
            throw new OverflowException($"{value} does not fit UInt<{Bits}>");
        Value = value;
    }

    public static UInt<W> Wrap(BigInteger value) => new(value & Width<W>.Mask);
    public UInt<V> ResizeChecked<V>()
        where V : struct, IWidth => new(Value);
    public static UInt<W> operator +(UInt<W> a, UInt<W> b) => new(a.Value + b.Value);
    public static UInt<W> operator -(UInt<W> a, UInt<W> b) => new(a.Value - b.Value);
    public static UInt<W> operator *(UInt<W> a, UInt<W> b) => new(a.Value * b.Value);
    public static UInt<W> operator &(UInt<W> a, UInt<W> b) => new(a.Value & b.Value);
    public static UInt<W> operator |(UInt<W> a, UInt<W> b) => new(a.Value | b.Value);
    public static UInt<W> operator ^(UInt<W> a, UInt<W> b) => new(a.Value ^ b.Value);
    public static UInt<W> operator ~(UInt<W> a) => new(a.Value ^ Width<W>.Mask);
    public UInt<W> AddWrap(UInt<W> other) => Wrap(Value + other.Value);
    public UInt<W> SubtractWrap(UInt<W> other) => Wrap(Value - other.Value);
    public override string ToString() => Value.ToString();
}

public readonly record struct SInt<W>
    where W : struct, IWidth
{
    public BigInteger Value { get; }
    public static int Bits => Width<W>.Bits;

    public SInt(BigInteger value)
    {
        var sign = BigInteger.One << (Bits - 1);
        if (value < -sign || value >= sign)
            throw new OverflowException($"{value} does not fit SInt<{Bits}>");
        Value = value;
    }

    public UInt<W> RawBits => UInt<W>.Wrap(Value);

    public static SInt<W> FromBits(UInt<W> bits) => new(bits.Value.TestBit(Bits - 1) ? bits.Value - Width<W>.Modulus : bits.Value);
    public static SInt<W> operator +(SInt<W> a, SInt<W> b) => new(a.Value + b.Value);
    public static SInt<W> operator -(SInt<W> a, SInt<W> b) => new(a.Value - b.Value);
    public SInt<W> AddWrap(SInt<W> b) => FromBits(UInt<W>.Wrap(Value + b.Value));
}

internal static class BigBits
{
    public static bool TestBit(this BigInteger v, int i) => (v & (BigInteger.One << i)) != 0;
}

public enum Logic
{
    U,
    X,
    Zero,
    One,
    Z,
    W,
    L,
    H,
    DontCare
}

public static partial class LogicOps
{
    public static Logic Parse(char c) => char.ToUpperInvariant(c) switch
    {
        'U' => Logic.U,
        'X' => Logic.X,
        '0' => Logic.Zero,
        '1' => Logic.One,
        'Z' => Logic.Z,
        'W' => Logic.W,
        'L' => Logic.L,
        'H' => Logic.H,
        '-' => Logic.DontCare,
        _ => throw new FormatException($"Invalid logic '{c}'")};
    public static char Symbol(this Logic v) => "UX01ZWLH-"[(int)v];
    public static bool ToBool(this Logic v) => v switch
    {
        Logic.Zero or Logic.L => false,
        Logic.One or Logic.H => true,
        _ => throw new InvalidOperationException($"Unresolved logic {v}")};
}

/// <summary>Immutable nine-state values, in displayed MSB-to-LSB order.</summary>
public sealed partial class LogicArray
{
    readonly string bits;
    /// <summary>Number of displayed logic states.</summary>
    public int Length => bits.Length;
    /// <summary>Immutable inclusive HDL bounds and direction.</summary>
    public HdlRange Range { get; }

    /// <summary>Parse a nine-state string with default descending Length-1 downto 0 bounds.</summary>
    public LogicArray(string value) : this(value, new HdlRange((value ?? throw new ArgumentNullException(nameof(value))).Length - 1, 0, false))
    {
    }

    /// <summary>Parse a nine-state string whose length must match the declared range.</summary>
    public LogicArray(string value, HdlRange range)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != range.Length)
            throw new ArgumentException("Range length mismatch");
        Range = range;
        bits = value.ToUpperInvariant();
        foreach (char c in bits)
            LogicOps.Parse(c);
    }

    /// <summary>Read by zero-based displayed position; use At for an HDL index.</summary>
    public Logic this[int offset] => LogicOps.Parse(bits[offset]);
    /// <summary>Read by declared HDL index, including negative indices.</summary>
    public Logic At(int hdlIndex) => this[Range.Offset(hdlIndex)];
    /// <summary>Copy an inclusive same-direction HDL slice; explicit null slices produce empty values.</summary>
    public LogicArray Slice(HdlRange range)
    {
        var (offset, length) = ValidateSlice(range);
        return new(bits.Substring(offset, length), range);
    }

    /// <summary>Return uppercase states in displayed MSB-to-LSB order.</summary>
    public override string ToString() => bits;
}

/// <summary>Four-state vector. Unknown mask covers X and Z; high-Z mask distinguishes Z.</summary>
public readonly record struct LogicVector<W>
    where W : struct, IWidth
{
    public BigInteger Value { get; }
    public BigInteger Unknown { get; }
    public BigInteger HighZ { get; }
    public static int Bits => Width<W>.Bits;
    public bool IsKnown => Unknown.IsZero;

    public LogicVector(BigInteger value, BigInteger unknown = default, BigInteger highZ = default)
    {
        if (value < 0 || unknown < 0 || highZ < 0 || value > Width<W>.Mask || unknown > Width<W>.Mask || (highZ & ~unknown) != 0)
            throw new ArgumentOutOfRangeException();
        Value = value & ~unknown;
        Unknown = unknown;
        HighZ = highZ;
    }

    /// <summary>Parse exactly the declared number of MSB-first 0/1/X/Z characters.</summary>
    public static LogicVector<W> Parse(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (s.Length != Bits)
            throw new ArgumentException($"Expected {Bits} bits, got {s.Length}");
        if (PerformanceOptions.LinearText) return VectorText.Parse<W>(s);
        // Retained reference implementation for controlled performance comparisons.
        BigInteger v = 0, x = 0, z = 0;
        foreach (char ch in s)
        {
            v <<= 1;
            x <<= 1;
            z <<= 1;
            switch (char.ToUpperInvariant(ch))
            {
                case '0':
                    break;
                case '1':
                    v |= 1;
                    break;
                case 'X':
                    x |= 1;
                    break;
                case 'Z':
                    x |= 1;
                    z |= 1;
                    break;
                default:
                    throw new FormatException("Four-state vector accepts only 01XZ");
            }
        }

        return new(v, x, z);
    }

    public UInt<W> ToUInt() => IsKnown ? new(Value) : throw new InvalidOperationException("Cannot convert unresolved X/Z to integer");
    public SInt<W> ToSInt() => SInt<W>.FromBits(ToUInt());
    public LogicVector<V> Slice<V>(int lsb)
        where V : struct, IWidth
    {
        if (lsb < 0 || lsb + Width<V>.Bits > Bits)
            throw new ArgumentOutOfRangeException(nameof(lsb));
        var m = Width<V>.Mask;
        return new((Value >> lsb) & m, (Unknown >> lsb) & m, (HighZ >> lsb) & m);
    }

    /// <summary>Format every bit, preserving leading zeroes and unresolved X/Z states.</summary>
    public override string ToString()
    {
        if (PerformanceOptions.LinearText) return VectorText.Format(this);
        // Retained reference implementation for controlled performance comparisons.
        var s = new char[Bits];
        for (int i = 0; i < Bits; i++)
            s[Bits - 1 - i] = HighZ.TestBit(i) ? 'Z' : Unknown.TestBit(i) ? 'X' : Value.TestBit(i) ? '1' : '0';
        return new(s);
    }

    public static LogicVector<W> operator &(LogicVector<W> a, LogicVector<W> b)
    {
        var mask = Width<W>.Mask;
        var zero = ((~a.Value & ~a.Unknown) | (~b.Value & ~b.Unknown)) & mask;
        var one = a.Value & b.Value;
        return new(one, mask & ~(zero | one));
    }

    public static LogicVector<W> operator |(LogicVector<W> a, LogicVector<W> b)
    {
        var mask = Width<W>.Mask;
        var one = a.Value | b.Value;
        var zero = (~a.Value & ~a.Unknown & ~b.Value & ~b.Unknown) & mask;
        return new(one, mask & ~(zero | one));
    }

    public static LogicVector<W> operator ^(LogicVector<W> a, LogicVector<W> b) => new((a.Value ^ b.Value) & ~(a.Unknown | b.Unknown), a.Unknown | b.Unknown);
    public static LogicVector<W> operator ~(LogicVector<W> a) => new((~a.Value) & ~a.Unknown & Width<W>.Mask, a.Unknown);
    public static LogicVector<W> From(UInt<W> value) => new(value.Value);
    public static LogicVector<W> From(SInt<W> value) => new(value.RawBits.Value);
}

/// <summary>Inclusive HDL bounds with explicit direction and structural equality.</summary>
/// <param name="Left">Declared left bound.</param>
/// <param name="Right">Declared inclusive right bound.</param>
/// <param name="Ascending">True for to, false for downto; inconsistent bounds form a null range.</param>
public readonly record struct HdlRange(int Left, int Right, bool Ascending) : IEnumerable<int>
{
    /// <summary>Create a range with direction inferred from its bounds.</summary>
    public HdlRange(int left, int right) : this(left, right, left <= right)
    {
    }

    /// <summary>Create an ascending range, including a null range when left exceeds right.</summary>
    public static HdlRange To(int left, int right) => new(left, right, true);
    /// <summary>Create a descending range, including a null range when left is below right.</summary>
    public static HdlRange DownTo(int left, int right) => new(left, right, false);
    /// <summary>True when the bounds cannot be traversed in the declared direction.</summary>
    public bool IsNull => Ascending ? Left > Right : Left < Right;
    /// <summary>Element count; throws OverflowException if it exceeds Int32.MaxValue.</summary>
    public int Length => IsNull ? 0 : checked((int)(Math.Abs((long)Left - Right) + 1));

    /// <summary>Extract the declared bounds without discarding the stored direction.</summary>
    public void Deconstruct(out int left, out int right)
    {
        left = Left;
        right = Right;
    }

    /// <summary>Test membership without computing length, including ranges longer than Int32.MaxValue.</summary>
    public bool Contains(int index) => !IsNull && index >= Math.Min(Left, Right) && index <= Math.Max(Left, Right);

    /// <summary>Enumerate declared indices without overflowing at either Int32 boundary.</summary>
    /// <remarks>Enumeration is lazy and supports all Int32 endpoints, even if Length overflows.</remarks>
    public IEnumerator<int> GetEnumerator()
    {
        if (IsNull) yield break;
        int current = Left;
        while (true)
        {
            yield return current;
            if (current == Right) yield break;
            current += Ascending ? 1 : -1;
        }
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Convert an HDL index to a zero-based position; throws on absence or Int32 offset overflow.</summary>
    public int Offset(int index)
    {
        if (!Contains(index))
            throw new IndexOutOfRangeException();
        return checked(Ascending ? index - Left : Left - index);
    }

    /// <summary>Convert a nonnegative position to an HDL index; requires Length to fit in Int32.</summary>
    public int Index(int offset)
    {
        if (offset < 0 || offset >= Length)
            throw new IndexOutOfRangeException();
        return checked(Ascending ? Left + offset : Left - offset);
    }
}
