# Development instructions

- Read README.md, CONTRIBUTING.md, and docs/README.md before editing.
- Preserve all existing changes. Do not reset, clean, delete files, push, publish, trigger remote CI, or change system settings without explicit user approval.
- Use the .NET 10.0.402 SDK pinned in global.json. Managed checks: `dotnet build SharpTestBench.slnx -c Release` and `dotnet run --project tests/SharpTestBench.Tests -c Release`.
- On this workstation use Ubuntu-24.04, /mnt/c/dev/SharpTestBench, and `source /mnt/c/dev/Enter-Development.sh` for native simulation. Keep its environment separate from RISCue.
- For simulator changes run `python tools/run.py --sim icarus --label <unique-label>` and the corresponding Verilator test. Report backend skips and unavailable tools accurately.
- Respect simulator-thread, cancellation, phase, and native ABI rules in CONTRIBUTING.md. Preserve upstream pins and notices.
- Keep generated output under ignored build/bin/obj directories. Do not overwrite historical evidence.
