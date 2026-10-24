# Git-only development handoff

The repository contains the source **and** the saved development history. A clone or pull is enough; no Dell network share, ZIP, or previous chat is required.

1. Find an existing checkout and preserve local changes before updating it. If none exists, clone `https://github.com/jeffadickson/SharpTestBench.git` locally. Inspect remote `main` normally; do not reset unknown work.
2. Open the checkout in Codex and paste the complete [restart prompt](RESTART-PROMPT.txt), or ask it to read and follow that file.
3. Have it check and reuse this machine's Windows/WSL dependencies first, then run a fresh local baseline. The OptiPlex is already configured; old paths in historical inventories are reference data.

[Development history](../development-history/README.md) includes rounds 1–8, validation/coverage summaries, iteration/review ledgers, the final deep peer reviews and delivery state. The archived code batch ended at `6de9bb7`; subsequent commit `f19e028` implements Runner contract coverage and persisted generated sources. This documentation import preserves both. The saved results are historical, not verification of the current machine.

Keep GitHub CI disabled and leave PR #62 open/unmerged/unchanged. Use C# for production helper orchestration. Use separate Windows/WSL build outputs and a native-filesystem Linux checkout. Full Tools is ReadyToRun; standalone Runner is NativeAOT. See [native deployment](../NATIVE_WORKERS.md) for the distinction and prerequisites.

Suggested prompt:

```text
Continue SharpTestBench development on this OptiPlex. Read and follow docs/handoff/RESTART-PROMPT.txt from this checkout. Check and reuse existing Windows/WSL dependencies first, then verify a fresh local baseline. Use only Git for the handoff; keep CI disabled and PR #62 untouched.
```


Latest local follow-up: [scope200](../development-history/scope200-report.md) records
200 engineering units, current tests/coverage, native qualification and deep
reviews. Its initial push reached f203e00; its subsequent four checkpoint commits
remain local unless a later delivery record explicitly says they were pushed.
Read actual Git history/status and the latest report before continuing; do not
reset to historical SHAs or reuse old results as a fresh baseline.

The active [selection200 follow-up](../development-history/selection200-report.md) records the subsequent requested push of fee2ace and new local milestone 50/100/150 work. Read its current completion and pending-work sections alongside Git status/history; pending fixture capacity is not executed qualification. Stable requirements and traceability are in docs/requirements/.