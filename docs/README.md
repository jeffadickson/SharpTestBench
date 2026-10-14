# Documentation

- [Quick start and installation](../README.md)
- [Contribution guide and repository layout](../CONTRIBUTING.md)
- [All tracked GitHub tasks](TASKS_INDEX.md)
- [Remaining work narrative](OPEN_WORK.md)
- [Compatibility inventory](COMPATIBILITY.csv)
- [Validation and limitations](VALIDATION.md)
- [Hardware types and generated bindings](HDL_TYPES.md)
- [Functional coverage and export](COVERAGE.md)
- [Regression stages and failure policies](REGRESSION_STAGES.md)
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
