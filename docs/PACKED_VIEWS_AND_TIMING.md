# Packed object views and timing validation (v0.6)

## Typed packed arrays and fields

Simulators can flatten packed dimensions and packed structs into one bit vector.
The new views let the caller provide the layout explicitly, with bounds and total
width checks. They do not infer missing HDL declarations.

```csharp
// HDL: logic [1:0][2:0][7:0] samples; total width 48.
public readonly struct W48 : IWidth { public static int Bits => 48; }
var raw = ctx.Dut.Child("samples").Signal<W48>();
var samples = raw.Packed<W8>(HdlRange.DownTo(1, 0), HdlRange.DownTo(2, 0));
samples[1, 2].Write(new UInt<W8>(0xA5));
await Triggers.ReadOnly();
UInt<W8> value = samples[1, 2].ReadUInt();
```

Dimensions are outermost first. The rightmost element in declared order occupies
the least-significant bits. Ascending/descending ranges, nonzero bounds, negative
indices and mixed directions are supported. `PackedLayout` copies the supplied
dimensions and validates rank, bounds, multiplication overflow and maximum width.

```csharp
// HDL packed struct: tag[3:0], payload[7:0], flags[3:0].
var packet = ctx.Dut.Child("packet").Signal<W16>();
var tag = packet.Field<W4>(12);
var payload = packet.Field<W8>(4);
var flags = packet.Field<W4>(0);
tag.Write(new UInt<W4>(0xA));
payload.Write(new UInt<W8>(0xBC));
flags.Write(new UInt<W4>(0xD));
// Settles to 0xABCD. Wrong field widths are rejected by the C# compiler.
```

Fields expose four-state `Read`, `ReadUInt`, `ReadSInt` and width-checked Write
methods. A field write is a **whole-object deposit**: it merges the selected bits
with the latest pending value, or with the currently visible value when no write
is pending. Multiple fields and multiple wrappers for the same handle compose.
A later whole-vector deposit replaces earlier field changes. Untouched X/Z bits
are preserved. Field writes obey constant and ReadOnly guards.

These overlays require ownership of the whole packed object. HDL writes between
the snapshot and deposit may be overwritten. They are not separate HDL drivers,
and do not expose per-field Force/Release. Use the underlying full signal for
those actions. Native struct-member discovery and automatic dimension/field-layout
generation remain separate work.

## ReadWrite bug found and fixed

NVC exposed an actual lost-update bug in the first implementation: a deposit during
ReadWrite was not yet visible through a native read, so a second field update could
replace the first. The runtime now retains the latest submitted bit value across
ReadWrite callbacks and clears that phase-local state when another callback phase
begins. The normal pending-write map remains separate. This also handles a queued
whole-vector write followed by a field update during ReadWrite.

Registration is now attempted before pending write state is mutated. An injected
ReadWrite registration failure must leave all write/preview maps empty.

## Independent checks

- Verilog fixtures contain real multidimensional packed arrays and a packed struct.
  HDL-side subscript and struct-member wires independently verify the C# layout.
- Mixed ascending/descending dimensions include negative indices. Whole writes,
  sibling updates, replacing updates, retained X/Z, ReadOnly and bounds failures
  are covered on Icarus and Verilator.
- NVC and GHDL test field composition inside ReadWrite and after queued whole writes.
- Compiler checks reject writing an eight-bit UInt to a four-bit field.
- An installed NuGet consumer writes a packed element and reads its HDL-side alias.

## Delta cycles and driver resolution

Both HDL fixtures have three successive signal assignments. Twenty source changes
per run issue two coalesced deposits, wait for first/last-stage value changes,
and check the final value in ReadOnly **without simulation time advancing**.
The Icarus C# trace exactly matches the equivalent pinned Python cocotb test.
This extends the earlier edge/NBA/ReadOnly comparison; it is not a complete timing
conformance suite. Verilator may optimize the intermediate evaluations, so no
particular number of native delta callbacks is asserted there.

The VHDL fixture has two independent concurrent drivers on one `std_logic` signal.
All 81 ordered pairs from U/X/0/1/Z/W/L/H/- are checked against the IEEE resolution
table. The table was checked against the installed GHDL IEEE package source;
its path and SHA-256 are retained in the evidence. Both NVC and GHDL pass, including
weak/strong drive conflicts, high impedance and uninitialized values.

v0.7 adds status-bearing discovery APIs and native Verilator packed-layout
inference; see DISCOVERY.md. Still open: driver/load extraction on unsupported
backends, packed struct field inference,
broader struct/backend matrices, force/deposit ordering beyond the existing Icarus
checks, and mixed-language/commercial-simulator timing validation.
