# WP-01 / WP-02 finite qualification evidence

Read the [validation report](../../development-history/work-packages-01-02-validation.md)
and [work-package gates](../../WORK_PACKAGES.md). Windows and Linux qualification
JSON retain fresh inventories, four-production-assembly coverage, property/fuzz
seeds, selected Stryker outcomes and source hashes. Source Git blob IDs bind the
candidate despite working-tree newline differences. Requirement status is not
closed by these records. Matrix records retain actual local publication and
positive/negative execution, including Linux NativeAOT negatives.

The nine benchmark JSON files retain all twelve workload samples per report
(one warmup plus five measured repetitions for each runner), phase distributions,
protected inputs and original raw report hashes. No raw binary or full path is
embedded. The existing checker ran on each original full report and also verified the
common timing fields in all nine compact records. These portable records omit
machine command paths and are not byte-identical original reports. Earlier Windows shared-host binaries were replaced
by later fixture preparation; their records explicitly retain that limitation.

Each platform checksums.json binds its other JSON files. Files are normalized to
LF before hashing and marked -text so Git preserves their exact bytes. Do not
reformat them without updating checksums and provenance. Raw logs, full reports,
binaries and coverage HTML remain local under the report's ignored build paths.
These are finite local measurements, not HDL/AOT instruction coverage or an
independent VPI-backend qualification. No GitHub result branch was uploaded.