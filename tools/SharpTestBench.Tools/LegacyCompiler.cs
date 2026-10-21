namespace SharpTestBench.Tools;

internal sealed class LegacyCompilerPublicationException(string message,Exception error):IOException(message,error);
internal static class LegacyCompiler
{
    internal static void Run(ToolContext context,string work,string label,string[] command,TimeSpan timeout)
    {
        CommandResult? result=null;Exception? failure=null;CapturedDiagnostics? partial=null;
        bool diagnosticsRetained=false;
        void Logs(string output,string error)
        {
            File.WriteAllText(Path.Combine(work,label+"-compile.stdout.log"),output);
            File.WriteAllText(Path.Combine(work,label+"-compile.stderr.log"),error);diagnosticsRetained=true;
        }
        try
        {
            try { result=context.Run(command,capture:true,check:false,timeout:timeout); }
            catch(Exception error) when(error is IPartialCommandFailure capture)
            {
                partial=capture.Diagnostics;
                try { Logs(partial.Output,partial.Error); }
                catch(Exception logError) { Console.Error.WriteLine("Partial compiler diagnostics could not be written: "+logError.Message); }
                throw;
            }
            var compilerError=result.ExitCode==0?null:new IOException("Icarus compiler failed (exit "+result.ExitCode+"): "+SharpTestBench.Internal.DiagnosticText.Limit(result.Error,4096));
            try { Logs(result.Output,result.Error); }
            catch(Exception logError)
            {
                if(compilerError is not null) { Console.Error.WriteLine("Compiler diagnostics could not be written: "+logError.Message);throw compilerError; }
                throw new LegacyCompilerPublicationException("Compiler diagnostic publication failed",logError);
            }
            if(compilerError is not null)throw compilerError;
        }
        catch(Exception error) { failure=error;throw; }
        finally
        {
            try
            {
                StrictJson.Write(Path.Combine(work,label+"-compile-process.json"),new { schemaVersion=1,
                    status=failure is null?"completed":"failed",command,timeoutSeconds=timeout.TotalSeconds,
                    exitCode=result?.ExitCode,timedOut=failure is TimeoutException,errorType=failure?.GetType().FullName,error=failure is null?null:SharpTestBench.Internal.DiagnosticText.Limit(failure.Message),
                    diagnosticsRetained,diagnosticsComplete=result is not null,maximumCaptureBytes=BoundedCapture.MaximumBytes,
                    stdoutLimited=partial?.StdoutLimited??false,stderrLimited=partial?.StderrLimited??false,captureSettled=partial?.Settled??true });
            }
            catch(Exception evidenceError)
            {
                if(failure is not null)Console.Error.WriteLine("Compiler process evidence could not be written: "+evidenceError.Message);
                else throw new LegacyCompilerPublicationException("Compiler process evidence publication failed",evidenceError);
            }
        }
    }
}
