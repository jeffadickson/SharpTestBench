using System.Globalization;

namespace SharpTestBench.Tools;

internal sealed class PriorityFault
{
    public bool Enabled { get; }
    public int Configuration { get; }
    public int Kind { get; }
    private PriorityFault(bool enabled, int configuration, int kind) => (Enabled, Configuration, Kind) = (enabled, configuration, kind);
    public static PriorityFault Parse(string? configuration, string? kind, bool enabled)
    {
        if (!enabled && (configuration is not null || kind is not null))
            throw new ArgumentException("Fault options require --negative-control");
        if (!int.TryParse(configuration ?? "0", NumberStyles.None, CultureInfo.InvariantCulture, out int index) || index is < 0 or > 11)
            throw new ArgumentException("fault-config must be an integer from 0 to 11");
        int mode = (kind ?? "valid") switch { "valid" => 0, "index" => 1, "onehot" => 2, _ => throw new ArgumentException("fault-kind must be valid, index or onehot") };
        return new(enabled, index, mode);
    }
    public string Suffix => !Enabled ? "" : "-negative" + (Configuration == 0 && Kind == 0 ? "" : $"-c{Configuration}-k{Kind}");
    public string Marker => $"PRIORITY_MISMATCH configuration={Configuration} fault={Kind} observed={Kind switch { 0 => "valid", 1 => "index", _ => "onehot" }} ";
    public void Apply(Dictionary<string, string?> environment)
    {
        environment["PRIORITY_FAULT"] = Enabled ? "1" : "0";
        environment["PRIORITY_CONFIG"] = Configuration.ToString(CultureInfo.InvariantCulture);
        environment["PRIORITY_KIND"] = Kind.ToString(CultureInfo.InvariantCulture);
    }
}
