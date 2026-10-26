# Portable endurance evidence

These are exact summary/checksum blobs from clean standalone qualification
against temporary local bare Git remotes. They identify tested source, bounded
outcomes, seeds, counts, tool versions and scoped coverage. They contain no raw
logs, binaries, corpora or absolute machine paths.

Do not reformat these JSON files: checksums bind their exact bytes.
`.gitattributes` disables newline conversion for these blobs, preserving original
Windows and Linux byte representations across checkouts. Review/import
them with the trusted checkout's endurance commands as documented in
[ENDURANCE.md](../../ENDURANCE.md). Import is data verification, not test
reexecution. A checksum establishes byte consistency, not independent author trust.

See the [validation report](../../development-history/endurance-validation-report.md)
for result commit IDs, execution/review scope, failed attempts and limitations.
No hours-long stability, NativeAOT instruction, HDL or benchmarking claim follows
from these finite managed campaigns.
