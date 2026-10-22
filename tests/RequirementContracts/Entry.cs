using SharpTestBench;
using System.Runtime.InteropServices;
using static SharpTestBench.Triggers;

public static partial class Entry
{
    [UnmanagedCallersOnly] public static int Initialize(nint api,int size)=>Regression.Initialize(typeof(Entry).Assembly);
    static void Check(bool value,string message){if(!value)throw new InvalidDataException("REQUIREMENT_CONTRACT: "+message);}
    static T Throws<T>(Action action) where T:Exception
    {try{action();}catch(T error){return error;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    static async Task<T> Fails<T>(Func<Task> action) where T:Exception
    {try{await action();}catch(T error){return error;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    static Signal Clock(TestContext c)=>c.Dut.Child("clk").Signal();
    static async Task Drive(TestContext c,Signal clock,bool initial)
    {
        clock.WriteBit(initial);await Timer(1);
    }
    [SimTest] public static async Task R001_NullClockIsAnArgumentFailure(TestContext c)
    {var error=await Fails<ArgumentNullException>(()=>ClockCycles(null!,0,CancellationToken.None));Check(error.ParamName=="clock","null clock parameter");}
    [SimTest] public static async Task R002_InvalidEdgeCannotRegister(TestContext c)
    {int before=c.Runtime.PendingCallbacks;await Fails<ArgumentOutOfRangeException>(()=>ClockCycles(Clock(c),1,null,(ClockEdge)99));Check(before==c.Runtime.PendingCallbacks,"invalid edge registration");}
    [SimTest] public static async Task R003_NegativeCountRejectsBeforeRegistration(TestContext c)
    {int before=c.Runtime.PendingCallbacks;await Fails<ArgumentOutOfRangeException>(()=>ClockCycles(Clock(c),-1));Check(before==c.Runtime.PendingCallbacks,"negative count registration");}
    [SimTest] public static async Task R004_ZeroCountCannotBypassClockWidth(TestContext c)
    {await Fails<ArgumentException>(()=>ClockCycles(c.Dut.Child("data").Signal(),0));}
    [SimTest] public static Task R005_ExplicitTokenCannotBypassSimulatorThread(TestContext c)
    {
        var signal=Clock(c);Exception? failure=null;var thread=new Thread(()=>{try{ClockCycles(signal,0,CancellationToken.None).GetAwaiter().GetResult();}catch(Exception error){failure=error;}});
        thread.Start();thread.Join();Check(failure is InvalidOperationException,"off-thread zero wait");return Task.CompletedTask;
    }
    [SimTest] public static async Task R006_ZeroCountDoesNotAdvanceOrRegister(TestContext c)
    {int before=c.Runtime.PendingCallbacks;ulong time=c.Runtime.Now;await ClockCycles(Clock(c),0);Check(c.Runtime.Now==time&&c.Runtime.PendingCallbacks==before,"zero count owns no callback");}
    [SimTest] public static async Task R007_FallingCyclesExcludeRisingEdges(TestContext c)
    {
        var clock=Clock(c);await Drive(c,clock,true);ulong start=c.Runtime.Now;
        var driver=c.StartSoon(async _=>{await Timer(2);clock.WriteBit(false);await Timer(2);clock.WriteBit(true);await Timer(2);clock.WriteBit(false);});
        await ClockCycles(clock,2,null,ClockEdge.Falling);Check(c.Runtime.Now==start+6,"falling edge count/timing");await driver;
    }
    [SimTest] public static async Task R008_AnyCyclesCountBothTransitions(TestContext c)
    {
        var clock=Clock(c);await Drive(c,clock,false);ulong start=c.Runtime.Now;
        var driver=c.StartSoon(async _=>{await Timer(2);clock.WriteBit(true);await Timer(2);clock.WriteBit(false);await Timer(2);clock.WriteBit(true);});
        await ClockCycles(clock,3,null,ClockEdge.Any);Check(c.Runtime.Now==start+6,"all edge count/timing");await driver;
    }
    [SimTest] public static async Task R009_CancelledCycleWaitDetachesItsCallback(TestContext c)
    {
        int before=c.Runtime.PendingCallbacks;var job=c.StartSoon(token=>ClockCycles(Clock(c),100,token,ClockEdge.Falling));await Yield();
        job.Cancel();await job.Complete;Check(job.Task.IsCanceled,"cancelled wait status");await Timer(1);Check(c.Runtime.PendingCallbacks==before,"cancelled cycle callback leak");
    }
    [SimTest] public static async Task R010_DefaultCyclesRemainRisingOnly(TestContext c)
    {
        var clock=Clock(c);await Drive(c,clock,false);ulong start=c.Runtime.Now;
        var driver=c.StartSoon(async _=>{await Timer(2);clock.WriteBit(true);await Timer(2);clock.WriteBit(false);await Timer(2);clock.WriteBit(true);});
        await ClockCycles(clock,2);Check(c.Runtime.Now==start+6,"default rising compatibility");await driver;
    }
}
