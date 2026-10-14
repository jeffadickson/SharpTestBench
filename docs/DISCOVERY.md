# HDL discovery and automatic packed layouts

Version 0.7 adds runtime metadata discovery, with explicit limits. This does not
claim full cocotb parity or complete connectivity extraction.

## Packed dimensions

For `logic [1:0][2:0][7:0] packed_matrix`, Verilator 5.048 preserves all three
ranges in VPI even though GPI presents one flattened vector. The optional native
extension looks up the object by its qualified name, scans VPI ranges, and checks
that their product equals the signal width. It does not cast GPI C++ handles.

```csharp
var matrix = ctx.Dut.Child("packed_matrix").Signal<W48>();
var bytes = matrix.PackedInferred<W8>();
bytes[1, 1].Write(new UInt<W8>(0xA5));
await Triggers.ReadOnly();
```

`W48` is a user-defined `IWidth`. Element width must consume complete innermost
packed dimensions and leave an indexing dimension. A four-bit element for this
layout is rejected, rather than inventing a subdivision of the eight-bit dimension.
Direction and negative indices are preserved. Field writes retain the whole-vector
ownership and deferred-write rules described in PACKED_VIEWS_AND_TIMING.md.

`DiscoverPackedLayout()` returns a layout and its source:

- `NativePackedRanges`: native dimensions were found and width-checked. Enabled
  for tested Verilator only. A packed struct may still appear as a flat vector;
  this source does not imply recovery of field names or source typedefs.
- `GpiBitRange`: only the simulator-visible bit range is known. It may be flat.
- `Unavailable`: no suitable nonempty range is available.

`PackedBits()` uses the discovered range(s) for bit indexing. For `[0:7]`, index
zero selects the most significant bit. `PackedInferred<E>()` requires native
packed ranges and refuses the flattened fallback. `Packed<E>(ranges...)` remains
available when the caller knows the HDL declaration. Original multidimensional
layout recovery is NOT implemented for Icarus, GHDL or NVC. Automatic named
packed-struct field offsets remain open on all tested backends.

## Packages, roots and connectivity

`HdlObject.Roots()` discovers top-level modules; `HdlObject.Packages()` discovers
HDL packages, not NuGet packages. Returned handles support existing child access
and binding generation. `signal.Drivers()` and `signal.Loads()` query connectivity.
A driver produces a value; a load consumes it.

Every query returns `ObjectDiscovery`:

| Status | Meaning |
|---|---|
| Observed | Some objects returned; not proof the relation is complete |
| Unavailable | Nothing returned; GPI does not distinguish empty from unavailable |
| Unsupported | Known missing implementation in the tested backend/adapter |

Never use `Objects.Count == 0` as proof of no drivers. Iterators are fully drained
before wrapping handles and duplicate handle identities are removed. All calls
require the simulator thread. GPI global iteration chooses the first adapter
providing an iterator, so results are not a union across a mixed-language design.

## Actual validation

| Backend | Roots | Fixture package/member | Driver/load query | Layout |
|---|---|---|---|---|
| Icarus 11 | top observed | unavailable | unavailable | visible bit range |
| Verilator 5.048 | top observed | discovered; marker read as 37 | unsupported | three native packed dimensions |
| GHDL 2/VPI | top observed | unavailable; no VHDL package fixture | unavailable | visible bit range |
| NVC 1.23/VHPI | unsupported by pinned adapter | unsupported | unsupported | visible bit range |

Tests compare inferred writes against independently selected HDL aliases for
both descending and mixed-direction/negative-index matrices. Native extension
checks cover capacity exhaustion, wrong width, missing object, zero capacity,
and 100 repeated range scans. Package iteration tests read a real package member
on Verilator; the marker is exposed as packed bits there, not GPI Integer.

Closing the remaining backend gaps requires new simulator-adapter implementations
or an elaborated HDL metadata provider. An empty iterator and a flat bit range
cannot reconstruct connectivity or lost field declarations.

## GitHub CI follow-up: Icarus 12

Icarus 12 exposes the fixture package, unlike the earlier Icarus 11 run. Its GPI
name lookup did not resolve the package variable. The pinned VPI package iterator
enumerates parameters only. Child access now falls back to that enumeration,
and a separate constant-member fixture checks this path; Verilator retains its
variable-member assertion. Package-variable access on Icarus remains open in #13.
