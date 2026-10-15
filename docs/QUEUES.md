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

A blocked consumer reserves the next item assigned to it. Until that consumer
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

## Upstream review and remaining gaps

All 15 inventoried definitions in pinned `cocotb.queue` are reviewed in
API_REVIEWS.json, with the source hash checked by tools/audit_reviews.py.

| Upstream surface | SharpTestBench decision |
|---|---|
| Queue / put / get | FIFO transfer with explicit reserved-waiter and cancellation semantics |
| qsize / maxsize / empty / full | Count / Capacity / IsEmpty / IsFull; positive-capacity policy differs |
| put_nowait / get_nowait | TryPut / TryGet; boolean status, no QueueFull/QueueEmpty exception types |
| AbstractQueue | Gap: SimQueue is sealed; pluggable queue storage is not exposed |
| PriorityQueue / SupportsRichComparison | Gap: no simulation-aware priority queue/comparer API yet |
| LifoQueue | Gap: no simulation-aware LIFO queue yet |

The standard .NET PriorityQueue and Stack do not themselves supply simulation
waiting/cancellation semantics, so their availability does not close these gaps.
Python string/repr formatting and exception inheritance are not .NET compatibility
claims. The inventory review is not a full parity percentage.

QueueImmediateContract, QueueReservedHandoffContract and
QueueCancellationHandoffContract run in both the Verilog and VHDL integration
assemblies. They cover FIFO payloads, null/default distinction, capacity boundaries,
reservation protection and cancellation races. Existing queue/stream and
semaphore fairness tests provide additional coverage.

Remaining queue families and storage customization are tracked in [issue #46](https://github.com/jeffadickson/SharpTestBench/issues/46).
