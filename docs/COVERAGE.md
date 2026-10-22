# Functional coverage

Functional coverage answers which scenarios a test observed. It is distinct from
C#/HDL statement, branch or toggle coverage. This release provides explicit sampled
functional coverage only; it does not claim simulator code-coverage support or merge
external coverage databases.

```csharp
var payloads = ctx.Coverage.AddPoint("payload",
    new CoverageBin<uint>("zero", value => value == 0),
    new CoverageBin<uint>("large", value => value >= 128, Goal: 3));
await Triggers.ReadOnly();
payloads.Sample((uint)dut.Child("payload").Signal<W8>().ReadUInt().Value);
```

A point must have at least one bin. Names are nonblank and unique within the suite
(points) or point (bins); hit goals are positive. Each matching bin increments,
including overlapping bins. Samples that match none increment Unmatched. A bin
is covered when Hits reaches Goal. Percent is the fraction of covered bins, weighted
equally per bin across all points; it is not average hit progress. Empty suites have
null Percent, never a misleading 100%. Sampling does not automatically fail a test
for missing coverage: inspect Snapshot and assert a project-specific threshold.

Predicates must be pure. A throwing predicate or counter overflow leaves that point's
counters unchanged; recursive sampling is rejected. Sample uses fixed storage per
bin, allocating no per-sample framework objects. Predicates can allocate their own
objects. Snapshots/export allocate in proportion to configured points and bins,
not the number of samples. Access is restricted to the creating thread. Snapshots
copy counter values so later samples do not change earlier reports. Explicitly
sample tuples to define cross scenarios; automatic cross-bin generation, exclusion
rules are not supplied. Validated additive snapshot merges are supported as described below.

Every regression writes a sibling `*-results.coverage.json` alongside JSON/JUnit.
The versioned document has SchemaVersion=1, Kind=functional and test entries with
Test, Seed, Status and Coverage. Tests without configured points are omitted;
failed tests retain the samples collected before failure. Coverage counters are
per-test, not carried into later cases. CI uploads these reports with other results.
A passing test need not have 100% coverage, and coverage does not establish correctness.

`AQ_FunctionalCoverage` samples independent HDL packed-field readback of four known
values, producing two hits in each bin and 50% goal coverage on Icarus and Verilator.
`tools/SharpTestBench.Tools/ValidationCommands.cs --label icarus` validates the exported counts, goals and
identity against the regression result. Managed unit tests cover overlapping bins,
null percentage, immutable counter snapshots, invalid definitions, predicate failure,
recursion, thread checks and JSON counts. Managed/HDL code coverage remains outside
this functional API's support contract.


## Reset and additive snapshot merge

`point.Reset()` clears that point's hits, samples and unmatched count, retaining
bin names, goals and predicates. `coverage.Reset()` clears all points only after
checking every point can be changed. Both require the creating thread. Point reset rejects that point's active sample;
suite reset/merge reject sampling active on any point. Predicates must remain pure. Previously returned snapshots retain
their values. Reset deliberately discards earlier-window counts from the final
per-test export unless the caller saves those snapshots.

`coverage.Merge(snapshot)` adds a caller-supplied CoverageSnapshot. A subset of
existing points is allowed; every included point must have exactly the configured
bin names and goals. Point/bin ordering is irrelevant. Duplicate/unknown identities,
null shapes, impossible matched/unmatched counters and overflow reject the whole
merge before changing any point. Overlapping bins remain supported. Repeated merges
are additive, not deduplicated by run identity. Callers must ensure matching names
represent equivalent predicates and retain any required seed/status provenance.
The API merges functional counters, not entire test-result envelopes or external
HDL/code-coverage databases. Existing SchemaVersion=1 export fields are unchanged;
a regression still exports the final per-test counter snapshot.
