namespace SharpTestBench;
public enum ObjectKind
{
    Unknown = 0,
    Memory = 1,
    Module = 2,
    Array = 6,
    Enum = 7,
    Structure = 8,
    Real = 9,
    Integer = 10,
    String = 11,
    FixedString = 12,
    GenerateArray = 13,
    Package = 14,
    Packed = 15,
    Logic = 16,
    LogicArray = 17
}

/// <summary>A simulator-owned HDL object; access requires the owning simulation thread.</summary>
public partial class HdlObject
{
    internal readonly nint Handle;
    internal readonly SimRuntime Runtime;
    readonly Dictionary<string, HdlObject> named = new(StringComparer.Ordinal);
    readonly Dictionary<int, HdlObject> indexed = new();
    internal HdlObject(SimRuntime runtime, nint handle)
    {
        runtime.CheckThread();
        if (handle == 0)
            throw new ArgumentException("Null HDL handle");
        Runtime = runtime;
        Handle = handle;
    }

    /// <summary>Local object name reported by the simulator, without its parent hierarchy.</summary>
    public string Name
    {
        get
        {
            Runtime.CheckThread();
            return Native.Text(Native.gpi_get_signal_name_str(Handle));
        }
    }

    /// <summary>Simulator-qualified object name; separators are backend-specific.</summary>
    public string FullName
    {
        get
        {
            Runtime.CheckThread();
            return Native.Text(Native.gpi_get_signal_fullname_str(Handle));
        }
    }

    /// <summary>HDL type name reported by the backend; spelling is simulator-specific.</summary>
    public string TypeName
    {
        get
        {
            Runtime.CheckThread();
            return Native.Text(Native.gpi_get_signal_type_str(Handle));
        }
    }

    /// <summary>HDL definition metadata, or null when unavailable.</summary>
    public string? DefinitionName
    {
        get
        {
            Runtime.CheckThread();
            return OptionalText(Native.gpi_get_definition_name(Handle));
        }
    }

    public string? DefinitionFile
    {
        get
        {
            Runtime.CheckThread();
            return OptionalText(Native.gpi_get_definition_file(Handle));
        }
    }

    static string? OptionalText(nint value) => value == 0 ? null : Native.Text(value)is { Length: > 0 } text ? text : null;
    public ObjectKind Kind
    {
        get
        {
            Runtime.CheckThread();
            return (ObjectKind)Native.gpi_get_object_type(Handle);
        }
    }

    public static HdlObject Root(string? name = null)
    {
        var r = SimRuntime.Active ?? throw new InvalidOperationException("No runtime");
        r.CheckThread();
        return new(r, Native.gpi_get_root_handle(name));
    }

    public HdlObject Child(string name)
    {
        Runtime.CheckThread();
        ArgumentNullException.ThrowIfNull(name);
        if (named.TryGetValue(name, out var cached))
            return cached;
        var h = Native.gpi_get_handle_by_name(Handle, name, 0);
        // Some package backends enumerate members but cannot resolve their
        // qualified names through GPI. Use real child handles, never guess separators.
        if (h == 0 && Kind == ObjectKind.Package)
        {
            Children();
            if (named.TryGetValue(name, out cached))
                return cached;
        }
        if (h == 0)
            throw new KeyNotFoundException($"HDL member {name} not found under {Name}");
        var child = new HdlObject(Runtime, h);
        named.Add(name, child);
        return child;
    }

    public HdlObject At(int index)
    {
        Runtime.CheckThread();
        if (indexed.TryGetValue(index, out var cached))
            return cached;
        var child = new HdlObject(Runtime, Native.gpi_get_handle_by_index(Handle, index));
        indexed.Add(index, child);
        return child;
    }

    public IReadOnlyList<HdlObject> Children()
    {
        Runtime.CheckThread();
        var result = new List<HdlObject>();
        var iterator = Native.gpi_iterate(Handle, 1);
        if (iterator == 0)
            return result;
        while (true)
        {
            var h = Native.gpi_next(iterator);
            if (h == 0)
                break;
            var child = new HdlObject(Runtime, h);
            string name = child.Name;
            if (!named.TryGetValue(name, out var cached))
                named.Add(name, cached = child);
            if (!result.Contains(cached))
                result.Add(cached);
        }

        return result;
    }

    public bool? DeclaredSignedness
    {
        get
        {
            Runtime.CheckThread();
            return Native.gpi_is_signed(Handle) switch
            {
                0 => false,
                1 => true,
                _ => null
            };
        }
    }

    public bool IsSigned => DeclaredSignedness ?? throw new NotSupportedException($"Simulator does not expose signedness for {Name}; use Signal<W> with an explicit interpretation");

    public bool IsConstant
    {
        get
        {
            Runtime.CheckThread();
            return Native.gpi_is_constant(Handle) != 0;
        }
    }

    public HdlRange Range
    {
        get
        {
            Runtime.CheckThread();
            int direction = Native.gpi_get_range_dir(Handle);
            if (direction == 0)
                throw new InvalidOperationException($"{Name} has no HDL range");
            return new(Native.gpi_get_range_left(Handle), Native.gpi_get_range_right(Handle), direction > 0);
        }
    }

    public HdlArray<T> Array<T>(Func<HdlObject, T> convert) => new(this, convert);
    public IntegerSignal Integer() => new(Runtime, Handle);
    public EnumSignal<T> Enum<T>()
        where T : struct, Enum => new(this);
    public LogicSignal<W> Logic<W>()
        where W : struct, IWidth => new(this);
    public UnsignedSignal<W> Unsigned<W>()
        where W : struct, IWidth => new(this);
    public SignedSignal<W> Signed<W>()
        where W : struct, IWidth => new(this);
    public Signal Signal() => new(Runtime, Handle);
    public Signal<W> Signal<W>()
        where W : struct, IWidth => new(Runtime, Handle);
    public RealSignal Real() => new(Runtime, Handle);
    public StringSignal String() => new(Runtime, Handle);
}

public class Signal : HdlObject
{
    /// <summary>True when this object uses checked direct VPI vector transport instead of binary text.</summary>
    public bool UsesDirectPackedTransport { get; }
    internal Signal(SimRuntime r, nint h) : base(r, h)
    {
        if (Kind is not (ObjectKind.Logic or ObjectKind.LogicArray or ObjectKind.Integer or ObjectKind.Enum or ObjectKind.Packed))
            throw new ArgumentException($"{Name} is not a logic/integer signal: {Kind}");
        if (PerformanceOptions.DirectPacked && (r.NativeFeatures & 4) != 0
            && r.Capabilities.Family is SimulatorFamily.Icarus or SimulatorFamily.Verilator)
            UsesDirectPackedTransport = Native.cs_v1_enable_direct(h) == 0;
    }

    public int Width
    {
        get
        {
            Runtime.CheckThread();
            return Native.gpi_get_num_elems(Handle);
        }
    }

    public string ReadBits()
    {
        Runtime.CheckThread();
        return Native.Text(Native.gpi_get_signal_value_binstr(Handle)).ToUpperInvariant();
    }

    public LogicArray ReadLogic() => Kind == ObjectKind.Logic ? new(ReadBits()) : new(ReadBits(), Range);
    public void WriteBits(string value, WriteAction action = WriteAction.Deposit)
    {
        Runtime.CheckWrite();
        if (value.Length != Width)
            throw new ArgumentException($"Expected {Width} bits, got {value.Length}");
        string normalized = new LogicArray(value).ToString();
        if (normalized.Any(c => c is not ('0' or '1' or 'X' or 'Z')))
            Runtime.Capabilities.Require(SimulatorFeature.NineStateWrites);
        else if (normalized.Any(c => c is 'X' or 'Z'))
            Runtime.Capabilities.Require(SimulatorFeature.FourStateWrites);
        Runtime.Write(Handle, () => Native.gpi_set_signal_value_binstr(Handle, normalized, (int)action), action, () => normalized);
    }

    public bool ReadBit()
    {
        if (Width != 1)
            throw new InvalidOperationException("Not a scalar");
        return LogicOps.Parse(ReadBits()[0]).ToBool();
    }

    public void WriteBit(bool value, WriteAction action = WriteAction.Deposit)
    {
        if (Width != 1)
            throw new InvalidOperationException("Not a scalar");
        if (Runtime.BatchTransfers && Runtime.Capabilities.Family is SimulatorFamily.Icarus or SimulatorFamily.Verilator)
        {
            int raw = value ? 1 : 0;
            Runtime.Write(Handle, () => Native.gpi_set_signal_value_int(Handle, raw, (int)action), action,
                () => value ? "1" : "0", raw);
        }
        else WriteBits(value ? "1" : "0", action);
    }

    public void Release() => WriteBits(new string ('0', Width), WriteAction.Release);
}

public sealed class Signal<W> : Signal where W : struct, IWidth
{
    internal Signal(SimRuntime r, nint h) : base(r, h)
    {
        if (Width != Width<W>.Bits)
            throw new ArgumentException($"HDL signal {Name} is {Width} bits, expected {Width<W>.Bits}");
    }

    public LogicVector<W> Read()
    {
        Runtime.CheckThread();
        return PackedTransport.Read<W>(Handle);
    }

    public UInt<W> ReadUInt()
    {
        if (Width<W>.Bits > 64)
            return Read().ToUInt();
        try
        {
            return new UInt<W>(ulong.Parse(ReadBits(), System.Globalization.NumberStyles.AllowBinarySpecifier, System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (FormatException e)
        {
            throw new InvalidOperationException("Cannot convert unresolved X/Z to integer", e);
        }
    }

    public SInt<W> ReadSInt() => Read().ToSInt();
    public void Write(UInt<W> value, WriteAction action = WriteAction.Deposit)
    {
        if (Width<W>.Bits <= 32)
        {
            int raw = unchecked((int)(uint)value.Value);
            Runtime.Write(Handle, () => Native.gpi_set_signal_value_int(Handle, raw, (int)action), action, () => LogicVector<W>.From(value).ToString(), raw);
        }
        else
            Write(LogicVector<W>.From(value), action);
    }

    public void Write(SInt<W> value, WriteAction action = WriteAction.Deposit) => Write(value.RawBits, action);
    public void Write(LogicVector<W> value, WriteAction action = WriteAction.Deposit)
    {
        Runtime.CheckWrite();
        if (!value.IsKnown)
            Runtime.Capabilities.Require(SimulatorFeature.FourStateWrites);
        // Capture immutable planes; temporary encoding storage is acquired only when applied.
        Runtime.Write(Handle, () => PackedTransport.Write(Handle, value, action), action, () => value.ToString());
    }

    public PackedField<W, F> Field<F>(int lsb)
        where F : struct, IWidth => new(this, lsb);
    public PackedArray<W, E> Packed<E>(params HdlRange[] dimensions)
        where E : struct, IWidth => new(this, dimensions);

    public PackedStruct<W> Struct(PackedStructLayout layout) => new(this, layout);

    /// <summary>Automatically index bits using the simulator-visible range.</summary>
    public PackedArray<W, W1> PackedBits()
    {
        var discovery = DiscoverPackedLayout();
        if (discovery.Layout is not { } layout)
            throw new NotSupportedException(discovery.Explanation);
        return new(this, layout.Dimensions.ToArray());
    }

    /// <summary>Infer typed elements by consuming complete innermost native packed dimensions.</summary>
    public PackedArray<W, E> PackedInferred<E>() where E : struct, IWidth
    {
        var discovery = DiscoverPackedLayout();
        if (discovery.Source != PackedLayoutSource.NativePackedRanges || discovery.Layout is not { } layout)
            throw new NotSupportedException("Original packed dimensions unavailable; supply explicit dimensions to Packed<E>().");
        var dimensions = layout.Dimensions;
        int rank = dimensions.Count, bits = 1;
        while (rank > 0 && bits < Width<E>.Bits)
            bits = checked(bits * dimensions[--rank].Length);
        if (bits != Width<E>.Bits || rank == 0)
            throw new ArgumentException("Element width must match complete innermost dimensions and leave at least one indexing dimension.");
        return new(this, dimensions.Take(rank).ToArray());
    }
    public void Force(LogicVector<W> value) => Write(value, WriteAction.Force);
}

public sealed class RealSignal : HdlObject
{
    internal RealSignal(SimRuntime r, nint h) : base(r, h)
    {
        if (Kind != ObjectKind.Real)
            throw new ArgumentException("Expected HDL real");
    }

    public double Read()
    {
        Runtime.CheckThread();
        return Native.gpi_get_signal_value_real(Handle);
    }

    public void Write(double value, WriteAction action = WriteAction.Deposit) => Runtime.Write(Handle, () => Native.gpi_set_signal_value_real(Handle, value, (int)action), action);
}

public sealed class StringSignal : HdlObject
{
    internal StringSignal(SimRuntime r, nint h) : base(r, h)
    {
        if (Kind is not (ObjectKind.String or ObjectKind.FixedString))
            throw new ArgumentException("Expected HDL string");
    }

    public string Read()
    {
        Runtime.CheckThread();
        nint pointer = Native.gpi_get_signal_value_str(Handle);
        if (pointer == 0)
            throw new InvalidOperationException("Null native string");
        int length = 0;
        if (Kind == ObjectKind.FixedString)
            length = Native.gpi_get_num_elems(Handle);
        else
            while (System.Runtime.InteropServices.Marshal.ReadByte(pointer, length) != 0)
                length++;
        var bytes = new byte[length];
        System.Runtime.InteropServices.Marshal.Copy(pointer, bytes, 0, length);
        return System.Text.Encoding.Latin1.GetString(bytes);
    }

    public void Write(string value, WriteAction action = WriteAction.Deposit)
    {
        ArgumentNullException.ThrowIfNull(value);
        Runtime.CheckWrite();
        if (value.Any(c => c > 255))
            throw new ArgumentException("HDL strings contain 8-bit characters; value is not Latin-1");
        if (Kind == ObjectKind.FixedString && value.Length != Native.gpi_get_num_elems(Handle))
            throw new ArgumentException("Fixed HDL string length mismatch");
        if (value.Contains('\0'))
        {
            if (Kind != ObjectKind.FixedString)
                Runtime.Capabilities.Require(SimulatorFeature.DynamicStringNul);
            if (IsConstant)
                throw new InvalidOperationException("Cannot write an HDL constant");
            // GPI's whole-string setter is NUL-terminated. Character handles preserve NUL.
            var chars = Enumerable.Range(0, value.Length).Select(i => At(Range.Index(i))).ToArray();
            for (int i = 0; i < chars.Length; i++)
            {
                var character = chars[i];
                // vhpiCharT is C char (signed on the supported Linux x86-64 ABI).
                int ordinal = unchecked((sbyte)(byte)value[i]);
                Runtime.Write(character.Handle, () => Native.gpi_set_signal_value_int(character.Handle, ordinal, (int)action), action);
            }

            return;
        }

        var bytes = System.Text.Encoding.Latin1.GetBytes(value + "\0");
        Runtime.Write(Handle, () => Native.gpi_set_signal_value_str_bytes(Handle, bytes, (int)action), action);
    }
}

/// <summary>Strict unsigned API. Signed writes require an explicit conversion.</summary>
public sealed class UnsignedSignal<W>
    where W : struct, IWidth
{
    public Signal<W> Raw { get; }

    internal UnsignedSignal(HdlObject obj)
    {
        Raw = obj.Signal<W>();
        if (obj.IsSigned)
            throw new ArgumentException($"{obj.Name} is signed");
    }

    public UInt<W> Read() => Raw.ReadUInt();
    public void Write(UInt<W> value, WriteAction action = WriteAction.Deposit) => Raw.Write(value, action);
}

/// <summary>Strict signed API. Unsigned writes require an explicit conversion.</summary>
public sealed class SignedSignal<W>
    where W : struct, IWidth
{
    public Signal<W> Raw { get; }

    internal SignedSignal(HdlObject obj)
    {
        Raw = obj.Signal<W>();
        if (!obj.IsSigned)
            throw new ArgumentException($"{obj.Name} is unsigned");
    }

    public SInt<W> Read() => Raw.ReadSInt();
    public void Write(SInt<W> value, WriteAction action = WriteAction.Deposit) => Raw.Write(value, action);
}
