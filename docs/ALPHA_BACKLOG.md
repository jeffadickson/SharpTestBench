# Alpha backlog and broader parity work

The release gates and the full parity backlog are related but not identical.
A source alpha exists; a production-ready distribution and complete cocotb parity
are not established. GitHub issues are authoritative for completion state.

| Area | Present evidence | Remaining work |
|---|---|---|
| Object model | Typed signals, explicit packed schemas, arrays, discovery and selected force/release tests | Named-field inference, broader packed metadata, package/structure matrix, connectivity and dynamic-string edges (#8, #10–#14) |
| Simulator compatibility | Linux Icarus/Verilator/GHDL/NVC; native Icarus on Windows x64 and Mac ARM64 | Licensed commercial fixtures and actual mixed-language traversal (#9, #26); other OS/backend combinations remain unverified |
| Timing and concurrency | Ordering traces, cancellation stress, native callback faults, structured groups with joined parent-cancellation callback failures | Full timing conformance beyond the Gather/Select tests (#44), full upstream audit (#1), shutdown investigation (#16), external-thread wakeups (#36) |
| Packaging | Native packages and fresh installed Icarus consumers pass on Linux x64 / Windows x64 / Mac ARM64; concurrent workspaces tested | Public publication and broader adapter/architecture distribution (#22–#23) |
| Performance | Microbenchmarks plus AXI-Lite-shaped memory comparison with repeated runs and fault negatives | Broader AXI bursts/concurrency, DSP and CPU workloads, with comparable correctness and throughput measurements (#18); native transport/clock work (#17, #19) |
| Debugging | Linux managed breakpoint/local/exception and separate GDB startup/finish checks pass | Broader OS and simultaneous mixed debugger validation (#45) |
| Functional coverage | Explicit bins and bounded two-axis automatic crosses with exclusions, atomic counters and stable flat snapshots; managed and native acceptance contracts | General multidimensional crosses and broader coverage planning remain outside the bounded API (#29); functional bins do not measure HDL instructions |
| API compatibility | Inventory and explicit task ownership mapping | Audit every entry; classify essential semantics, idiomatic .NET alternatives, and Python-only conveniences (#1, #35, #37) |

## Release order

1. Maintain the implemented owned Gather/Select contracts and reference tests (#44),
   including explicit differences from released cocotb 2.1 cancellation.
2. Retain native package/isolated-consumer/workspace CI gates; finish public release
   preparation and preserve Linux debugger gates (see DEBUGGING.md).
3. Complete the API audit and shutdown investigation before making stronger
   compatibility or reliability claims.
4. Expand realistic benchmarks and simulator/object coverage with explicit
   evidence per backend. Commercial testing requires access to licensed tools;
   adding an adapter name is not validation.

Windows/macOS execution is no longer wholly outstanding: see ALPHA.md for the
passing native matrix. Distribution, additional architectures, and broader
backend support remain open. Python-specific spelling or reflection behavior
should not be treated as essential verification semantics without a use case.
