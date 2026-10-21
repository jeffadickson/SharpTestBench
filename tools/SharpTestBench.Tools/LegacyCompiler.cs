namespace SharpTestBench.Tools;

// Keep compiler diagnostics separate from simulator/result evidence.
internal static class LegacyCompiler
{
    internal static void Run(ToolContext context,string work,string label,string[] command,TimeSpan timeout)
    {
        CommandResult? result=null;
        Exception? failure=null;
        bool diagnosticsRetained=false;
        try
        {
            result=context.Run(command,capture:true,check:false,timeout:timeout);
            File.WriteAllText(Path.Combine(work,label+"-compile.stdout.log"),result.Output);
            File.WriteAllText(Path.Combine(work,label+"-compile.stderr.log"),result.Error);
            diagnosticsRetained=true;
            if (result.ExitCode!=0) throw new IOException("Icarus compiler failed (exit "+result.ExitCode+"): "+result.Error);
        }
        catch(Exception error) { failure=error;throw; }
        finally
        {
            try
            {
                StrictJson.Write(Path.Combine(work,label+"-compile-process.json"),new { schemaVersion=1,
                    status=failure is null ? "completed" : "failed",command,timeoutSeconds=timeout.TotalSeconds,
                    exitCode=result?.ExitCode,timedOut=failure is TimeoutException,errorType=failure?.GetType().FullName,error=failure?.Message,
                    diagnosticsRetained });
            }
            catch(Exception evidenceError) when(failure is not null)
            { Console.Error.WriteLine("Compiler process evidence could not be written: "+evidenceError.Message); }
        }
    }
}
