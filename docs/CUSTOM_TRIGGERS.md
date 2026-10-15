# Custom triggers and simulator callback inspection

A test or driver assembly can derive from `Trigger`. Override `Subscribe` using
`protected override` outside SharpTestBench. It registers one independent wait and
returns an `IDisposable` that detaches that wait. `SubscribeTo` lets a custom trigger
own an existing trigger subscription without exposing native callback handles.

```csharp
public sealed class EitherReady(Trigger first, Trigger second) : Trigger
{
    protected override IDisposable Subscribe(
        SimRuntime runtime, Action<Exception?> complete)
        => SubscribeTo(Triggers.First(first, second), runtime, complete);
}

// The custom object also works with cancellation, First, Combine and WithTimeout.
await new EitherReady(Triggers.RisingEdge(request), Triggers.RisingEdge(response));
```

Ordinary composition does not require subclassing. The extension point is useful
when a reusable driver needs its own registration logic or lifetime accounting.
Each subscription must have independent state so one trigger can have multiple
waiters and be reused after completion or cancellation.

## Subscription contract

- Register, notify and dispose on the simulator thread. This does not provide an
  external-thread wakeup mechanism; that remains separate work.
- Invoke `complete(null)` for success or `complete(exception)` for failure.
  Synchronous completion during `Subscribe` is supported. A per-subscription guard
  ignores duplicate completion and late notifications after detachment, including
  inside combinators; a duplicate cannot satisfy a different `Combine` child.
- Return a non-null, idempotent, nonthrowing detach handle. Release every child
  subscription and event handler it owns. If registration throws, roll back resources
  acquired before throwing. Do not throw after reporting completion.
- Cancellation and losing a race detach the subscription. Notification alone does
  not transfer ownership of your resources to another consumer.

`tests/Integration/TriggerExtensionTests.cs` compiles subclasses in an external
assembly. The simulator tests cover two waiters, cancellation, reuse, synchronous
and duplicate completion, completion/registration errors, timeout, losing First,
Combine and partial-registration rollback. The same source runs in the Icarus
and NVC integration assemblies.

## Last actual simulator callback

`runtime.LastSimulatorCallback` is a nullable immutable `SimulatorCallbackInfo`:

| Field | Meaning |
|---|---|
| `Kind` | Native callback category: Start, End, Timer, Edge, ReadWrite, ReadOnly or NextStep |
| `Time` | Simulation time in integer simulator steps |
| `Trigger` | The exact originating built-in native trigger object; null for runtime-owned callbacks |

The snapshot is null before the first native callback and is updated when a
callback actually dispatches, before continuations run. Registering, rejecting or
cancelling a callback does not update it. Managed events and Yield leave it intact.
Read it on the simulator thread. It is runtime-wide, not a per-task explanation of
why a particular continuation resumed. Retain a copy if later code needs the value.

```csharp
var timer = Triggers.Timer(10);
await timer;
var wakeup = c.Runtime.LastSimulatorCallback;
// wakeup.Value.Kind == CallbackKind.Timer
// ReferenceEquals(wakeup.Value.Trigger, timer)
```

A custom trigger wrapping a timer reports the native timer as the origin. Internal
regression deadlines and deferred-write callbacks report their category/time with
`Trigger == null`; no synthetic user trigger is invented. A dedicated expected-
timeout test verifies the internal deadline case. Other tests distinguish Timer
from NextTimeStep, preserve native trigger identity through custom wrappers, and
check that snapshots remain unchanged by managed waits or rejected registration.

This is an idiomatic diagnostic alternative to cocotb's `current_gpi_trigger()`.
It intentionally includes internal callbacks and has no fabricated startup Timer
or Python cached-trigger identity semantics. It does not establish full scheduler
or upstream API equivalence.
