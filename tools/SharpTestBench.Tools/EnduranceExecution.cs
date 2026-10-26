using System.Diagnostics;

namespace SharpTestBench.Tools;

/// <summary>Injectable process/monotonic boundary; production campaigns always execute children serially.</summary>
internal interface IEnduranceExecution
{
    void StartBudget();
    TimeSpan Elapsed { get; }
    EnduranceProcessResult Run(string[] command, string directory, TimeSpan timeout, int logBytes, CancellationToken cancellation);
}
internal sealed class EnduranceExecution : IEnduranceExecution
{
    readonly Stopwatch clock = new();
    public void StartBudget() => clock.Restart();
    public TimeSpan Elapsed => clock.Elapsed;
    public EnduranceProcessResult Run(string[] command, string directory, TimeSpan timeout, int logBytes, CancellationToken cancellation) =>
        EnduranceProcess.Run(command[0], command.Skip(1), directory, timeout, logBytes, cancellation);
}
internal sealed class EnduranceCancellation : IDisposable
{
    readonly ConsoleCancelEventHandler handler;
    internal EnduranceCancellation(CancellationTokenSource source)
    {
        handler = (_, args) => { args.Cancel = true; source.Cancel(); };
        Console.CancelKeyPress += handler;
    }
    public void Dispose() => Console.CancelKeyPress -= handler;
}
