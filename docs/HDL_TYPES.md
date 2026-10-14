# HDL types and generated bindings

The adapters are tested through the pinned cocotb GPI revision, not through a mock
simulator. NVC 1.23.0/VHPI supplies the broad VHDL type coverage in this release;
GHDL 2.0/VPI supplies the narrower logic/timing suite.

| C# API | Contract | Verified backend |
|---|---|---|
| UInt<W>, SInt<W> | Checked numeric values, explicit wraparound | Unit tests and Icarus/Verilator |
| LogicVector<W> | Four-state 0/1/X/Z bits | Icarus, GHDL, NVC; known values on Verilator |
| LogicSignal<W>, LogicArray | Width-checked nine-state transport and HDL-indexed slicing | GHDL and NVC |
| HdlRange.To / DownTo | Explicit direction, including null ranges | Unit tests; native null vector on NVC |
| IntegerSignal | Native integer/enum getter; int32 setter | NVC |
| EnumSignal<T> | Explicit C# enum numeric values mapped to HDL ordinals | NVC |
| HdlArray<T> | Cached typed elements, declared index bounds and order | Icarus, Verilator, NVC |
| Nested HdlArray | Arrays of arrays retaining each dimension's indices | NVC |
| HdlObject.Child | Cached hierarchy and record fields | Icarus/Verilator scopes; NVC records |
| StringSignal | Latin-1 bytes, exact fixed-string length, embedded NUL | NVC fixed strings |
| IsConstant | Reject writes to parameters/generics | Icarus/Verilator parameters; NVC generics |

## Signedness is three-valued metadata

`DeclaredSignedness` returns true, false, or null when the simulator does not expose
it. The native API uses -1 for unavailable; that must never be interpreted as true.
`IsSigned` and strict Signed/Unsigned adapters reject unavailable metadata. Raw
`Signal<W>` permits explicit ReadUInt/ReadSInt interpretation without claiming that
the HDL declaration had that sign. VHDL std_logic_vector is not intrinsically signed.

Generated bindings use strict adapters only where metadata is available. NVC logic
vectors therefore use Signal<W>; integer/enum objects use IntegerSignal. Generated
enum ordinal names are not inferred: use EnumSignal<MyEnum> with an explicit mapping
when a named C# enum is wanted.

## Ranges and strings

The two-argument HdlRange constructor infers direction for non-null ranges. Use
`HdlRange.To(3, 2)` or `HdlRange.DownTo(2, 3)` to represent empty ranges. Signal.Range
reads the native direction; it does not guess from the endpoints. Objects with no
range throw. LogicArray positional indexing is retained for compatibility; At uses
HDL indices. Empty logic arrays are supported; positive-width generic types cannot
represent them, so generated null vectors use the non-generic Signal adapter.

HDL strings are byte strings. Read/write uses Latin-1, rejecting characters above
255 instead of silently encoding a different number of UTF-8 bytes. Fixed-string
reads use their native length, preserving embedded NUL. NUL writes use indexed
character handles because GPI's whole-string setter is NUL-terminated. Character
ordinals follow the supported Linux x86-64 signed C-char ABI. Dynamic-string NUL
writes remain unsupported. Individual native setters have a void GPI return API;
their backend-specific error reporting needs further work.

## Waiting for typed value changes

`Triggers.ValueChange` accepts native scalar value objects, including `RealSignal`,
`StringSignal` and `IntegerSignal`, as well as the original logic `Signal` types.
For an enum wrapper, pass its `Raw` integer signal. Scope and aggregate handles
are rejected; select the individual value object you want to observe.

```csharp
var temperature = c.Dut.Child("temperature").Real();
await Triggers.ValueChange(temperature).WithCancellation(c.Cancellation);
await Triggers.ReadOnly(); // Let downstream HDL reactions settle before sampling.
double measured = temperature.Read();
```

Real notifications are tested on Icarus and NVC; fixed-string notifications on
NVC. Equal-value writes do not wake these waiters. Cancellation detaches native
registrations, and a trigger can be reused or awaited by multiple consumers.
Dynamic-string callbacks and other backend/type combinations remain unverified.
The C# trigger object is not identity-equivalent to Python's cached trigger object.

## Generation and limits

`Bindings.Generate(root, namespaceName: "GeneratedDut")` traverses scopes and
records, uses homogeneous array element types, and generates nested HdlArray
factories. Child lookups and typed array elements are cached. Generate-instance
arrays use HdlObject elements because their fields can differ by index. Unknown
object kinds remain HdlObject properties rather than disappearing from the API.

Generation has depth/object limits and detects sanitized C# member-name collisions.
Native iterators are drained and duplicate objects are removed. Choose a different
namespace for each independently generated binding tree to avoid duplicate width
type definitions. This does not yet infer directions, interface roles, package
semantics or all multidimensional packed layouts.

## Backend restrictions

Force/release is enabled only for Icarus, where force, deposit while forced, release,
and subsequent deposit satisfy the tested contract. NVC and Verilator did not hold
the forced value through the deposit test; these calls are rejected. GHDL force
semantics remain unverified. Passing the other tests does not imply force support.
GHDL's five skipped tests have simulator requirements recorded in JSON/JUnit.
