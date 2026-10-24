using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace SharpTestBench;
[AttributeUsage(AttributeTargets.Method)]
public sealed class SimTestAttribute : Attribute
{
    public ulong TimeoutSteps { get; set; } = 100000;
    public ulong CleanupTimeoutSteps { get; set; } = 1000;
    public bool Skip { get; set; }
    public int Stage { get; set; }
    public string[] Simulators { get; set; } = [];
    public Type? ExpectedException { get; set; }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class SimCaseAttribute(params object[] arguments) : Attribute
{
    public object[] Arguments { get; } = arguments;
}

public sealed class SimRandom
{
    ulong state;
    public SimRandom(ulong seed) => state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
    public ulong NextUInt64()
    {
        ulong x = state;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        state = x;
        return unchecked(x * 2685821657736338717UL);
    }

    public int Next(int exclusiveMax)
    {
        if (exclusiveMax <= 0)
            throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
        ulong bound = (ulong)exclusiveMax, threshold = unchecked(0UL - bound) % bound;
        ulong x;
        do
        {
            x = NextUInt64();
        }
        while (x < threshold);
        return (int)(x % bound);
    }
}

public class SimJob
{
    static readonly AsyncLocal<SimJob?> current = new();
    /// <summary>The StartSoon job executing in this async flow, or null in the test body.</summary>
    public static SimJob? Current => current.Value;
    internal static void Enter(SimJob job) => current.Value = job;
    internal readonly Dictionary<object, object?> Locals = new();
    internal readonly CancellationTokenSource Source;
    public Task Task { get; }
    public string Name { get; private set; }
    public TaskStatus Status => Task.Status;
    public bool IsCompleted => Task.IsCompleted;
    public AggregateException? Exception => Task.Exception;
    readonly List<Action<Exception?>> completionObservers = new();

    /// <summary>A reusable completion-only trigger. It never propagates this job's
    /// result, failure or cancellation and never takes ownership of the job.</summary>
    public Trigger Complete => new CompletionTrigger(this);

    internal void NotifyCompletion()
    {
        var observers = completionObservers.ToArray();
        completionObservers.Clear();
        foreach (var observer in observers) observer(null);
    }

    sealed class CompletionTrigger(SimJob job) : Trigger
    {
        protected internal override IDisposable Subscribe(SimRuntime runtime, Action<Exception?> complete)
        {
            runtime.CheckThread();
            if (job.IsCompleted)
            {
                complete(null);
                return new ActionDisposable(() => { });
            }
            job.completionObservers.Add(complete);
            return new ActionDisposable(() => job.completionObservers.Remove(complete));
        }
    }

    internal SimJob(CancellationTokenSource source, string name, Task task)
    {
        Source = source;
        Name = name;
        Task = task;
    }

    public void Cancel()
    {
        (SimRuntime.Active ?? throw new InvalidOperationException("No runtime")).CheckThread();
        Source.Cancel();
    }

    /// <summary>Change the diagnostic name on the simulator thread; task identity and state are unchanged.</summary>
    public void SetName(string name)
    {
        (SimRuntime.Active ?? throw new InvalidOperationException("No runtime")).CheckThread();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public System.Runtime.CompilerServices.TaskAwaiter GetAwaiter() => Task.GetAwaiter();
}

public sealed class SimJob<T> : SimJob
{
    internal SimJob(CancellationTokenSource source, string name, Task<T> task) : base(source, name, task)
    {
    }

    public new Task<T> Task => (Task<T>)base.Task;
    public T Result => Task.IsCompleted ? Task.GetAwaiter().GetResult() : throw new InvalidOperationException("Job is not complete; await it instead of blocking the simulator");

    public new System.Runtime.CompilerServices.TaskAwaiter<T> GetAwaiter() => Task.GetAwaiter();
}

public sealed class TestContext
{
    public HdlObject Dut { get; }
    public SimRuntime Runtime { get; }
    public SimRandom Random { get; }
    public ulong Seed { get; }
    public FunctionalCoverage Coverage { get; } = new();
    public CleanupScope Cleanup { get; }
    public string TestName { get; }
    public int Stage { get; }
    public CancellationToken Cancellation => source.Token;

    readonly CancellationTokenSource source = new();
    readonly List<SimJob> jobs = new();
    internal Exception? BackgroundFailure;
    internal TestContext(SimRuntime r, ulong seed, string testName, int stage)
    {
        Runtime = r;
        Cleanup = new CleanupScope(r.CheckThread);
        TestName = testName;
        Stage = stage;
        Dut = HdlObject.Root(Environment.GetEnvironmentVariable("CS_TOPLEVEL") ?? "top");
        Seed = seed;
        Random = new(seed);
    }

    void FailTest(Exception error)
    {
        BackgroundFailure ??= error;
        source.Cancel();
    }

    public SimJob StartSoon(Func<CancellationToken, Task> body, string? name = null) => StartJob(body, name, Runtime.DefaultCancellation, FailTest);
    public SimJob<T> StartSoon<T>(Func<CancellationToken, Task<T>> body, string? name = null) => StartJob(body, name, Runtime.DefaultCancellation, FailTest);
    public Task RunGroup(Func<SimTaskGroup, Task> body) => SimTaskGroup.Run(this, body);
    internal SimJob StartJob(Func<CancellationToken, Task> body, string? name, CancellationToken parent, Action<Exception> onFailure)
    {
        ArgumentNullException.ThrowIfNull(body);
        return StartJob<bool>(async token =>
        {
            await body(token);
            return true;
        }, name, parent, onFailure);
    }

    internal SimJob<T> StartJob<T>(Func<CancellationToken, Task<T>> body, string? name, CancellationToken parent, Action<Exception> onFailure)
    {
        Runtime.CheckThread();
        ArgumentNullException.ThrowIfNull(body);
        if(name is not null)ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var child = CancellationTokenSource.CreateLinkedTokenSource(Cancellation, parent);
        var started = new TaskCompletionSource<T>();
        var job = new SimJob<T>(child, name ?? $"job-{jobs.Count + 1}", started.Task);
        jobs.Add(job);
        Runtime.Enqueue(async () =>
        {
            // AsyncLocal is restored when this async delegate yields or returns.
            // Every continuation in the child retains its own cancellation scope.
            Runtime.DefaultCancellation = child.Token;
            SimJob.Enter(job);
            try
            {
                child.Token.ThrowIfCancellationRequested();
                started.SetResult(await body(child.Token));
            }
            catch (OperationCanceledException)when (child.IsCancellationRequested)
            {
                started.SetCanceled(child.Token);
            }
            catch (Exception e)
            {
                // Publish the failure before completing the task: an awaiting group
                // can resume inline from SetException and must already see it.
                try
                {
                    onFailure(e);
                }
                finally
                {
                    started.SetException(e);
                }
            }
            finally
            {
                job.NotifyCompletion();
            }
        });
        return job;
    }

    internal void Cancel()
    {
        source.Cancel();
    }

    internal bool JobsComplete => jobs.All(x => x.Task.IsCompleted);

    internal void Dispose()
    {
        foreach (var j in jobs)
        {
            _ = j.Task.Exception;
            j.Source.Dispose();
        }

        source.Dispose();
    }

    public void Log(string message, SimLogLevel level = SimLogLevel.Info, Exception? error = null,
        [CallerFilePath] string? file = null, [CallerLineNumber] int line = 0,
        [CallerMemberName] string? member = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        Runtime.CheckThread();
        Console.WriteLine($"[{Runtime.Now} step] {message}");
        SimulationLog.Write(Runtime, "message", TestName, Seed, Stage, level, message, error, file, line, member);
    }
}

public sealed record TestResult(string Name, string Status, ulong Seed, ulong StartStep, ulong EndStep, double Seconds, string? Error)
{
    public string? QualifiedName { get; init; }
    public int? Stage { get; init; }
    public int? CaseIndex { get; init; }
}
public sealed record TestDescriptor(string Name, string QualifiedName, int Stage, int CaseIndex,
    ulong Seed, ulong TimeoutSteps, bool Skip, string[] Simulators);
public static class Regression
{
    static SimRuntime? runtime;
    static readonly List<TestResult> results = new();
    static readonly List<object> coverage = new();
    static readonly List<TestDescriptor> discovered = new();
    static bool listOnly;
    static bool finished;
    static RegressionSelection selection = new();
    /// <summary>Start a regression with existing environment selection and ordinary declared-skip policy.</summary>
    public static int Initialize(Assembly assembly) => Initialize(assembly, new RegressionSelection());
    /// <summary>Start a regression with an immutable selection that intersects the existing environment selectors.</summary>
    /// <remarks>Configuration is bound by the managed entry point. Testcase mode bypasses only explicit skips.</remarks>
    public static int Initialize(Assembly assembly, RegressionSelection configuredSelection)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(assembly);
            ArgumentNullException.ThrowIfNull(configuredSelection);
            SimulationLog.Initialize();
            runtime = new SimRuntime();
            // Publish configuration only after acquiring the single runtime; a rejected second initialization cannot replace it.
            selection = configuredSelection;
            runtime.FatalError = Fatal;
            runtime.Register(CallbackKind.End, () =>
            {
                if (finished)
                    return;
                results.Add(new("simulator-termination", "failed", 0, 0, 0, 0, "Simulator ended before the regression completed"));
                Finish(false);
            });
            runtime.Register(CallbackKind.Start, () =>
            {
                _ = Run(assembly);
            });
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    static void Fatal(Exception e)
    {
        if (finished)
            return;
        if (runtime != null) SimulationLog.Write(runtime, "runtime-failure", "runtime", 0, null, SimLogLevel.Error, e.Message, e);
        results.Add(new("runtime", "failed", 0, 0, 0, 0, e.ToString()));
        Finish();
    }

    static async Task Run(Assembly assembly)
    {
        try
        {
            string? stagesText = Environment.GetEnvironmentVariable("CS_TEST_STAGES");
            HashSet<int>? stages = stagesText == null ? null : stagesText.Split(',').Select(int.Parse).ToHashSet();
            string stagePolicy = Environment.GetEnvironmentVariable("CS_STAGE_FAILURE_POLICY") ?? "continue";
            if (stagePolicy is not ("continue" or "stop-after-stage"))
                throw new ArgumentException("CS_STAGE_FAILURE_POLICY must be continue or stop-after-stage");
            int? failedStage = null;
            listOnly = Environment.GetEnvironmentVariable("CS_LIST_TESTS") == "1";
            string? exactText = Environment.GetEnvironmentVariable("CS_TEST_NAMES");
            var exact = exactText == null ? null : (JsonSerializer.Deserialize<string[]>(exactText)
                ?? throw new ArgumentException("CS_TEST_NAMES must be a JSON string array")).ToHashSet(StringComparer.Ordinal);
            string? filter = Environment.GetEnvironmentVariable("CS_TEST_FILTER");
            ulong seed = ulong.Parse(Environment.GetEnvironmentVariable("CS_SEED") ?? "1");
            var planned = RegressionPlanning.Build(assembly.GetTypes().SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static)),
                selection, seed, descriptor => (stages == null || stages.Contains(descriptor.Stage)) &&
                (filter == null || descriptor.Name.Contains(filter, StringComparison.Ordinal)) &&
                (exact == null || exact.Contains(descriptor.Name) || exact.Contains(descriptor.QualifiedName)));
            foreach (var plan in planned)
            {
                    var method = plan.Method;
                    var attr = plan.Attribute;
                    var descriptor = plan.Descriptor;
                    // Retain prior early-abort discovery behavior while all selection work has already succeeded.
                    discovered.Add(descriptor);
                    string name = descriptor.Name, qualifiedName = descriptor.QualifiedName;
                    ulong testSeed = descriptor.Seed;
                    int ci = descriptor.CaseIndex;
                    if (listOnly) continue;
                    bool unavailable = attr.Simulators.Length > 0 && !attr.Simulators.Any(s => runtime!.Simulator.Contains(s, StringComparison.OrdinalIgnoreCase));
                    bool stoppedStage = stagePolicy == "stop-after-stage" && failedStage.HasValue && attr.Stage > failedStage.Value;
                    if (attr.Skip && selection.Mode != RegressionMode.Testcase || unavailable || stoppedStage)
                    {
                        results.Add(new(name, "skipped", testSeed, runtime!.Now, runtime.Now, 0, stoppedStage ? $"Earlier stage {failedStage} failed" : unavailable ? "Requires simulator: " + string.Join(", ", attr.Simulators) : "Explicit skip") { QualifiedName = qualifiedName, Stage = attr.Stage, CaseIndex = ci });
                        continue;
                    }

                    await Triggers.Timer(1).WithCancellation(CancellationToken.None);
                    var ctx = new TestContext(runtime!, testSeed, qualifiedName, attr.Stage);
                    SimulationLog.Write(runtime!, "test-start", qualifiedName, testSeed, attr.Stage, SimLogLevel.Info, "Test started");
                    runtime!.DefaultCancellation = ctx.Cancellation;
                    var start = runtime.Now;
                    var watch = Stopwatch.StartNew();
                    Exception? error = null;
                    Task? body = null;
                    bool timedOut = false;
                    Exception? cancellationError = null;
                    var deadline = new TaskCompletionSource();
                    using var timer = runtime.Register(CallbackKind.Timer, () =>
                    {
                        timedOut = true;
                        try { ctx.Cancel(); }
                        catch (Exception e) { cancellationError = e; }
                        finally { deadline.TrySetResult(); }
                    }, attr.TimeoutSteps > 0 ? attr.TimeoutSteps : throw new ArgumentException("Zero test timeout"));
                    try
                    {
                        var arguments = new object[]
                        {
                            ctx
                        }.Concat(plan.Arguments).ToArray();
                        body = (Task? )method.Invoke(null, arguments) ?? throw new InvalidOperationException("Tests must return Task");
                        var winner = await Task.WhenAny(body, deadline.Task);
                        if (winner == deadline.Task)
                            throw new TimeoutException($"{name} exceeded {attr.TimeoutSteps} simulation steps");
                        await body;
                    }
                    catch (TargetInvocationException e)
                    {
                        error = e.InnerException ?? e;
                    }
                    catch (Exception e)
                    {
                        error = e;
                    }

                    timer.Dispose();
                    try { ctx.Cancel(); }
                    catch (Exception e) { cancellationError = e; }
                    runtime.DefaultCancellation = CancellationToken.None;
                    runtime.DiscardWrites();
                    await Triggers.Timer(1).WithCancellation(CancellationToken.None);
                    error = timedOut ? new TimeoutException($"{name} exceeded {attr.TimeoutSteps} simulation steps") : error;
                    if (cancellationError != null)
                        error = error == null ? cancellationError : new AggregateException(error, cancellationError);
                    if (ctx.BackgroundFailure != null && !ReferenceEquals(error, ctx.BackgroundFailure))
                        error = error == null || error is OperationCanceledException ? ctx.BackgroundFailure : new AggregateException(error, ctx.BackgroundFailure);
                    bool leaked = (body != null && !body.IsCompleted) || !ctx.JobsComplete || runtime.PendingCallbacks != 0;
                    Exception? cleanupError = null;
                    if (ctx.Cleanup.HasEntries)
                    {
                        using var cleanupCancellation = new CancellationTokenSource();
                        var cleanupDeadline = new TaskCompletionSource();
                        bool cleanupTimedOut = false;
                        Exception? cleanupCancellationError = null;
                        using var cleanupTimer = runtime.Register(CallbackKind.Timer, () =>
                        {
                            cleanupTimedOut = true;
                            try { cleanupCancellation.Cancel(); }
                            catch (Exception e) { cleanupCancellationError = e; }
                            finally { cleanupDeadline.TrySetResult(); }
                        }, Math.Max(1UL, attr.CleanupTimeoutSteps));
                        runtime.DefaultCancellation = cleanupCancellation.Token;
                        Task cleanup = ctx.Cleanup.CloseAsync(cleanupCancellation.Token);
                        await Task.WhenAny(cleanup, cleanupDeadline.Task);
                        cleanupTimer.Dispose();
                        runtime.DefaultCancellation = CancellationToken.None;
                        await Triggers.Timer(1).WithCancellation(CancellationToken.None);
                        cleanupError = cleanup.IsFaulted ? cleanup.Exception!.InnerException : null;
                        if (cleanupCancellationError != null)
                            cleanupError = cleanupError == null ? cleanupCancellationError : new AggregateException(cleanupError, cleanupCancellationError);
                        if (cleanupTimedOut)
                            cleanupError = cleanupError == null ? new TimeoutException("Fixture cleanup timed out") :
                                new AggregateException(new TimeoutException("Fixture cleanup timed out"), cleanupError);
                        if (cleanupError != null)
                            error = error == null ? cleanupError : new AggregateException("Test and cleanup failed", error, cleanupError);
                        runtime.DiscardWrites();
                        leaked |= !cleanup.IsCompleted || runtime.PendingCallbacks != 0;
                    }
                    else await ctx.Cleanup.CloseAsync();
                    if (leaked)
                        error = new InvalidOperationException("Test leaked tasks or callbacks; regression aborted to avoid contamination", error);
                    bool expected = !leaked && cleanupError == null && attr.ExpectedException != null && error != null && attr.ExpectedException.IsAssignableFrom(error.GetType());
                    if (attr.ExpectedException != null && error == null)
                        error = new InvalidOperationException("Expected exception was not raised");
                    string status = error == null || expected ? "passed" : "failed";
                    if (status == "failed") failedStage ??= attr.Stage;
                    watch.Stop();
                    results.Add(new(name, status, testSeed, start, runtime.Now, watch.Elapsed.TotalSeconds, expected ? null : error?.ToString()) { QualifiedName = qualifiedName, Stage = attr.Stage, CaseIndex = ci });
                    Console.WriteLine($"CS_TEST {status} {name} steps={runtime.Now - start} seed={testSeed}");
                    SimulationLog.Write(runtime, "test-end", qualifiedName, testSeed, attr.Stage,
                        status == "failed" ? SimLogLevel.Error : SimLogLevel.Info, status, expected ? null : error);
                    var snapshot = ctx.Coverage.Snapshot();
                    if (snapshot.Points.Length > 0)
                        coverage.Add(new { Test = name, QualifiedName = qualifiedName, Stage = attr.Stage, Seed = testSeed, Status = status, Coverage = snapshot });
                    ctx.Dispose();
                    if (leaked)
                    {
                        Finish();
                        return;
                    }
            }

            if (discovered.Count == 0)
                throw new InvalidOperationException("No tests matched");
            Finish();
        }
        catch (Exception e)
        {
            Fatal(e);
        }
    }

    static void Finish(bool stopSimulator = true)
    {
        if (finished)
            return;
        finished = true;
        string output = Environment.GetEnvironmentVariable("CS_RESULTS") ?? "csharp-results.json";
        ResultPublication.Complete(output, results, discovered, coverage, listOnly,
            stopSimulator ? () => runtime!.Finish() : null);
    }
}
