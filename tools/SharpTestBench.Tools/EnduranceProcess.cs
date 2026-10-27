#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpTestBench.Tools;

/// <summary>Bounded child result shared with the PowerShell bootstrap before the SDK host is compiled.</summary>
/// <param name="ExitCode">Null when the child did not settle after termination.</param>
/// <param name="TimedOut">The monotonic child deadline expired.</param>
/// <param name="Interrupted">Cancellation requested process-tree termination.</param>
/// <param name="Truncated">One or more streams exceeded retention or were closed during cleanup.</param>
/// <param name="Settled">All stream tasks finished within bounded cleanup.</param>
/// <param name="Stdout">Retained stdout prefix, at most the requested byte cap.</param>
/// <param name="Stderr">Retained stderr prefix, at most the requested byte cap.</param>
public sealed record EnduranceProcessResult(int? ExitCode, bool TimedOut, bool Interrupted, bool Truncated, bool Settled, byte[] Stdout, byte[] Stderr)
{
    /// <summary>Success with complete retained diagnostics.</summary>
    public bool Passed => ExitCode == 0 && !TimedOut && !Interrupted && !Truncated && Settled;
    /// <summary>Successful exit; diagnostic truncation is reported separately.</summary>
    public bool Succeeded => ExitCode == 0 && !TimedOut && !Interrupted && Settled;
    /// <summary>Strict UTF-8 decoding for structured child responses.</summary>
    public string Output => new UTF8Encoding(false, true).GetString(Stdout);
}

/// <summary>Serial child process boundary with bounded retained bytes, deadlines, cancellation and drain cleanup.</summary>
public static class EnduranceProcess
{
    /// <summary>Launches without a shell, drains bounded prefixes and terminates the child tree on deadline/cancellation.</summary>
    public static EnduranceProcessResult Run(string executable, IEnumerable<string> arguments, string directory, TimeSpan timeout,
        int maximumBytes = 131072, CancellationToken cancellation = default, IReadOnlyDictionary<string, string?>? environment = null,
        byte[]? stdin = null)
    {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromDays(2) || maximumBytes is < 4096 or > 16777216) throw new ArgumentException("Invalid process bounds");
        var info = new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = stdin is not null, CreateNoWindow = true };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        if (environment is not null) foreach (var (name, value) in environment) info.Environment[name] = value;
        using var process = new Process { StartInfo = info };
        var clock = Stopwatch.StartNew();
        if (!process.Start()) throw new IOException("Child process did not start");
        using var stopStreams = new CancellationTokenSource();
        // The synchronous facade blocks its caller; pipe continuations must never
        // require that caller's synchronization context (including test/UI contexts).
        var stdout = Capture(process.StandardOutput.BaseStream, maximumBytes, stopStreams.Token);
        var stderr = Capture(process.StandardError.BaseStream, maximumBytes, stopStreams.Token);
        var input = stdin is null ? Task.CompletedTask : Feed(process.StandardInput.BaseStream, stdin, stopStreams.Token);
        bool interrupted = false, timedOut = false;
        while (!process.WaitForExit(20))
        {
            interrupted = cancellation.IsCancellationRequested;
            timedOut = clock.Elapsed >= timeout;
            if (!interrupted && !timedOut) continue;
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            break;
        }
        bool exited = process.WaitForExit(2000);
        // Inherited descendant pipes must not keep a budget-expired parent alive.
        bool settled = Settle(Task.WhenAll(stdout, stderr, input), TimeSpan.FromSeconds(2));
        if (!settled)
        {
            // Cancel outstanding pipe I/O before closing handles: descendants can
            // inherit writers after their parent exits, especially on Windows.
            stopStreams.Cancel();
            settled = Settle(Task.WhenAll(stdout, stderr, input), TimeSpan.FromSeconds(2));
            process.StandardOutput.Close(); process.StandardError.Close();
            if (stdin is not null) process.StandardInput.Close();
            settled = Settle(Task.WhenAll(stdout, stderr, input), TimeSpan.FromSeconds(1));
        }
        var a = stdout.IsCompletedSuccessfully ? stdout.Result : (Array.Empty<byte>(), true);
        var b = stderr.IsCompletedSuccessfully ? stderr.Result : (Array.Empty<byte>(), true);
        return new(exited ? process.ExitCode : null, timedOut, interrupted, a.Item2 || b.Item2, settled, a.Item1, b.Item1);
    }
    static bool Settle(Task task, TimeSpan timeout)
    {
        try { return task.Wait(timeout); } catch (AggregateException) { return false; }
    }
    static async Task Feed(Stream stream, byte[] bytes, CancellationToken cancellation)
    {
        try { await stream.WriteAsync(bytes, cancellation).ConfigureAwait(false); await stream.FlushAsync(cancellation).ConfigureAwait(false); }
        catch (Exception error) when (error is IOException or ObjectDisposedException or OperationCanceledException) { }
        finally { stream.Close(); }
    }
    static async Task<(byte[], bool)> Capture(Stream stream, int maximum, CancellationToken cancellation)
    {
        using var output = new MemoryStream(); bool truncated = false;
        byte[] buffer = new byte[8192];
        try
        {
            int count;
            while ((count = await stream.ReadAsync(buffer.AsMemory(), cancellation).ConfigureAwait(false)) > 0)
            {
                int keep = Math.Min(count, maximum - (int)output.Length);
                if (keep > 0) output.Write(buffer, 0, keep);
                if (keep != count) truncated = true;
            }
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or OperationCanceledException) { truncated = true; }
        return (output.ToArray(), truncated);
    }
}
