namespace SharpTestBench;
/// <summary>Native HDL integer / enumeration access, not a fabricated packed width.</summary>
public sealed class IntegerSignal : HdlObject
{
    internal IntegerSignal(SimRuntime runtime, nint handle) : base(runtime, handle)
    {
        if (Kind is not (ObjectKind.Integer or ObjectKind.Enum))
            throw new ArgumentException("Expected HDL integer or enumeration");
    }

    public long Read()
    {
        Runtime.CheckThread();
        return Native.gpi_get_signal_value_long(Handle);
    }

    // The pinned GPI setter accepts int32 even where the native getter uses C long.
    public void Write(int value, WriteAction action = WriteAction.Deposit) => Runtime.Write(Handle, () => Native.gpi_set_signal_value_int(Handle, value, (int)action), action);
}

/// <summary>Maps explicit C# enum numeric values to HDL enum ordinals.</summary>
public sealed class EnumSignal<T>
    where T : struct, Enum
{
    public IntegerSignal Raw { get; }

    internal EnumSignal(HdlObject obj) => Raw = obj.Integer();
    public T Read()
    {
        long ordinal = Raw.Read();
        foreach (T value in System.Enum.GetValues<T>())
            if (Convert.ToInt64(value) == ordinal)
                return value;
        throw new InvalidOperationException($"HDL ordinal {ordinal} is not defined in {typeof(T).Name}");
    }

    public void Write(T value, WriteAction action = WriteAction.Deposit)
    {
        if (!System.Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        Raw.Write(checked((int)Convert.ToInt64(value)), action);
    }
}

/// <summary>Width-checked nine-state signal, preserving U/X/0/1/Z/W/L/H/-.</summary>
public sealed class LogicSignal<W>
    where W : struct, IWidth
{
    public Signal<W> Raw { get; }

    internal LogicSignal(HdlObject obj) => Raw = obj.Signal<W>();
    public LogicArray Read() => Raw.ReadLogic();
    public void Write(LogicArray value, WriteAction action = WriteAction.Deposit) => Raw.WriteBits(value.ToString(), action);
}
