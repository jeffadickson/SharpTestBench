using SharpTestBench;
using static SharpTestBench.Triggers;

public static class PrimitiveContractTests
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    [SimTest]
    public static async Task PrimitiveEventContract(TestContext c)
    {
        var gate = new SimEvent();
        Check(!gate.IsSet, "event starts clear");
        var reusable = gate.Wait();
        int resumed = 0;
        var first = c.StartSoon(async _ => { await reusable; resumed++; });
        var second = c.StartSoon(async _ => { await gate.Wait(); resumed++; });
        await Timer(1);
        Check(resumed == 0, "clear event blocks all waiters");
        gate.Set();
        gate.Clear(); // Already signalled waiters must not be lost.
        await Task.WhenAll(first.Task, second.Task);
        Check(resumed == 2 && !gate.IsSet, "set then clear preserves existing wakeups");
        var future = c.StartSoon(async _ => { await reusable; resumed++; });
        await Timer(1);
        Check(resumed == 2, "reused trigger waits after reset");
        gate.Set(); gate.Set();
        await future;
        Check(resumed == 3 && gate.IsSet, "repeated Set wakes each waiter once");
        ulong start = c.Runtime.Now;
        await reusable;
        Check(c.Runtime.Now == start, "set event consumes no simulation time");
        gate.Clear();
        using var cancellation = new CancellationTokenSource();
        var cancelled = gate.Wait().AsTask(cancellation.Token);
        cancellation.Cancel();
        try { await cancelled; throw new Exception("cancellation missing"); }
        catch (OperationCanceledException) { }
        gate.Set();
        await gate.Wait();
    }

    [SimTest]
    public static async Task PrimitiveLockContract(TestContext c)
    {
        var mutex = new SimLock();
        Check(!mutex.IsLocked, "new lock is free");
        var held = await mutex.Acquire();
        Check(mutex.IsLocked, "acquired lock is held");
        using var cancellation = new CancellationTokenSource();
        var cancelled = mutex.Acquire(cancellation.Token);
        var order = new List<int>();
        var first = c.StartSoon(async token =>
        {
            using (await mutex.Acquire(token))
            {
                Check(mutex.IsLocked, "lock remains held after handoff");
                order.Add(1);
                await Timer(1);
            }
        });
        var second = c.StartSoon(async token =>
        {
            using (await mutex.Acquire(token)) order.Add(2);
        });
        await Timer(1);
        cancellation.Cancel();
        try { await cancelled; throw new Exception("cancelled acquirer succeeded"); }
        catch (OperationCanceledException) { }
        held.Dispose();
        Check(mutex.IsLocked, "handoff reserves lock before continuation runs");
        held.Dispose(); // C# lease disposal is deliberately idempotent.
        await Task.WhenAll(first.Task, second.Task);
        Check(order.SequenceEqual(new[] { 1, 2 }), "FIFO survivors retain order");
        Check(!mutex.IsLocked, "final release makes lock available");
        using (await mutex.Acquire()) Check(mutex.IsLocked, "lock reusable after cancellation");
        Check(!mutex.IsLocked, "scoped disposal releases");
    }
}
