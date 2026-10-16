# Optional Icarus elaboration metadata helper

The simulation-time GPI view can flatten packed arrays. A C++ helper can expose
information that exists at a different phase: Icarus's public `ivl_target.h`
elaboration target API retains the original packed dimensions for the tested
arrays. `native/icarus_layouts.cpp` is an optional compiler target, separate from
the simulation host. It uses no private simulator object layout and changes no
runtime ABI.

```sh
python tools/icarus_layouts.py \
  --source tests/Integration/top.sv --top top \
  --ivl-root /usr/lib/x86_64-linux-gnu/ivl \
  --include /usr/include/iverilog \
  --output build/layouts.json
```

The tool compiles the helper with C++20, O3, LTO, loop and SLP vectorization. A
private temporary compiler directory links to the supplied installation on Unix
and copies it on Windows (no symlink privileges required); no
installed compiler files are modified. The compiler walks the elaborated scopes
and emits component paths, widths and ordered `[left,right]` packed ranges.
Component paths preserve dots within escaped identifiers. The exporter checks
range products, rank/depth limits and duplicate paths. Failed exports leave an
existing output document intact.

The exporter supports Linux, macOS and Windows source builds and is not required
by the normal simulator runner. Linux x64 has local execution evidence; Windows
x64, Windows ARM64 and macOS ARM64 are exercised by platform CI. Do not treat configured jobs as
successful validation until their results are green.

macOS uses Apple Clang and a Mach-O bundle with dynamic lookup of the loading
compiler's public `ivl_*` symbols. Windows uses MinGW C++ and `dlltool` to build
an import library for the public functions exported by `ivl.exe`, with static
compiler runtimes. `ivl_target.h` declares the exported target entry points.
Use an Icarus installation that includes its matching development header, and
match the C++ toolchain architecture to that compiler. `CXX` and `DLLTOOL` can
select explicit tools. No simulator-private layouts or runtime ABI changes are
involved.

On macOS with Homebrew:

```sh
python tools/icarus_layouts_check.py \
  --ivl-root "$(brew --prefix icarus-verilog)/lib/ivl" \
  --include "$(brew --prefix icarus-verilog)/include/iverilog"
```

On Windows, pass the directory containing the installed `ivl.exe` as
`--ivl-root`, and the directory containing its `ivl_target.h` as `--include`.
Put the matching MinGW `g++` and `dlltool`, Icarus `bin`, and .NET on PATH.
The CI job locates these two SDK files within `C:/iverilog` and rejects ambiguous
installations. This is a source-built optional helper, not a prebuilt plugin
package. The current
CLI accepts explicit SystemVerilog source files and a top name, using `-g2012`;
it does not reproduce arbitrary defines, include paths or parameter overrides.

| Fixture | Exported dimensions |
|---|---|
| `packed_matrix` | `[1:0][2:0][7:0]` |
| `packed_mixed` | `[-1:0][3:1][7:0]` |
| `packed_packet` | `[15:0]`; no named fields inferred |

`tools/icarus_layouts_check.py` exports metadata and runs an isolated C# consumer
against the same source fixture. The consumer constructs typed packed views from
the JSON dimensions and checks independent HDL subscripts. A same-width reversal
of the outer dimension must fail the HDL comparison. A stale explicit-source hash
must also fail. Linux, Windows and macOS CI run all three cases and retains the documents/logs.

```sh
python tools/icarus_layouts_check.py \
  --ivl-root /usr/lib/x86_64-linux-gnu/ivl \
  --include /usr/include/iverilog
```

This is a validated export/consumption experiment, **not automatic discovery in
`DiscoverPackedLayout` or `PackedInferred`**. The metadata is produced by a separate
elaboration. Explicit source and helper/header hashes are recorded, but they do
not authenticate it against a compiled VVP image or capture every transitive
include/build option. The isolated test verifies explicit source hashes; it does
not claim full binary provenance. Production import needs a single build manifest
binding compiler identity, all source dependencies/options, metadata and simulation
artifact before stale metadata can safely be accepted. Issue #11 remains open.

For #10, the tested packed struct remains a flat vector with no field names in
this export. No C++ helper can reconstruct missing names from that vector alone.
Verilator 5.048's inspected runtime VPI relationship handlers expose ranges but
not struct typespec/member traversal. Named/nested struct discovery requires a
separately validated metadata provider; caller-supplied PackedStructLayout remains
the supported portable route.

C++ is appropriate for simulator metadata traversal, native callback lifetimes,
length-aware native value access where supported, and the already implemented
packed-vector transport/native clocks. Typed APIs, scoreboards and schema checks
remain managed. Moving managed arithmetic into C++ without evidence of a bottleneck
is not part of this change.


Windows ARM64 uses the MSYS2 CLANGARM64 installation: pass its `lib/ivl` and
`include/iverilog` directories, set `CXX=clang++`, `DLLTOOL=llvm-dlltool`, and use
ARM64 Python and .NET. The tool selects ARM64 imports and the CI job verifies
actual PE architecture tags rather than accepting emulated x64 execution.
