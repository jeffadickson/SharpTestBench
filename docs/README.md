# Documentation

- [Roslyn incremental bindings](GENERATOR.md)
- [Alpha packages and installed consumers](PACKAGING.md)
- [Concurrent simulator workspaces](WORKSPACES.md)
- [Dual-runner CI parity and failure evidence](DUAL_RUNNER_PARITY.md)

- [Alpha backlog and parity priorities](ALPHA_BACKLOG.md)
- [cocotb 2.1 concurrency comparison](CONCURRENCY_COMPARISON.md)

- [Cross-platform alpha target and example](ALPHA.md)
- [Quick start and installation](../README.md)
- [Contribution guide and repository layout](../CONTRIBUTING.md)
- [Development test-tool assessment](TEST_TOOLING.md)
- [All tracked GitHub tasks](TASKS_INDEX.md)
- [Remaining work narrative](OPEN_WORK.md)
- [Simulator capability policy](CAPABILITIES.md)
- [Compatibility inventory](COMPATIBILITY.csv)
- [Validation and limitations](VALIDATION.md)
- [Hardware types and generated bindings](HDL_TYPES.md)
- [Timing conformance and reference traces](TIMING_CONFORMANCE.md)
- [Waveform controls and artifacts](WAVEFORMS.md)
- [Structured logging and source locations](LOGGING.md)
- [Functional coverage and export](COVERAGE.md)
- [Regression stages and failure policies](REGRESSION_STAGES.md)
- [Fixture setup and cleanup](FIXTURES.md)
- [Task ownership and cancellation](TASKS.md)
- [Upstream task API mapping](TASK_API_MAP.md)
- [Packed fields and timing](PACKED_VIEWS_AND_TIMING.md)
- [Metadata discovery](DISCOVERY.md)
- [Native ABI](NATIVE_ABI.md)
- [Packed transport](PACKED_TRANSPORT.md)
- [Rename migration, named structs and adjustable clocks](MIGRATION_0_8.md)

## Architecture

The simulator loads a small C host, which starts .NET and calls the managed test
entry point. The C ABI shim forwards handle/value/callback calls to pinned cocotb
GPI adapters. The managed runtime schedules tasks on the simulator thread and
coordinates deferred writes and simulation phases. Python scripts orchestrate
builds and reference comparisons; they are not embedded in the C# test process.

- [Debugger investigation](DEBUGGING.md) — opt-in reproducer and unresolved attachment result.

- [API semantic reviews](API_REVIEW.md) — source-pinned decisions, gaps and evidence.

- [AXI memory verification and benchmark](../examples/AxiMemory/README.md) — scoreboards, fault detection and scoped measurements.
