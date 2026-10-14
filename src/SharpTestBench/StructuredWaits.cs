using System.Runtime.ExceptionServices;

namespace SharpTestBench;

/// <summary>An explicit owned operation factory or a borrowed task observation.</summary>
public sealed class SimAwaitable<T>
{
    internal Func<CancellationToken, Task<T>> Start { get; }
    internal SimAwaitable(Func<CancellationToken, Task<T>> start) => Start = start;
}

public static partial class SimTasks
{
    /// <summary>Transfer lifetime ownership of a deferred operation to a combinator.</summary>
    public static SimAwaitable<T> Owned<T>(Func<CancellationToken, Task<T>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(factory);
    }

    /// <summary>Observe a task without taking cancellation ownership.</summary>
    public static SimAwaitable<T> Borrowed<T>(Task<T> task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return new(token => task.WaitAsync(token));
    }

    public static SimAwaitable<T> Borrowed<T>(SimJob<T> job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return Borrowed(job.Task);
    }

    /// <summary>Own a trigger subscription. Its result is the original trigger.</summary>
    public static SimAwaitable<Trigger> Owned(Trigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return Owned(async token => { await trigger.AsTask(token); return trigger; });
    }

    public static Task<T[]> Gather<T>(TestContext context, params SimAwaitable<T>[] inputs) => Gather(context, null, inputs);
    public static async Task<T[]> Gather<T>(TestContext context, CancellationToken? cancellation, params SimAwaitable<T>[] inputs)
    {
        var (_, tasks) = await Structured(context, false, cancellation, inputs);
        return tasks.Select(t => t.GetAwaiter().GetResult()).ToArray();
    }

    public static Task<(int Index, T Value)> Select<T>(TestContext context, params SimAwaitable<T>[] inputs) => Select(context, null, inputs);
    public static async Task<(int Index, T Value)> Select<T>(TestContext context, CancellationToken? cancellation, params SimAwaitable<T>[] inputs)
    {
        var (index, tasks) = await Structured(context, true, cancellation, inputs);
        return (index, tasks[index].GetAwaiter().GetResult());
    }

    static async Task<(int Index, Task<T>[] Tasks)> Structured<T>(TestContext context, bool first,
        CancellationToken? cancellation, SimAwaitable<T>[] inputs)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Runtime.CheckThread();
        ArgumentNullException.ThrowIfNull(inputs);
        var snapshot = (SimAwaitable<T>[])inputs.Clone();
        if (snapshot.Any(x => x is null) || (first && snapshot.Length == 0))
            throw new ArgumentException("Select requires inputs; no input may be null", nameof(inputs));
        var parent = Token(cancellation);
        parent.ThrowIfCancellationRequested();
        // Do not link parent cancellation directly: cancellation callbacks can throw.
        // The coordinator requests cancellation once, records errors and still joins.
        var jobs = snapshot.Select((input, i) => context.StartJob(input.Start,
            $"{(first ? "select" : "gather")}-{i}", CancellationToken.None, _ => { })).ToArray();
        var tasks = jobs.Select(job => job.Task).ToArray();
        var remaining = tasks.ToList();
        var index = -1;
        Exception? primary = null;
        var errors = new List<Exception>();
        try
        {
            while (remaining.Count > 0)
            {
                var completed = await Task.WhenAny(remaining).WaitAsync(parent);
                parent.ThrowIfCancellationRequested();
                index = Array.IndexOf(tasks, completed);
                await completed;
                remaining.Remove(completed);
                if (first) break;
            }
        }
        catch (Exception error) { primary = error; }
        finally
        {
            foreach (var job in jobs)
                if (!job.IsCompleted)
                {
                    try { job.Cancel(); }
                    catch (Exception error) { errors.Add(error); }
                }
            // Cancellation is cooperative. Regression deadlines/leak checks still bound
            // noncooperative children. Never abandon a running owned child here.
            try { await Task.WhenAll(tasks); }
            catch (Exception) { /* Inspect all faults below, including cleanup failures. */ }
        }
        if (primary is not null) errors.Insert(0, primary);
        foreach (var task in tasks)
            if (task.Exception is { } failure)
                errors.AddRange(failure.Flatten().InnerExceptions);
        var distinct = errors.SelectMany(e => e is AggregateException a ? a.Flatten().InnerExceptions : new[] { e }.AsEnumerable())
            .Distinct(ReferenceEqualityComparer.Instance).Cast<Exception>().ToArray();
        if (distinct.Length == 1) ExceptionDispatchInfo.Capture(distinct[0]).Throw();
        if (distinct.Length > 1) throw new AggregateException("Concurrent operation or cleanup failed", distinct);
        return (index, tasks);
    }
}
