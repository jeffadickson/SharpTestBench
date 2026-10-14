# Task ownership and cancellation (v0.3)

All simulator operations run on the simulator thread. `Task.Run` and `Task.Delay`
do not provide simulation scheduling or a thread-to-simulator wakeup mechanism.

## Jobs and cancellation scopes

`ctx.StartSoon(body, name)` schedules a job for a later scheduler turn. It exposes
`Name`, `Task`, `Status`, `IsCompleted`, `Exception`, `Cancel()` and direct awaiting.
`StartSoon<T>` returns `SimJob<T>`: awaiting it produces T. Its `Result` property
throws while unfinished instead of blocking the simulator thread; once finished,
it returns the value or rethrows failure/cancellation. The job receives a token,
but plain simulation waits already inherit it:

```csharp
var job = ctx.StartSoon(async token =>
{
    try { while (true) await Triggers.Timer(10); }
    finally { ctx.Log("monitor stopped"); }
}, "monitor");
await Triggers.Timer(25);
job.Cancel();
try { await job; }
catch (OperationCanceledException) { }
```

Cancellation before the job starts prevents its body from running. Child jobs
link to both the enclosing test and the caller's current cancellation scope.
AsyncLocal carries that scope across continuations without changing siblings'
or parents' scopes. Native registrations are removed when a pending wait cancels.
If a trigger has already completed, its completion wins over later cancellation;
there is no retroactive cancellation of a successful wait.

Cancellation remains cooperative: code may catch OperationCanceledException and
return normally, in which case the job succeeds. New default waits in a cancelled
scope immediately cancel. Explicit `CancellationToken.None` opts out on
`WithCancellation`, `AsTask`, `ClockCycles`, semaphore/lock/queue waits and SimTasks.
Use this only for bounded cleanup. A non-cooperative job may prevent a group from
joining; the enclosing regression deadline and leak guard remain in effect.

An ordinary StartSoon failure fails its test, even if its Task exception is caught
locally. Use a group when failure should be handled within the test.

## Structured groups

```csharp
try
{
    await ctx.RunGroup(async group =>
    {
        var producer = group.StartSoon(Produce, "producer");
        var consumer = group.StartSoon(Consume, "consumer");
        await SimTasks.Combine(producer.Task, consumer.Task);
    });
}
catch (ProtocolException error)
{
    // Both children have settled before this handler runs.
    ctx.Log(error.Message);
}
```

`Produce` and `Consume` above are user functions taking CancellationToken and
returning Task; ProtocolException is a user-defined exception.

The first failing child cancels the group token, waking sibling and body default
waits. Every scope exit closes the group to new work, cancels its children and joins
them before returning or throwing. To wait for normal completion, explicitly await
jobs within the group body. Returning from that body means remaining jobs are no
longer needed. One failure is rethrown as its original type. Multiple child/cleanup/body failures
produce a flattened AggregateException. Awaiting a child does not duplicate the
same exception as a body failure; cancellation caused by failures is control flow.
Failure during cancellation cleanup is included. The caller can handle an entire
nested group failure without cancelling the outer test.

Only `group.StartSoon` jobs belong to the group. `ctx.StartSoon` retains test-level
failure supervision even when called inside a group. All jobs are tracked by the
test's final leak check. A group reference cannot be reused after scope exit.

## Non-owning task waits

| API | Result | Ownership |
|---|---|---|
| `SimTasks.First(tasks)` | Winning Task; its failure/cancellation propagates | Losers keep running |
| `SimTasks.Combine(tasks)` | Completes when all settle; propagates an error | Cancelling the wait leaves tasks running |
| `SimTasks.WithTimeout(task, steps)` | Task completion or TimeoutException | Timed-out task keeps running |
| `SimTasks.WithTimeout<T>(task, steps)` | Typed result T | Same ownership rule |

First/Combine require at least one non-null task. Already-completed First ties use
input order; otherwise normal .NET completion ordering applies. For WithTimeout,
an operation already complete when the deadline is observed wins. Cancellation of
the timeout wait takes precedence at its explicit cancellation check. Timeout
registrations are cancelled and joined before the wait returns.

These contracts are intentionally explicit; full one-for-one parity with cocotb's
task API remains unverified. `Triggers.First` has different ownership: its losing
native trigger subscriptions are removed. Caller-owned Task objects cannot in
general be cancelled by a combinator, so they must be joined or owned by a group.

## Test boundaries

Test cleanup cancels jobs, discards queued writes and allows a simulator step for
continuations. Remaining tasks or native callbacks fail the test and abort the
regression to prevent contamination. ExpectedException never exempts a leak.
The simulator-process runner also enforces a wall-clock watchdog. An end-of-simulation
callback emits failing JSON/JUnit if HDL terminates the simulator before regression
completion, including when no ordinary test has yet produced a result.
