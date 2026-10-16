# Port direction metadata and binding guards

`HdlObject.Direction` reports `PortDirection.Input`, `Output`, `InOut`, or
`Unknown`, relative to the HDL module that declares the port. Direction is read
from elaborated simulator metadata. Signal names, write permissions and driver
counts are never used to guess direction.

```csharp
var request = context.Dut.Child("request");
var checkedRequest = request.RequireDirection(PortDirection.Input).Signal<W32>();
```

`RequireDirection` returns the same object on success. A known mismatch throws
`InvalidOperationException`; unavailable metadata throws `NotSupportedException`.
Requiring Unknown or an invalid enum value throws `ArgumentOutOfRangeException`.
All access stays on the simulator thread. Unknown covers both non-port objects
and unavailable metadata; it does not assert that an object is an internal signal.

The Roslyn manifest accepts an optional exact-case direction attribute:

```xml
<Bindings namespace="Generated" class="Ports">
  <Signal name="Request" width="32" direction="Input"><Child name="request"/></Signal>
  <Signal name="Response" width="32" direction="Output"><Child name="response"/></Signal>
</Bindings>
```

The generated constructor checks actual direction before creating the width-typed
signal. Invalid direction names produce STBGEN001 at compilation. HDL metadata
checks occur when connecting to the simulator, not inside Roslyn. Omitting the
attribute preserves existing behavior. Directions do not create read-only signal
types or prohibit force/release: driving an HDL output deliberately remains a
valid verification operation. Protocol roles such as AXI initiator/target are not
inferred from these individual port directions.

| Backend/host | Verified behavior |
|---|---|
| Icarus 12 | Top-level and nested input/output/inout ports; internal objects Unknown |
| Verilator 5.048 | Top-level input/output/inout ports; tested flattened nested ports Unknown |
| GHDL/NVC/commercial or unrecognized families | Unknown; direction provider not implemented/validated |
| Older ABI-v1 hosts | Unknown without resolving the new native symbol |

Icarus's connected net/reg objects do not expose `vpiDirection`. The bridge
enumerates the actual containing module's `vpiPort` objects and matches the
local declaration name. Nonmatching/renamed/complex connections are not inferred.
Verilator exposes direction directly on its VPI variable handle, but flattening
can replace nested port flags with `vpiNoDirection`; that loss is preserved as
Unknown. Alternate elaboration options can change metadata availability.

Native ABI version remains 1. Feature bit 6 (value 64) advertises
`cs_v1_port_direction`; the feature mask is now 95. Bit 5 is not advertised
(the abandoned transfer-group experiment is not part of main). The extension
borrows the name and retains no native object. Temporary VPI handles are released,
iterators are exhausted, and borrowed strings are copied before another VPI call.
No simulator-private layout is used. Existing optimized C++20 build flags remain.

Validation includes actual ports on both simulators, repeated queries, non-port
objects, mismatches, missing metadata, generated bindings, seven additional
Roslyn cases and a run against the prior e72ffe8 native host. Local full suites:
Icarus 75 passed/2 existing skips; Verilator 73 passed/4 existing skips. Seventeen
Roslyn cases pass. The C11/native export gate resolves 52 unmangled exports.
Windows/macOS CI runs the same Icarus contract; platform results must be checked
before claiming validation for a particular revision.
