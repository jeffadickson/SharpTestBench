namespace SharpTestBench;
/// <summary>Explicit HDL packed dimensions, outermost first. Rightmost element is least significant.</summary>
public sealed class PackedLayout
{
    readonly HdlRange[] dimensions;
    public IReadOnlyList<HdlRange> Dimensions { get; }
    public int ElementBits { get; }
    public int ElementCount { get; }
    public int TotalBits { get; }

    public PackedLayout(int elementBits, params HdlRange[] dimensions)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        if (elementBits < 1 || dimensions.Length == 0)
            throw new ArgumentException("Positive element width and at least one dimension required");
        this.dimensions = (HdlRange[])dimensions.Clone();
        Dimensions = Array.AsReadOnly(this.dimensions);
        ElementBits = elementBits;
        int count = 1;
        foreach (var dimension in this.dimensions)
        {
            if (dimension.IsNull)
                throw new ArgumentException("Packed signal dimensions cannot be empty");
            count = checked(count * dimension.Length);
        }

        ElementCount = count;
        TotalBits = checked(count * elementBits);
        if (TotalBits > 1048576)
            throw new ArgumentOutOfRangeException(nameof(dimensions));
    }

    public int BitOffset(params int[] indices)
    {
        ArgumentNullException.ThrowIfNull(indices);
        if (indices.Length != dimensions.Length)
            throw new ArgumentException("Index count does not match packed rank");
        int offset = 0;
        for (int i = 0; i < indices.Length; i++)
            offset = checked(offset * dimensions[i].Length + dimensions[i].Offset(indices[i]));
        return checked((ElementCount - 1 - offset) * ElementBits);
    }
}

/// <summary>A typed field of a packed signal. Writes are whole-vector deposits composed with pending deposits.</summary>
public sealed class PackedField<Total, Field>
    where Total : struct, IWidth where Field : struct, IWidth
{
    readonly Signal<Total> signal;
    public int BitOffset { get; }

    internal PackedField(Signal<Total> signal, int lsb)
    {
        signal.Runtime.CheckThread();
        if (lsb < 0 || Width<Field>.Bits > Width<Total>.Bits || lsb > Width<Total>.Bits - Width<Field>.Bits)
            throw new ArgumentOutOfRangeException(nameof(lsb));
        this.signal = signal;
        BitOffset = lsb;
    }

    public LogicVector<Field> Read() => signal.Read().Slice<Field>(BitOffset);
    public UInt<Field> ReadUInt() => Read().ToUInt();
    public SInt<Field> ReadSInt() => Read().ToSInt();
    public void Write(UInt<Field> value) => Write(LogicVector<Field>.From(value));
    public void Write(SInt<Field> value) => Write(value.RawBits);
    public void Write(LogicVector<Field> value)
    {
        signal.Runtime.CheckWrite();
        string original = signal.Runtime.PendingBits(signal.Handle) ?? signal.ReadBits();
        int start = Width<Total>.Bits - BitOffset - Width<Field>.Bits;
        string merged = original[..start] + value.ToString() + original[(start + Width<Field>.Bits)..];
        signal.WriteBits(merged);
    }
}

/// <summary>Typed indexing over explicitly supplied packed HDL dimensions.</summary>
public sealed class PackedArray<Total, Element>
    where Total : struct, IWidth where Element : struct, IWidth
{
    readonly Signal<Total> signal;
    public PackedLayout Layout { get; }

    internal PackedArray(Signal<Total> signal, HdlRange[] dimensions)
    {
        this.signal = signal;
        Layout = new(Width<Element>.Bits, dimensions);
        if (Layout.TotalBits != Width<Total>.Bits)
            throw new ArgumentException("Packed layout does not match HDL signal width");
    }

    public PackedField<Total, Element> this[params int[] indices] => signal.Field<Element>(Layout.BitOffset(indices));
}
