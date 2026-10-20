# Structured simulation logs

Use `tools/SharpTestBench.Tools/LegacyRunCommand.cs --log-json build/run-events.jsonl` or set CS_LOG_JSON to an
output path. Each process truncates that file at startup, then appends one JSON
object per line. Use distinct paths for concurrent simulator processes. Logging is
opt-in; disabled structured logging performs no event serialization or event-buffer
allocation. Existing console logging is retained.

```csharp
ctx.Log("Reset complete");
ctx.Log("Unexpected response", SimLogLevel.Error, error);
```

Events carry SchemaVersion=1, Kind (message/test-start/test-end/runtime-failure),
TimeStep in simulator precision units, qualified Test, Seed, Stage, optional TaskId
and TaskName, Level and Message. TestContext exposes TestName and Stage for custom
instrumentation. Job identity comes from SimJob.Current; the test body has no job.
Task IDs identify jobs within one process and are not stable cross-run identifiers.

Caller attributes capture SourceFile, SourceLine and SourceMember for ctx.Log;
ExceptionType, ExceptionMessage and ExceptionDetail retain managed exception and
stack information. Matching PDB symbols improve exception stack file/line details.
This does not map a simulator-native failure back into HDL source or expose live
Python coroutine frames. Test boundary events can have null caller source fields;
the exception detail remains available for failures.

Text fields are limited to 16,384 UTF-16 code units in exported JSON and Truncated
is set when any field exceeds that bound. Newlines are JSON escaped, so messages
cannot split the event stream into invalid records. There is no retained in-memory
log queue: writes are synchronous and memory use depends on the current event,
not all previous events. User message construction and exception formatting still
have their own cost, and console text is not truncated by this JSON limit. Log I/O
can slow a verbose test; this feature makes no zero-overhead performance claim.

An output I/O failure fails the regression; the JSON writer disables itself before
reporting the error so failure reporting cannot recursively retry the same write.
Logs are not automatically rotated.

AR_StructuredLogging and tools/SharpTestBench.Tools/ValidationCommands.cs verify parent/child identities,
monotonic simulation timestamps, source file/line, caught exception stacks,
multiline escaping and the text limit on Icarus and Verilator. tools/SharpTestBench.Tools/StageCommand.cs
also checks the failing-stage event against its exception and seed. CI uploads
*-events.jsonl with the test results.
