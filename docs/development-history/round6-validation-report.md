# SharpTestBench development round 6

Repository: jeffadickson/SharpTestBench. Branch: main. Head: `b272e007ede2c34c9a4cd2507526a1b0a818a36d`.

Completed 200 separately recorded C# test-case additions, with full local Release builds, three executable C# suites, the retained contract suite, and a local commit at each 50-addition checkpoint. Seven additional regression cases accompany the platform fix and CRC-64 fixtures, for 207 new cases overall. The iteration ledger records a different source hash before and after every addition.

All production helper implementations remain C#. Coverlet 10.1.0 and ReportGenerator 5.5.11 were already installed and are retained; fresh reports were generated on Windows and Linux.

Changes:

- 50 independent CRC-32/CRC-16 byte-stream vectors generated with CPython's standard-library zlib and binascii implementations.
- 50 LFSR linearity, reversal and output-bound cases, including maximum-width seeds.
- 50 malformed LFSR stimulus rejection cases.
- 50 AXI held-payload cases with invalid handoff and counter evidence controls.
- Three runner-manifest regressions and a fix selecting the platform's native host library instead of a hardcoded Linux library.
- CRC-64/ECMA-182 and reflected CRC-64/XZ fixtures: 64-bit state, LOOP/REDUCTION Verilog wrappers, managed published check vectors and plan attestation, and expanded CI matrix. [Pinned upstream Verilog](https://github.com/alexforencich/verilog-lfsr/blob/c1f86d036745935f3812ec6ddc7821c70ef4c9de/rtl/lfsr.v); [independent CRC check values](https://reveng.sourceforge.io/crc-catalogue/17plus.htm#crc.cat.crc-64-ecma-182). Vendored source and MIT license unchanged.
- Explicit pinned SDK installation in release-bundle and installed-container CI jobs. The preceding GitHub release-bundle job failed because SDK 10.0.402 was absent; decoded job logs confirmed the cause. Local assembly of the actual prior CI artifacts passed and produced five validated packages. No packages were published.

Validation:

- Windows and isolated Linux: 480 core + 17 generator + 331 helper + 742 contract cases = 1,570 passing cases on each platform.
- New CRC-64 Verilog fixtures: 8 positive and 8 negative paired scenarios across both runner implementations, both styles, seeds 1/17 and both corruption channels. All full positive traces agreed; all negative controls were detected by both runners. Positive runs checked 2,240 vectors across the two runners.
- Linux source hashes match all 14 changed source/configuration files in the Windows checkout.
- Windows merged coverage: 28.69% lines (1222/4259), 30.72% branches (1270/4134). Cobertura and ReportGenerator JSON totals agree. Coverage describes managed assemblies, not native/HDL coverage.
- PR #62 remains open, draft and unmerged at its original head.

Checkpoint commits:

- f002c2c5f610decf14947af902b13844b0888c8e test: add independent CRC reference vectors (#60)
- b1e2a608ddd6973ba8dce336c0f3b3b8880a533b test: expand managed fixture coverage at round 6 checkpoint 100 (#60)
- 259345fd7748831206164d3f098cfe3069e3d2f0 test: expand managed fixture coverage at round 6 checkpoint 150 (#60)
- b272e007ede2c34c9a4cd2507526a1b0a818a36d test: expand managed fixture coverage at round 6 checkpoint 200 (#60)

Remote delivery status is recorded separately in `round6-delivery.json`. Local Linux uses an isolated source copy and does not overwrite Windows build outputs. Native 64-bit parity evidence and source hashes are preserved in the Linux archive.
