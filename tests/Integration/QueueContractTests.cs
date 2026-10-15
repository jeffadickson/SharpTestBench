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
}
