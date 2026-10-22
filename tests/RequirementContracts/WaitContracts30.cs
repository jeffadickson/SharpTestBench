using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest] public static async Task R021_CompletionWinsSameStepDeadline(TestContext c)
    {
        var done=new TaskCompletionSource<int>();var writer=c.StartSoon(async _=>{await Timer(2);done.SetResult(21);});await Yield();
        Check(await SimTasks.WithTimeout(done.Task,2)==21,"same-step completed task wins");await writer;
    }
    [SimTest] public static async Task R022_TimeoutPreservesTypedReferenceIdentity(TestContext c)
    {
        var expected=new object();var job=c.StartSoon(async _=>{await Timer(2);return expected;});
        Check(ReferenceEquals(expected,await SimTasks.WithTimeout(job.Task,100)),"typed timeout identity");
    }
    [SimTest] public static async Task R023_ExternalTimeoutCancellationDetachesDeadline(TestContext c)
    {
        using var cancel=new CancellationTokenSource();var job=c.StartSoon(async _=>await Timer(3));await Yield();int before=c.Runtime.PendingCallbacks;
        var waiting=SimTasks.WithTimeout(job.Task,100,cancel.Token);cancel.Cancel();await Fails<OperationCanceledException>(()=>waiting);
        Check(c.Runtime.PendingCallbacks==before&&!job.IsCompleted,"cancel timeout deadline only");await job;
    }
    [SimTest] public static async Task R024_ZeroDeadlineFailsWithoutRegistration(TestContext c)
    {int before=c.Runtime.PendingCallbacks;await Fails<ArgumentOutOfRangeException>(()=>SimTasks.WithTimeout(Task.CompletedTask,0));Check(before==c.Runtime.PendingCallbacks,"invalid deadline callback");}
    [SimTest] public static async Task R025_FirstSnapshotsCallerInputArray(TestContext c)
    {
        var a=new TaskCompletionSource();var b=new TaskCompletionSource();Task[] inputs=[a.Task,b.Task];var waiting=SimTasks.First(inputs);inputs[0]=Task.CompletedTask;
        b.SetResult();Check(ReferenceEquals(await waiting,b.Task),"caller mutation cannot replace awaited input");a.SetResult();
    }
    [SimTest] public static async Task R026_PreCancelledFirstLeavesInputsUntouched(TestContext c)
    {
        var task=new TaskCompletionSource();await Fails<OperationCanceledException>(()=>SimTasks.First(new CancellationToken(true),task.Task));
        Check(!task.Task.IsCompleted,"pre-cancelled first borrowed input");task.SetResult();
    }
    [SimTest] public static async Task R027_CombineCancellationPreservesBothInputs(TestContext c)
    {
        using var source=new CancellationTokenSource();var a=new TaskCompletionSource();var b=new TaskCompletionSource();var waiting=SimTasks.Combine(source.Token,a.Task,b.Task);
        source.Cancel();await Fails<OperationCanceledException>(()=>waiting);Check(!a.Task.IsCompleted&&!b.Task.IsCompleted,"combine borrowed inputs survive");a.SetResult();b.SetResult();
    }
    [SimTest] public static async Task R028_FirstDuplicateEventSubscriptionsDetachCleanly(TestContext c)
    {
        var ev=new SimEvent();var trigger=new CountedTrigger(ev.Wait());var waiting=First(trigger,trigger).AsTask();Check(trigger.Registered==2&&trigger.Active==2,"first owns two duplicate subscriptions");ev.Set();await waiting;Check(trigger.Active==0&&trigger.Detached==2,"first detaches both duplicate subscriptions");
        ev.Clear();using var cancel=new CancellationTokenSource();var next=ev.Wait().AsTask(cancel.Token);Check(!next.IsCompleted,"duplicate first leaves no sticky state");cancel.Cancel();await Fails<OperationCanceledException>(()=>next);
    }
    [SimTest] public static async Task R029_CombineDuplicateEventWaitsIndependently(TestContext c)
    {
        var ev=new SimEvent();var trigger=new CountedTrigger(ev.Wait());var waiting=Combine(trigger,trigger).AsTask();Check(!waiting.IsCompleted&&trigger.Registered==2&&trigger.Active==2,"combine owns independent duplicate subscriptions");ev.Set();await waiting;Check(trigger.Active==0&&trigger.Detached==2,"combine detaches both duplicate subscriptions");ev.Clear();Check(!ev.IsSet,"duplicate combine preserves event reset");
    }
    [SimTest] public static async Task R030_ExpiredTriggerWaitDoesNotConsumeFutureEvent(TestContext c)
    {
        var ev=new SimEvent();await Fails<TimeoutException>(()=>WithTimeout(ev.Wait(),1).AsTask());
        var next=ev.Wait().AsTask();Check(!next.IsCompleted,"future waiter remains pending");ev.Set();await next;Check(ev.IsSet,"late event survives expired timeout");
    }
}
