namespace SharpTestBench;

/// <summary>Owns cleanup callbacks in reverse registration order. Register immediately after acquisition.</summary>
public sealed class CleanupScope
{
    readonly List<Func<CancellationToken, Task>> callbacks = new();
    readonly Action checkAccess;
    Task? completion;
    bool closing;

    internal bool HasEntries => callbacks.Count != 0;

    public CleanupScope() : this(() => { }) { }
    internal CleanupScope(Action checkAccess) => this.checkAccess = checkAccess;

    public void Defer(Func<CancellationToken, Task> cleanup)
    {
        checkAccess();
        ArgumentNullException.ThrowIfNull(cleanup);
        if (closing) throw new InvalidOperationException("Cleanup scope is already closing");
        callbacks.Add(cleanup);
    }

    public void Defer(Action cleanup)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        Defer(_ => { cleanup(); return Task.CompletedTask; });
    }

    public T Own<T>(T resource) where T : IDisposable
    {
        ArgumentNullException.ThrowIfNull(resource);
        Defer(resource.Dispose);
        return resource;
    }

    public T OwnAsync<T>(T resource) where T : IAsyncDisposable
    {
        ArgumentNullException.ThrowIfNull(resource);
        Defer(_ => resource.DisposeAsync().AsTask());
        return resource;
    }

    public CleanupScope CreateScope()
    {
        var child = new CleanupScope(checkAccess);
        Defer(child.CloseAsync);
        return child;
    }

    public Task CloseAsync(CancellationToken cancellation = default)
    {
        checkAccess();
        if (completion != null) return completion;
        // Publish completion before invoking user code, including synchronous callbacks.
        closing = true;
        var done = new TaskCompletionSource();
        completion = done.Task;
        _ = Run(done, cancellation);
        return completion;
    }

    async Task Run(TaskCompletionSource done, CancellationToken cancellation)
    {
        List<Exception> errors = new();
        for (int i = callbacks.Count - 1; i >= 0; i--)
        {
            try { await callbacks[i](cancellation); }
            catch (Exception error) { errors.Add(error); }
        }
        callbacks.Clear();
        if (errors.Count == 0) done.SetResult();
        else done.SetException(new AggregateException("Fixture cleanup failed", errors));
    }
}
