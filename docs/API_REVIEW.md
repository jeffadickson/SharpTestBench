# Evidence-backed API review

UPSTREAM_API_INVENTORY.csv is generated inventory, not a conformance result.
API_REVIEWS.json records manual semantic reviews separately, keyed by module,
definition and source line (so property getter/setter entries remain distinct).
Reviews identify their pinned source revision, source-file hash, rationale and
code/test evidence. tools/audit_reviews.py rejects changed source hashes, unknown
or duplicate definitions, missing evidence and stale code/test anchors. Run:

```sh
python tools/audit_reviews.py --source ../cocotb
```

The initial review covers the 19 inventoried definitions in _base_triggers.
It compares actual implementation with Primitives.cs and Triggers.cs, including
the deprecated accessor implementations that remain in upstream despite its
Event docstring's removal wording. No automatic claim of exact parity is made.

- Event uses the same manual-reset behavior: wake current waiters, keep future waits
  ready until Clear, and allow reuse. PrimitiveEventContract tests broadcast,
  Set/Clear before continuation, repeated Set, trigger reuse and cancellation.
  C# returns a new wait object; Python caches one. Object identity is not equivalent.
- Lock uses a disposable lease and IsLocked. PrimitiveLockContract checks FIFO
  surviving waiters, cancellation, reserved handoff, reuse and state inspection.
  Disposing a C# lease twice is a no-op; Python release on an unlocked lock raises.
  Python's unowned release operation is deliberately not added to the C# API.
- Deprecated Event/Lock names and Event data are omitted. Keep labels/payloads in
  caller variables. This is an explicit compatibility difference, not full parity.
- TriggerCallback is upstream callback-registration machinery: C# internally uses
  IDisposable subscriptions and cancellation tokens rather than exposing it.
- Custom Trigger subclassing remains a gap: Subscribe is internal abstract, so an
  external assembly cannot implement a trigger. Composing Task/Trigger waits is
  available, but is not a replacement claim for user-defined scheduler triggers.
- NullTrigger maps to Yield only as a zero-time cooperative scheduling operation;
  ordering and immediate-completion internals differ. Use events for coordination.

Classifications describe design decisions, not test pass counts. The remaining
inventory and exported aliases/dynamic members still require review under #1.

The Timer review adds a twentieth decision. Positive durations rounded to zero
are clamped to one simulator step, matching upstream Timer; the separate
Runtime.Steps conversion stays strict. Exact mode still rejects fractional steps,
and zero/negative inputs fail before rounding. TimerRoundingMinimum covers all
three rounding modes and ties-to-even behavior. Decimal/ulong input and explicit
per-call rounding replace Python's broader numeric protocols and mutable default.
