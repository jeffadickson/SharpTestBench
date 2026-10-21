namespace SharpTestBench.Tools;

internal static class LegacyFailure
{
    internal static string Kind(string phase,Exception error,bool functional) => error switch {
        TimeoutException => "timeout",
        _ when functional => "test_failure",
        _ when phase=="compile" => "build_failure",
        _ when phase=="simulate" => "simulator_failure",
        ArgumentException or InvalidDataException or System.Text.Json.JsonException or FileNotFoundException or DirectoryNotFoundException or System.Text.DecoderFallbackException => "configuration_error",
        _ => "infrastructure_failure"
    };
    internal static void Write(string work,string label,string runId,string phase,Exception error,bool functional,int? simulatorExitCode)
    {
        var artifacts=new[] { label+"-invocation.json",label+"-input-manifest.json",label+"-run.log",label+"-simulate.stdout.log",label+"-simulate.stderr.log",label+"-simulate-capture.json",label+"-compile.json",label+"-compile-process.json",label+"-compile.stdout.log",label+"-compile.stderr.log",label+"-results.json",label+"-results.xml",label+"-results.coverage.json",label+"-results.discovery.json" }
            .Where(name=>File.Exists(Path.Combine(work,name))).ToArray();
        StrictJson.Write(Path.Combine(work,label+"-failure.json"),new { schemaVersion=1,status="failed",runId,phase,
            kind=Kind(phase,error,functional),errorType=error.GetType().FullName,error=SharpTestBench.Internal.DiagnosticText.Limit(error.Message),simulatorExitCode,
            recordedAtUtc=DateTimeOffset.UtcNow,artifacts });
    }
}
