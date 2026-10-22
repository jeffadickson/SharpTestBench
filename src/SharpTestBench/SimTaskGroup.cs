using System.Runtime.ExceptionServices;

namespace SharpTestBench;
/// <summary>A lexical scope for owned simulator tasks. Use TestContext.RunGroup.</summary>
public sealed class SimTaskGroup
{
    readonly TestContext context;
    readonly CancellationTokenSource source;
    readonly List<SimJob> jobs = new();
    readonly List<Exception> failures = new();
    bool closed;
    SimTaskGroup(TestContext context, CancellationTokenSource source)
    {
        this.context = context;
        this.source = source;
    }

    void CheckOpen()
    {
        context.Runtime.CheckThread();
        if (closed)
            throw new InvalidOperationException("Task group is closed");
    }

    void Fail(Exception error)
    {
        failures.Add(error);
        CancelChildren();
    }

    void CancelChildren()
    {
        try
        {
            source.Cancel();
        }
        catch (Exception error)
        {
            failures.Add(error);
        }
    }

    public SimJob StartSoon(Func<CancellationToken, Task> body, string? name = null)
    {
        CheckOpen();
        var job = context.StartJob(body, name, source.Token, Fail);
        jobs.Add(job);
        return job;
    }

    public SimJob<T> StartSoon<T>(Func<CancellationToken, Task<T>> body, string? name = null)
    {
        CheckOpen();
        var job = context.StartJob(body, name, source.Token, Fail);
        jobs.Add(job);
        return job;
    }

    internal static async Task Run(TestContext context, Func<SimTaskGroup, Task> body)
    {
        context.Runtime.CheckThread();
        ArgumentNullException.ThrowIfNull(body);
        var parent = context.Runtime.DefaultCancellation;
        parent.ThrowIfCancellationRequested();
        using var source = CancellationTokenSource.CreateLinkedTokenSource(parent, context.Cancellation);
        var group = new SimTaskGroup(context, source);
        context.Runtime.DefaultCancellation = source.Token;
        Exception? bodyFailure = null;
        try
        {
            await body(group);
        }
        catch (Exception error)
        {
            bodyFailure = error;
        }
        finally
        {
            group.closed = true;
            group.CancelChildren();
            try
            {
                await Task.WhenAll(group.jobs.Select(j => j.Task));
            }
            catch (Exception)
            { /* Individual failures are recorded by StartJob. */
            }
        }

        // Awaiting a failing child may surface the same exception in the body.
        // Keep that exception once; cancellation caused by a child is control flow.
        if (bodyFailure != null && !(bodyFailure is OperationCanceledException && group.failures.Count > 0) && !group.failures.Any(e => ReferenceEquals(e, bodyFailure)))
            group.failures.Add(bodyFailure);
        if (group.failures.Count == 1)
            ExceptionDispatchInfo.Capture(group.failures[0]).Throw();
        if (group.failures.Count > 1)
            throw new AggregateException("Multiple task group failures", group.failures).Flatten();
        parent.ThrowIfCancellationRequested();
    }
}
