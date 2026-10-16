# Packed four-state transport (v0.5)

`Signal<W>.Read()` and `Write(LogicVector<W>)` now cross the managed/native boundary
as three packed byte planes. Wide UInt/SInt operations use the same path. The
32-bit integer write fast path, small `ReadUInt`, explicit `ReadBits`/`WriteBits`,
and nine-state `ReadLogic` retain their existing paths.

Each plane contains ceil(width/8) bytes, least-significant byte first. Plane order
is value, unknown, high-Z. Unknown includes both X and Z; high-Z distinguishes Z.
Value bits under unknown bits are zero. Unused high bits must be zero. The native
adapter validates the exact buffer length, HDL width, masks, actions and constants.
It returns explicit status codes. Read buffers must be discarded on failure.

Known Icarus/Verilator objects now use optional direct VPI vector words after
a checked per-object probe. The bridge owns separately name-resolved VPI handles;
it does not reinterpret private GPI objects or modify upstream adapters. Other
backends and unavailable objects retain pinned GPI binary-string functions.
Direct VHPI transfer is not implemented. Nine-state symbols such as U/W/L/H/- are rejected by four-state reads; use
`ReadLogic()` to preserve them.

Deferred writes capture immutable values and encode into stack/pool buffers only
when applied; no rented buffer survives a scheduler turn. Deposit
coalescing, constant checks, ReadOnly restrictions, force/release restrictions,
and Verilator X/Z rejection are unchanged. Native setter success means the shim
validated and forwarded the request; GPI's void setter cannot report backend errors.
Direct VPI operations additionally check available VPI error status.

## ABI extension

ABI version remains 1; no existing export signature changed. New hosts expose
`cs_abi_features()`, with bit 0 for packed planes. New managed libraries require
that feature and report an explicit upgrade error for older hosts. Older v0.4
managed callers can use the existing v1 exports on the new host.

`cs_v1_read_planes` and `cs_v1_write_planes` receive handle, uint32 width, byte
pointer and uint32 length; writes also receive an int32 action. A caller must
supply an accessible buffer of that length and bind GPI successfully beforehand.
Calls remain simulator-thread-only.

| Status | Meaning |
|---|---|
| 0 | Validated and forwarded/converted |
| 1 | Invalid pointer, size, masks, padding or action |
| 2 | HDL width mismatch |
| 3 | Unsupported nine-state symbol in a four-state read |
| 4 | Native allocation failed |
| 5 | Null value or unexpected native string length |
| 6 | Write to a constant |
| 7 | Direct VPI operation failed |
| 8 | Native clock callback registration/removal failed |

## Measured effect

`tools/packed_benchmark.py` compares the previous string write/parse implementation
with packed write/read on Icarus 11.0. Each mode performs 100 warmups and 500
immediate round trips of a known value with its top bit set. Three fresh processes
alternate mode order. The table reports medians. Every round trip checks its value.

| Width | String path, 500 round trips | Packed path | Speed ratio | Managed bytes/round trip: string → packed |
|---|---:|---:|---:|---:|
| 256 bits | 86.85 ms | 3.91 ms | 22.2× | 43,289 → 336 |
| 4,096 bits | 1,305.66 ms | 35.82 ms | 36.4× | 4,678,202 → 4,296 |

These results compare two C# conversion paths, not C# against Python. They exclude
build/startup and simulation-time advancement, and do not establish a full-design
speedup. Managed allocation counts exclude native malloc and GPI allocations.
The old path repeatedly shifted immutable BigIntegers; wide vectors especially
benefit from eliminating those intermediate values. Raw samples and method details
are in `docs/evidence/v0.5/packed-benchmark.json`.

## Correctness evidence

- 24 deterministic vectors at each of 73, 256 and 4,096 bits on Icarus and Verilator;
  alternate packed and string writes, compare both read representations. Icarus
  includes mixed X/Z; Verilator uses known bits and retains its X/Z rejection test.
- Deferred last-write-wins checks at each width.
- GHDL and NVC preserve nine-state reads and reject them through four-state transport.
- 18 isolated native boundary assertions include guard bytes, malformed masks,
  nonzero padding, size/width errors, constants and unsupported native values.
- Installed NuGet consumers exercise packed X/Z on Icarus and NVC, plus a wide
  generated binding on Icarus.

These v0.5 timings are historical. Current direct-vector, linear-string and batch
results are in [NATIVE_PERFORMANCE.md](NATIVE_PERFORMANCE.md), with independently
disabled features and preserved-binary comparisons.
