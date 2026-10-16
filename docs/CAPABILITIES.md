# Simulator capabilities

`runtime.Capabilities` is an immutable, cached policy for the pinned native adapters.
Every `SimulatorFeature` has a status, explanation and evidence reference. This is
an explicit compatibility policy, not a live probe or certification of every release
with the same product name. Validated versions are Icarus 11/12, Verilator 5.048,
GHDL 2.0 and NVC 1.23 on Linux x64. Native Icarus CI also passes on Windows x64
and macOS ARM64; see ALPHA.md for the exact run and scope. Other backends on those
platforms and commercial backends remain unverified.

```csharp
var feature = context.Runtime.Capabilities[SimulatorFeature.ForceRelease];
context.Log($"Force/release: {feature.Status}: {feature.Explanation}");
```

| Status | Meaning |
|---|---|
| Supported | Verified contract on the documented fixtures; HDL object type still matters. |
| Conditional | Requires per-object metadata, a probe or runner configuration. |
| Unsupported | Known adapter limitation or unimplemented framework operation. |
| Unverified | Insufficient evidence; never interpreted as supported. |

`Require(feature)` accepts only Supported. Raw and typed nonbinary writes and all
force/release writes use this policy before scheduling or native mutation. Known
binary writes remain available on unknown products. Legacy `SupportsFourState`,
`SupportsNineState` and `SupportsForceRelease` delegate to the same catalogue.
Read-only discovery may probe Conditional/Unverified features; an empty iterator
means unavailable evidence, not proof that no HDL objects exist. Successful probes
do not promote the family-wide policy. Packed ranges additionally require the
native feature bit and validated per-object dimension/width checks.

| Feature | Icarus | Verilator | GHDL | NVC |
|---|---|---|---|---|
| Four-state writes | Supported | Unsupported | Supported | Supported |
| Nine-state writes | Unsupported | Unsupported | Supported | Supported |
| Force/release hold contract | Supported | Unsupported | Unverified | Unsupported |
| Immediate signal visibility | Supported | Supported | Unsupported | Unsupported |
| Root/package discovery | Conditional | Conditional | Unverified | Unsupported |
| Driver/load discovery | Unverified | Unsupported | Unverified | Unsupported |
| Package variables | Unverified | Conditional | Unverified | Unverified |
| Fixed-string NUL | Unverified | Unverified | Unverified | Conditional |
| VHDL objects | Unverified | Unverified | Conditional | Conditional |
| Signedness metadata / VCD capture | Conditional | Conditional | Conditional | Conditional |
| Native packed ranges | Unverified* | Conditional* | Unverified* | Unverified* |
| Mixed-language traversal | Unverified | Unverified | Unverified | Unverified |
| Native clocks | Supported** | Supported** | Unverified** | Unverified** |

*Native packed ranges are Unsupported when ABI bit 1 is absent. Packed-plane
transport is separately Supported only when ABI bit 0 is present; it does not
promise simulator X/Z support or direct packed adapter transfers. Current native
ABI initialization also requires bit 0. Direct VPI transfers additionally require
bit 2 and a successful per-object symbol/name/width probe; inspect
`Signal.UsesDirectPackedTransport`. Unavailable probes retain the string fallback.

**Native clocks are Unsupported when ABI bit 4 is absent. Auto mode selects them
only for the validated Icarus/Verilator families; other families use managed clocks.
Dynamic-string NUL, named-struct inference and external-thread wakeup remain
Unsupported (#14, #10, #36).

Unknown product families default to Unverified except explicit ABI information and
universal framework limitations. Matching is case insensitive with product-name
boundaries; a name merely containing “Verilator” is not recognized. VHDL Immediate
writes remain usable, but signals settle on a later delta; the visibility capability
reports that distinction rather than banning the write action.

## Reproduction and evidence

Build the solution and run `tests/SharpTestBench.Tests` for product-boundary,
complete-catalogue, ABI-bit and conservative-status checks. Run
`CS_DOTNET_ROOT=/path/to/dotnet python tools/native_faults.py` to verify actual raw
and typed operations on an unknown fake backend reject before native mutation.
`tools/run.py --sim BACKEND --label capabilities-BACKEND` runs the shared
`TimingTests.CapabilityCatalogue` fixture alongside the existing transport,
discovery and force tests. It exports the catalogue beside the trace. Captured
catalogues and result files are in `docs/evidence/capabilities/`; timing behavior
is documented in [TIMING_CONFORMANCE.md](TIMING_CONFORMANCE.md).
