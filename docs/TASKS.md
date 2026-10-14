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

## Completion triggers

`await Triggers.First(job.Complete, Triggers.RisingEdge(clock))` waits for either
observation. `Combine` waits for both. `Complete` is reusable and supports multiple
simultaneous observers. It succeeds for successful, failed, and cancelled jobs;
use `await job` or `job.Result` to retrieve the result or propagate the failure.
It does not suppress test/group failure supervision. Group failure may cancel the
observer's default token; an explicitly bounded cleanup observation can opt out.

Cancelling or timing out the observation detaches it immediately, without cancelling
the job. Losing native triggers unregister; losing job observers detach. Already
completed input triggers resolve synchronously in input order during subscription;
otherwise the first simulator-thread completion notification wins. No thread-pool
continuation is attached to a job, so abandoned observers do not survive until its
completion. AL_CompletionTriggers exercises success, cancellation, failure,
repeated subscriptions, mixed waits and 128 timeout/cleanup cycles.

## Cancellation and Python kill policy

`Cancel()` intentionally has no message overload. .NET CancellationToken represents
a request, not an injected exception carrying user text. Log a diagnostic reason
before calling Cancel, or keep it in application state; exception-message matching
is not part of this API. Repeated cancellation is idempotent while the test context
is alive. Cancelling an already completed job does not rewrite its result or state.
Job mutation after the owning test context is disposed is unsupported.

Python's abrupt `kill()` is deliberately not implemented. Terminating a C# async
state machine without running finally/disposal would violate resource ownership;
CancellationToken cannot safely preempt arbitrary user code. Use cooperative Cancel,
join the job, and use bounded cancellation-independent cleanup where necessary.
The process watchdog bounds noncooperative code; it is not a per-job kill API.

Conformance evidence: S_ChildCancellationScope (finally and native cleanup),
T_NestedCancellationAndSuppression (inheritance, suppression, pre-start cancellation),
AM_CancellationContract (repetition, bounded async cleanup, late cancellation).
Related remaining work: [task locals/introspection](https://github.com/jeffadickson/SharpTestBench/issues/5),
[scheduler stress](https://github.com/jeffadickson/SharpTestBench/issues/6), and
[external-thread wakeups](https://github.com/jeffadickson/SharpTestBench/issues/36).

## Typed task-local state and introspection

Create a shared `SimTaskLocal<T>` key and read/write its `Value` inside a StartSoon
job. Each job has independent storage; a new child starts empty rather than copying
parent values. Missing Value returns default(T); `TryGetValue` distinguishes missing
from explicitly stored null/default, and `Clear` removes the current job's value.
Storage survives awaits and is retained with the job. Mutable reference values
explicitly shared by the application remain shared objects; no deep copy is made.
Access from the test body or a foreign thread throws. Pass shared test state as an
ordinary parameter instead of relying on ambient inheritance.

`SimJob.Current` identifies the job in the current async flow (null in the test
body). It uses AsyncLocal so identity survives awaits and nested jobs restore their
parent's identity. It is diagnostic identity, not permission to access the simulator
from Task.Run. Existing Name, Status, IsCompleted, Task and Exception expose managed
job state. Exception.InnerException.StackTrace retains managed async source locations
when matching symbols are available. Python live coroutine/frame inspection is
intentionally unsupported: a .NET async state machine does not expose that contract.
Use a managed debugger for live state inspection; debugger attachment validation is
tracked separately in issue #27. AN_TaskLocalsAndIntrospection verifies isolation,
null/absent values, thread checks, identity restoration and exception stack capture.

## Synchronization fairness and stress

SimSemaphore grants permits in FIFO subscription order among surviving waiters.
SimLock inherits that ordering and holds the permit until its IDisposable is disposed;
disposal is idempotent. SimQueue preserves item FIFO and uses the same FIFO queues
for blocked producers and consumers. These are simulator-thread ordering guarantees,
not wall-clock scheduling or fairness across external threads. A waiter must yield
for another job to run. Cancelling a pending waiter skips it; cancellation after its
trigger has already completed does not retroactively revoke a successful acquisition.

AO_PrimitiveFairnessStress runs 64 rounds of 24 contenders for each of semaphore,
lock, bounded-queue producers and bounded-queue consumers (6,144 job attempts).
Seeded cancellation masks verify surviving FIFO order, permit/item conservation,
lock release across awaits, double disposal and native callback cleanup. The seed
is recorded in regression JSON and assertions identify phase/round. Existing
Y_SameTimeCancellationStress covers 100 grant-versus-cancellation ties.
AP_MultipleFailureTeardownStress adds 256 groups of four seeded timer/failure jobs,
checking cleanup exactly once, four distinct retained errors, no duplicate body
failure and zero child native registrations after every round. This supplements
native_faults.py registration-failure tests and the regression's leak/early-end
fixtures; the historical native shutdown diagnostic remains tracked in issue #16.

## Typed structured combinators

Gather and Select now accept explicitly Owned factories/triggers and Borrowed typed
tasks/jobs. See [contracts, examples and executed cocotb 2.1 comparison](CONCURRENCY_COMPARISON.md).
Unlike First/Combine they cancel their owned work and join asynchronous cleanup.
