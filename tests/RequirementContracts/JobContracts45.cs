using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest] public static async Task R041_CurrentJobIdentitySurvivesNativeAwait(TestContext c)
    {
        Check(SimJob.Current is null,"test body has no StartSoon identity");SimJob? observed=null;
        var job=c.StartSoon(async _=>{observed=SimJob.Current;await Timer(2);Check(ReferenceEquals(SimJob.Current,observed),"job identity across native await");});
        await job;Check(ReferenceEquals(job,observed)&&SimJob.Current is null,"root identity restored");
    }
    [SimTest] public static async Task R042_NestedJobRestoresParentIdentity(TestContext c)
    {
        var parent=c.StartSoon(async _=>{var own=SimJob.Current;SimJob? childIdentity=null;var child=c.StartSoon(async _=>{childIdentity=SimJob.Current;await Timer(1);});await child;
            Check(ReferenceEquals(childIdentity,child)&&!ReferenceEquals(own,childIdentity)&&ReferenceEquals(own,SimJob.Current),"nested identity scope");});await parent;
    }
    [SimTest] public static async Task R043_UnfinishedResultCannotBlockSimulator(TestContext c)
    {
        var job=c.StartSoon(async _=>{await Timer(3);return 43;});ulong time=c.Runtime.Now;
        Throws<InvalidOperationException>(()=>_ = job.Result);Check(c.Runtime.Now==time&&!job.IsCompleted,"unfinished Result did not block");Check(await job==43,"later typed result remains valid");
    }
    [SimTest] public static async Task R044_CancelledJobPreservesItsTokenIdentity(TestContext c)
    {
        CancellationToken captured=default;var job=c.StartSoon(async token=>{captured=token;await Timer(100);return 44;});await Yield();job.Cancel();await job.Complete;
        var failure=Throws<TaskCanceledException>(()=>_ = job.Result);Check(job.Task.IsCanceled&&failure.CancellationToken==captured,"job cancellation token identity");
    }
    [SimTest] public static async Task R045_LateCancelCannotRewriteCompletedResult(TestContext c)
    {
        var expected=new object();var job=c.StartSoon(_=>Task.FromResult(expected));Check(ReferenceEquals(expected,await job),"original success");
        job.Cancel();job.Cancel();Check(job.Task.IsCompletedSuccessfully&&ReferenceEquals(expected,job.Result)&&job.Exception is null,"late cancellation preserves success");
    }
}
