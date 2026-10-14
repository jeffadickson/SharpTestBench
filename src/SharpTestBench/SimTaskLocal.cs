namespace SharpTestBench;

/// <summary>A typed key for job-local state. Jobs start empty; values are not inherited.
/// Access is restricted to a StartSoon job on the simulator thread.</summary>
public sealed class SimTaskLocal<T>
{
    static SimJob Job
    {
        get
        {
            (SimRuntime.Active ?? throw new InvalidOperationException("No runtime")).CheckThread();
            return SimJob.Current ?? throw new InvalidOperationException("Task-local state requires a StartSoon job");
        }
    }

    public T Value
    {
        get => TryGetValue(out var value) ? value : default!;
        set => Job.Locals[this] = value;
    }

    public bool TryGetValue(out T value)
    {
        if (Job.Locals.TryGetValue(this, out var stored))
        {
            value = (T)stored!;
            return true;
        }
        value = default!;
        return false;
    }

    public bool Clear() => Job.Locals.Remove(this);
}
