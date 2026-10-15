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
-DNDEBUG`, warnings as errors, and explicit export visibility. Windows statically
links the compiler runtimes; macOS/Linux retain their platform loader conventions.
LTO failure fails the build. Debug information remains available for native
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
optimization; they do not establish a measured performance improvement. PGO and
backend-specific packed transfer remain separate work requiring representative data.

`tools/native_build_check.py` resolves every public export by literal name and links
and runs a C11 consumer against the actual optimized C++20 binary. CI runs this on
Linux, Windows and macOS alongside simulator, debugger and package-consumer checks.
