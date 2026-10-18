# Native cocotb GPI retirement — engineering migration plan

Status: migration specification; existing production runtime still calls cocotb's GPI.
Owner: SharpTestBench C++20 native subsystem. Tracking: #63, #60.

## Hard boundary
Preserve `cs_abi_version() == 1`, exports in `native/abi.h`, callback-thread affinity,
numeric contracts, unmanaged .NET bootstrap and native clock/packed optimizations.
A native C++20 forwarding shim is **not** an independent simulator adapter while
`cs_bind_gpi` still loads `libgpi`.

## Existing component inventory
| Existing component | Current dependency | Migration |
|---|---|---|
| `native/host.cpp` | Calls `cs_bind_gpi` | Bind an owned simulator backend before CLR startup |
| `native/abi.cpp` | Includes `upstream/gpi.h`, forwards GPI methods | Separate ABI facade and owned backend vtables |
| `native/transport.inc` | Mixed GPI string fallback and direct VPI | Route fallback through owned VPI signal interface |
| `native/clocks.inc` | Existing native simulator clocks | Keep and qualify after adapter change |
| `tools/run.py` | `cocotb_tools.config`, bootstrap, GPI vars | Replace with SharpTestBench .NET runner |
| `tools/prepare_upstream.py` | Installs upstream native cocotb | Restrict to temporary oracle / remove from release |

## Implementation sequence
1. Define owned internal backend interface for callbacks, handles, signal operations,
   metadata, time and shutdown. Keep public `cs_v1_*` exports unchanged.
2. Introduce independent Icarus VPI bootstrap; call into existing hostfxr entry.
3. Implement VPI signal operations and opaque handle ownership.
4. Implement simulator callback registration, phase semantics and cancellation.
5. Test existing counter, packed value, stress, repeated reset and injected failures
   against new backend. Run legacy GPI path only as a comparison oracle.
6. Add Verilator path, preserve its two-state capability restrictions.
7. Make the .NET runner provision supported native packages without cocotb/Python.
8. Delete production GPI dependency after both simulator paths qualify.

## Non-negotiable exit checks
Production native and runner paths must not import or load upstream `gpi.h`,
`libgpi`, `cocotb_tools` or `COCOTB_BOOTSTRAP`. The legacy oracle path can
remain as an explicitly optional test tool until its removal is independently approved.
For every simulator, measure callback ordering and timing, signal read/write parity,
test lifecycle/teardown, crash classification and exact test identity attestation.
No claim of independent GPI replacement until these execute successfully in CI.

### Compatibility warning
The GPI object pointer model must not be passed directly to VPI calls. Introduce
owned handle wrappers or a backend handle table, enforce lifetime validity, and
do not assume simulator object kinds or callback ownership are interchangeable.
