namespace SharpTestBench.Internal;

// Bound serialized diagnostics independently of retained process log files.
internal static class DiagnosticText
{
    internal const int MaximumCharacters=16*1024;
    internal const string Marker="\n[diagnostic truncated]";
    internal static string Limit(string value,int maximum=MaximumCharacters)
    {
        ArgumentNullException.ThrowIfNull(value);
        if(maximum<=Marker.Length)throw new ArgumentOutOfRangeException(nameof(maximum));
        if(value.Length<=maximum)return value;
        int count=maximum-Marker.Length;
        if(count>0&&char.IsHighSurrogate(value[count-1])&&char.IsLowSurrogate(value[count]))count--;
        return value[..count]+Marker;
    }
}
