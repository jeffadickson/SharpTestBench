using System.Collections;

namespace SharpTestBench;
/// <summary>Fixed-size mutable array addressed by declared HDL indices, with explicit positional access.</summary>
/// <typeparam name="T">Stored element type; equality defaults to EqualityComparer&lt;T&gt;.Default.</typeparam>
public sealed class ValueArray<T> : IEnumerable<T>
{
    readonly T[] values;
    HdlRange range;
    /// <summary>Declared indices. Reassignment must preserve length and never reorders stored values.</summary>
    public HdlRange Range
    {
        get => range;
        set
        {
            if (value.Length != values.Length) throw new ArgumentException("Range length mismatch", nameof(value));
            range = value;
        }
    }
    /// <summary>Number of stored elements, including zero for a null range.</summary>
    public int Length => values.Length;

    /// <summary>Copy values in iteration order and assign the specified HDL range.</summary>
    /// <exception cref="ArgumentException">The number of values differs from the range length.</exception>
    public ValueArray(IEnumerable<T> values, HdlRange range)
    {
        ArgumentNullException.ThrowIfNull(values);
        this.values = values.ToArray();
        Range = range;
    }

    /// <summary>Read or write one element by its HDL index, which can be negative.</summary>
    public T this[int hdlIndex] { get => values[Range.Offset(hdlIndex)]; set => values[Range.Offset(hdlIndex)] = value; }

    /// <summary>Read an element by its zero-based position from the declared left bound.</summary>
    public T ByOffset(int offset) => values[offset];

    (int Offset, int Length) ValidateSlice(HdlRange slice)
    {
        if (slice.Ascending != Range.Ascending) throw new ArgumentException("Slice direction differs", nameof(slice));
        int length = slice.Length;
        // Retain the existing explicit null-slice contract: empty ranges need no in-bounds endpoints.
        if (length == 0) return (0, 0);
        int offset = Range.Offset(slice.Left);
        _ = Range.Offset(slice.Right);
        return (offset, length);
    }

    /// <summary>Copy an inclusive HDL slice. Direction must match; null slices produce empty arrays.</summary>
    public ValueArray<T> Slice(HdlRange range)
    {
        var (offset, length) = ValidateSlice(range);
        return new(values.AsSpan(offset, length).ToArray(), range);
    }

    /// <summary>Replace an inclusive HDL slice after snapshotting and validating all replacement values.</summary>
    /// <remarks>Aliased/self sources are safe. Bounds, length and enumeration failures cause no writes
    /// by this operation. Side effects performed by the supplied enumerable itself cannot be undone.</remarks>
    /// <exception cref="ArgumentException">Direction or replacement length does not match.</exception>
    public void SetSlice(HdlRange range, IEnumerable<T> replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        var (offset, length) = ValidateSlice(range);
        T[] snapshot = replacement.ToArray();
        if (snapshot.Length != length) throw new ArgumentException("Slice length mismatch", nameof(replacement));
        snapshot.CopyTo(values, offset);
    }

    /// <summary>Find the first matching element in declaration order, optionally within an inclusive HDL slice.</summary>
    /// <param name="value">Value to find, including null when permitted by T.</param>
    /// <param name="hdlIndex">Matching HDL index on success; default on failure. Use the boolean result.</param>
    /// <param name="searchRange">Search bounds, or null for the whole array.</param>
    /// <param name="comparer">Optional equality comparer; defaults to EqualityComparer&lt;T&gt;.Default.</param>
    /// <returns>True on a match. Negative indices, including -1, are valid successes.</returns>
    public bool TryIndexOf(T value, out int hdlIndex, HdlRange? searchRange = null, IEqualityComparer<T>? comparer = null)
    {
        var (offset, length) = ValidateSlice(searchRange ?? Range);
        comparer ??= EqualityComparer<T>.Default;
        for (int i = offset; i < offset + length; i++)
            if (comparer.Equals(values[i], value)) { hdlIndex = Range.Index(i); return true; }
        hdlIndex = default;
        return false;
    }

    /// <summary>Count matching values using the supplied comparer or EqualityComparer&lt;T&gt;.Default.</summary>
    public int CountOf(T value, IEqualityComparer<T>? comparer = null)
    {
        comparer ??= EqualityComparer<T>.Default;
        int count = 0;
        foreach (T item in values) if (comparer.Equals(item, value)) count++;
        return count;
    }

    /// <summary>Enumerate values from the declared left bound toward the right bound.</summary>
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)values).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
