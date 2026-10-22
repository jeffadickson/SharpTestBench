using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest(TimeoutSteps=10000000)] public static async Task R041_ParentCancellationCallbackFailureIsReportedByJoinedGroup(TestContext c)
    {
        var ready=new SimEvent();var marker=new FormatException("parent-linked cancellation callback");bool cleaned=false;int before=c.Runtime.PendingCallbacks;
        var parent=c.StartSoon<Exception?>(async token=>{
            try {await c.RunGroup(async group=>{
                var started=new SimEvent();_ = group.StartSoon(async childToken=>{
                    using var registration=childToken.Register(()=>throw marker);started.Set();
                    try {await Timer(10000);}finally{await Timer(2).WithCancellation(CancellationToken.None);cleaned=true;}
                });await started.Wait();ready.Set();await Timer(10000);
            });}catch(Exception error){return error;}return null;
        });
        await ready.Wait();Exception? requestFailure=null;try{parent.Cancel();}catch(Exception error){requestFailure=error;}
        var joined=await parent;Check(cleaned&&c.Runtime.PendingCallbacks==before,"parent cancellation must join asynchronous cleanup and detach waits");
        Check(requestFailure is null,"group callback failure must be reported at group join, not escape parent cancellation request");
        Check(joined is AggregateException aggregate&&aggregate.Flatten().InnerExceptions.Any(error=>ReferenceEquals(error,marker)),"joined group preserves original cancellation callback failure");
    }

    [SimTest(TimeoutSteps=10000000)] public static async Task R042_ParentCancellationJoinsFailingAsynchronousCleanup(TestContext c)
    {
        var ready=new SimEvent();var marker=new IOException("parent-cancel cleanup");bool cleaned=false;
        var parent=c.StartSoon<Exception?>(async token=>{try{await c.RunGroup(async group=>{
            var started=new SimEvent();_ = group.StartSoon(async child=>{started.Set();try{await Timer(10000);}finally{await Timer(3).WithCancellation(CancellationToken.None);cleaned=true;throw marker;}});
            await started.Wait();ready.Set();await Timer(10000);
        });}catch(Exception error){return error;}return null;});
        await ready.Wait();parent.Cancel();var error=await parent;Check(cleaned&&ReferenceEquals(error,marker),"parent cancellation retains awaited cleanup failure identity");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R043_ParentCancellationAggregatesAllChildCallbackFailures(TestContext c)
    {
        var ready=new SimEvent();Exception[] markers=[new IOException("callback-a"),new FormatException("callback-b")];int cleaned=0;
        var parent=c.StartSoon<Exception?>(async token=>{try{await c.RunGroup(async group=>{
            var started=new SimEvent();int count=0;foreach(var marker in markers)_ = group.StartSoon(async child=>{using var registration=child.Register(()=>throw marker);if(++count==2)started.Set();try{await Timer(10000);}finally{await Timer(1).WithCancellation(CancellationToken.None);cleaned++;}});
            await started.Wait();ready.Set();await Timer(10000);
        });}catch(Exception error){return error;}return null;});
        await ready.Wait();Exception? escaped=null;try{parent.Cancel();}catch(Exception cancelError){escaped=cancelError;}var result=await parent;
        Check(escaped is null&&cleaned==2,"all callback failures contained while both children join");var errors=(result as AggregateException)?.Flatten().InnerExceptions;
        Check(errors?.Count==2&&markers.All(marker=>errors.Any(error=>ReferenceEquals(marker,error))),"all original callback failures exactly once");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R044_ParentCancellationJoinsNestedGrandchildCleanup(TestContext c)
    {
        var ready=new SimEvent();var order=new List<string>();
        var parent=c.StartSoon<Exception?>(async token=>{try{await c.RunGroup(async outer=>{
            _ = outer.StartSoon(async child=>{try{await c.RunGroup(async inner=>{
                _ = inner.StartSoon(async grandchild=>{ready.Set();try{await Timer(10000);}finally{await Timer(2).WithCancellation(CancellationToken.None);order.Add("grandchild");}});await Timer(10000);
            });}finally{order.Add("child");}});await Timer(10000);
        });}catch(Exception error){return error;}return null;});
        await ready.Wait();parent.Cancel();var error=await parent;Check(error is OperationCanceledException&&order.SequenceEqual(new[]{"grandchild","child"}),"nested group join precedes parent completion");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R045_ParentCancellationLeavesUnrelatedRootJobRunning(TestContext c)
    {
        var ready=new SimEvent();var unrelated=c.StartSoon(async token=>{await Timer(7);return 73;});
        var parent=c.StartSoon<Exception?>(async token=>{try{await c.RunGroup(async group=>{_ = group.StartSoon(async child=>{ready.Set();await Timer(10000);});await Timer(10000);});}catch(Exception error){return error;}return null;});
        await ready.Wait();parent.Cancel();Check(await parent is OperationCanceledException,"canceled subtree reports cancellation");Check(await unrelated==73,"root sibling is not owned by canceled subtree");
    }

    [SimTest(TimeoutSteps=10000000)] public static async Task R046_CaughtGroupFailureRestoresParentCancellationForRecoveryScope(TestContext c)
    {
        var ready=new SimEvent();var marker=new FormatException("failed first scope");bool recovered=false,cleaned=false;
        var parent=c.StartSoon<Exception?>(async token=>{
            var first=await Fails<FormatException>(()=>c.RunGroup(_=>throw marker));Check(ReferenceEquals(first,marker),"first scope error identity");
            try{await c.RunGroup(async group=>{_ = group.StartSoon(async child=>{recovered=true;ready.Set();try{await Timer(10000);}finally{cleaned=true;}});await Timer(10000);});}catch(Exception error){return error;}return null;
        });
        await ready.Wait();parent.Cancel();Check(await parent is OperationCanceledException&&recovered&&cleaned,"recovery scope inherits live parent then receives its later cancellation");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R047_ParentCancellationDoesNotRewriteCompletedTypedChild(TestContext c)
    {
        var ready=new SimEvent();SimJob<int>? completed=null;
        var parent=c.StartSoon<Exception?>(async token=>{try{await c.RunGroup(async group=>{completed=group.StartSoon(async child=>{await Timer(1);return 47;});Check(await completed==47,"child completed before parent cancellation");ready.Set();await Timer(10000);});}catch(Exception error){return error;}return null;});
        await ready.Wait();parent.Cancel();Check(await parent is OperationCanceledException&&completed!.Result==47&&!completed.Task.IsCanceled,"completed result retained across parent teardown");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R048_ParentCancellationPreservesBodyAndCallbackFailures(TestContext c)
    {
        var ready=new SimEvent();var callback=new FormatException("parent callback");var body=new IOException("body-finally");
        var parent=c.StartSoon<Exception?>(async token=>{try{await c.RunGroup(async group=>{
            _ = group.StartSoon(async child=>{using var registration=child.Register(()=>throw callback);ready.Set();await Timer(10000);});try{await Timer(10000);}finally{throw body;}
        });}catch(Exception error){return error;}return null;});
        await ready.Wait();Exception? escaped=null;try{parent.Cancel();}catch(Exception cancelError){escaped=cancelError;}var error=await parent;
        var errors=(error as AggregateException)?.Flatten().InnerExceptions;Check(escaped is null&&errors?.Count==2&&errors.Any(e=>ReferenceEquals(e,body))&&errors.Any(e=>ReferenceEquals(e,callback)),"body failure must not mask linked callback failure");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R049_RepeatedParentCancellationCannotRepeatChildCleanup(TestContext c)
    {
        var ready=new SimEvent();int callbacks=0,cleaned=0;
        var parent=c.StartSoon<Exception?>(async token=>{try{await c.RunGroup(async group=>{_ = group.StartSoon(async child=>{using var registration=child.Register(()=>callbacks++);ready.Set();try{await Timer(10000);}finally{await Timer(2).WithCancellation(CancellationToken.None);cleaned++;}});await Timer(10000);});}catch(Exception error){return error;}return null;});
        await ready.Wait();parent.Cancel();parent.Cancel();Check(await parent is OperationCanceledException&&callbacks==1&&cleaned==1,"idempotent cancel while async cleanup in flight");parent.Cancel();Check(callbacks==1&&cleaned==1,"completed teardown not replayed");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R050_ParentCancellationDetachesNestedWaitCallbacks(TestContext c)
    {
        var ready=new SimEvent();int before=c.Runtime.PendingCallbacks;
        var parent=c.StartSoon<Exception?>(async token=>{try{await c.RunGroup(async group=>{_ = group.StartSoon(async child=>{ready.Set();await First(Timer(10000),Timer(20000));});await Timer(10000);});}catch(Exception error){return error;}return null;});
        await ready.Wait();Check(c.Runtime.PendingCallbacks>=before+3,"both child race timers and body timer subscribed");parent.Cancel();Check(await parent is OperationCanceledException&&c.Runtime.PendingCallbacks==before,"nested loser timers detached after joined cancellation");await Timer(1);Check(c.Runtime.PendingCallbacks==before,"future scheduler turn stays quiescent");
    }

    static SimJob<Exception?> GroupParent(TestContext c,Func<SimTaskGroup,Task> body)=>c.StartSoon<Exception?>(async token=>{try{await c.RunGroup(body);}catch(Exception error){return error;}return null;});
}
