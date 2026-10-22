using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest] public static async Task R081_ClockIntrospectionTracksConfiguredTimingAndOwnership(TestContext c)
    {
        var signal=Clock(c);await Drive(c,signal,false);var clock=new SimClock(signal,10,3,ClockImplementation.Managed);
        Check(ReferenceEquals(signal,clock.Signal)&&clock.PeriodSteps==10&&clock.HighSteps==3&&clock.LowSteps==7&&clock.Implementation==ClockImplementation.Managed&&!clock.IsRunning,"idle clock configuration");
        var owner=c.StartSoon(clock.Run);await Yield();Check(clock.IsRunning,"clock ownership visible after start");clock.SetPeriod(8,2);
        Check(clock.PeriodSteps==8&&clock.HighSteps==2&&clock.LowSteps==6,"configured timing changes atomically");
        owner.Cancel();await owner.Complete;Check(!clock.IsRunning,"clock ownership cleared after join");
        Exception? failure=null;var thread=new Thread(()=>{try{_ = clock.PeriodSteps;}catch(Exception error){failure=error;}});thread.Start();thread.Join();Check(failure is InvalidOperationException,"clock introspection thread guard");
    }
    [SimTest] public static Task R082_TriggerFactoriesRejectNullBeforeAcquiringResources(TestContext c)
    {
        int before=c.Runtime.PendingCallbacks;var errors=new[]{CaptureFailure(()=>First((Trigger[])null!)),CaptureFailure(()=>Combine((Trigger[])null!)),CaptureFailure(()=>RisingEdge(null!)),CaptureFailure(()=>FallingEdge(null!))};
        Check(errors.All(error=>error is ArgumentNullException)&&before==c.Runtime.PendingCallbacks,"null factory outcomes: "+string.Join(",",errors.Select(error=>error?.GetType().Name??"success")));return Task.CompletedTask;
    }
    [SimTest] public static async Task R083_TimeoutValidationPrecedesSubscription(TestContext c)
    {
        int before=c.Runtime.PendingCallbacks;Throws<ArgumentNullException>(()=>WithTimeout(null!,1));
        var probe=new CountedTrigger(Timer(10000));await Fails<OperationCanceledException>(()=>WithTimeout(probe,20000).AsTask(new CancellationToken(true)));
        Check(probe.Registered==0&&before==c.Runtime.PendingCallbacks,"invalid/pre-cancelled timeout never subscribes");
    }
    [SimTest] public static Task R084_TimeConversionRejectsUndefinedPoliciesAndForeignThread(TestContext c)
    {
        int before=c.Runtime.PendingCallbacks;var unit=CaptureFailure(()=>c.Runtime.Steps(1,(TimeUnit)1));var rounding=CaptureFailure(()=>c.Runtime.Steps(1,TimeUnit.Step,(TimeRounding)99));
        Exception? failure=null;var thread=new Thread(()=>failure=CaptureFailure(()=>c.Runtime.Steps(1,TimeUnit.Step)));thread.Start();thread.Join();
        Check(unit is ArgumentOutOfRangeException&&rounding is ArgumentOutOfRangeException&&failure is InvalidOperationException&&before==c.Runtime.PendingCallbacks,"conversion outcomes: "+string.Join(",",new[]{unit,rounding,failure}.Select(error=>error?.GetType().Name??"success")));return Task.CompletedTask;
    }
    [SimTest] public static async Task R085_TimerQuantizationGuaranteesOnePositiveStep(TestContext c)
    {
        ulong start=c.Runtime.Now;await Timer(0.25m,TimeUnit.Step,TimeRounding.Floor);Check(c.Runtime.Now==start+1,"substep timer clamps to one step");
        Throws<ArgumentOutOfRangeException>(()=>c.Runtime.Steps(0.25m,TimeUnit.Step,TimeRounding.Floor));Check(c.Runtime.Steps(0.25m,TimeUnit.Step,TimeRounding.Ceiling)==1,"conversion rounding remains explicit");
    }
    [SimTest] public static async Task R086_FirstRegistrationFailureUnwindsEarlierNativeTimer(TestContext c)
    {
        int before=c.Runtime.PendingCallbacks;var error=await Fails<FormatException>(()=>First(Timer(10000),new FailingRegistration()).AsTask());Check(error.Message=="registration marker"&&c.Runtime.PendingCallbacks==before,"partial First registration cleanup");
    }
    [SimTest] public static async Task R087_NestedTimeoutUnwindsBothTimersOnSiblingFailure(TestContext c)
    {
        int before=c.Runtime.PendingCallbacks;await Fails<FormatException>(()=>First(WithTimeout(Timer(10000),20000),new FailingRegistration()).AsTask());Check(c.Runtime.PendingCallbacks==before,"nested timeout and target both detached");
    }
    [SimTest] public static async Task R088_DuplicateSynchronousCompletionDetachesOnce(TestContext c)
    {
        var probe=new DoubleCompletion();ulong time=c.Runtime.Now;await probe;Check(probe.Detached==1&&c.Runtime.Now==time,"duplicate producer completion ignored and handle detached once");
    }
    [SimTest] public static async Task R089_StaleCancelledCallbackCannotCompleteNewSubscription(TestContext c)
    {
        var probe=new ManualCompletion();using var cancel=new CancellationTokenSource();var old=probe.AsTask(cancel.Token);var stale=probe.Completion!;cancel.Cancel();await Fails<OperationCanceledException>(()=>old);
        var next=probe.AsTask();stale(null);Check(!next.IsCompleted&&probe.Active==1,"stale completion cannot consume new subscription");probe.Completion!(null);await next;Check(probe.Active==0&&probe.Detached==2,"both subscription lifetimes settle");
    }
    [SimTest] public static async Task R090_ForeignCompletionCannotMutateSubscriptionState(TestContext c)
    {
        var probe=new ManualCompletion();var waiting=probe.AsTask();Exception? failure=null;var thread=new Thread(()=>{try{probe.Completion!(null);}catch(Exception error){failure=error;}});thread.Start();thread.Join();
        Check(failure is InvalidOperationException&&!waiting.IsCompleted&&probe.Active==1,"foreign callback rejected before mutation");probe.Completion!(null);await waiting;Check(probe.Active==0&&probe.Detached==1,"owner callback still completes");
    }
    static Exception? CaptureFailure(Action action){try{action();return null;}catch(Exception error){return error;}}
    sealed class FailingRegistration:Trigger
    {protected override IDisposable Subscribe(SimRuntime runtime,Action<Exception?> complete)=>throw new FormatException("registration marker");}
    sealed class DoubleCompletion:Trigger
    {
        public int Detached{get;private set;}
        protected override IDisposable Subscribe(SimRuntime runtime,Action<Exception?> complete)
        {complete(null);complete(new FormatException("late duplicate"));return new Detach(()=>Detached++);}
    }
    sealed class ManualCompletion:Trigger
    {
        public Action<Exception?>? Completion{get;private set;}public int Active{get;private set;}public int Detached{get;private set;}
        protected override IDisposable Subscribe(SimRuntime runtime,Action<Exception?> complete)
        {Active++;Completion=complete;return new Detach(()=>{Active--;Detached++;});}
    }
}
