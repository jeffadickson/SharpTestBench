# SharpTestBench v0.8 migration and new APIs

CocotbSharp is now SharpTestBench. This is the same project and git history.

| Before | After |
|---|---|
| NuGet CocotbSharp | NuGet SharpTestBench |
| using CocotbSharp; | using SharpTestBench; |
| CocotbSharp.dll | SharpTestBench.dll |
| libcocotbsharp_host.so | libsharptestbench_host.so |
| src/CocotbSharp/CocotbSharp.csproj | src/SharpTestBench/SharpTestBench.csproj |
| CocotbSharpCopyRuntimeAssets | SharpTestBenchCopyRuntimeAssets |

Update package/project references and namespaces, regenerate bindings and rebuild
applications. Old assembly references are not forwarded. Do not mix hosts and
managed libraries from different package identities. Existing CS_* environment
variables and cs_v1_* C entry points are retained; ABI version remains 1.
Historical evidence remains unchanged under its original name.

## Named packed structs

A portable schema computes field positions from HDL declaration order, most
significant field first. It does not discover lost HDL typedef information.

```csharp
var layout = new PackedStructLayout(
    new PackedMember("tag", 4),
    new PackedMember("payload", 8),
    new PackedMember("flags", 4));
var packet = ctx.Dut.Child("packed_packet").Signal<W16>().Struct(layout);
packet.Field<W8>("payload").Write(new UInt<W8>(0xB7));
await Triggers.ReadOnly();
```

Offsets are calculated as tag=12, payload=4, flags=0. Duplicate names, zero widths,
unknown names, total-width mismatches and incorrect field widths are rejected.
A retrieved Field<W8> accepts UInt<W8>, SInt<W8>, or LogicVector<W8>; passing a
UInt<W16> is a compiler error. The string field name and schema are runtime checks.
Field writes compose through the existing whole-vector deposit implementation;
callers must own the whole packed signal. These are not independent HDL drivers.
Schemas can describe vectors on VHDL backends as well as SystemVerilog structs.
Automatic source-field discovery and nested source-type generation remain open.

## Adjustable managed clock

```csharp
var clock = new SimClock(clk, period: 10);
var job = ctx.StartSoon(clock.Run);
await Triggers.RisingEdge(clk);
clock.SetPeriod(6, highSteps: 2);
```

The current cycle keeps its original durations. The next cycle has four low
steps and two high steps. Invalid timing does not change the previous settings.
SetPeriod must run on the simulator thread. Since #19/#53, SimClock Auto mode
selects native callbacks on validated Icarus/Verilator hosts; explicit Managed mode
retains the original implementation. Both preserve these pulse timing semantics.
See NATIVE_ABI.md for lifecycle and fallback contracts.

## Validation

Four simulator integration suites, unit assertions, compiler rejection tests,
native failure injection and Python timing parity are rerun after the rename.
The named struct test checks a real SystemVerilog member alias, not only a C#
round trip. VHDL tests check the same overlay on a vector. The clock test checks
individual edge times and rejects invalid updates without changing timing.
