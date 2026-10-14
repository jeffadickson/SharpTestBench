using System.Collections;

namespace SharpTestBench;
/// <summary>Array addressed by declared HDL indices, with explicit positional access.</summary>
public sealed class ValueArray<T> : IEnumerable<T>
{
    readonly T[] values;
    public HdlRange Range { get; }
    public int Length => values.Length;

    public ValueArray(IEnumerable<T> values, HdlRange range)
    {
        this.values = values.ToArray();
        if (this.values.Length != range.Length)
            throw new ArgumentException("Range length mismatch");
        Range = range;
    }

    public T this[int hdlIndex] { get => values[Range.Offset(hdlIndex)]; set => values[Range.Offset(hdlIndex)] = value; }

    public T ByOffset(int offset) => values[offset];
    public ValueArray<T> Slice(HdlRange range)
    {
        if (range.Ascending != Range.Ascending)
            throw new ArgumentException("Slice direction differs");
        return new(Enumerable.Range(0, range.Length).Select(i => this[range.Index(i)]), range);
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)values).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
