sealed record RunManifest(
    int SchemaVersion, string Simulator, string Top, string[] Sources,
    string Assembly, string NativeHost, string IcarusBootstrap, string GpiLibrary,
    string DotnetRoot, string OutputDirectory, ulong Seed = 1,
    string? EntryType = null, string GpiEntryPoint = "entry_point", string? GpiImpl = null,
    string? Iverilog = null, string? Vvp = null, int? ExpectedTests = null,
    string[]? ExpectedNames = null, int TimeoutSeconds = 120, string HdlStandard = "2012", string[]? IncludeDirectories = null, Dictionary<string, string?>? Defines = null, Dictionary<string, string>? Parameters = null, string[]? TestNames = null, int MaxLogBytes = 16 * 1024 * 1024);
