using System.Threading;
using SharpTestBench.Tools;
using Xunit;

/// <summary>Shared by normal regression and Stryker against exact production process code.</summary>
public sealed class ProcessContextMutationTests
{
    sealed class RecordingContext : SynchronizationContext
    {
        int posts;
        internal int Posts => Volatile.Read(ref posts);
        public override void Post(SendOrPostCallback callback, object? state)
        {
            // Allow progress while observing context capture. A UI context could
            // instead queue this work to the caller blocked in synchronous Run.
            Interlocked.Increment(ref posts);
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }
    }
    /// <summary>Pipe draining and backpressured stdin must not depend on the caller's context.</summary>
    /// <param name="inputBytes">Zero exercises only capture; one MiB forces asynchronous pipe writes.</param>
    [Theory][InlineData(0)][InlineData(1048576)]
    public void SynchronousProcessBoundaryDoesNotCaptureCallerContext(int inputBytes)
    {
        var context = new RecordingContext();
        var previous = SynchronizationContext.Current;
        EnduranceProcessResult result;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            string command = "Start-Sleep -Milliseconds 100; " +
                (inputBytes == 0 ? "$n=0; " : "$n=[Console]::In.ReadToEnd().Length; ") +
                "[Console]::Out.Write($n); [Console]::Error.Write('diagnostic')";
            result = EnduranceProcess.Run("pwsh", ["-NoProfile", "-Command", command],
                Path.GetTempPath(), TimeSpan.FromSeconds(20),
#if STB_XUNIT_V3
                cancellation: TestContext.Current.CancellationToken,
#else
                cancellation: CancellationToken.None,
#endif
                stdin: inputBytes == 0 ? null : new byte[inputBytes]);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        Assert.True(result.Passed);
        Assert.Equal(inputBytes.ToString(System.Globalization.CultureInfo.InvariantCulture), result.Output);
        Assert.Equal("diagnostic", System.Text.Encoding.UTF8.GetString(result.Stderr));
        Assert.Equal(0, context.Posts);
    }
}