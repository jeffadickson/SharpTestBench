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
| Task.get_name() | Name | Constructor-assigned name |
| Task.set_name() | SimJob.SetName | Simulator-thread checked; nonblank name; typed and untyped jobs |
| Task.cancel(msg) | Cancel() | Cooperative CancellationToken; no message or Python cancellation injection |
| Task.kill() | No abrupt kill | Would bypass C# cleanup; deliberately not presented as equivalent to Cancel |
| Task.complete | Task / IsCompleted | No dedicated completion Trigger accepting mixed trigger/task lists yet |
| Task.join() / Join | Await SimJob | Deprecated Python API is not copied |
| Task.locals | AsyncLocal used internally for cancellation | No public task-local dictionary API |
| Task traceback / coroutine introspection | .NET exception stack traces | No equivalent Python frame/traceback inspection |

SimTasks.First/Combine/WithTimeout are non-owning waits. RunGroup is an additional
structured ownership API with sibling cancellation, cleanup joining and aggregate
errors. TASKS.md specifies their behavior, including how it differs from trigger
subscription cancellation. Full upstream scheduler/conformance tests remain open.
