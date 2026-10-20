# Simulation queue contracts

`SimQueue<T>` is a FIFO queue for tasks on the simulator thread. `Put` and `Get`
wait when no unreserved slot/item is available; their nullable cancellation token
inherits the simulation task's token by default. `TryPut` and `TryGet` are immediate
boolean operations: they neither wait nor advance simulation time.

```csharp
var samples = new SimQueue<int>(capacity: 16);
if (!samples.TryPut(42))
    await samples.Put(42, c.Cancellation);
if (samples.TryGet(out int sample))
    c.Log($"Sample = {sample}");
```

`TryGet` returns `default(T)` through its output when it returns false. Null values
are legitimate items for nullable types; use the boolean result to distinguish
an absent item from a stored null.

## Capacity and reserved handoffs

Capacity must be positive. The default is `int.MaxValue`; cocotb instead uses
nonpositive maxsize to mean unbounded. Count reports physically stored items;
IsEmpty and IsFull describe that count, not the availability of reserved permits.

A blocked consumer reserves an item permit. Until that consumer
resumes, Count may be positive while TryGet returns false. Similarly, a queued
producer can reserve a free slot: IsFull can be false while TryPut returns false.
This prevents immediate callers from bypassing existing waiters. It deliberately
differs from pinned cocotb's wake-and-recheck implementation, whose get_nowait and
put_nowait check physical occupancy without reserving for awakened tasks.

A cancellation that wins before a waiter consumes its reservation returns the
permit to the next surviving waiter. The tests cancel a producer/consumer and
make a slot/item available before cancellation cleanup runs. They verify no item
is lost, no cancelled producer inserts its value, and capacity remains reusable.
Already-cancelled tokens reject Put/Get even if they could otherwise complete
immediately. Cancellation after a wait has already successfully completed does
not retroactively undo that transfer.

## Queue families and upstream review

All 15 inventoried definitions in pinned `cocotb.queue` are reviewed in
API_REVIEWS.json, with the source hash checked by tools/SharpTestBench.Tools/AuditCommand.cs.

| Upstream surface | SharpTestBench decision |
|---|---|
| Queue / put / get | FIFO transfer with explicit reserved-waiter and cancellation semantics |
| qsize / maxsize / empty / full | Count / Capacity / IsEmpty / IsFull; positive-capacity policy differs |
| put_nowait / get_nowait | TryPut / TryGet; boolean status, no QueueFull/QueueEmpty exception types |
| AbstractQueue | Intentional difference: shared SimQueueBase coordinates built-in queues; external storage subclasses are unsupported |
| PriorityQueue / SupportsRichComparison | SimPriorityQueue uses IComparer<T> or Comparer<T>.Default; smallest first, stable ties, transactional insertion |
| LifoQueue | SimLifoQueue removes the newest stored item; waiter permits remain FIFO |

Python string/repr formatting and exception inheritance are not .NET compatibility
claims. The inventory review is not a full parity percentage.

QueueImmediateContract, QueueReservedHandoffContract and
QueueCancellationHandoffContract run in both the Verilog and VHDL integration
assemblies. They cover FIFO payloads, null/default distinction, capacity boundaries,
reservation protection and cancellation races. Existing queue/stream and
semaphore fairness tests provide additional coverage.

Queue family implementation and the storage decision are tracked in [issue #46](https://github.com/jeffadickson/SharpTestBench/issues/46).

```csharp
var undo = new SimLifoQueue<string>(capacity: 8);
var work = new SimPriorityQueue<(int Priority, string Payload)>(capacity: 16,
    comparer: Comparer<(int Priority, string Payload)>.Create(
        (a, b) => a.Priority.CompareTo(b.Priority)));
await work.Put((2, "later"));
await work.Put((1, "first"));
var next = await work.Get(); // (1, "first")
```

The default priority comparer compares the whole payload. Supply a comparer to
compare only a priority field. Equal priorities preserve insertion order; Python
heap ordering does not promise this stability. Null ordering follows the selected
.NET comparer. Comparers must be consistent, leave queued sort keys unchanged,
and never reenter queue operations. A comparison exception leaves contents intact
and returns the insertion's slot, including when a blocked producer resumes.
Reentrant queue operations throw InvalidOperationException before taking permits.

Priority storage performs O(log n) comparisons and O(n) element movement per
insertion/removal. It is deliberately a sorted list so all comparisons finish
before storage changes. This is not a heap-performance claim; large-queue
benchmarks may justify a transactional heap implementation later.

SimQueueBase provides a common consumer-facing API, but its constructor and
storage hooks are private protected. Arbitrary external storage hooks are
intentionally unsupported: storage exceptions and reentrancy must not invalidate
permit accounting. All built-in queues share cancellation and handoff behavior.
Reservations cover item counts, not payload identity; the next resumed consumer
removes the then-current head according to the queue's ordering.

QueueFamiliesContract and PriorityQueueFailureContract exercise both native
backends, including FIFO waiter handoffs, cancellation races, null payloads,
stable ties, comparer failure, reentrancy, capacity reuse and callback counts.
QueueOrderingReference also verifies timeout ownership: a task-combinator timeout
leaves a caller-owned queue wait alive and able to receive a later item.
`tools/SharpTestBench.Tools/OracleFixturesCommand.cs` compares 32 ordering/occupancy rows from each native
backend with hash-verified pinned cocotb source, including reverse priority.
This immediate-operation comparison does not claim scheduling or tie stability parity.
