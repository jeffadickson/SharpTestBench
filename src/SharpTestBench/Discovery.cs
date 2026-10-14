namespace SharpTestBench;

/// <summary>An empty native iterator cannot distinguish no objects from missing backend support.</summary>
public enum DiscoveryStatus { Observed, Unavailable, Unsupported }

public sealed record ObjectDiscovery(
    DiscoveryStatus Status, IReadOnlyList<HdlObject> Objects, string Explanation);

/// <summary>A bit-addressable view of the range exposed by GPI, not a reconstruction of HDL source types.</summary>
public enum PackedLayoutSource { Unavailable, GpiBitRange, NativePackedRanges }

public sealed record PackedLayoutDiscovery(PackedLayout? Layout, string Explanation,
    PackedLayoutSource Source = PackedLayoutSource.Unavailable)
{
    public bool Available => Layout is not null;
}

public partial class HdlObject
{
    /// <summary>Discover modules from the first GPI implementation providing an iterator.</summary>
    public static ObjectDiscovery Roots() => DiscoverGlobal(5);

    /// <summary>Discover HDL package scopes (not .NET/NuGet packages).</summary>
    public static ObjectDiscovery Packages() => DiscoverGlobal(4);

    public ObjectDiscovery Drivers() => Discover(Runtime, Handle, 2);
    public ObjectDiscovery Loads() => Discover(Runtime, Handle, 3);

    static ObjectDiscovery DiscoverGlobal(int kind)
    {
        var runtime = SimRuntime.Active ?? throw new InvalidOperationException("No runtime");
        return Discover(runtime, 0, kind);
    }

    static ObjectDiscovery Discover(SimRuntime runtime, nint parent, int kind)
    {
        runtime.CheckThread();
        // Restrictions describe pinned adapters/tested products, not universal HDL limitations.
        if (runtime.Simulator.Contains("NVC", StringComparison.OrdinalIgnoreCase))
            return new(DiscoveryStatus.Unsupported, System.Array.Empty<HdlObject>(),
                "The pinned VHPI adapter does not implement this iterator.");
        if (kind is 2 or 3 && runtime.Simulator.Contains("Verilator", StringComparison.OrdinalIgnoreCase))
            return new(DiscoveryStatus.Unsupported, System.Array.Empty<HdlObject>(),
                "The tested Verilator VPI implementation does not implement driver/load iteration.");
        var iterator = Native.gpi_iterate(parent, kind);
        var handles = new List<nint>();
        var seen = new HashSet<nint>();
        // Drain before inspecting names: GPI releases its iterator on exhaustion, with no disposal API.
        if (iterator != 0)
            while (true)
            {
                var handle = Native.gpi_next(iterator);
                if (handle == 0) break;
                if (seen.Add(handle)) handles.Add(handle);
            }
        var objects = handles.Select(h => new HdlObject(runtime, h)).ToArray();
        return new(objects.Length == 0 ? DiscoveryStatus.Unavailable : DiscoveryStatus.Observed,
            System.Array.AsReadOnly(objects), objects.Length == 0
                ? "No objects returned; GPI cannot distinguish an empty relation from unavailable metadata."
                : "Objects returned by the backend; completeness and mixed-language coverage are not guaranteed.");
    }

    /// <summary>Infer a bit layout from native packed dimensions, or fall back to the GPI-visible bit range.</summary>
    public PackedLayoutDiscovery DiscoverPackedLayout()
    {
        Runtime.CheckThread();
        if (Kind is not (ObjectKind.Packed or ObjectKind.LogicArray))
            return new(null, "Only packed/vector handles expose a bit-range layout; structures require native member handles or an explicit layout.");
        int width = Native.gpi_get_num_elems(Handle);
        if (Runtime.Simulator.Contains("Verilator", StringComparison.OrdinalIgnoreCase) && (Native.cs_abi_features() & 2) != 0)
        {
            int[] pairs = new int[128];
            int status = Native.cs_v1_vpi_packed_ranges(FullName, (uint)width, pairs, 64, out uint count);
            if (status == 0 && count > 0 && count <= 64)
            {
                var dimensions = Enumerable.Range(0, (int)count)
                    .Select(i => new HdlRange(pairs[2 * i], pairs[2 * i + 1], pairs[2 * i] <= pairs[2 * i + 1])).ToArray();
                var layout = new PackedLayout(1, dimensions);
                if (layout.TotalBits == width)
                    return new(layout, "Native VPI packed ranges, outermost first, with product checked against the signal width.", PackedLayoutSource.NativePackedRanges);
            }
        }
        int direction = Native.gpi_get_range_dir(Handle);
        if (direction == 0) return new(null, "The backend did not provide a range.");
        var range = Range;
        if (range.IsNull || range.Length != width || width > 1048576)
            return new(null, "The range is empty or does not establish a supported bit layout.");
        return new(new PackedLayout(1, range),
            "Inferred from the GPI-visible bit range. Original packed dimensions and struct field positions may have been flattened and cannot be recovered from this range.", PackedLayoutSource.GpiBitRange);
    }
}
