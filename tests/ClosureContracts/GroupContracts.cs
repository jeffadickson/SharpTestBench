using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest] public static async Task R041_GroupExitJoinsChildAsyncCleanup(TestContext c)
    {bool cleaned=false;await c.RunGroup(async group=>{_ = group.StartSoon(async token=>{try{await Timer(10000).WithCancellation(token);}finally{await Timer(1).WithCancellation(CancellationToken.None);cleaned=true;}});await Yield();});Check(cleaned,"group exit joins asynchronous cleanup");await Timer(1);}
    [SimTest] public static async Task R042_GroupResultSurvivesScopeCancellation(TestContext c)
    {SimJob<int>? child=null;await c.RunGroup(async group=>{child=group.StartSoon(async token=>{await Timer(1).WithCancellation(token);return 42;});Check(await child==42,"typed group result");});Check(child!.Result==42&&!child.Task.IsCanceled,"completed child preserved at scope exit");}
    [SimTest] public static async Task R043_NullGroupBodyCannotRegisterCallbacks(TestContext c)
    {int before=c.Runtime.PendingCallbacks;await Fails<ArgumentNullException>(()=>c.RunGroup(null!));Check(before==c.Runtime.PendingCallbacks,"null body registration");}
    [SimTest] public static async Task R044_ClosedGroupCannotScheduleOrConsumeAutomaticName(TestContext c)
    {SimTaskGroup? saved=null;await c.RunGroup(group=>{saved=group;return Task.CompletedTask;});Throws<InvalidOperationException>(()=>saved!.StartSoon(_=>Task.CompletedTask));var next=c.StartSoon(_=>Task.CompletedTask);await next;Check(next.Name=="job-1","closed group did not allocate a child");}
    [SimTest] public static async Task R045_GroupBodyFailureWaitsForCancelledChild(TestContext c)
    {var marker=new FormatException("body failure");bool cleaned=false;var error=await Fails<FormatException>(()=>c.RunGroup(async group=>{_ = group.StartSoon(async token=>{try{await Timer(10000).WithCancellation(token);}finally{await Timer(1).WithCancellation(CancellationToken.None);cleaned=true;}});await Yield();throw marker;}));Check(cleaned&&ReferenceEquals(error,marker),"body failure preserved after child join");}
    [SimTest] public static async Task R046_CaughtNestedGroupFailureDoesNotCancelOuterChild(TestContext c)
    {await c.RunGroup(async outer=>{var survivor=outer.StartSoon(async token=>{await Timer(6).WithCancellation(token);return 46;});await Fails<FormatException>(()=>c.RunGroup(_=>throw new FormatException("inner")));Check(await survivor==46,"outer child survives caught inner failure");});await Timer(1);}
    [SimTest] public static async Task R047_ForeignGroupStartCannotEnqueueUserBody(TestContext c)
    {bool invalidRan=false;await c.RunGroup(async group=>{Exception? failure=null;var thread=new Thread(()=>{try{_ = group.StartSoon(_=>{invalidRan=true;return Task.CompletedTask;});}catch(Exception error){failure=error;}});thread.Start();thread.Join();Check(failure is InvalidOperationException,"group start ownership");await group.StartSoon(async token=>await Timer(1).WithCancellation(token));});Check(!invalidRan,"foreign group body never queued");}
    [SimTest] public static async Task R048_AwaitedChildFaultIsNotDuplicatedByGroup(TestContext c)
    {var marker=new FormatException("same child");var error=await Fails<FormatException>(()=>c.RunGroup(async group=>{await group.StartSoon(async token=>{await Timer(1).WithCancellation(token);throw marker;});}));Check(ReferenceEquals(error,marker),"group child/body identity deduplicated");}
    [SimTest] public static async Task R049_GroupRetainsBodyAndBothCleanupFailures(TestContext c)
    {var body=new FormatException("body");var first=new IOException("first cleanup");var second=new ArithmeticException("second cleanup");var error=await Fails<AggregateException>(()=>c.RunGroup(async group=>{_ = group.StartSoon(async token=>{try{await Timer(10000).WithCancellation(token);}finally{throw first;}});_ = group.StartSoon(async token=>{try{await Timer(10000).WithCancellation(token);}finally{throw second;}});await Yield();throw body;}));var errors=error.Flatten().InnerExceptions;Check(errors.Count==3&&errors.Any(x=>ReferenceEquals(x,body))&&errors.Any(x=>ReferenceEquals(x,first))&&errors.Any(x=>ReferenceEquals(x,second)),"three unique group failures retained");}
    [SimTest] public static async Task R050_CancellationCallbackFailureCannotBypassGroupJoin(TestContext c)
    {var marker=new IOException("cancel callback");bool cleaned=false;int before=c.Runtime.PendingCallbacks;var error=await Fails<AggregateException>(()=>c.RunGroup(async group=>{_ = group.StartSoon(async token=>{using var registration=token.Register(()=>throw marker);try{await Timer(10000).WithCancellation(token);}finally{await Timer(1).WithCancellation(CancellationToken.None);cleaned=true;}});await Yield();}));Check(error.Flatten().InnerExceptions.Any(x=>ReferenceEquals(x,marker))&&cleaned&&before==c.Runtime.PendingCallbacks,"cancellation callback error retained after child cleanup and detachment");
      var primary=new FormatException("child primary");var callback=new IOException("child-triggered callback");bool joined=false;
      var childError=await Fails<AggregateException>(()=>c.RunGroup(async group=>{
        _ = group.StartSoon(async token=>{using var registration=token.Register(()=>throw callback);try{await Timer(10000).WithCancellation(token);}finally{await Timer(1).WithCancellation(CancellationToken.None);joined=true;}});
        _ = group.StartSoon(async token=>{await Timer(2).WithCancellation(token);throw primary;});await Yield();await Timer(100).WithCancellation(CancellationToken.None);
      }));var errors=childError.Flatten().InnerExceptions;Check(joined&&errors.Any(x=>ReferenceEquals(x,primary))&&errors.Any(x=>ReferenceEquals(x,callback))&&before==c.Runtime.PendingCallbacks,"child failure cancellation preserves both errors and joins cleanup");}
}
