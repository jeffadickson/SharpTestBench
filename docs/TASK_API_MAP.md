# Task API mapping to pinned cocotb

Reviewed against `src/cocotb/task.py` in upstream commit
5fa952e92c46fd9cb58f55a8655312a4bf02e7de. This is a semantic mapping, not a claim
that Python APIs or Python coroutine internals have been reproduced in C#.

| cocotb API | C# equivalent | Difference / status |
|---|---|---|
| start_soon(awaitable, name) | TestContext.StartSoon / StartSoon<T> | C# accepts a delegate so cancellation before start prevents invocation |
| await Task | await SimJob<T> | Typed result; failures/cancellation propagate |
| Task.result() | SimJob<T>.Result | Throws if unfinished; never blocks the simulator |
| Task.done() | IsCompleted / Task.IsCompleted | Tested completed, failed and cancelled jobs |
| Task.cancelled() | Task.IsCanceled | .NET cancellation state |
| Task.exception() | Exception / Task.Exception | AggregateException container; .NET property behavior differs |
| Task.get_name() | Name | Mutable diagnostic name |
| Task.set_name() | SimJob.SetName | Simulator-thread checked; nonblank name; typed and untyped jobs |
| Task.cancel(msg) | Cancel() | Cooperative CancellationToken; deliberate no-message policy (TASKS.md) |
| Task.kill() | No abrupt kill | Would bypass C# cleanup; deliberately not presented as equivalent to Cancel |
| Task.complete | SimJob.Complete | Completion-only Trigger; composes with native triggers, without propagating job failure |
| current_task() | SimJob.Current | Current StartSoon identity; null in test body |
| TaskComplete.task | Keep the originating SimJob reference | Completion trigger does not expose a reverse lookup |
| Task.join() / Join | Await SimJob | Deprecated Python API is not copied |
| Task.locals | SimTaskLocal<T> | Typed key; isolated per StartSoon job, no inheritance |
| Task traceback / coroutine introspection | SimJob.Current, Status, Exception | Managed exception stacks; no Python frame/coroutine inspection |

SimTasks.First/Combine/WithTimeout are non-owning waits. RunGroup is an additional
structured ownership API with sibling cancellation, cleanup joining and aggregate
errors. TASKS.md specifies their behavior, including how it differs from trigger
subscription cancellation. Full upstream scheduler/conformance tests remain open.

See [the cocotb 2.1 concurrency comparison](CONCURRENCY_COMPARISON.md): First/Combine are not gather/select equivalents. Remaining structured combinators are tracked in #44.
