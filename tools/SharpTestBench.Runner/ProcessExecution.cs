using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

internal sealed class RunnerProcessException(string phase, string executable, int? exitCode, string detail)
    : Exception($"{phase}: {executable}: {detail}")
{
    internal string Kind => phase == "compile" ? "build_failure" : "simulator_failure";
    internal int? ExitCode { get; } = exitCode;
}

internal sealed record DrainResult(long ObservedBytes, int RetainedBytes, bool Truncated);

internal static class ProcessExecution
{
    internal static async Task Run(string executable, IEnumerable<string> arguments, string cwd,
        Dictionary<string, string>? environment, string phase, int timeoutSeconds = 120, int maxLogBytes = 16 * 1024 * 1024)
    {
        if (phase is not ("compile" or "simulate")) throw new InvalidDataException("Unknown process phase.");
        if (timeoutSeconds is < 1 or > 3600) throw new InvalidDataException("Invalid process timeout.");
        if (string.IsNullOrWhiteSpace(executable) || executable.Any(char.IsControl)) throw new InvalidDataException("Invalid executable path.");
        if (!Directory.Exists(cwd)) throw new DirectoryNotFoundException(cwd);
        var argv = arguments.ToArray();
        if (argv.Any(argument => argument is null || argument.Contains('\0'))) throw new InvalidDataException("Invalid process argument.");
        if (maxLogBytes is < 1024 or > 64 * 1024 * 1024) throw new InvalidDataException("Invalid process log budget.");
        var psi = new ProcessStartInfo(executable) { WorkingDirectory = cwd, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in argv) psi.ArgumentList.Add(argument);
        // Selection inherited from the caller must not silently shrink the expected regression.
        foreach (var key in new[] { "CS_TEST_FILTER", "CS_TEST_NAMES", "CS_TEST_STAGES", "CS_LIST_TESTS", "CS_STAGE_FAILURE_POLICY" })
            psi.Environment.Remove(key);
        if (environment is not null)
            foreach (var (key, value) in environment) psi.Environment[key] = value;
        await using var stdout = File.Create(Path.Combine(cwd, phase + ".stdout.log"));
        await using var stderr = File.Create(Path.Combine(cwd, phase + ".stderr.log"));
        using var process = new Process { StartInfo = psi };
        var watch = Stopwatch.StartNew();
        bool started = false, timedOut = false;
        int? exitCode = null;
        string? errorDetail = null;
        Task<DrainResult>? outTask = null, errTask = null;
        try
        {
            try { started = process.Start(); }
            catch (Win32Exception error)
            {
                throw new RunnerProcessException(phase, executable, null, error.Message);
            }
            if (!started) throw new RunnerProcessException(phase, executable, null, "Cannot start process");
            outTask = Drain(process.StandardOutput.BaseStream, stdout, maxLogBytes);
            errTask = Drain(process.StandardError.BaseStream, stderr, maxLogBytes);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                // Include pipe draining in the deadline: descendants may retain inherited pipes.
                await Task.WhenAll(process.WaitForExitAsync(), outTask, errTask).WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                timedOut = true;
                throw new TimeoutException($"{phase}: process exceeded {timeoutSeconds} seconds: {executable}");
            }
            if (outTask.Result.Truncated || errTask.Result.Truncated)
                throw new RunnerProcessException(phase, executable, process.ExitCode, "Process log exceeded the configured byte limit.");
            exitCode = process.ExitCode;
            if (exitCode != 0)
                throw new RunnerProcessException(phase, executable, exitCode, $"Exited with {exitCode}");
        }
        catch (Exception error)
        {
            errorDetail = error.Message;
            throw;
        }
        finally
        {
            if (started && !process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) when (process.HasExited) { }
                await process.WaitForExitAsync();
            }
            if (started) exitCode = process.ExitCode;
            if (timedOut)
            {
                process.StandardOutput.Close();
                process.StandardError.Close();
            }
            // Observe copy failures before disposing the destination files, including timeouts.
            Exception? drainError = null;
            if (outTask is not null && errTask is not null)
                try { await Task.WhenAll(outTask, errTask).WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception error) { drainError = error; }
            var detail = errorDetail ?? drainError?.Message;
            var evidence = new ProcessEvidence(1, phase, executable, argv, detail is null ? null : SharpTestBench.Internal.DiagnosticText.Limit(detail),
                started, exitCode, timedOut, watch.Elapsed.TotalMilliseconds, maxLogBytes,
                outTask is { IsCompletedSuccessfully: true } ? outTask.Result : null,
                errTask is { IsCompletedSuccessfully: true } ? errTask.Result : null);
            EvidenceWriter.Write(Path.Combine(cwd, phase + "-process.json"), JsonSerializer.SerializeToElement(evidence, EvidenceJsonContext.Default.ProcessEvidence));
            if (!timedOut && drainError is not null)
                throw new RunnerProcessException(phase, executable, exitCode, drainError.Message);
        }
    }

    private static async Task<DrainResult> Drain(Stream source, Stream destination, int limit)
    {
        var buffer = new byte[8192];
        int written = 0;
        long observed = 0;
        int count;
        while ((count = await source.ReadAsync(buffer)) != 0)
        {
            observed += count;
            int keep = Math.Min(count, limit - written);
            if (keep > 0) await destination.WriteAsync(buffer.AsMemory(0, keep));
            written += keep;
        }
        return new(observed, written, observed > written);
    }
}
