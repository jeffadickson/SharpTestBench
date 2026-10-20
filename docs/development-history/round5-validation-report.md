# SharpTestBench round 5 validation

Local head: `32e41cd6e5337eb05d70add2fb6a12d82afae97a`. Pushed to main; GitHub CI is running.

Completed 200 logged, non-noop code edits, with successful build/test/local-commit checkpoints at 50, 100, 150 and 200. The added C# migration work is tracked separately rather than inflating that ledger.

Coverlet 10.1.0 and ReportGenerator 5.5.11 are pinned locally. All 62 helper implementations and CI callers use C#. The 62 superseded Python tool scripts have been removed. Independent Python scoreboards and test bindings remain intentional.

| Checkpoint | Managed | Generator | Helper | Contract |
|---|---:|---:|---:|---:|
| 50 | 420 | 17 | 0 | 668 |
| 100 | 468 | 17 | 0 | 668 |
| 150 | 468 | 17 | 0 | 715 |
| 200 | 480 | 17 | 124 | 742 |

Final inventory: 1,363 passing cases, a net increase of 300. Four existing dependency tests moved into C#; no assertions were dropped. The mutation control deliberately fails exactly one named case while the other 479 pass.

Real Linux qualification passed 40 paired runner scenarios, 18 independent Python-oracle workload fault runs, structured/timing parity, cleanup/lifecycle/workspace/stage/waveform checks, native ABI probes, both debugger modes, compiler metadata controls and an installed NuGet consumer. The LFSR matrix checked 1,930 positive vectors per runner against the pinned MIT upstream RTL.

Windows and Linux coverage match: **28.65% lines** (1220/4258) and **30.63% branches** (1267/4136). This measures managed unit/contract execution; HDL and native qualification are reported separately. The added helper assembly changes the denominator, so the combined percentage is not directly comparable to the earlier two-assembly baseline.

[Open HTML coverage](round5-coverage/index.html).

Historical baseline provisioning and every OS-specific optional profile are not claimed as locally executed. The pushed revision is verified on GitHub; its seven CI workflows are queued or running at this report snapshot. PR #67 was verified merged at `eb3275f83f7472be23903d5d7f5676ece7671ac9`; PR #62 remains open/draft/unmerged at `0318a864d9b570f9ffbc4857e7365ec9ed6a4736`.

## Local commits

- 661a1463a8be743938869d563d87687e64708773 test: add managed coverage reporting and functional coverage boundaries (#60)
- af02491a0f158e847b3f02ec0b57a525b5bfb96b test: cover numeric overflow and representation boundaries (#60)
- d1432d4341f73a8883b25f2e0441f195de4bc0f4 test: reject malformed and empty managed coverage evidence (#60)
- 79f674b024ce4bbee897d57e83a3df91253a4702 feat: port helper tooling to C# and expand coverage (#60)
- 32e41cd6e5337eb05d70add2fb6a12d82afae97a refactor: remove superseded Python helper implementations (#60)
