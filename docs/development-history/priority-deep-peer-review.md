# Priority encoder: interval and final independent source reviews

Base: main `36b2e1a8f55ea7980877e2b64fd4c0105cceb9f4`.
Source reviews were read-only, without builds or simulator execution.

- At 15 oracle cases: no actionable findings. The linear oracle, truncation of
  out-of-width bits, wrapper sizes and invalid-output handling agreed. No execution
  evidence was implied by this review.
- At 30 cases: no actionable findings. Helper report paths, exact case selection,
  provenance checks, environment cleanup and negative-control matching agreed.
  The reviewer identified a coverage gap: two identically malformed traces could
  pass the initial count/equality check. A per-row validator and 15 malformed
  report regressions were subsequently added.
- At 45 cases: no actionable findings. Row dimensions, configuration order,
  shared stimulus, flags, index range and onehot consistency were reviewed.
  The final five cases cover zero and nonzero accepted traces including bit 31,
  valid index overflow, valid onehot mismatch and later configuration reordering.

The separate final deep reviewer found no actionable defects. Twelve wrapper
configurations, the linear oracle and trace order agree. There are 453 masks,
producing 5,436 comparisons per successful runner. Unsigned masks, bit 31,
non-power-of-two widths and the specified unchecked 64-bit generator are handled
correctly. Invalid HDL outputs are normalized consistently. File hashing verified
the provenance inventory; MIT notices are retained. Existing evidence schemas
and fields remain compatible. Production orchestration remains C#.

Limits retained from the deep review:

- `PriorityTrace` validates structure and consistency; winner semantics come from
  the independent simulator scoreboard. It does not reconstruct seeded stimuli.
- The negative fault fails at the first vector/configuration. It demonstrates
  end-to-end fault detection, rather than detection independently in all 12.
- Fifty managed scenarios are regression cases, not fifty features or distinct
  development iterations. They do not measure HDL or native instruction coverage.

Fresh builds, tests, simulation, coverage, publication and delivery are recorded
separately in [the validation report](priority-validation-report.md).
