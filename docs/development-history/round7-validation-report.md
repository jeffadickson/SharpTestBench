# SharpTestBench development round 7

Repository: jeffadickson/SharpTestBench. Branch: main. Head: `eb9e2f6c68e51e7a807f903e02257510b7b01973`.

Completed 200 separately recorded C# test-case additions. At each 50-addition checkpoint, built the full solution in Release with locked restore, ran core, generator, helper and contract suites, checked whitespace, and made a local commit. Nine additional regression cases cover non-object DAP messages and negative workload counts, for 209 new cases in total.

PowerShell **7.6.7** was installed globally in Ubuntu 24.04 WSL using the [official Microsoft package installation procedure](https://learn.microsoft.com/powershell/scripting/install/install-ubuntu). It launches as normal user `jeffd`. The final isolated Linux validation was driven by PowerShell, including its build and native simulator commands.

Changes:

- Benchmark parsing requires one exact checksum, two distinct expected measurements, and positive finite numeric values. It rejects trailing garbage, duplicate metrics, unknown metrics and ambiguous checksum evidence while accepting standard line endings and whitespace.
- Debugger protocol coverage checks segmented UTF-8 reads and consecutive frame boundaries for 50 Unicode/chunk-size combinations. Non-object JSON messages are rejected. [Protocol framing specification](https://microsoft.github.io/debug-adapter-protocol/overview.html#base-protocol).
- Performance comparisons pair records by unique, nonnegative round IDs rather than input order. Missing metrics, nonfinite values and unrepresentable ratios are rejected.
- CPU program iteration counts must fit a positive signed 16-bit immediate. Negative DSP/AXI plan counts now fail instead of silently returning a prefix-only plan. Tests cover reset scheduling, bus delays, strobes, address controls, loop instructions and deterministic seeds.

Validation:

- Windows and isolated Linux: 480 core + 17 generator + 540 helper + 742 contract cases = **1,779 passing cases** per platform.
- Real Linux benchmark: five fresh measured processes per language, ten complete validated records.
- Real managed debugger: breakpoint, inspected local `41`, expected exception and clean expected-failure exit passed.
- CPU workload at two iterations: C# and independent Python traces agree with 24 commits; all six injected fault controls were detected.
- Linux hashes match every changed source file in the Windows checkout.
- Fresh Coverlet/ReportGenerator reports were generated on both platforms; merged Cobertura and JSON totals agree. Windows managed coverage: 29.39% lines, 31.85% branches. Coverage is for managed assemblies, not HDL/native code.
- GitHub Actions remains disabled. PR #62 is left untouched. Remote delivery status is recorded separately in `round7-delivery.json`.

Checkpoint commits:

- c648d558af8bab63351f106438469db444f495b1 fix: validate complete benchmark evidence (#60)
- 00adee9cc70a3e6aac0c10139dccab61c8a15a43 fix: validate segmented debugger protocol messages (#60)
- dcec5c9517d3568431853d31a6646cf211da41a7 fix: validate paired performance measurement rounds (#60)
- eb9e2f6c68e51e7a807f903e02257510b7b01973 fix: validate workload plan boundaries (#60)
