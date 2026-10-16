# Experimental typed transfer groups

`SignalTransferGroup` binds a fixed set of signals and reuses packed buffers for
explicit ReadOnly snapshots. It also stages typed deferred deposits. Existing
`Signal<W>` methods retain their behavior. This API is experimental: fewer native
calls have not produced a consistent performance improvement across simulators.

```csharp
var address = context.Dut.Child("address").Signal<W32>();
var payload = context.Dut.Child("payload").Signal<W256>();
var transfers = new SignalTransferGroup(address, payload);
transfers.Set(address, new UInt<W32>(0x100));
transfers.Set(payload, new UInt<W256>(123));
transfers.Commit();
await Triggers.Timer(1);
await Triggers.ReadOnly();
transfers.Capture();
LogicVector<W256> observed = transfers.Get(payload);
```

The generic signal and value widths must agree at compile time. Four-state values
use `LogicVector<W>`; X/Z writes require simulator support. A group may mix widths.

* `Capture` requires ReadOnly and refreshes every member. `Get` performs no simulator
  access and returns an immutable value. A transport failure invalidates the whole
  snapshot. A phase-check failure leaves the previous snapshot intact.
* `Set` only stages values, even in ReadOnly. `Commit` rejects ReadOnly, validates
  constants before queue mutation, copies the payload, and submits staged members
  in group order. Later staging cannot alter an already queued payload.
* Normal per-signal last-write-wins ordering applies. Scalar writes can replace
  individual group members. Only adjacent surviving entries from the same
  submission are batched, in chunks of at most 64. In ReadWrite, deposits apply
  immediately under the existing scalar scheduler rule.
* `ClearStaged` discards uncommitted values. Successful Commit clears staging.
  Cancelling the submitting task does not retract committed deposits.
* Calls run on the simulator thread. The group borrows existing signal handles;
  no native group object exists, no pointer survives a call, and no disposal is
  needed. Do not carry a group across runtime lifetimes.
* Duplicate handles are rejected. Limits are 65,536 members, 1,048,576 bits per
  member, and 64 MiB of packed payload. Two reusable buffers are allocated; Commit
  additionally copies the payload to guarantee ownership.

The additive ABI exports `cs_v1_read_plane_batch` and `cs_v1_write_plane_batch`
are advertised by feature bit 5 (value 32). `UsesNativeBatch` exposes negotiation;
`STB_TRANSFER_GROUPS=0` forces the existing scalar packed exports. Older ABI-v1
hosts with packed transport work without the new exports. Simulator operations
remain ordinary GPI/VPI accesses: no simulator ABI change or private model layout
is required. Group transfers are ordered, not HDL-atomic transactions. Operational
backend failures cannot roll back earlier writes.

## Initial measurements

Five fresh processes per simulator, Release .NET 10.0.402, optimized C++20 bridge,
Icarus 12 and Verilator 5.048, Linux x64. Each process compares scalar and grouped
access to two signals (256 and 4096 bits). Reads include Capture/Get and exact-value
checks; writes include staging, scheduler turns, scalar readback and checks.
There are 50,000 read iterations and 5,000 write iterations per mode. Scalar mode
runs first; these exploratory measurements are not a randomized performance gate.

| Simulator | Operation | Scalar median (s) | Group median (s) | Scalar/group |
|---|---|---:|---:|---:|
| Icarus | Read snapshot | 1.2016 | 1.0212 | 1.18x |
| Icarus | Deferred write | 0.2096 | 0.2205 | 0.95x |
| Verilator | Read snapshot | 0.2450 | 0.3383 | 0.72x |
| Verilator | Deferred write | 0.1022 | 0.1118 | 0.91x |

Read allocations were unchanged in this workload (about 59.2 MB per sample).
Grouped writes increased allocations from about 19.6 MB to 28.2 MB because of
immutable submission storage. These results support retaining the API as an
opt-in experiment; they do not support replacing scalar operations automatically.
Larger groups, realistic bus workloads, randomized order and native profiling
remain necessary before making a general speed claim.

Raw samples: [measurements.json](evidence/transfer-groups-2026-10-16/measurements.json).
Reproduce with `python tools/run.py --project Benchmark --exact TransferGroupBenchmark
--sim icarus` (or `--sim verilator`). Benchmark methods verify every observed value.

Tests cover mixed-width snapshots, X/Z, staging lifetime, scalar overrides,
constant rejection, phase restrictions, empty groups and the 64-member chunk
boundary. Native fault tests cover descriptor bounds and rejection before writes.
The same managed tests also run against the previous native host at e72ffe8.
