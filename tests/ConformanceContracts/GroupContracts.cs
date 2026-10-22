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
    [SimTest(TimeoutSteps=10000000)] public static async Task R051_ParentCancelledEventWaiterCannotConsumeFutureSet(TestContext c)
    {
        var ev=new SimEvent();var ready=new SimEvent();bool woke=false;var parent=GroupParent(c,async group=>{_ = group.StartSoon(async child=>{ready.Set();await ev.Wait();woke=true;});await Timer(10000);});
        await ready.Wait();parent.Cancel();Check(await parent is OperationCanceledException&&!woke,"canceled child did not wake successfully");ev.Set();await ev.Wait();Check(!woke&&ev.IsSet,"future root waiter observes set without reviving canceled child");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R052_ParentCancelledSemaphoreWaiterCannotStealReleasedPermit(TestContext c)
    {
        var semaphore=new SimSemaphore(0,1);var ready=new SimEvent();bool acquired=false;var parent=GroupParent(c,async group=>{_ = group.StartSoon(async child=>{ready.Set();await semaphore.Acquire();acquired=true;});await Timer(10000);});
        await ready.Wait();parent.Cancel();Check(await parent is OperationCanceledException&&!acquired,"canceled acquisition");semaphore.Release();await semaphore.Acquire();Check(!acquired&&semaphore.CurrentCount==0,"released permit belongs to surviving root consumer");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R053_ParentCancellationAwaitsLockReleaseBeforeJoin(TestContext c)
    {
        var gate=new SimLock();var ready=new SimEvent();bool released=false;var parent=GroupParent(c,async group=>{_ = group.StartSoon(async child=>{using(await gate.Acquire()){ready.Set();try{await Timer(10000);}finally{await Timer(2).WithCancellation(CancellationToken.None);}}released=true;});await Timer(10000);});
        await ready.Wait();parent.Cancel();Check(await parent is OperationCanceledException,"lock owner canceled");using(await gate.Acquire()){Check(!released,"post-cancellation code skipped but using released owned lock");}await Timer(1);
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R054_ParentCancelledBoundedProducerPreservesExistingQueueItem(TestContext c)
    {
        var queue=new SimQueue<int>(1);await queue.Put(71);var ready=new SimEvent();bool inserted=false;var parent=GroupParent(c,async group=>{_ = group.StartSoon(async child=>{ready.Set();await queue.Put(72);inserted=true;});await Timer(10000);});
        await ready.Wait();parent.Cancel();Check(await parent is OperationCanceledException&&queue.Count==1&&!inserted,"blocked producer detached");Check(await queue.Get()==71&&queue.Count==0,"existing item not overwritten");await queue.Put(73);Check(await queue.Get()==73&&!inserted,"free slot not stolen by canceled producer");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R055_ParentCancelledQueueConsumerDoesNotRemoveFutureItem(TestContext c)
    {
        var queue=new SimQueue<int>(1);var ready=new SimEvent();bool removed=false;var parent=GroupParent(c,async group=>{_ = group.StartSoon(async child=>{ready.Set();await queue.Get();removed=true;});await Timer(10000);});
        await ready.Wait();parent.Cancel();Check(await parent is OperationCanceledException&&!removed,"blocked consumer detached");await queue.Put(0x59);Check(await queue.Get()==0x59&&queue.IsEmpty&&!removed,"future item consumed only by surviving root");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R056_ParentCancelledMiddleSemaphoreWaiterPreservesSurvivorOrder(TestContext c)
    {
        var semaphore=new SimSemaphore(0,3);var firstReady=new SimEvent();var middleReady=new SimEvent();var lastReady=new SimEvent();var order=new List<int>();
        var first=c.StartSoon(async token=>{firstReady.Set();await semaphore.Acquire();order.Add(1);});await firstReady.Wait();
        var parent=GroupParent(c,async group=>{_ = group.StartSoon(async child=>{middleReady.Set();await semaphore.Acquire();order.Add(2);});await Timer(10000);});await middleReady.Wait();
        var last=c.StartSoon(async token=>{lastReady.Set();await semaphore.Acquire();order.Add(3);});await lastReady.Wait();parent.Cancel();Check(await parent is OperationCanceledException,"middle owner canceled");
        semaphore.Release();semaphore.Release();await first;await last;Check(order.SequenceEqual(new[]{1,3})&&semaphore.CurrentCount==0,"canceled middle waiter neither steals nor reorders surviving permits");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R057_ForeignParentCancelCannotMutateLiveGroup(TestContext c)
    {
        var ready=new SimEvent();bool cleaned=false;var parent=GroupParent(c,async group=>{_ = group.StartSoon(async child=>{ready.Set();try{await Timer(10000);}finally{cleaned=true;}});await Timer(10000);});await ready.Wait();
        int before=c.Runtime.PendingCallbacks;Exception? rejected=null;var thread=new Thread(()=>{try{parent.Cancel();}catch(Exception error){rejected=error;}});thread.Start();thread.Join();
        Check(rejected is InvalidOperationException&&!cleaned&&!parent.IsCompleted&&c.Runtime.PendingCallbacks==before,"foreign cancel rejected before cancellation state changes");parent.Cancel();Check(await parent is OperationCanceledException&&cleaned,"owning thread can still cancel and join");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R058_ParentCancellationRetainsFailuresAtBothNestedGroupLevels(TestContext c)
    {
        var ready=new SimEvent();var outerError=new IOException("outer callback");var innerError=new FormatException("inner callback");int cleaned=0;
        var parent=GroupParent(c,async outer=>{
            _ = outer.StartSoon(async child=>{using var registration=child.Register(()=>throw outerError);try{await c.RunGroup(async inner=>{
                _ = inner.StartSoon(async grandchild=>{using var nested=grandchild.Register(()=>throw innerError);ready.Set();try{await Timer(10000);}finally{await Timer(1).WithCancellation(CancellationToken.None);cleaned++;}});await Timer(10000);
            });}finally{cleaned++;}});await Timer(10000);
        });await ready.Wait();Exception? escaped=null;try{parent.Cancel();}catch(Exception cancelError){escaped=cancelError;}var result=await parent;var errors=(result as AggregateException)?.Flatten().InnerExceptions;
        Check(escaped is null&&cleaned==2&&errors?.Count==2&&errors.Any(e=>ReferenceEquals(e,outerError))&&errors.Any(e=>ReferenceEquals(e,innerError)),"nested callback failures preserved once after both cleanup levels");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R059_CancelledBorrowerCannotCancelGroupOwnedTypedChild(TestContext c)
    {
        var ready=new SimEvent();SimJob<int>? owned=null;var parent=GroupParent(c,async group=>{owned=group.StartSoon(async token=>{await Timer(5);return 59;});ready.Set();Check(await owned==59,"owner sees typed result");});await ready.Wait();
        using var cancel=new CancellationTokenSource();var borrower=c.StartSoon(async token=>{try{await owned!.Complete.WithCancellation(cancel.Token);return false;}catch(OperationCanceledException){return true;}});await Yield();cancel.Cancel();Check(await borrower,"borrowed completion waiter canceled");Check(await parent is null&&owned!.Result==59,"group-owned job survives canceled borrower");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R060_ClosingGroupRejectsLateChildWhileAsynchronousJoinIsPending(TestContext c)
    {
        var cleanupStarted=new SimEvent();var ready=new SimEvent();SimTaskGroup? saved=null;bool lateRan=false,joined=false;
        var parent=GroupParent(c,async group=>{saved=group;_ = group.StartSoon(async child=>{ready.Set();try{await Timer(10000);}finally{cleanupStarted.Set();await Timer(3).WithCancellation(CancellationToken.None);joined=true;}});await Timer(10000);});
        await ready.Wait();parent.Cancel();await cleanupStarted.Wait();await Yield();Check(!parent.IsCompleted,"join still awaits cleanup");Throws<InvalidOperationException>(()=>saved!.StartSoon(_=>{lateRan=true;return Task.CompletedTask;}));Check(await parent is OperationCanceledException&&joined&&!lateRan,"closed registration cannot escape parent join");
    }
}
