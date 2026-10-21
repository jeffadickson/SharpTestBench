using SharpTestBench;
using static SharpTestBench.SimTasks;
using static SharpTestBench.Triggers;

public static class RequirementsConcurrencyTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    [SimTest]
    public static async Task InputSnapshotSurvivesFactoryMutation(TestContext c)
    {
        var inputs = new SimAwaitable<int>[2];
        inputs[0] = Owned<int>(_ => {
            inputs[1] = Owned<int>(_ => throw new Exception("mutated input started"));
            return Task.FromResult(1);
        });
        inputs[1] = Owned<int>(_ => Task.FromResult(2));
        Check((await Gather(c, inputs)).SequenceEqual(new[] { 1, 2 }), "snapshot retains the original second operation");
    }

    [SimTest]
    public static async Task SynchronousFactoryFailureJoinsAlreadyStartedSibling(TestContext c)
    {
        bool started = false, cleaned = false;
        var expected = new FormatException("synchronous factory");
        try
        {
            await Gather(c, Owned<int>(async _ => {
                started = true;
                try { await Timer(100); return 1; }
                finally { await Timer(3).WithCancellation(CancellationToken.None); cleaned = true; }
            }), Owned<int>(_ => throw expected));
            throw new Exception("synchronous failure disappeared");
        }
        catch (FormatException actual) { Check(ReferenceEquals(actual, expected), "original synchronous failure retained"); }
        Check(started && cleaned, "started sibling cleanup joined before throw");
    }

    [SimTest]
    public static async Task SelectionFailureWinsAfterLoserCleanup(TestContext c)
    {
        bool cleaned = false;
        var expected = new FormatException("first selected failure");
        ulong start = c.Runtime.Now;
        try
        {
            await Select(c, Owned<int>(async _ => { await Timer(1); throw expected; }),
                Owned<int>(async _ => {
                    try { await Timer(100); return 2; }
                    finally { await Timer(3).WithCancellation(CancellationToken.None); cleaned = true; }
                }));
            throw new Exception("selected failure disappeared");
        }
        catch (FormatException actual) { Check(ReferenceEquals(actual, expected), "selection preserves original failure"); }
        Check(cleaned && c.Runtime.Now == start + 4, "failed selection joins timed loser cleanup");
    }

    [SimTest]
    public static async Task DuplicateBorrowedTaskHasIndependentWaiterResults(TestContext c)
    {
        int calls = 0;
        async Task<int> Body() { calls++; await Timer(2); return 37; }
        var shared = Body();
        var values = await Gather(c, Borrowed(shared), Borrowed(shared));
        Check(calls == 1 && values.SequenceEqual(new[] { 37, 37 }), "duplicate borrowed observations preserve both input positions without restarting work");
    }

    [SimTest]
    public static async Task ParentCancellationBeforeDispatchDoesNotInvokeFactories(TestContext c)
    {
        using var parent = new CancellationTokenSource(); int invoked = 0;
        var pending = Gather(c, parent.Token,
            Owned<int>(_ => { invoked++; return Task.FromResult(1); }),
            Owned<int>(_ => { invoked++; return Task.FromResult(2); }));
        parent.Cancel();
        try { await pending; throw new Exception("parent cancellation disappeared"); }
        catch (OperationCanceledException) { }
        Check(invoked == 0, "queued owned jobs honor cancellation before dispatch");
    }

    [SimTest]
    public static async Task SharedExceptionObjectIsReportedOnce(TestContext c)
    {
        var expected = new FormatException("shared failure object");
        try
        {
            await Gather(c, Owned<int>(async _ => { await Timer(1); throw expected; }),
                Owned<int>(async _ => { try { await Timer(100); return 2; } finally { throw expected; } }));
            throw new Exception("shared failures disappeared");
        }
        catch (FormatException actual) { Check(ReferenceEquals(actual, expected), "deduplication uses original error identity"); }
    }

    [SimTest]
    public static async Task SuccessfulCompletedInputsAreNotCancelledDuringJoin(TestContext c)
    {
        int cancelled = 0;
        var values = await Gather(c,
            Owned<int>(token => { token.Register(() => cancelled++); return Task.FromResult(11); }),
            Owned<int>(token => { token.Register(() => cancelled++); return Task.FromResult(22); }));
        Check(values.SequenceEqual(new[] { 11, 22 }), "both completed values are retained");
        Check(cancelled == 0, "completed input tokens are not cancelled by combinator teardown");
    }
}
