namespace SharpTestBench;

/// <summary>HDL port direction relative to the declaring module, obtained from simulator metadata.</summary>
public enum PortDirection
{
    /// <summary>Metadata is unavailable or the object is not a port; these cases are not inferred.</summary>
    Unknown = 0,
    /// <summary>The module receives this port.</summary>
    Input = 1,
    /// <summary>The module drives this port.</summary>
    Output = 2,
    /// <summary>The module can both receive and drive this port.</summary>
    InOut = 3
}

public partial class HdlObject
{
    /// <summary>Query authoritative port direction, or Unknown for unavailable metadata/non-port objects.</summary>
    /// <remarks>Older native hosts and unverified simulator families return Unknown. Direction
    /// does not imply a prohibition on verification force/release or internal-signal writes.</remarks>
    public PortDirection Direction
    {
        get
        {
            Runtime.CheckThread();
            if ((Runtime.NativeFeatures & 64) == 0) return PortDirection.Unknown;
            return (PortDirection)Native.cs_v1_port_direction(FullName);
        }
    }

    /// <summary>Validate an expected HDL direction before constructing a typed binding.</summary>
    /// <returns>This object for fluent binding construction.</returns>
    /// <exception cref="NotSupportedException">The simulator cannot supply the required metadata.</exception>
    /// <exception cref="InvalidOperationException">The known direction differs from the expectation.</exception>
    public HdlObject RequireDirection(PortDirection expected)
    {
        Runtime.CheckThread();
        if (expected is < PortDirection.Input or > PortDirection.InOut)
            throw new ArgumentOutOfRangeException(nameof(expected));
        var actual = Direction;
        if (actual == PortDirection.Unknown)
            throw new NotSupportedException($"Port direction metadata unavailable for {FullName}");
        if (actual != expected)
            throw new InvalidOperationException($"Port {FullName}: expected {expected}, simulator reports {actual}");
        return this;
    }
}
