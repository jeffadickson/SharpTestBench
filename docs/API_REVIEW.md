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
- Custom Trigger subclassing is supported through protected Subscribe and SubscribeTo.
  External-assembly tests cover independent waiter ownership, cancellation, reuse,
  synchronous/duplicate completion and composite rollback. This contract is confined
  to the simulator thread; see CUSTOM_TRIGGERS.md.
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

## Native trigger review and AXI evidence

All ten inventoried _gpi_triggers definitions are now reviewed (29 total decisions;
273 inventoried definitions remain). ReadOnly/ReadWrite phase guards and callback
allocation rejection are covered by ReadOnlyTransitionContract. AXI stimulus
samples READY/VALID before the active edge in ReadOnly, advances time before
writing again, and matches the released cocotb cycle trace. Existing edge, next-time
and deposit/coalescing tests support the scoped mappings.

ValueChange now accepts scalar HdlObject families, including RealSignal and
StringSignal, and retains the original Signal overload. RealValueChangeContract
passes on Icarus and NVC; StringValueChangeContract covers NVC fixed strings.
These check unchanged writes, cancellation, trigger reuse, two waiters and cleanup.
Scopes and null targets reject before callback registration. Dynamic-string change
notifications and other backend/type combinations remain unverified.
LastSimulatorCallback now reports the actual callback kind/time and native trigger
identity. Internal callbacks have null Trigger; managed waits do not change it.
This deliberately differs from Python startup/cached-identity behavior; see
CUSTOM_TRIGGERS.md and the callback introspection tests.
Cached Python trigger identity and X-to-edge backend matrices are not claimed
equivalent. See API_REVIEWS.json for each definition's evidence and limitations.


## Queue review

All 15 inventoried cocotb.queue definitions are now reviewed: 44 total decisions,
258 inventory definitions remaining, plus aliases/dynamic exports. SimQueue now
has TryPut/TryGet and capacity/occupancy properties with real-scheduler tests for
reserved waiter handoff and cancellation. Boolean results, positive capacity and
reservation protection are explicit differences from Python. Priority/LIFO queues and comparison support are implemented with explicit .NET
semantics. External storage customization is intentionally unsupported; see QUEUES.md.


## Range and value-array review

All 20 inventoried definitions in cocotb.types._range, _abstract_array and _array
are reviewed: **64 decisions; 238 inventoried definitions remain**, plus aliases
and dynamic exports. ValueArray now supports same-length range reassignment,
HDL-index search, occurrence counting and transactional slice writes. HdlRange
supports membership and safe lazy enumeration. See VALUE_ARRAYS.md for explicit
Int32, equality, null-slice and Python protocol differences and 98 pinned oracle
cases. These review counts are not a parity percentage.


## Logic conversion review

All 24 inventoried definitions in types._logic, _logic_array and _resolve are now
reviewed: **88 decisions; 214 inventoried definitions remain**, plus aliases and
dynamic exports. Resolution and BigInteger/byte conversions have pinned-oracle
evidence. The LogicArray class-level gap remains tracked by #49. See
LOGIC_CONVERSIONS.md for strict unknown rejection, the upstream leading-don't-care
parser discrepancy, explicit RNG policy, immutable range semantics and deprecated
alias omissions. Counts represent review coverage, not full semantic parity.
