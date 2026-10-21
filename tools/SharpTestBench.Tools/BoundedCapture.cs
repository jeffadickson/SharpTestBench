using System.Diagnostics;
using System.Text;

namespace SharpTestBench.Tools;

internal sealed record CapturedDiagnostics(string Output,string Error,bool StdoutLimited,bool StderrLimited,bool Settled);
internal interface IPartialCommandFailure { CapturedDiagnostics Diagnostics {get;} }
internal sealed class CommandDeadlineException(string message,CapturedDiagnostics diagnostics):TimeoutException(message),IPartialCommandFailure
{ public CapturedDiagnostics Diagnostics {get;}=diagnostics; }
internal sealed class CommandCaptureLimitException(string message,CapturedDiagnostics diagnostics):IOException(message),IPartialCommandFailure
{ public CapturedDiagnostics Diagnostics {get;}=diagnostics; }

internal static class BoundedCapture
{
    internal const int MaximumBytes=16*1024*1024;
    sealed class Buffer
    {
        readonly MemoryStream bytes=new();readonly object gate=new();
        internal bool Limited {get;private set;}
        internal async Task Drain(Stream source,Action overflow)
        {
            var block=new byte[8192];int count;
            while((count=await source.ReadAsync(block))!=0)
            {
                bool notify=false;
                lock(gate)
                {
                    int retain=Math.Min(count,MaximumBytes-(int)bytes.Length);
                    bytes.Write(block,0,retain);
                    if(retain<count && !Limited) { Limited=true;notify=true; }
                }
                if(notify)overflow();
            }
        }
        internal string Text()
        {
            byte[] data;lock(gate)data=bytes.ToArray();
            using var reader=new StreamReader(new MemoryStream(data),Encoding.UTF8,true);
            return reader.ReadToEnd();
        }
    }
    internal static async Task<CommandResult> Run(ProcessStartInfo start,TimeSpan? timeout,bool check)
    {
        // Validate before starting a child: invalid deadlines must not leak processes.
        if(timeout is not null && (timeout.Value<=TimeSpan.Zero || timeout.Value.TotalMilliseconds>uint.MaxValue-1))throw new ArgumentOutOfRangeException(nameof(timeout));
        using var deadline=timeout is null?new CancellationTokenSource():new CancellationTokenSource(timeout.Value);
        using var overflow=new CancellationTokenSource();
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(deadline.Token,overflow.Token);
        using var process=Process.Start(start)??throw new IOException("Unable to start "+start.FileName);
        bool capture=start.RedirectStandardOutput;
        var stdout=new Buffer();var stderr=new Buffer();
        Action signalOverflow=()=> { try { overflow.Cancel(); }catch(ObjectDisposedException) { } };
        var outTask=capture?stdout.Drain(process.StandardOutput.BaseStream,signalOverflow):Task.CompletedTask;
        var errTask=capture?stderr.Drain(process.StandardError.BaseStream,signalOverflow):Task.CompletedTask;
        var drains=Task.WhenAll(outTask,errTask);
        var completion=Task.WhenAll(process.WaitForExitAsync(),drains);
        bool settled=false;
        CapturedDiagnostics Snapshot()=>new(stdout.Text(),stderr.Text(),stdout.Limited,stderr.Limited,settled);
        async Task Stop()
        {
            try { if(!process.HasExited)process.Kill(entireProcessTree:true); }catch(Exception error) when(error is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }catch(TimeoutException) { }
            try { await drains.WaitAsync(TimeSpan.FromSeconds(1)); }catch(Exception) { }
            if(!drains.IsCompleted && capture)
            {
                try { process.StandardOutput.Close();process.StandardError.Close(); }catch(IOException) { }
            }
            try { await drains.WaitAsync(TimeSpan.FromSeconds(2)); }catch(Exception) { }
            settled=drains.IsCompleted;
            // A descendant can retain pipes after its parent exits. Close local readers
            // and bound caller cleanup; this does not promise to kill orphan descendants.
            _=completion.ContinueWith(task=>{_=task.Exception;},TaskContinuationOptions.OnlyOnFaulted);
        }
        try
        {
            await completion.WaitAsync(cancellation.Token);settled=true;
        }
        catch(OperationCanceledException) when(cancellation.IsCancellationRequested)
        {
            await Stop();
            if(overflow.IsCancellationRequested)throw new CommandCaptureLimitException("Command output exceeds16MiB per-stream limit: "+Path.GetFileName(start.FileName),Snapshot());
            throw new CommandDeadlineException("Command timed out: "+Path.GetFileName(start.FileName),Snapshot());
        }
        catch { await Stop();throw; }
        if(stdout.Limited || stderr.Limited)throw new CommandCaptureLimitException("Command output exceeds16MiB per-stream limit: "+Path.GetFileName(start.FileName),Snapshot());
        var result=new CommandResult(process.ExitCode,stdout.Text(),stderr.Text());
        if(check && result.ExitCode!=0)throw new IOException($"Command failed ({result.ExitCode}): {Path.GetFileName(start.FileName)}\n{SharpTestBench.Internal.DiagnosticText.Limit(result.Error,4096)}");
        return result;
    }
}
