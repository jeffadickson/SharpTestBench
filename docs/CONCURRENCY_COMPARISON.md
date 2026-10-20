# Structured concurrency: cocotb 2.1 and SharpTestBench

Issue #44 adds typed `SimTasks.Gather` and `SimTasks.Select`. Existing First,
Combine and WithTimeout retain their non-owning contracts.

```csharp
using static SharpTestBench.SimTasks;

int[] values = await Gather(ctx,
    Owned(async token => { await Triggers.Timer(5); return 10; }),
    Owned(async token => { await Triggers.Timer(2); return 20; }));
// values is [10, 20], in argument order.

var (index, value) = await Select(ctx,
    Borrowed(existingJob),
    Owned(async token => { await Triggers.Timer(50); return -1; }));
```

`Owned` takes a deferred `Func<CancellationToken, Task<T>>`; it starts on the
simulator scheduler and inherits the child cancellation scope. `Owned(trigger)`
owns a trigger subscription and returns that trigger. `Borrowed(Task<T>)` and
`Borrowed(SimJob<T>)` own only the observation, never the supplied operation.
Factories may adapt void-returning operations to a typed sentinel result. Inputs
share T; heterogeneous values can use an explicit application result type.

## Contracts

- Gather returns T[] in input order; empty input returns an empty array. A failed
  or cancelled child ends the wait promptly and cancels unfinished owned work.
- Select returns `(int Index, T Value)`; empty input is rejected. First observed
  completion wins, including failure/cancellation. Avoid depending on same-time
  ordering. Selection cancels unfinished owned losers.
- Both accept an optional cancellation token after ctx. The default is the caller's
  simulation scope. Pre-cancellation prevents factories from starting. All inputs
  are validated before work starts; the input array is snapshotted.
- All owned jobs are joined before return/throw, including asynchronous finally
  work. Parent cancellation is requested once per pending child; repeated requests
  do not interrupt joining. Cleanup that needs simulation time must explicitly use
  CancellationToken.None for bounded waits. Completed jobs are not cancelled.
- Borrowed waiters detach on cancellation; underlying tasks survive. Existing
  StartSoon jobs retain their original test/group failure supervision.
- The original single error is rethrown. Distinct operation, cancellation-handler
  and cleanup errors are aggregated without duplicating the same exception object.
  A cleanup failure can turn successful selection into failure.
- Cancellation suppression is permitted; the combinator still joins that child.
  A noncooperative operation can prevent return. Regression deadlines, tracked-job
  leak checks and the simulator-process watchdog remain the outer bounds.

Factories must return the Task representing all their work. Detached jobs started
with ctx.StartSoon retain their separate owner; they are not silently adopted.
External-thread completion/wakeup remains outside this simulator-thread contract.
No public low-level wait(return_when) API is introduced in this issue; First,
Combine and the new structured APIs expose the supported high-level policies.

## Executed comparison

The oracle uses **released cocotb 2.1.0 in a separate environment**, not the pinned
native-adapter development checkout. Reference documentation:
https://docs.cocotb.org/en/v2.1.0/library_reference.html#cocotb.triggers.gather

The common cases verify ordered results, empty gather, borrowed-task survival,
child cancellation and selected result identity. However, real cancellation tests
show a significant difference that the earlier documentation-only review missed:
2.1's Task._resume rejects a cancelled coroutine that yields again. The following
async-finally scenarios therefore deliberately have different outcomes.

| Fixture | SharpTestBench | Released cocotb 2.1 |
|---|---|---|
| Select wins at step 2; loser finally awaits 3 steps | Returns at step 5, cleanup finished | Returns at step 2, cleanup unfinished |
| Gather fails at step 2; sibling finally awaits 3 steps | Raises at step 5 | Raises at step 2 |
| Parent cancelled at step 1, repeated at step 2; child finally awaits 3 | Observed at step 4 | Observed at step 2 |
| Losing select operation throws during finally | Cleanup error propagated | Selected value returned; cleanup error not propagated |

The comparison script asserts both sides of these differences explicitly; it does
not normalize timestamps or claim equivalence. SharpTestBench preserves bounded
asynchronous cleanup and cleanup failures as intentional stronger guarantees.
Python cancellation suppression and cleanup-error policies must not be inferred
from matching only the selected result. Same-time tie ordering is not compared.

## Reproduction and coverage

Run the managed integration suite with label `owned-icarus`, then:

```sh
dotnet run --project tools/SharpTestBench.Tools -c Release -- structured-reference-env --label owned-icarus
```

The helper provisions build/reference-python with cocotb==2.1.0 and
find_libpython==0.5.1, leaving development adapters untouched. It runs an actual
Icarus process and compares JSON traces. Build artifacts include the Python log,
JUnit report, both traces and structured-comparison.json. CI runs this on Linux,
Windows and macOS immediately after native integration, before another HDL build.

StructuredWaitTests covers validation, results, trigger inputs, mixed ownership,
nested calls, parent and child cancellation, suppression, asynchronous cleanup,
cleanup faults, cancellation-handler faults and native callback conservation.
The older shared characterization tests preserve First/Combine behavior.
