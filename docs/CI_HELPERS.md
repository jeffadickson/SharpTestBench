# Managed helper migration

The helper implementation lives in `tools/SharpTestBench.Tools` in C#.
Use `dotnet run --project tools/SharpTestBench.Tools -c Release -- <command>`.
`docs/helper-migration.json` tracks each original helper and its migration status.
All 62 implementations and their CI callers have moved to C#. Original scripts
remain temporarily pending an explicit cleanup decision. Independent Python
scoreboards and test-only contract bindings remain under tests and examples.

The C# implementation passes argument arrays directly to native processes and
preserves failure propagation, pinned dependency hashes, platform exports and
release gates. Publishing is tested with fakes and is never invoked by development
validation. Python remains a runtime dependency of the upstream cocotb adapters
and their independent reference fixtures.

The installed-package runner requires no Python. Upstream integration uses
cocotb configuration/build APIs, direct reference-object operations, pip/venv,
the Python AST parser and Sphinx. Optional historical benchmark profiles retain
their documented provisioning requirements.
