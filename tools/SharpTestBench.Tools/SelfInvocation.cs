namespace SharpTestBench.Tools;

internal static class SelfInvocation
{
    public static string[] Prefix(string? executable, string assembly)
    {
        if (string.IsNullOrWhiteSpace(assembly)) throw new InvalidOperationException("Full helpers require a directory-based managed or ReadyToRun deployment.");
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.IsNullOrWhiteSpace(executable) &&
            string.Equals(Path.GetFileName(executable), OperatingSystem.IsWindows() ? "SharpTestBench.Tools.exe" : "SharpTestBench.Tools", comparison) &&
            string.Equals(Path.GetDirectoryName(Path.GetFullPath(executable)), Path.GetDirectoryName(Path.GetFullPath(assembly)), comparison)) return [executable];
        return ["dotnet", assembly];
    }
}
