using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest] public static async Task R051_PreSetEventDoesNotConsumeItsLevel(TestContext c)
    {
        var ev=new SimEvent();ev.Set();ulong start=c.Runtime.Now;await ev.Wait();await ev.Wait();Check(ev.IsSet&&c.Runtime.Now==start,"manual-reset event level retained");
    }
    [SimTest] public static async Task R052_ClearCannotRetractAnAlreadyDeliveredWake(TestContext c)
    {
        var ev=new SimEvent();var old=ev.Wait().AsTask();ev.Set();ev.Clear();await old;
        var next=ev.Wait().AsTask();Check(!next.IsCompleted&&!ev.IsSet,"clear only affects future waits");ev.Set();await next;
    }
    [SimTest] public static async Task R053_CancellingOneEventWaitPreservesOtherWaiter(TestContext c)
    {
        var ev=new SimEvent();using var cancel=new CancellationTokenSource();var removed=ev.Wait().AsTask(cancel.Token);var survivor=ev.Wait().AsTask();
        cancel.Cancel();await Fails<OperationCanceledException>(()=>removed);Check(!survivor.IsCompleted,"other event wait survives cancellation");ev.Set();await survivor;
    }
    [SimTest] public static async Task R054_ForeignThreadCannotSetOrWakeEvent(TestContext c)
    {
        var ev=new SimEvent();var waiting=ev.Wait().AsTask();Exception? failure=null;var thread=new Thread(()=>{try{ev.Set();}catch(Exception error){failure=error;}});thread.Start();thread.Join();
        Check(failure is InvalidOperationException&&!ev.IsSet&&!waiting.IsCompleted,"foreign event Set noninterference");ev.Set();await waiting;
    }
    [SimTest] public static async Task R055_LockQueuedOwnersEnterInArrivalOrder(TestContext c)
    {
        var gate=new SimLock();var lease=await gate.Acquire();var order=new List<int>();
        var first=c.StartSoon(async _=>{using var acquired=await gate.Acquire();order.Add(1);await Timer(2);});
        var second=c.StartSoon(async _=>{using var acquired=await gate.Acquire();order.Add(2);});await Yield();lease.Dispose();await first;await second;
        Check(order.SequenceEqual(new[]{1,2})&&!gate.IsLocked,"queued lock ownership order");
    }
    [SimTest] public static async Task R056_CancelledMiddleLockWaiterCannotBlockQueue(TestContext c)
    {
        var gate=new SimLock();var lease=await gate.Acquire();using var cancel=new CancellationTokenSource();var middle=gate.Acquire(cancel.Token);var tail=gate.Acquire();
        cancel.Cancel();await Fails<OperationCanceledException>(()=>middle);lease.Dispose();using var acquired=await tail;Check(gate.IsLocked,"cancelled queue entry skipped");
    }
    [SimTest] public static async Task R057_CancelAfterPermitGrantReturnsReservation(TestContext c)
    {
        var semaphore=new SimSemaphore(0,1);using var cancel=new CancellationTokenSource();var waiting=semaphore.Acquire(cancel.Token);
        semaphore.Release();cancel.Cancel();await Fails<OperationCanceledException>(()=>waiting);
        Check(semaphore.CurrentCount==1,"unconsumed cancelled grant returned");await semaphore.Acquire();Check(semaphore.CurrentCount==0,"returned permit remains usable");
    }
    [SimTest] public static async Task R058_CancelAfterLockHandoffCannotLeakLease(TestContext c)
    {
        var gate=new SimLock();var owner=await gate.Acquire();using var cancel=new CancellationTokenSource();var waiting=gate.Acquire(cancel.Token);
        owner.Dispose();cancel.Cancel();await Fails<OperationCanceledException>(()=>waiting);Check(!gate.IsLocked,"cancelled handoff released lock");using var recovered=await gate.Acquire();Check(gate.IsLocked,"subsequent lock acquisition");
    }
    [SimTest] public static async Task R059_DisposingLeaseTwiceCannotReleaseAnotherOwner(TestContext c)
    {
        var gate=new SimLock();var original=await gate.Acquire();var first=gate.Acquire();var second=gate.Acquire();original.Dispose();original.Dispose();
        var next=await first;Check(!second.IsCompleted&&gate.IsLocked,"double Dispose did not release another owner");next.Dispose();var last=await second;last.Dispose();Check(!gate.IsLocked,"last owner releases exactly once");
    }
    [SimTest] public static async Task R060_PreCancelledAcquireCannotReserveFreeLock(TestContext c)
    {
        var gate=new SimLock();await Fails<OperationCanceledException>(()=>gate.Acquire(new CancellationToken(true)));Check(!gate.IsLocked,"pre-cancelled lock leaves permit");using var lease=await gate.Acquire();Check(gate.IsLocked,"free permit remains usable");
    }
}
