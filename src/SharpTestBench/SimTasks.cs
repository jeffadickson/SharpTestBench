namespace SharpTestBench;
/// <summary>Simulator task combinators with explicit ownership.</summary>
public static partial class SimTasks
{
    static CancellationToken Token(CancellationToken? token)
    {
        var runtime = SimRuntime.Active ?? throw new InvalidOperationException("No runtime");
        runtime.CheckThread();
        return token ?? runtime.DefaultCancellation;
    }

    static Task[] Snapshot(Task[] tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        if (tasks.Length == 0 || tasks.Any(t => t == null))
            throw new ArgumentException("At least one non-null task required", nameof(tasks));
        return (Task[])tasks.Clone();
    }

    /// <summary>Wait for the first task, propagating its error or cancellation.
    /// Already-complete ties select the first task in input order. Losers keep running.</summary>
    public static Task<Task> First(params Task[] tasks) => First(null, tasks);
    public static async Task<Task> First(CancellationToken? cancellation, params Task[] tasks)
    {
        var token = Token(cancellation);
        token.ThrowIfCancellationRequested();
        var winner = await Task.WhenAny(Snapshot(tasks)).WaitAsync(token);
        await winner;
        return winner;
    }

    /// <summary>Wait for all tasks. All must settle before errors propagate.</summary>
    public static Task Combine(params Task[] tasks) => Combine(null, tasks);
    public static async Task Combine(CancellationToken? cancellation, params Task[] tasks)
    {
        var token = Token(cancellation);
        token.ThrowIfCancellationRequested();
        await Task.WhenAll(Snapshot(tasks)).WaitAsync(token);
    }

    /// <summary>Apply a simulation-time deadline to a wait; the task remains caller-owned.
    /// A task already complete when the deadline is observed wins the tie.</summary>
    public static async Task WithTimeout(Task task, ulong steps, CancellationToken? cancellation = null)
    {
        ArgumentNullException.ThrowIfNull(task);
        var timer = Triggers.Timer(steps);
        var token = Token(cancellation);
        token.ThrowIfCancellationRequested();
        using var timerCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var deadline = timer.AsTask(timerCancellation.Token);
        try
        {
            await Task.WhenAny(task, deadline).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            if (task.IsCompleted)
                await task;
            else
            {
                await deadline;
                throw new TimeoutException($"Task exceeded {steps} simulation steps");
            }
        }
        finally
        {
            timerCancellation.Cancel();
            // Join the cancelled timer so its registration and continuation are gone.
            try
            {
                await deadline;
            }
            catch (OperationCanceledException)when (timerCancellation.IsCancellationRequested)
            {
            }
        }
    }

    public static async Task<T> WithTimeout<T>(Task<T> task, ulong steps, CancellationToken? cancellation = null)
    {
        await WithTimeout((Task)task, steps, cancellation);
        return await task;
    }
}
