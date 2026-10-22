using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest] public static async Task R016_CombineDefersFaultUntilAllSettle(TestContext c)
    {
        var failed=new TaskCompletionSource();var original=new IOException("controlled combine fault");
        var writer=c.StartSoon(async _=>{await Timer(1);failed.SetException(original);});var late=c.StartSoon(async _=>await Timer(5));ulong start=c.Runtime.Now;
        var error=await Fails<IOException>(()=>SimTasks.Combine(failed.Task,late.Task));Check(ReferenceEquals(error,original)&&late.IsCompleted&&c.Runtime.Now==start+5,"combine fault after all settle");await writer;
    }
    [SimTest] public static async Task R017_CombineCancelledMemberStillJoinsOthers(TestContext c)
    {
        var late=c.StartSoon(async _=>await Timer(3));var cancelled=Task.FromCanceled(new CancellationToken(true));
        await Fails<TaskCanceledException>(()=>SimTasks.Combine(cancelled,late.Task));Check(late.IsCompleted,"combine cancellation waits borrowed member");
    }
    [SimTest] public static async Task R018_CompletedTaskRemovesUnusedDeadline(TestContext c)
    {
        int before=c.Runtime.PendingCallbacks;int result=await SimTasks.WithTimeout(Task.FromResult(37),10000);
        Check(result==37&&before==c.Runtime.PendingCallbacks,"unused typed deadline removed");
    }
    [SimTest] public static async Task R019_TimeoutDoesNotCancelBorrowedTask(TestContext c)
    {
        bool completed=false;var job=c.StartSoon(async _=>{await Timer(5);completed=true;return 19;});
        await Fails<TimeoutException>(()=>SimTasks.WithTimeout(job.Task,2));Check(!completed&&!job.IsCompleted,"timeout preserves borrowed job");
        Check(await job==19&&completed,"borrowed job completes later");
    }
    [SimTest] public static async Task R020_CompletedFaultWinsOverDeadline(TestContext c)
    {
        var original=new FormatException("controlled completed fault");int before=c.Runtime.PendingCallbacks;
        var error=await Fails<FormatException>(()=>SimTasks.WithTimeout(Task.FromException(original),1));
        Check(ReferenceEquals(error,original)&&before==c.Runtime.PendingCallbacks,"fault identity/deadline retirement");
    }
}
