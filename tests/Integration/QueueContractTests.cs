using SharpTestBench;
using static SharpTestBench.Triggers;

/// <summary>Queue contracts run against the real simulation scheduler on Verilog and VHDL backends.</summary>
public static class QueueContractTests
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    [SimTest]
    public static async Task QueueImmediateContract(TestContext c)
    {
        var queue = new SimQueue<string?>(2);
        ulong now = c.Runtime.Now;
        int callbacks = c.Runtime.PendingCallbacks;
        Check(queue.Capacity == 2 && queue.IsEmpty && !queue.IsFull, "empty queue metadata");
        Check(!queue.TryGet(out var absent) && absent is null, "empty try-get returns false/default");
        Check(queue.TryPut(null) && queue.TryPut("second"), "null payload is a valid item");
        Check(queue.Count == 2 && queue.IsFull && !queue.TryPut("overflow"), "bounded immediate put");
        Check(queue.TryGet(out var first) && first is null, "null distinguished from missing by boolean result");
        Check(queue.TryGet(out var second) && second == "second" && queue.IsEmpty, "FIFO order");
        Check(!queue.TryGet(out _) && c.Runtime.Now == now && c.Runtime.PendingCallbacks == callbacks,
            "immediate operations do not allocate native callbacks or consume simulation time");
        foreach (int capacity in new[] { 0, -1 })
        {
            bool rejected = false;
            try { _ = new SimQueue<int>(capacity); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "nonpositive capacity is explicitly unsupported");
        }
        await Timer(1);
    }

    [SimTest]
    public static async Task QueueReservedHandoffContract(TestContext c)
    {
        var queue = new SimQueue<int>(1);
        var consumer = queue.Get();
        Check(queue.TryPut(11), "put wakes queued consumer");
        Check(queue.Count == 1 && !queue.IsEmpty && !queue.TryGet(out _),
            "stored item reserved for consumer cannot be stolen");
        Check(await consumer == 11, "waiting consumer receives its item");
        await queue.Put(20);
        var producer = queue.Put(21);
        Check(!producer.IsCompleted, "full queue blocks producer");
        Check(queue.TryGet(out int value) && value == 20, "remove original item");
        Check(queue.Count == 0 && !queue.IsFull && !queue.TryPut(99),
            "free slot reserved for producer cannot be stolen");
        await producer;
        Check(queue.TryGet(out value) && value == 21 && queue.IsEmpty, "producer handoff preserves item");
    }

    [SimTest]
    public static async Task QueueCancellationHandoffContract(TestContext c)
    {
        var queue = new SimQueue<int>(1);
        int baseline = c.Runtime.PendingCallbacks;
        using var consumerCancel = new CancellationTokenSource();
        var cancelledConsumer = queue.Get(consumerCancel.Token);
        var survivingConsumer = queue.Get();
        // Cancellation queues cleanup; release a permit before that cleanup runs.
        consumerCancel.Cancel();
        Check(queue.TryPut(31), "item arrives during cancelled consumer handoff");
        try { await cancelledConsumer; throw new Exception("missing consumer cancellation"); }
        catch (OperationCanceledException) { }
        Check(await survivingConsumer == 31 && queue.IsEmpty, "cancelled reservation returns item to next consumer");

        await queue.Put(40);
        using var producerCancel = new CancellationTokenSource();
        var cancelledProducer = queue.Put(41, producerCancel.Token);
        var survivingProducer = queue.Put(42);
        producerCancel.Cancel();
        Check(queue.TryGet(out int value) && value == 40, "slot opens during cancelled producer handoff");
        try { await cancelledProducer; throw new Exception("missing producer cancellation"); }
        catch (OperationCanceledException) { }
        await survivingProducer;
        Check(queue.TryGet(out value) && value == 42, "cancelled producer does not insert its item or lose capacity");
        using var alreadyCancelled = new CancellationTokenSource(); alreadyCancelled.Cancel();
        try { await queue.Put(99, alreadyCancelled.Token); throw new Exception("pre-cancelled put accepted"); }
        catch (OperationCanceledException) { }
        Check(queue.IsEmpty && queue.TryPut(50), "pre-cancellation leaves slot reusable");
        try { await queue.Get(alreadyCancelled.Token); throw new Exception("pre-cancelled get accepted"); }
        catch (OperationCanceledException) { }
        Check(queue.TryGet(out value) && value == 50 && c.Runtime.PendingCallbacks == baseline,
            "pre-cancellation preserves item and callbacks");
    }
    [SimTest]
    public static async Task QueueFamiliesContract(TestContext c)
    {
        int baseline = c.Runtime.PendingCallbacks;
        foreach (int capacity in new[] { 0, -1 })
        {
            try { _ = new SimLifoQueue<int>(capacity); throw new Exception("invalid LIFO capacity accepted"); }
            catch (ArgumentOutOfRangeException) { }
            try { _ = new SimPriorityQueue<int>(capacity); throw new Exception("invalid priority capacity accepted"); }
            catch (ArgumentOutOfRangeException) { }
        }
        foreach (var queue in new SimQueueBase<int>[] { new SimQueue<int>(3), new SimLifoQueue<int>(3), new SimPriorityQueue<int>(3) })
        {
            foreach (int value in new[] { 3, 1, 2 }) Check(queue.TryPut(value), "initial insertion");
            Check(queue.IsFull && !queue.TryPut(9), "bounded storage");
            int[] expected = queue is SimLifoQueue<int> ? [2, 1, 3] : queue is SimPriorityQueue<int> ? [1, 2, 3] : [3, 1, 2];
            foreach (int value in expected) Check(await queue.Get() == value, "family ordering");
            using var cancel = new CancellationTokenSource();
            var cancelled = queue.Get(cancel.Token);
            var first = queue.Get();
            var second = queue.Get();
            cancel.Cancel();
            Check(queue.TryPut(10) && !queue.TryGet(out _), "reserved item cannot be stolen");
            try { await cancelled; throw new Exception("missing cancellation"); } catch (OperationCanceledException) { }
            Check(await first == 10, "cancelled reservation passes to first surviving waiter");
            await queue.Put(20);
            Check(await second == 20, "FIFO consumer wakeup");
            foreach (int value in new[] { 1, 1, 1 }) await queue.Put(value);
            using var producerCancel = new CancellationTokenSource();
            var cancelledProducer = queue.Put(99, producerCancel.Token);
            var producer = queue.Put(7);
            producerCancel.Cancel();
            Check(await queue.Get() == 1 && !queue.TryPut(8), "producer reservation cannot be stolen");
            try { await cancelledProducer; throw new Exception("missing producer cancellation"); } catch (OperationCanceledException) { }
            await producer;
            var remaining = new List<int>();
            while (queue.TryGet(out int value)) remaining.Add(value);
            Check(remaining.Count == 3 && remaining.Contains(7) && !remaining.Contains(99), "producer cancellation preserves capacity");
        }
        var nullable = new SimLifoQueue<string?>(2);
        await nullable.Put("first"); await nullable.Put(null);
        Check(await nullable.Get() is null && await nullable.Get() == "first", "LIFO null payload");
        var nullPriority = new SimPriorityQueue<string?>(2);
        await nullPriority.Put("text"); await nullPriority.Put(null);
        Check(await nullPriority.Get() is null && await nullPriority.Get() == "text", "default comparer null ordering");
        Check(c.Runtime.PendingCallbacks == baseline, "queue families leak no callbacks");
    }

    [SimTest]
    public static async Task PriorityQueueFailureContract(TestContext c)
    {
        int baseline = c.Runtime.PendingCallbacks;
        bool fail = false;
        var queue = new SimPriorityQueue<int>(2, Comparer<int>.Create((a, b) =>
            fail ? throw new FormatException("comparison failure") : a.CompareTo(b)));
        await queue.Put(2);
        fail = true;
        try { queue.TryPut(1); throw new Exception("missing immediate comparison failure"); } catch (FormatException) { }
        try { await queue.Put(1); throw new Exception("missing async comparison failure"); } catch (FormatException) { }
        Check(queue.Count == 1, "failed comparisons preserve contents");
        fail = false;
        await queue.Put(3);
        var blocked = queue.Put(0);
        var survivor = queue.Put(4);
        fail = true;
        Check(await queue.Get() == 2, "removal needs no comparisons");
        try { await blocked; throw new Exception("missing handoff comparison failure"); } catch (FormatException) { }
        // The survivor may already have resumed and failed too. Every failure must return its slot.
        try { await survivor; throw new Exception("missing successor comparison failure"); } catch (FormatException) { }
        fail = false;
        Check(queue.TryPut(1) && queue.IsFull, "failed handoffs return capacity");
        Check(await queue.Get() == 1 && await queue.Get() == 3, "failed inserts preserve ordering");
        var ties = new SimPriorityQueue<(int Priority, string Name)>(3,
            Comparer<(int Priority, string Name)>.Create((a, b) => a.Priority.CompareTo(b.Priority)));
        await ties.Put((1, "first")); await ties.Put((0, "smallest")); await ties.Put((1, "second"));
        Check((await ties.Get()).Name == "smallest" && (await ties.Get()).Name == "first" &&
            (await ties.Get()).Name == "second", "stable equal priorities");
        SimPriorityQueue<int>? reentrant = null;
        reentrant = new(2, Comparer<int>.Create((a, b) => { reentrant!.TryGet(out _); return 0; }));
        await reentrant.Put(5);
        try { reentrant.TryPut(6); throw new Exception("missing reentrancy rejection"); } catch (InvalidOperationException) { }
        Check(reentrant.Count == 1 && await reentrant.Get() == 5 && reentrant.TryPut(7), "reentrancy rollback");
        Check(c.Runtime.PendingCallbacks == baseline, "comparison failure leaks no callbacks");
    }
    [SimTest]
    public static async Task QueueOrderingReference(TestContext c)
    {
        var trace = new Dictionary<string, List<int[]>>();
        foreach (var (name, queue) in new (string, SimQueueBase<int>)[] {
            ("fifo", new SimQueue<int>(4)), ("lifo", new SimLifoQueue<int>(4)),
            ("priority", new SimPriorityQueue<int>(4)),
            ("reverse", new SimPriorityQueue<int>(4, Comparer<int>.Create((a,b) => b.CompareTo(a)))) })
        {
            var rows = new List<int[]>(); trace[name] = rows;
            foreach (int value in new[] { 4, -2, 4, 1 })
            {
                await queue.Put(value);
                rows.Add([value, queue.Count, queue.IsEmpty ? 1 : 0, queue.IsFull ? 1 : 0]);
            }
            while (!queue.IsEmpty)
            {
                int value = await queue.Get();
                rows.Add([value, queue.Count, queue.IsEmpty ? 1 : 0, queue.IsFull ? 1 : 0]);
            }
            var pending = queue.Get();
            try { await SimTasks.WithTimeout(pending, 1); throw new Exception("missing timeout"); }
            catch (TimeoutException) { }
            Check(!pending.IsCompleted, "timeout does not cancel caller-owned queue wait");
            await queue.Put(8);
            Check(await pending == 8, "queue usable after timeout");
        }
        File.WriteAllText((Environment.GetEnvironmentVariable("CS_TRACE") ?? "trace.txt") + ".queues.json",
            System.Text.Json.JsonSerializer.Serialize(trace));
    }
}
