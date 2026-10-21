using System.Globalization;

namespace SharpTestBench.Tools;

internal sealed class PriorityFault
{
    public bool Enabled { get; }
    public int Configuration { get; }
    public int Kind { get; }
    public int OnsetVector { get; }
    private PriorityFault(bool enabled, int configuration, int kind, int vector) =>
        (Enabled, Configuration, Kind, OnsetVector) = (enabled, configuration, kind, vector);
    public static PriorityFault Parse(string? configuration, string? kind, bool enabled, string? vector = null)
    {
        if (!enabled && (configuration is not null || kind is not null || vector is not null))
            throw new ArgumentException("Fault options require --negative-control");
        if (!int.TryParse(configuration ?? "0", NumberStyles.None, CultureInfo.InvariantCulture, out int index) || index is < 0 or > 11)
            throw new ArgumentException("fault-config must be an integer from 0 to 11");
        int mode = (kind ?? "valid") switch { "valid" => 0, "index" => 1, "onehot" => 2, _ => throw new ArgumentException("fault-kind must be valid, index or onehot") };
        if (!int.TryParse(vector ?? "0", NumberStyles.None, CultureInfo.InvariantCulture, out int onset) || onset is < 0 or > 452)
            throw new ArgumentException("fault-vector must be an integer from 0 to 452");
        return new(enabled, index, mode, onset);
    }
    public string Suffix => !Enabled ? "" : "-negative" + (Configuration == 0 && Kind == 0 ? "" : $"-c{Configuration}-k{Kind}") + (OnsetVector == 0 ? "" : $"-v{OnsetVector}");
    public int DetectedVector(ulong seed)
    {
        if (!Enabled) throw new InvalidOperationException("Positive qualification has no fault vector");
        if (Kind == 0) return OnsetVector;
        int width = new[] { 2, 3, 5, 8, 17, 32 }[Configuration / 2];
        uint mask = width == 32 ? uint.MaxValue : (1u << width) - 1;
        int vector = 0;
        foreach (uint request in PriorityTrace.Requests(seed))
        {
            if (vector >= OnsetVector && (request & mask) != 0) return vector;
            vector++;
        }
        throw new InvalidDataException("Priority fault is not observable in the remaining stimulus");
    }
    public string Marker(ulong seed) => $"PRIORITY_MISMATCH configuration={Configuration} fault={Kind} observed={Kind switch { 0 => "valid", 1 => "index", _ => "onehot" }} vector={DetectedVector(seed)} ";
    public void Apply(Dictionary<string, string?> environment)
    {
        environment["PRIORITY_FAULT"] = Enabled ? "1" : "0";
        environment["PRIORITY_CONFIG"] = Configuration.ToString(CultureInfo.InvariantCulture);
        environment["PRIORITY_KIND"] = Kind.ToString(CultureInfo.InvariantCulture);
        environment["PRIORITY_VECTOR"] = OnsetVector.ToString(CultureInfo.InvariantCulture);
    }
}
