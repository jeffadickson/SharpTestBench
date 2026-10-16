namespace SharpTestBench;

public enum CapabilityStatus { Supported, Conditional, Unsupported, Unverified }
public enum SimulatorFamily { Unknown, Icarus, Verilator, Ghdl, Nvc }
public enum SimulatorFeature
{
    FourStateWrites, NineStateWrites, ForceRelease, PackedPlaneTransport,
    NativePackedRanges, RootDiscovery, PackageDiscovery, DriverDiscovery, LoadDiscovery,
    PackageVariables, NamedStructInference, FixedStringNul, DynamicStringNul,
    SignednessMetadata, VhdlObjects, MixedLanguageTraversal, ImmediateSignalVisibility,
    VcdCapture, NativeClock, ExternalThreadWakeup
}

public sealed record SimulatorCapability(SimulatorFeature Feature, CapabilityStatus Status,
    string Explanation, string Evidence)
{
    public bool IsSupported => Status == CapabilityStatus.Supported;
}

/// <summary>Conservative policy for the pinned adapters and tested product families.
/// This is not a live probe or a certification of every version of a simulator.</summary>
public sealed class SimulatorCapabilities
{
    readonly IReadOnlyDictionary<SimulatorFeature, SimulatorCapability> entries;
    public string Product { get; }
    public SimulatorFamily Family { get; }
    public ulong NativeFeatures { get; }
    public IReadOnlyCollection<SimulatorCapability> All { get; }
    public SimulatorCapability this[SimulatorFeature feature] => entries[feature];

    public void Require(SimulatorFeature feature)
    {
        var capability = this[feature];
        if (!capability.IsSupported)
            throw new NotSupportedException($"{feature} is {capability.Status} for '{Product}': {capability.Explanation}");
    }

    public static SimulatorCapabilities Describe(string product, ulong nativeFeatures) => new(product, nativeFeatures);

    SimulatorCapabilities(string product, ulong nativeFeatures)
    {
        ArgumentNullException.ThrowIfNull(product);
        Product = product;
        NativeFeatures = nativeFeatures;
        string name = product.Trim();
        bool Named(string prefix) => name.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
            (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && name.Length > prefix.Length &&
             (char.IsWhiteSpace(name[prefix.Length]) || name[prefix.Length] == '('));
        Family = Named("Icarus Verilog") ? SimulatorFamily.Icarus : Named("Verilator") ? SimulatorFamily.Verilator :
            Named("GHDL") ? SimulatorFamily.Ghdl : Named("NVC") ? SimulatorFamily.Nvc : SimulatorFamily.Unknown;
        bool known = Family != SimulatorFamily.Unknown;
        bool vhdl = Family is SimulatorFamily.Ghdl or SimulatorFamily.Nvc;
        var map = Enum.GetValues<SimulatorFeature>().ToDictionary(f => f,
            f => new SimulatorCapability(f, CapabilityStatus.Unverified,
                "No validated contract for this product/operation; absence of evidence is not support.", "docs/CAPABILITIES.md"));
        void Set(SimulatorFeature f, CapabilityStatus status, string explanation, string evidence) =>
            map[f] = new(f, status, explanation, evidence);
        void Unsupported(SimulatorFeature f, string reason, string issue) =>
            Set(f, CapabilityStatus.Unsupported, reason, issue);
        if (known)
        {
            Set(SimulatorFeature.FourStateWrites, Family == SimulatorFamily.Verilator ? CapabilityStatus.Unsupported : CapabilityStatus.Supported,
                Family == SimulatorFamily.Verilator ? "Tested Verilator operation is two-state." : "X/Z transport verified on logic/vector fixtures.", "docs/VALIDATION.md");
            Set(SimulatorFeature.NineStateWrites, vhdl ? CapabilityStatus.Supported : CapabilityStatus.Unsupported,
                vhdl ? "Nine-state VHDL logic transport verified; object type must represent those states." : "Verilog logic does not preserve U/W/L/H/- as nine-state values.", "tests/VhdlIntegration/Tests.cs");
            Set(SimulatorFeature.VcdCapture, CapabilityStatus.Conditional, "Runner can capture VCD; type representation and available signals depend on the backend.", "docs/WAVEFORMS.md");
            Set(SimulatorFeature.ImmediateSignalVisibility, vhdl ? CapabilityStatus.Unsupported : CapabilityStatus.Supported,
                vhdl ? "VHDL signal writes require delta settling even with Immediate; variables have different semantics." : "Immediate visibility verified for Verilog register fixtures; races between writers remain possible.", "docs/TIMING_CONFORMANCE.md");
            Set(SimulatorFeature.SignednessMetadata, CapabilityStatus.Conditional, "Per-handle metadata can be absent; retain nullable signedness rather than infer it.", "docs/HDL_TYPES.md");
        }
        if (Family == SimulatorFamily.Icarus)
            Set(SimulatorFeature.ForceRelease, CapabilityStatus.Supported, "Force/deposit hold and release ordering verified on Icarus 11 and 12 fixtures.", "docs/TIMING_CONFORMANCE.md");
        else if (Family is SimulatorFamily.Verilator or SimulatorFamily.Nvc)
            Unsupported(SimulatorFeature.ForceRelease, "The tested force/deposit hold contract is not supported; operations are rejected.", "docs/OPEN_WORK.md");
        Set(SimulatorFeature.PackedPlaneTransport, (nativeFeatures & 1) != 0 ? CapabilityStatus.Supported : CapabilityStatus.Unsupported,
            "ABI feature bit 0 advertises managed/native packed planes; this does not establish simulator X/Z support or direct adapter transport.", "docs/NATIVE_ABI.md");
        Set(SimulatorFeature.NativePackedRanges,
            Family == SimulatorFamily.Verilator && (nativeFeatures & 2) != 0 ? CapabilityStatus.Conditional :
            (nativeFeatures & 2) == 0 ? CapabilityStatus.Unsupported : CapabilityStatus.Unverified,
            "Native packed-range inference requires ABI feature bit 1, the validated Verilator provider, and successful per-object dimensions/width checks.", "docs/DISCOVERY.md");
        if (Family == SimulatorFamily.Nvc)
            foreach (var feature in new[] { SimulatorFeature.RootDiscovery, SimulatorFeature.PackageDiscovery, SimulatorFeature.DriverDiscovery, SimulatorFeature.LoadDiscovery })
                Unsupported(feature, "The pinned VHPI adapter does not implement this iterator.", "docs/DISCOVERY.md");
        if (Family == SimulatorFamily.Verilator)
        {
            foreach (var feature in new[] { SimulatorFeature.DriverDiscovery, SimulatorFeature.LoadDiscovery })
                Unsupported(feature, "Tested Verilator VPI does not implement driver/load iteration.", "docs/DISCOVERY.md");
            Set(SimulatorFeature.PackageVariables, CapabilityStatus.Conditional, "Fixture package variable reads succeed; availability remains per-object.", "docs/DISCOVERY.md");
        }
        if (Family is SimulatorFamily.Icarus or SimulatorFamily.Verilator)
            foreach (var feature in new[] { SimulatorFeature.RootDiscovery, SimulatorFeature.PackageDiscovery })
                Set(feature, CapabilityStatus.Conditional, "Probe required. Package availability differs by version; empty iterators do not prove absence or completeness.", "docs/DISCOVERY.md");
        if (vhdl)
            Set(SimulatorFeature.VhdlObjects, CapabilityStatus.Conditional, "VHDL transport verified; GHDL VPI exposes fewer object kinds than NVC VHPI.", "docs/HDL_TYPES.md");
        if (Family == SimulatorFamily.Nvc)
            Set(SimulatorFeature.FixedStringNul, CapabilityStatus.Conditional, "NUL-preserving indexed-character transport is verified when fixed-string character handles are available.", "tests/VhdlIntegration/Tests.cs");
        Unsupported(SimulatorFeature.DynamicStringNul, "Current dynamic-string ABI is NUL terminated, not length aware.", "GitHub #14");
        Unsupported(SimulatorFeature.NamedStructInference, "Only explicit caller-provided packed-struct schemas are available.", "GitHub #10");
        Set(SimulatorFeature.NativeClock,
            (nativeFeatures & 16) == 0 ? CapabilityStatus.Unsupported :
            Family is SimulatorFamily.Icarus or SimulatorFamily.Verilator ? CapabilityStatus.Supported : CapabilityStatus.Unverified,
            "Native clock requires ABI bit 4; pulse/cancellation equivalence is validated on Icarus and Verilator. Other families use managed clocks.", "GitHub #19; tests/Integration/PerformanceTests.cs");
        Unsupported(SimulatorFeature.ExternalThreadWakeup, "External work has no deterministic native simulator wakeup bridge.", "GitHub #36");
        entries = new System.Collections.ObjectModel.ReadOnlyDictionary<SimulatorFeature, SimulatorCapability>(map);
        All = Array.AsReadOnly(Enum.GetValues<SimulatorFeature>().Select(f => map[f]).ToArray());
    }
}
