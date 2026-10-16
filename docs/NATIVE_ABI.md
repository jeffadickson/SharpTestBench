# Native ABI v1

Every managed simulator call goes through the platform SharpTestBench host library.
`native/abi.h` is the public C contract; `native/abi.cpp` is the adapter to the pinned
upstream GPI. The managed library no longer imports `libgpi.so` directly.

- `cs_abi_version()` returns 1. Managed runtime construction rejects other versions
  before creating a runtime or registering callbacks.
- `cs_bind_gpi()` returns null on success or a borrowed diagnostic string on failure.
  It loads and retains GPI, resolves every required symbol, and is idempotent after
  success. The native bootstrap checks it before starting .NET; direct managed
  consumers also check it. Symbol presence cannot prove binary compatibility with
  an arbitrary GPI build: the pinned dependency remains required.
- Callers must bind successfully before calling any `cs_v1_*` export. All calls,
  including binding, belong on the simulator thread. Concurrent binding is outside
  the contract. Handles are opaque borrowed pointers; callback handles follow GPI's
  one-shot/removal rules. No handle is a managed object pointer.
- Integers use explicit 32/64-bit types. Native C `long` is widened to `int64_t`,
  preserving negative values without a platform-dependent managed declaration.
  Time uses `uint64_t` and pairs of `uint32_t`. Callbacks use the C calling convention.
- Text pointers are borrowed, remain owned by GPI, and must be copied before the
  next native call. Names are UTF-8; signal byte strings retain their existing
  Latin-1/fixed-length semantics. No new ownership or string-length guarantee is
  invented by the shim.
- ABI v1 freezes the current numeric object, edge, action and range codes. Compile
  assertions detect changes in the upstream header. A future adapter must translate
  changed codes while preserving v1; incompatible public signatures require v2.
- The GPI setters still return void. This shim does not claim to report backend
  setter errors. Version 0.5 adds a feature-negotiated packed-plane extension;
  see PACKED_TRANSPORT.md for its contract and native string fallback.

The host has a stable ELF SONAME. The runner also puts the selected host directory
on the native loader path. This avoids depending on the simulator's working
directory, and makes a custom `--host` usable with a prebuilt test assembly.
The host now uses POSIX loading on Linux/macOS and wide-character Windows loading.
Native Icarus execution is validated on Linux x64, Windows x64 and macOS ARM64. See ALPHA.md for current evidence and packaging limitations.

## Validation

`tools/native_faults.py` builds isolated fake libraries in `build/native-faults`
and launches a fresh .NET process per case. It checks rejection of ABI version 99,
a missing GPI export, failure of all seven callback registrations without dictionary
leaks, 100 failed Timer tasks completing with errors, and signed values outside the
32-bit range. Fake libraries are never placed on a real simulator's search path.

The real simulator suites exercise the forwarding implementation separately.
The NuGet package contains the same native shim, its public header, pinned upstream
headers and licenses. GPI itself is still an external dependency.

## Object metadata

`HdlObject.FullName` and `TypeName` expose the simulator's qualified name and native
GPI type string. `DefinitionName` and `DefinitionFile` return null when unavailable.
Names, separators, case and source metadata depend on the simulator; clients should
not parse qualified names to implement hierarchy traversal. Use `Child` and `At`.

## Optional native packed metadata (v0.7)

Feature bit 1 (`cs_abi_features() & 2`) advertises
`cs_v1_vpi_packed_ranges(name, bits, pairs, capacity, count)`. The caller supplies
space for `2 * capacity` int32 endpoints, with capacity 1..64. Width is 1..1048576.
On success count ranges are outermost first, and their product equals width.
Statuses: 0 success; 1 unavailable; 2 invalid arguments; 3 width/product mismatch;
4 capacity insufficient. Discard pairs on any failure. Count is only defined after
argument validation; after capacity failure it reports the scanned rank.

This optional extension dynamically resolves VPI functions from the simulator.
Managed calls are gated to tested Verilator and require the simulator thread.
Other backends and older hosts retain GPI bit-range discovery. ABI version stays 1;
the packed transport feature bit 0 remains required. Native adapters are unchanged.
Pinned VPI declarations and compatibility header come from the cocotb source archive.

## C++20 release build and source documentation

The owned bridge (`host.cpp`, `abi.cpp`, `abi.h`, `platform.h`) documents every
function plus callback, loader, handle, buffer, error and simulator-thread contracts.
C++20 implementation types use `decltype` from the pinned GPI declarations; the
public header remains C-compatible and exports retain explicit C linkage.
Hostfxr function-address outputs use actual `void*` slots before conversion, avoiding
pointer-to-pointer aliasing. Failed initialization closes any returned host context.
Windows loader cleanup preserves its original error and checks UTF-8 conversion.

`tools/native_build.py` requires GCC/G++ or Clang with C++20 and LTO support.
`CXX` selects the compiler. All owned host builds use `-std=c++20 -O3 -flto -g
-DNDEBUG`, warnings as errors, and explicit export visibility. Windows uses `-static` to include the compiler and winpthread runtimes in the
host DLL; its C ABI gate rejects dynamic compiler-runtime imports before loading.
macOS/Linux retain their platform loader conventions.
Builds use the final library basename inside a temporary directory, then atomically
replace the output. This also keeps the Windows PE export-table DLL name stable
for linked native consumers. LTO failure fails the build. Debug information remains available for native
breakpoints, although optimized locals may be unavailable or reordered.
`build/native-build.json` records compiler identity, command, source and output hashes.

`tools/prepare_upstream.py` verifies the pinned archive, then applies a checked,
build-only CMake overlay: C++20, Release, required interprocedural optimization,
GCC/Clang `-O3 -g` or MSVC `/O2 /Ob2 /Zi /Zo` with release linker optimization.
It uses upstream explicit Windows export annotations instead of CMake automatic
export scanning, which cannot process the MSVC IPO objects in the tested toolchain.
It disables wheel install stripping and records original/configured CMake hashes
in `build/upstream/native-profile.json`. Upstream implementation files, comments,
licenses and checked-in headers remain unchanged. This is an explicitly modified
build configuration, not a claim that the upstream source itself was rewritten.

Neither profile enables fast-math or machine-specific instruction sets. This
preserves real-value semantics and package portability. These flags enable release
optimization; they do not establish a measured performance improvement. PGO remains separate work requiring representative data.

`tools/native_build_check.py` resolves every public export by literal name and links
and runs a C11 consumer against the actual optimized C++20 binary. CI runs this on
Linux, Windows and macOS alongside simulator, debugger and package-consumer checks.

## Packed-buffer and vectorization increment

Managed packed writes capture immutable `LogicVector` values and encode only when
applied. Replaced/discarded deferred writes own no pooled buffers. Read/write calls
use stack storage up to 1,536 bytes (4,096 bits across three planes), then rent an
exact logical slice from `ArrayPool<byte>` and return it in `finally`. Encoding clears
the entire logical span before `BigInteger.TryWriteBytes`, including padding and
unused unknown/high-Z planes. Immutable integers copy read data before pool return.

The native string fallback uses per-call stack storage through 4,096 bits, retaining
heap allocation for larger vectors. Per-call ownership is safe for reentrant callbacks.
Read conversion combines length validation with decoding while preserving malformed
length error precedence. Canonical write-plane validation uses a reduction that permits
SIMD without issuing writes before all input is checked.

Release builds explicitly enable loop and SLP vectorization: GCC
`-ftree-loop-vectorize -ftree-slp-vectorize`, Clang `-fvectorize -fslp-vectorize`.
The upstream build overlay applies the same switches; MSVC's `/O2` enables its
auto-vectorizer and `/Qvec-report:1` reports successful vectorization. No host-specific
ISA or fast-math is enabled. Owned-bridge compiler diagnostics are saved in
`native-vectorization.txt` and the compiler manifest. GCC confirms 16-byte and
8-byte vectorization of the plane-validation loop; enabling vectorization does not
imply that every conversion loop is vectorized.

## Direct vectors, batches and native clocks

Feature bit 2 (value 4) advertises `cs_v1_enable_direct` and `cs_v1_clear_direct`.
Known Icarus/Verilator signals can resolve a separate, owned VPI handle by qualified
name. Exact width and supported object kind are checked before caching. Packed reads
and writes then transfer VPI aval/bval words directly; no intermediate binary string
or private GPI layout is used. Unavailable symbols or objects retain the existing
string fallback. Operational errors after enabling direct transport are reported,
not silently retried. Runtime shutdown releases cached VPI handles.

Feature bit 3 (value 8) advertises ordered uint32 write/read batches. Writes validate
the complete input before mutation, then apply in order. Managed deferred deposits
retain last-write-wins ordering and ReadWrite flushing; nonnumeric writes are barriers.
`SignalReadBatch` reads an immutable list of 1..32-bit signals into a caller span,
rejecting X/Z. On any error discard the entire output. Neither operation advances
simulation time or promises HDL-atomic visibility; sample stable values in ReadOnly.

Feature bit 4 (value 16) advertises owned native clocks. Timer and ReadWrite callbacks
stay native for each edge; only registration failures call back into managed code.
`SimClock` Auto selects this path on validated Icarus/Verilator families. Explicit
Managed/Native modes support comparisons. Period updates start at the next complete
cycle. Stop removes both timer and queued write callbacks before releasing storage.
Failed callback removal retains stopped storage for a safe retry or late callback.

All extensions retain ABI version 1; the current feature mask is 95. Older hosts
use managed clock/scalar/string fallbacks. Calls remain simulator-thread-only.
`transport.inc` and `clocks.inc` document ownership, timing, failure and buffer contracts.
Native failure injection includes 62 packed/batch boundary checks and 43 clock checks.

The process-level controls `STB_DIRECT_PACKED=0`, `STB_BATCH_TRANSFERS=0`,
`STB_NATIVE_CLOCK=0`, and `STB_LINEAR_TEXT=0` independently disable each optimization
for regression and attribution runs. They are read once; defaults enable all four.
Linear managed text conversion builds byte planes in one pass and constructs each
BigInteger once, retaining width checks, X/Z semantics and pool cleanup.

Feature bit 6 (value 64) advertises `cs_v1_port_direction`, returning 0 unknown,
1 input, 2 output or 3 inout. ABI version remains 1. See
[port directions](PORT_DIRECTIONS.md) for ownership and backend contracts.

## ARM64 simulator processes

The native bridge, cocotb adapters, simulator executable and .NET runtime must
all have the same process architecture. The managed SharpTestBench assembly is
architecture-neutral and is JIT compiled by the selected runtime. Do not load an
x64 bridge or x64 adapter into an ARM64 Verilator process.

`tools/native_build.py` already selects the native `linux-arm64` or `osx-arm64`
.NET host pack and `tools/native_package.py` selects the matching package RID.
Build on the target architecture with its native C++ compiler; the same C++20
source uses O3, LTO and loop/SLP vectorization without x86-specific ISA flags.
Compiler auto-vectorization can target ARM SIMD where profitable; this is not a
claim that every loop vectorizes or that ARM64 is faster than x64.

Verilator CI now includes Linux x64 and Linux ARM64, compiling the pinned
Verilator version, model and bridge natively and running integration, scheduling,
wave and benchmark-value checks. `model-build.json` records host architecture
and OS alongside compiler flags. macOS ARM64 has its separate Icarus platform
job. Cross-compilation and universal macOS binaries are not provided. An ARM64 Verilator CI job does not by itself
validate a distributable Linux ARM64 NuGet release bundle.


Windows ARM64 uses a dedicated `windows-11-arm` job with ARM64 Python/.NET,
MSVC-built pinned adapters, and the MSYS2 CLANGARM64 Icarus/LLVM toolchain.
The checked CMake overlay selects ARM64 import libraries explicitly instead of
assuming every 64-bit process is x64. The exporter likewise passes ARM64 to
`llvm-dlltool`. PE machine tags for the simulator, compiler, bridge, adapters,
Python and .NET launcher/hostfxr must all be `0xaa64`; emulation does not satisfy
this gate. The job runs integration, elaboration positive/negative cases,
C ABI consumer, queue/wave tests and an installed `win-arm64` NuGet consumer.
Artifacts remain separate from the existing three-RID release bundle until its
promotion requirements are expanded. CI configuration is not itself evidence
that a new platform has passed.

**Windows Verilator remains a separate compatibility gap:** the pinned upstream
CMake build creates and installs its Verilator GPI adapter only under `UNIX`,
and its Python model runner uses Unix linker options. Availability of an ARM64
Verilator executable alone does not supply that adapter. Linux ARM64 Verilator
and Windows ARM64 Icarus are distinct validation paths.

## Batched 32-bit snapshot failure atomicity (2026-10-16)

`cs_v1_read_u32_batch` retains its existing ABI and signal-phase behavior. It now
stages values in a 32-entry stack scratch buffer (heap storage only for larger
batches), checks both unknown and high-impedance planes, and copies to the caller
only after every signal succeeds. Invalid widths, unavailable transport, or X/Z
on any signal leave the destination unchanged. This fixes partial writes in a
failed batch and avoids a heap allocation for common AXI/control snapshots.
It does not remove the underlying GPI transport yet and does not constitute
an independently measured throughput improvement. Native runner, managed
regressions, and integration CI must pass before accepting this change.
