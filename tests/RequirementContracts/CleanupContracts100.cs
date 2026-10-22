using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest] public static async Task R091_CleanupRunsMixedCallbacksInReverseOrder(TestContext c)
    {
        var scope=c.Cleanup.CreateScope();var order=new List<int>();scope.Defer(()=>order.Add(1));scope.Defer(async _=>{order.Add(2);await Timer(2);order.Add(3);});scope.Defer(()=>order.Add(4));
        await scope.CloseAsync();Check(order.SequenceEqual(new[]{4,2,3,1}),"mixed cleanup LIFO and awaited completion");
    }
    [SimTest] public static async Task R092_CancelledCleanupStillRunsRemainingCallbacks(TestContext c)
    {
        var scope=new CleanupScope();bool remaining=false;scope.Defer(()=>remaining=true);scope.Defer(token=>Task.FromCanceled(token));
        var error=await Fails<AggregateException>(()=>scope.CloseAsync(new CancellationToken(true)));Check(remaining&&error.Flatten().InnerExceptions.Single() is OperationCanceledException,"cleanup cancellation cannot skip older resources");
    }
    [SimTest] public static async Task R093_CleanupAggregatesEveryOriginalFailureInLifoOrder(TestContext c)
    {
        var scope=new CleanupScope();var first=new FormatException("first cleanup");var last=new IOException("last cleanup");scope.Defer(()=>throw first);scope.Defer(async _=>{await Timer(1);throw last;});
        var error=await Fails<AggregateException>(()=>scope.CloseAsync());Check(error.InnerExceptions.Count==2&&ReferenceEquals(error.InnerExceptions[0],last)&&ReferenceEquals(error.InnerExceptions[1],first),"cleanup preserves order and original exception identities");
    }
    [SimTest] public static async Task R094_ConcurrentAndRepeatedCloseShareCompletion(TestContext c)
    {
        var scope=c.Cleanup.CreateScope();int calls=0;scope.Defer(async _=>{calls++;await Timer(3);});var first=scope.CloseAsync();var second=scope.CloseAsync();Check(ReferenceEquals(first,second)&&!first.IsCompleted,"pending close published once");await first;Check(ReferenceEquals(first,scope.CloseAsync())&&calls==1,"completed close reused without repeating disposal");
    }
    [SimTest] public static async Task R095_ClosingScopeRejectsNewCleanupRegistration(TestContext c)
    {
        var scope=c.Cleanup.CreateScope();int calls=0;scope.Defer(async _=>{await Timer(2);calls++;});var pending=scope.CloseAsync();Throws<InvalidOperationException>(()=>scope.Defer(()=>calls+=100));await pending;Check(calls==1,"closing registration rejected without callback insertion");
    }
    [SimTest] public static async Task R096_NestedCleanupIsAwaitedAtItsRegistrationPosition(TestContext c)
    {
        var parent=c.Cleanup.CreateScope();var order=new List<int>();parent.Defer(()=>order.Add(1));var child=parent.CreateScope();child.Defer(async _=>{order.Add(2);await Timer(2);order.Add(3);});parent.Defer(()=>order.Add(4));await parent.CloseAsync();Check(order.SequenceEqual(new[]{4,2,3,1}),"parent waits child at LIFO registration position");
    }
    [SimTest] public static async Task R097_OwnReturnsIdentityAndDisposesExactlyOnce(TestContext c)
    {
        var scope=c.Cleanup.CreateScope();int calls=0;var resource=new CleanupDisposeProbe(()=>calls++);Check(ReferenceEquals(resource,scope.Own(resource)),"Own preserves resource identity");await scope.CloseAsync();await scope.CloseAsync();Check(calls==1,"owned resource disposed once across repeated close");
    }
    [SimTest] public static async Task R098_AsyncOwnedResourceDelaysCloseUntilDisposalCompletes(TestContext c)
    {
        var scope=c.Cleanup.CreateScope();var resource=new AsyncCleanupProbe();Check(ReferenceEquals(resource,scope.OwnAsync(resource)),"OwnAsync preserves identity");ulong start=c.Runtime.Now;var close=scope.CloseAsync();Check(!close.IsCompleted&&!resource.Disposed,"async disposal remains pending");await close;Check(resource.Disposed&&resource.Calls==1&&c.Runtime.Now==start+2,"close waits native timer in disposal");
    }
    [SimTest] public static async Task R099_ReentrantCloseSeesAlreadyPublishedCompletion(TestContext c)
    {
        var scope=c.Cleanup.CreateScope();Task? nested=null;int calls=0;scope.Defer(()=>{calls++;nested=scope.CloseAsync();});var outer=scope.CloseAsync();await outer;Check(ReferenceEquals(outer,nested)&&calls==1,"synchronous reentrant close returns published task");
    }
    [SimTest] public static async Task R100_ContextCleanupRejectsForeignThreadRegistration(TestContext c)
    {
        var scope=c.Cleanup.CreateScope();int calls=0;Exception? failure=null;var thread=new Thread(()=>{try{scope.Defer(()=>calls+=100);}catch(Exception error){failure=error;}});thread.Start();thread.Join();Check(failure is InvalidOperationException,"context scope inherits owner access guard");scope.Defer(()=>calls++);await scope.CloseAsync();Check(calls==1,"foreign registration cannot affect owner cleanup");
    }
    sealed class CleanupDisposeProbe(Action dispose):IDisposable {public void Dispose()=>dispose();}
    sealed class AsyncCleanupProbe:IAsyncDisposable
    {
        public bool Disposed;public int Calls;
        public async ValueTask DisposeAsync(){Calls++;await Timer(2);Disposed=true;}
    }
}
