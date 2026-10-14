using System.Collections;

namespace SharpTestBench;
/// <summary>Lazy, cached bindings addressed by declared HDL indices.</summary>
public sealed class HdlArray<T> : IReadOnlyCollection<T>
{
    readonly HdlObject root;
    readonly Func<HdlObject, T> convert;
    readonly Dictionary<int, T> elements = new();
    public HdlRange Range { get; }
    public int Count => Range.Length;

    public HdlArray(HdlObject root, Func<HdlObject, T> convert)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(convert);
        if (root.Kind is not (ObjectKind.Array or ObjectKind.Memory or ObjectKind.GenerateArray))
            throw new ArgumentException("Expected HDL array");
        this.root = root;
        this.convert = convert;
        Range = root.Range;
    }

    public T this[int index]
    {
        get
        {
            root.Runtime.CheckThread();
            Range.Offset(index);
            if (!elements.TryGetValue(index, out var value))
                elements.Add(index, value = convert(root.At(index)));
            return value;
        }
    }

    public T ByOffset(int offset) => this[Range.Index(offset)];
    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
            yield return ByOffset(i);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
