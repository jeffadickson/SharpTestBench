# Fixture cleanup

Register teardown immediately after acquiring a resource. Setup is ordinary C#;
if subsequent setup or the test body throws, already registered teardown still runs.

```csharp
[SimTest(TimeoutSteps = 10000, CleanupTimeoutSteps = 1000)]
public static async Task VerifyDevice(TestContext c)
{
    var stream = c.Cleanup.Own(File.Create("capture.bin"));
    var scope = c.Cleanup.CreateScope();
    scope.Defer(() => stream.Flush());
    c.Cleanup.Defer(async cancellation =>
    {
        // Cleanup has a fresh token, separate from the cancelled test body.
        await Triggers.Timer(1).WithCancellation(cancellation);
    });
    await Triggers.Timer(10);
}
```

Callbacks run once in reverse registration order. Nested scopes clean up at their
position in that order. Own registers IDisposable; OwnAsync registers
IAsyncDisposable. CloseAsync can also close a scope early; subsequent closes return
the same completion task and do not repeat callbacks. Registration after closing
is rejected. A callback must not await closing its own scope.

The regression cancels the body/jobs, gives cancellation a simulator step to settle,
then runs cleanup on the simulator thread. Cleanup receives an independent token
and simulation-step budget (default 1000; zero is clamped to one step). Ordinary
trigger awaits inherit this token. Callbacks that throw are collected and remaining
callbacks are attempted. An already cancelled token is still passed to remaining
callbacks; synchronous resource disposal can still run. Cancellation-handler errors
are recorded rather than bypassing teardown.

Body and cleanup errors are both retained in AggregateException. Cleanup failures
cannot be excused by ExpectedException on the test. Cancellation caused by a
background failure does not obscure that original background exception. Cleanup
finishes before reporting the test result and before starting the next test.

A noncooperative callback can prevent later callbacks from being reached. If it
remains active after the cleanup deadline and one cancellation-settling step, the
regression reports a leak and aborts; it cannot safely kill arbitrary C# code.
A synchronously blocked simulator thread cannot service simulation timers: callers
still need a process wall-clock watchdog. Process crashes cannot guarantee cleanup.
Use cancellation-aware async work, not blocking waits. Disposed resources must not
be reused by leaked jobs. Pending unflushed HDL writes are discarded after cleanup;
await the appropriate settling trigger inside cleanup if a deposit must take effect.

Standalone CleanupScope is intended for serialized use; it does not provide locking
or move continuations between threads. TestContext-owned scopes additionally enforce
simulator-thread access. No mocking library is needed for these callback contracts:
the managed tests use real callbacks with observable order, failures and cancellation.

## Verification

Three managed xUnit cases cover order/nesting/idempotence, failure aggregation, and
attempting remaining callbacks after cancellation. tools/cleanup_tests.py launches
six independent Icarus processes: normal, body plus cleanup failure, body timeout,
cleanup timeout, throwing cancellation handler, and a noncooperative cleanup callback.
The next test verifies cleanup order; the noncooperative case must abort before it.
The normal lifecycle fixture also runs on Verilator, GHDL and NVC.
