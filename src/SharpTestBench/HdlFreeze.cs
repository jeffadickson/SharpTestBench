namespace SharpTestBench;

public partial class HdlObject
{
    /// <summary>Force the current committed native value until explicitly released.</summary>
    /// <remarks>Supports logic, packed, integer/enum and real objects on force-capable backends.
    /// Queued deposits are not the captured value and remain queued. Release through the typed signal view.
    /// Strings, containers and constants are rejected. This operation requires the simulator thread and a writable phase.</remarks>
    public void Freeze()
    {
        Runtime.CheckWrite();
        var kind = Kind;
        if (kind is not (ObjectKind.Logic or ObjectKind.LogicArray or ObjectKind.Packed or ObjectKind.Integer or ObjectKind.Enum or ObjectKind.Real))
            throw new NotSupportedException($"Freeze does not support HDL object kind {kind}");
        if (IsConstant) throw new InvalidOperationException("Cannot freeze an HDL constant");
        Runtime.Capabilities.Require(SimulatorFeature.ForceRelease);
        if (kind == ObjectKind.Real)
        {
            var signal = Real();
            double committed = signal.Read();
            signal.Write(committed, WriteAction.Force);
            return;
        }
        var packed = Signal();
        int width = packed.Width;
        if (width < 1) throw new InvalidDataException("Freeze requires a positive native width");
        string bits = packed.ReadBits();
        if (bits.Length != width) throw new InvalidDataException("Native freeze readback width differs from object width");
        packed.WriteBits(bits, WriteAction.Force);
    }
}
