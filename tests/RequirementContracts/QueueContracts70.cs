using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest] public static async Task R061_CancelledReservationHandsDirectlyToQueuedSuccessor(TestContext c)
    {
        var sem=new SimSemaphore(0,1);using var cancel=new CancellationTokenSource();var first=sem.Acquire(cancel.Token);var next=sem.Acquire();
        sem.Release();cancel.Cancel();await Fails<OperationCanceledException>(()=>first);Check(sem.CurrentCount==0,"cancelled grant reserved for successor");
        using var thiefCancel=new CancellationTokenSource();var thief=sem.Acquire(thiefCancel.Token);Check(!thief.IsCompleted,"late acquirer cannot steal successor grant");thiefCancel.Cancel();await Fails<OperationCanceledException>(()=>thief);await next;sem.Release();Check(sem.CurrentCount==1,"successor can return exactly one permit");
        var queue=new SimQueue<int>(1);using var cancelGet=new CancellationTokenSource();var head=queue.Get(cancelGet.Token);var tail=queue.Get();queue.TryPut(61);cancelGet.Cancel();await Fails<OperationCanceledException>(()=>head);
        Check(!queue.TryGet(out _),"returned item reserved for queued successor");Check(await tail==61&&queue.IsEmpty,"successor receives cancelled consumer reservation");
    }
    [SimTest] public static async Task R062_CancelledPutAfterSlotHandoffPreservesCapacity(TestContext c)
    {
        var queue=new SimQueue<int>(1);queue.TryPut(1);using var cancel=new CancellationTokenSource();var pending=queue.Put(62,cancel.Token);
        Check(queue.TryGet(out int original)&&original==1,"release producer slot");cancel.Cancel();await Fails<OperationCanceledException>(()=>pending);
        Check(queue.IsEmpty&&queue.TryPut(3)&&await queue.Get()==3,"cancelled producer neither inserts nor loses slot");
    }
    [SimTest] public static async Task R063_TryGetCannotStealAQueuedConsumersItem(TestContext c)
    {
        var queue=new SimQueue<int>(1);var pending=queue.Get();Check(queue.TryPut(63),"producer insertion");
        Check(queue.Count==1&&!queue.TryGet(out _),"reserved item is unavailable to TryGet");Check(await pending==63&&queue.IsEmpty,"queued consumer retains item ownership");
    }
    [SimTest] public static async Task R064_TryPutCannotStealAQueuedProducersSlot(TestContext c)
    {
        var queue=new SimQueue<int>(1);queue.TryPut(1);var pending=queue.Put(64);Check(queue.TryGet(out int first)&&first==1,"release occupied slot");
        Check(queue.IsEmpty&&!queue.TryPut(3),"reserved slot unavailable to TryPut");await pending;Check(await queue.Get()==64,"queued producer retains slot ownership");
    }
    [SimTest] public static async Task R065_CancelledProducerHeadCannotBlockTail(TestContext c)
    {
        var queue=new SimQueue<int>(1);queue.TryPut(1);using var cancel=new CancellationTokenSource();var head=queue.Put(2,cancel.Token);var tail=queue.Put(3);
        cancel.Cancel();await Fails<OperationCanceledException>(()=>head);Check(await queue.Get()==1,"original FIFO payload");await tail;Check(await queue.Get()==3,"cancelled producer skipped without stale insertion");
    }
    [SimTest] public static async Task R066_PriorityComparisonFailureRollsBackCapacity(TestContext c)
    {
        bool fail=false;var queue=new SimPriorityQueue<int>(2,Comparer<int>.Create((a,b)=>fail?throw new FormatException("comparer marker"):a.CompareTo(b)));
        queue.TryPut(7);fail=true;Throws<FormatException>(()=>queue.TryPut(2));Check(queue.Count==1&&!queue.IsFull,"comparer failure leaves contents/capacity");
        fail=false;Check(queue.TryPut(3)&&await queue.Get()==3&&await queue.Get()==7,"priority queue recovers after comparison fault");
    }
    [SimTest] public static async Task R067_PriorityReentryCannotMutateTheQueue(TestContext c)
    {
        bool reenter=false;SimPriorityQueue<int>? queue=null;queue=new(3,Comparer<int>.Create((a,b)=>{if(reenter)queue!.TryGet(out _);return a.CompareTo(b);}));
        queue.TryPut(4);reenter=true;Throws<InvalidOperationException>(()=>queue.TryPut(2));Check(queue.Count==1,"reentrant compare leaves payload");reenter=false;
        Check(queue.TryPut(1)&&await queue.Get()==1&&await queue.Get()==4,"priority comparer reentry rollback");
    }
    [SimTest] public static async Task R068_EqualPriorityHandoffPreservesArrivalOrder(TestContext c)
    {
        var queue=new SimPriorityQueue<(int Priority,string Name)>(2,Comparer<(int Priority,string Name)>.Create((a,b)=>a.Priority.CompareTo(b.Priority)));
        queue.TryPut((1,"a"));queue.TryPut((1,"b"));var pending=queue.Put((1,"c"));Check((await queue.Get()).Name=="a","first equal priority payload");await pending;
        Check((await queue.Get()).Name=="b"&&(await queue.Get()).Name=="c","equal priority insertion stays stable across capacity handoff");
    }
    [SimTest] public static async Task R069_LifoHandoffKeepsStackOrder(TestContext c)
    {
        var queue=new SimLifoQueue<int>(2);queue.TryPut(1);queue.TryPut(2);var pending=queue.Put(3);
        Check(await queue.Get()==2,"LIFO old top");await pending;Check(await queue.Get()==3&&await queue.Get()==1&&queue.IsEmpty,"new producer becomes LIFO top after handoff");
    }
    [SimTest] public static async Task R070_ForeignThreadCannotInsertQueuePayload(TestContext c)
    {
        var queue=new SimQueue<int>(2);queue.TryPut(70);Exception? failure=null;var thread=new Thread(()=>{try{queue.TryPut(99);}catch(Exception error){failure=error;}});thread.Start();thread.Join();
        Check(failure is InvalidOperationException&&queue.Count==1&&await queue.Get()==70,"queue foreign-thread mutation rejected");
    }
}
