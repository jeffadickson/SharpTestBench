namespace SharpTestBench;

/// <summary>Immutable nine-state operations; positional indexing and HDL indexing remain explicitly separate.</summary>
public sealed partial class LogicArray : IReadOnlyList<Logic>
{
    /// <summary>Element count for IReadOnlyList; equivalent to Length.</summary>
    public int Count => Length;

    static void ValidateState(Logic value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
    }

    (int Offset, int Length) ValidateSlice(HdlRange range)
    {
        if (range.Ascending != Range.Ascending) throw new ArgumentException("Slice direction differs", nameof(range));
        int length = range.Length;
        if (length == 0) return (0, 0);
        int offset = Range.Offset(range.Left);
        _ = Range.Offset(range.Right);
        return (offset, length);
    }

    static LogicArray Binary(LogicArray left, LogicArray right, Func<Logic, Logic, Logic> operation)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Length != right.Length) throw new ArgumentException("Bitwise operands must have equal lengths", nameof(right));
        char[] result = new char[left.Length];
        for (int i = 0; i < result.Length; i++) result[i] = operation(left[i], right[i]).Symbol();
        // Pair displayed positions; preserve the left declaration even when right bounds differ.
        return new(new string(result), left.Range);
    }

    /// <summary>Nine-state AND by displayed position; lengths must match and the left operand's range is preserved.</summary>
    public static LogicArray operator &(LogicArray left, LogicArray right) => Binary(left, right, NineState.And);

    /// <summary>Nine-state OR by displayed position; lengths must match and the left operand's range is preserved.</summary>
    public static LogicArray operator |(LogicArray left, LogicArray right) => Binary(left, right, NineState.Or);

    /// <summary>Nine-state XOR by displayed position; lengths must match and the left operand's range is preserved.</summary>
    public static LogicArray operator ^(LogicArray left, LogicArray right) => Binary(left, right, NineState.Xor);

    /// <summary>Nine-state inversion, preserving the operand's range and leaving it unchanged.</summary>
    public static LogicArray operator ~(LogicArray value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(new string(value.Select(v => NineState.Not(v).Symbol()).ToArray()), value.Range);
    }

    /// <summary>Return a new value with one HDL-indexed element replaced; this value remains unchanged.</summary>
    public LogicArray WithElement(int hdlIndex, Logic value)
    {
        ValidateState(value);
        int offset = Range.Offset(hdlIndex);
        char[] result = bits.ToCharArray();
        result[offset] = value.Symbol();
        return new(new string(result), Range);
    }

    /// <summary>Return a new value with an inclusive HDL slice replaced by the supplied sequence.</summary>
    /// <param name="range">Same-direction slice; explicit null slices accept only an empty replacement.</param>
    /// <param name="replacement">Valid logic states in displayed order; count must exactly match slice length.</param>
    /// <remarks>The entire replacement is validated before creating the result. Self and overlapping sources
    /// are safe. Neither this value nor a replacement LogicArray is mutated, including on enumeration failure.</remarks>
    public LogicArray WithSlice(HdlRange range, IEnumerable<Logic> replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        var (offset, length) = ValidateSlice(range);
        char[] snapshot = replacement.Select(value => { ValidateState(value); return value.Symbol(); }).ToArray();
        if (snapshot.Length != length) throw new ArgumentException("Slice length mismatch", nameof(replacement));
        char[] result = bits.ToCharArray();
        snapshot.CopyTo(result, offset);
        return new(new string(result), Range);
    }

    /// <summary>Find a state in declaration order, optionally within an inclusive HDL search range.</summary>
    /// <param name="value">Exact nine-state symbol to find; L and 0 (or H and 1) are distinct.</param>
    /// <param name="hdlIndex">Matching HDL index on success; default on failure. Negative indices are valid.</param>
    /// <param name="searchRange">Optional same-direction inclusive bounds.</param>
    /// <returns>True if found; the boolean distinguishes absence from valid index zero or minus one.</returns>
    public bool TryIndexOf(Logic value, out int hdlIndex, HdlRange? searchRange = null)
    {
        ValidateState(value);
        var (offset, length) = ValidateSlice(searchRange ?? Range);
        int found = bits.IndexOf(value.Symbol(), offset, length);
        hdlIndex = found < 0 ? default : Range.Index(found);
        return found >= 0;
    }

    /// <summary>Count occurrences of an exact nine-state symbol; weak and strong states remain distinct.</summary>
    public int CountOf(Logic value)
    {
        ValidateState(value);
        char symbol = value.Symbol();
        return bits.Count(c => c == symbol);
    }

    /// <summary>Enumerate states from the declared left bound to the right bound, in displayed order.</summary>
    public IEnumerator<Logic> GetEnumerator()
    {
        foreach (char symbol in bits) yield return LogicOps.Parse(symbol);
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
