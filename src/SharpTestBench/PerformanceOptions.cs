namespace SharpTestBench;

/// <summary>Process-wide switches used for reproducible optimization attribution.</summary>
/// <remarks>Set STB_DIRECT_PACKED, STB_BATCH_TRANSFERS, STB_NATIVE_CLOCK or
/// STB_LINEAR_TEXT to 0 before starting the simulator to disable one feature.</remarks>
internal static class PerformanceOptions
{
    internal static readonly bool DirectPacked = Enabled("STB_DIRECT_PACKED");
    internal static readonly bool BatchTransfers = Enabled("STB_BATCH_TRANSFERS");
    internal static readonly bool NativeClock = Enabled("STB_NATIVE_CLOCK");
    internal static readonly bool LinearText = Enabled("STB_LINEAR_TEXT");
    /// <summary>Read a switch once; an absent value enables the implementation.</summary>
    static bool Enabled(string name) => Environment.GetEnvironmentVariable(name) != "0";
}
