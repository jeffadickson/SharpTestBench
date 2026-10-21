namespace SharpTestBench.Tools;

internal static class LegacySimulator
{
    static void Logs(string work,string label,string output,string error)
    {
        File.WriteAllText(Path.Combine(work,label+"-simulate.stdout.log"),output);
        File.WriteAllText(Path.Combine(work,label+"-simulate.stderr.log"),error);
        File.WriteAllText(Path.Combine(work,label+"-run.log"),output+error);
    }
    internal static void Record(string work,string label,CommandResult result)
    {
        Logs(work,label,result.Output,result.Error);
        StrictJson.Write(Path.Combine(work,label+"-simulate-capture.json"),new {schemaVersion=1,status="completed",exitCode=(int?)result.ExitCode,
            diagnosticsComplete=true,maximumCaptureBytes=BoundedCapture.MaximumBytes,stdoutLimited=false,stderrLimited=false,captureSettled=true});
    }
    internal static CommandResult Run(ToolContext context,string[] command,string directory,IReadOnlyDictionary<string,string?> environment,string work,string label,TimeSpan timeout)
    {
        try { return context.Run(command,directory:directory,environment:environment,capture:true,check:false,timeout:timeout); }
        catch(Exception error) when(error is IPartialCommandFailure capture)
        {
            var partial=capture.Diagnostics;
            try
            {
                Logs(work,label,partial.Output,partial.Error);
                StrictJson.Write(Path.Combine(work,label+"-simulate-capture.json"),new {schemaVersion=1,status="interrupted",exitCode=(int?)null,
                    diagnosticsComplete=false,maximumCaptureBytes=BoundedCapture.MaximumBytes,stdoutLimited=partial.StdoutLimited,stderrLimited=partial.StderrLimited,captureSettled=partial.Settled});
            }
            catch(Exception evidenceError) { Console.Error.WriteLine("Partial simulator diagnostics could not be written: "+evidenceError.Message); }
            throw;
        }
    }
}
