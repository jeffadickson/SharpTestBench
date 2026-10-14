# Concurrency comparison: cocotb 2.1 and SharpTestBench

This is a contract review, not a completed differential conformance claim.
Reference: [cocotb 2.1 library reference](https://docs.cocotb.org/en/v2.1.0/library_reference.html#cocotb.triggers.gather).
The pinned upstream `_concurrent_waiters.py` was also inspected; it is not a
substitute for executing the released 2.1 reference cases.

| Contract | cocotb 2.1 | SharpTestBench today |
|---|---|---|
| Gather results | Input-ordered tuple | Combine returns no values; typed tasks retain their results |
| Select result | Winning input index and value | First returns the winning Task after propagating its outcome |
| Failure during gather | Cancel owned siblings, then join cleanup | Combine waits for all supplied tasks; no sibling cancellation |
| Selection cleanup | Cancel owned losers, join cleanup | First leaves supplied tasks running |
| Existing tasks supplied | Borrowed; cancellation affects waiters, not tasks | First/Combine also borrow supplied tasks |
| Awaitable inputs | Coroutines, triggers, tasks and other awaitables | First/Combine accept Task; triggers require AsTask |
| Cancellation of outer wait | Owned cleanup finishes before propagation | First/Combine stop waiting; RunGroup joins owned children |
| Low-level wait | FIRST_COMPLETED, FIRST_EXCEPTION, ALL_COMPLETED | No single corresponding public API |
| Simultaneous completion | Do not depend on same-time winner order | Already-complete First ties use input order |

The pinned Python implementation returns an empty tuple for empty gather; select
and wait reject empty inputs. SharpTestBench First and Combine both reject empty
inputs. This is an API difference to cover in the released-version oracle.

## Why the distinction matters

Suppose a scoreboard fails at simulation step 2 while a stimulus generator is
scheduled to run until step 100. Our Combine observes the failure only after the
other task settles. That delay can obscure the first failure and prolong a run.
Putting both jobs in RunGroup cancels the stimulus promptly and waits for its
cleanup before returning. Simply renaming Combine to Gather would preserve the
wrong failure behavior and still omit typed result collection.

C# async methods generally start when invoked. Ownership must therefore be explicit:
planned owned inputs should be deferred `Func<CancellationToken, Task<T>>`
factories, while existing Task/SimJob inputs remain borrowed. AsTask alone does
not transfer ownership of its cancellation source. These are proposed API design
constraints, not APIs already shipped.

## Characterization tests

The shared TimingTests file runs in Verilog and VHDL integration projects:

- CombinatorBorrowedCancellation: cancelling First preserves a borrowed timer;
  joining it removes the native callback.
- CombinatorFailureIsNotFailFast: failure and child cancellation both leave Combine
  waiting until the other borrowed task settles; original failure identity survives.
- CombinatorGroupJoinsAsyncCleanup: a failure at step 2 cancels an owned sibling;
  its three-step asynchronous finally completes before the group throws at step 5.

Existing U_TaskCombinators covers result identity and completed ties;
AL_CompletionTriggers covers detached observers; AP_MultipleFailureTeardownStress
covers aggregation. None proves full gather/select equivalence.

Local validation on 2026-10-14: solution build succeeded with zero warnings;
Icarus passed 52 tests (one skip) and NVC passed 22 (one skip), including all three
new cases on each backend. Windows/macOS execution of these new cases is pending
the next CI run; the earlier platform matrix predates them.

## Remaining implementation and evidence

Tracked in [#44](https://github.com/jeffadickson/SharpTestBench/issues/44): typed
aggregation and indexed selection; mixed owned/borrowed inputs; fail-fast
observation; parent and repeated cancellation; cancellation suppression; cleanup
failure policy; empty inputs; released cocotb differential traces; native platform
and backend coverage. Preserve the existing non-owning APIs to avoid changing
caller ownership silently. Prefer an explicit aggregate-error policy over silently
losing cleanup failures, and document any difference from the Python oracle.
