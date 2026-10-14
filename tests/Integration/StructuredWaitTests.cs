using SharpTestBench;
using static SharpTestBench.SimTasks;
using static SharpTestBench.Triggers;

public static class StructuredWaitTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static async Task<int> Value(int value, ulong delay) { await Timer(delay); return value; }

    [SimTest]
    public static async Task StructuredReferenceTrace(TestContext c)
    {
        var trace = new Dictionary<string, object>();
        trace["ordered"] = await Gather(c, Owned(_ => Value(10, 4)), Owned(_ => Value(20, 1)));
        trace["empty"] = (await Gather<int>(c)).Length;
        bool cleaned = false;
        var start = c.Runtime.Now;
        var winner = await Select(c, Owned(_ => Value(11, 2)), Owned<int>(async _ =>
        {
            try { await Timer(100); return 99; }
            finally { await Timer(3).WithCancellation(CancellationToken.None); cleaned = true; }
        }));
        trace["selection"] = new[] { winner.Index, winner.Value, (int)(c.Runtime.Now - start), cleaned ? 1 : 0 };
        var borrowed = Value(77, 9);
        await Select(c, Borrowed(borrowed), Owned(_ => Value(88, 1)));
        trace["borrowed_alive"] = !borrowed.IsCompleted;
        await borrowed;
        start = c.Runtime.Now;
        try
        {
            await Gather(c, Owned<int>(async _ => { await Timer(2); throw new FormatException("root"); }),
                Owned<int>(async _ => { try { await Timer(100); return 0; } finally { await Timer(3).WithCancellation(CancellationToken.None); } }));
        }
        catch (FormatException) { trace["failure_after_cleanup"] = c.Runtime.Now - start; }
        using var parent = new CancellationTokenSource();
        start = c.Runtime.Now;
        var outer = Gather(c, parent.Token, Owned<int>(async _ =>
        {
            try { await Timer(100); return 0; } finally { await Timer(3).WithCancellation(CancellationToken.None); }
        }));
        await Timer(1); parent.Cancel();
        await Timer(1); parent.Cancel();
        try { await outer; } catch (OperationCanceledException) { trace["parent_cleanup"] = c.Runtime.Now - start; }
        winner = await Select(c, Owned(_ => Value(1, 1)), Owned<int>(async _ =>
        {
            try { await Timer(100); } catch (OperationCanceledException) { }
            await Timer(2).WithCancellation(CancellationToken.None); return 2;
        }));
        trace["suppression_winner"] = winner.Value;
        try
        {
            await Gather(c, Owned<int>(_ => Task.FromCanceled<int>(new CancellationToken(true))), Owned(_ => Value(2, 100)));
        }
        catch (OperationCanceledException) { trace["child_cancel"] = true; }
        try
        {
            await Select(c, Owned(_ => Value(1, 1)), Owned<int>(async _ =>
            {
                try { await Timer(100); return 2; } finally { throw new FormatException("cleanup"); }
            }));
            trace["cleanup_failure"] = "ignored";
        }
        catch (FormatException) { trace["cleanup_failure"] = "propagated"; }
        File.WriteAllText((Environment.GetEnvironmentVariable("CS_TRACE") ?? "trace.txt") + ".structured.json",
            System.Text.Json.JsonSerializer.Serialize(trace));
    }

    [SimTest]
    public static async Task StructuredResults(TestContext c)
    {
        var values = await Gather(c, Owned(_ => Value(10, 4)), Owned(_ => Value(20, 1)));
        Check(values.SequenceEqual(new[] { 10, 20 }), "gather result order");
        Check((await Gather<int>(c)).Length == 0, "empty gather");
        bool rejected = false;
        try { await Select<int>(c); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "empty select rejected");
        rejected = false;
        bool invoked = false;
        try { await Gather(c, Owned<int>(_ => { invoked = true; return Task.FromResult(0); }), null!); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected && !invoked, "validate all inputs before starting");
        var selected = await Select(c, Owned(_ => Value(10, 5)), Owned(_ => Value(20, 1)));
        Check(selected == (1, 20), "select index and value");
        var trigger = Timer(1);
        Check(ReferenceEquals((await Gather(c, Owned(trigger)))[0], trigger), "trigger input");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try { await Gather(c, cancelled.Token, Owned<int>(_ => { invoked = true; return Task.FromResult(0); })); }
        catch (OperationCanceledException) { }
        Check(!invoked, "pre-cancelled owned operation never starts");
    }

    [SimTest]
    public static async Task StructuredOwnershipAndCleanup(TestContext c)
    {
        var start = c.Runtime.Now;
        bool cleaned = false;
        var selected = await Select(c, Owned(_ => Value(11, 2)), Owned<int>(async _ =>
        {
            try { await Timer(100); return 99; }
            finally { await Timer(3).WithCancellation(CancellationToken.None); cleaned = true; }
        }));
        Check(selected == (0, 11) && cleaned && c.Runtime.Now == start + 5, "select joins async loser cleanup");
        var borrowed = Value(77, 9);
        selected = await Select(c, Borrowed(borrowed), Owned(_ => Value(88, 1)));
        Check(selected == (1, 88) && !borrowed.IsCompleted, "borrowed loser survives selection");
        Check(await borrowed == 77, "borrowed loser result preserved");
        borrowed = Value(33, 9);
        bool failed = false;
        start = c.Runtime.Now;
        try { await Gather(c, Borrowed(borrowed), Owned<int>(async _ => { await Timer(1); throw new FormatException("primary"); })); }
        catch (FormatException) { failed = true; }
        Check(failed && c.Runtime.Now == start + 1 && !borrowed.IsCompleted, "gather failure detaches borrowed waiter promptly");
        await borrowed;
        // A loser may suppress cancellation; joining still waits for it, but does not change the winner.
        selected = await Select(c, Owned(_ => Value(1, 1)), Owned<int>(async _ =>
        {
            try { await Timer(100); } catch (OperationCanceledException) { }
            await Timer(2).WithCancellation(CancellationToken.None);
            return 2;
        }));
        Check(selected == (0, 1), "suppression does not replace selected result");
    }

    [SimTest]
    public static async Task StructuredCancellationAndFailures(TestContext c)
    {
        bool cleaned = false, failed = false;
        var start = c.Runtime.Now;
        try
        {
            await Gather(c, Owned<int>(async _ => { await Timer(2); throw new FormatException("root"); }),
                Owned<int>(async _ =>
                {
                    try { await Timer(100); return 1; }
                    finally { await Timer(3).WithCancellation(CancellationToken.None); cleaned = true; }
                }));
        }
        catch (FormatException) { failed = true; }
        Check(failed && cleaned && c.Runtime.Now == start + 5, "gather fail-fast cancellation and joined cleanup");
        using var parent = new CancellationTokenSource();
        cleaned = false;
        var waiting = Gather(c, parent.Token, Owned<int>(async _ =>
        {
            try { await Timer(100); return 1; }
            finally { await Timer(3).WithCancellation(CancellationToken.None); cleaned = true; }
        }));
        await Timer(1);
        parent.Cancel(); parent.Cancel();
        try { await waiting; throw new Exception("parent cancellation missing"); }
        catch (OperationCanceledException) { }
        Check(cleaned, "outer repeated cancellation joins cleanup");
        try
        {
            await Select(c, Owned(_ => Value(1, 1)), Owned<int>(async _ =>
            {
                try { await Timer(100); return 2; }
                finally { throw new FormatException("cleanup"); }
            }));
            throw new Exception("cleanup failure lost");
        }
        catch (FormatException e) { Check(e.Message == "cleanup", "cleanup failure retained"); }
        try
        {
            await Gather(c, Owned<int>(async _ => { await Timer(1); throw new FormatException("root"); }),
                Owned<int>(async _ => { try { await Timer(100); return 2; } finally { throw new InvalidOperationException("cleanup"); } }));
            throw new Exception("aggregate missing");
        }
        catch (AggregateException e)
        {
            Check(e.InnerExceptions.Count == 2 && e.InnerExceptions.Any(x => x.Message == "root") &&
                e.InnerExceptions.Any(x => x.Message == "cleanup"), "root and cleanup failures retained once");
        }
        try
        {
            await Gather(c, Owned<int>(_ => Task.FromCanceled<int>(new CancellationToken(true))), Owned(_ => Value(2, 100)));
            throw new Exception("child cancellation lost");
        }
        catch (OperationCanceledException) { }
        try
        {
            await Select(c, Owned(_ => Value(1, 1)), Owned<int>(async token =>
            {
                using var registration = token.Register(() => throw new FormatException("cancel-handler"));
                await Timer(100);
                return 2;
            }));
            throw new Exception("cancellation handler failure lost");
        }
        catch (FormatException e) { Check(e.Message == "cancel-handler", "cancellation handler retained and remaining children joined"); }
        var nested = await Gather(c, Owned(async _ => (await Gather(c, Owned(_ => Value(5, 1))))[0]));
        Check(nested[0] == 5, "nested combinator result");
        Check(c.Runtime.PendingCallbacks == 1, "no native callback leaks");
    }
}
