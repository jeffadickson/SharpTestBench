namespace SharpTestBench;

/// <summary>A field declaration in HDL order, most significant first.</summary>
public sealed record PackedMember(string Name, int Bits);
public sealed record PackedMemberPosition(string Name, int Bits, int BitOffset);

/// <summary>Portable packed-struct schema. Offsets are calculated; source declarations are supplied by the caller.</summary>
public sealed class PackedStructLayout
{
    readonly Dictionary<string, PackedMemberPosition> members = new(StringComparer.Ordinal);
    public IReadOnlyList<PackedMemberPosition> Members { get; }
    public int TotalBits { get; }

    public PackedStructLayout(params PackedMember[] declarations)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        if (declarations.Length == 0) throw new ArgumentException("At least one field is required.");
        // Copy first; never retain a caller-owned mutable array.
        var fields = declarations.ToArray();
        foreach (var field in fields)
        {
            ArgumentNullException.ThrowIfNull(field);
            ArgumentException.ThrowIfNullOrWhiteSpace(field.Name);
            if (field.Bits < 1 || field.Bits > 1048576) throw new ArgumentOutOfRangeException(nameof(declarations));
            TotalBits = checked(TotalBits + field.Bits);
            if (TotalBits > 1048576) throw new ArgumentOutOfRangeException(nameof(declarations));
        }
        int remaining = TotalBits;
        var positions = new List<PackedMemberPosition>();
        foreach (var field in fields)
        {
            remaining -= field.Bits;
            var position = new PackedMemberPosition(field.Name, field.Bits, remaining);
            if (!members.TryAdd(field.Name, position)) throw new ArgumentException($"Duplicate packed field: {field.Name}");
            positions.Add(position);
        }
        Members = positions.AsReadOnly();
    }

    public PackedMemberPosition this[string name] => members.TryGetValue(name, out var member)
        ? member : throw new KeyNotFoundException($"Packed field {name} not found.");
}

/// <summary>Named typed fields over a whole packed signal, using a checked schema.</summary>
public sealed class PackedStruct<Total> where Total : struct, IWidth
{
    readonly Signal<Total> signal;
    public PackedStructLayout Layout { get; }
    internal PackedStruct(Signal<Total> signal, PackedStructLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        signal.Runtime.CheckThread();
        if (layout.TotalBits != Width<Total>.Bits) throw new ArgumentException("Struct width does not match signal width.");
        this.signal = signal;
        Layout = layout;
    }
    public PackedField<Total, Field> Field<Field>(string name) where Field : struct, IWidth
    {
        var member = Layout[name];
        if (member.Bits != Width<Field>.Bits) throw new ArgumentException($"Field {name} has {member.Bits} bits, not {Width<Field>.Bits}.");
        return signal.Field<Field>(member.BitOffset);
    }
}
