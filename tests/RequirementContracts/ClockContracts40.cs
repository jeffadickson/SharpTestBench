using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest] public static async Task R031_ManagedClockCanRestartAfterCancellation(TestContext c)
    {
        var signal=Clock(c);await Drive(c,signal,false);var clock=new SimClock(signal,6,implementation:ClockImplementation.Managed);
        var first=c.StartSoon(clock.Run);await RisingEdge(signal);first.Cancel();await first.Complete;
        await Timer(1);ulong start=c.Runtime.Now;var next=c.StartSoon(clock.Run);await RisingEdge(signal);Check(c.Runtime.Now==start+3,"restart resets full low pulse");next.Cancel();await next.Complete;
    }
    [SimTest] public static async Task R032_DuplicateClockRunCannotDisturbOwner(TestContext c)
    {
        var signal=Clock(c);await Drive(c,signal,false);var clock=new SimClock(signal,8,implementation:ClockImplementation.Managed);var owner=c.StartSoon(clock.Run);await Yield();
        await Fails<InvalidOperationException>(()=>clock.Run(CancellationToken.None));Check(!owner.IsCompleted,"duplicate run preserves clock owner");await RisingEdge(signal);owner.Cancel();await owner.Complete;
    }
    [SimTest] public static async Task R033_LowPulseUpdateWaitsForFullCycle(TestContext c)
    {
        var signal=Clock(c);await Drive(c,signal,false);var clock=new SimClock(signal,10,4,ClockImplementation.Managed);ulong start=c.Runtime.Now;var owner=c.StartSoon(clock.Run);
        await Timer(2);clock.SetPeriod(6,2);await RisingEdge(signal);Check(c.Runtime.Now==start+6,"current low unchanged");await FallingEdge(signal);Check(c.Runtime.Now==start+10,"current high unchanged");await RisingEdge(signal);Check(c.Runtime.Now==start+14,"new low applies next cycle");owner.Cancel();await owner.Complete;
    }
    [SimTest] public static async Task R034_HighPulseUpdateUsesLatestNextCycleTiming(TestContext c)
    {
        var signal=Clock(c);await Drive(c,signal,false);var clock=new SimClock(signal,10,4,ClockImplementation.Managed);ulong start=c.Runtime.Now;var owner=c.StartSoon(clock.Run);
        await RisingEdge(signal);clock.SetPeriod(8,3);clock.SetPeriod(6,1);await FallingEdge(signal);Check(c.Runtime.Now==start+10,"high pulse retained");await RisingEdge(signal);Check(c.Runtime.Now==start+15,"latest pending low wins");await FallingEdge(signal);Check(c.Runtime.Now==start+16,"latest pending high wins");owner.Cancel();await owner.Complete;
    }
    [SimTest] public static async Task R035_InvalidClockUpdatePreservesActiveTiming(TestContext c)
    {
        var signal=Clock(c);await Drive(c,signal,false);var clock=new SimClock(signal,8,3,ClockImplementation.Managed);var owner=c.StartSoon(clock.Run);await RisingEdge(signal);ulong start=c.Runtime.Now;
        Throws<ArgumentOutOfRangeException>(()=>clock.SetPeriod(1));Throws<ArgumentOutOfRangeException>(()=>clock.SetPeriod(8,8));
        await FallingEdge(signal);Check(c.Runtime.Now==start+3,"invalid update cannot change high");await RisingEdge(signal);Check(c.Runtime.Now==start+8,"invalid update cannot change low");owner.Cancel();await owner.Complete;
    }
    [SimTest] public static async Task R036_PreCancelledClockCannotWriteSignal(TestContext c)
    {
        var signal=Clock(c);await Drive(c,signal,true);var clock=new SimClock(signal,4,implementation:ClockImplementation.Managed);
        await Fails<OperationCanceledException>(()=>clock.Run(new CancellationToken(true)));await Timer(1);Check(signal.ReadBit(),"pre-cancelled run left signal high");
    }
    [SimTest] public static async Task R037_ReadOnlyClockStartCannotRegisterOrWrite(TestContext c)
    {
        var signal=Clock(c);await Drive(c,signal,true);var clock=new SimClock(signal,4,implementation:ClockImplementation.Managed);await ReadOnly();int before=c.Runtime.PendingCallbacks;
        await Fails<InvalidOperationException>(()=>clock.Run(CancellationToken.None));Check(signal.ReadBit()&&c.Runtime.PendingCallbacks==before,"ReadOnly clock start noninterference");
    }
    [SimTest] public static async Task R038_ClockUpdateRejectsForeignThread(TestContext c)
    {
        var signal=Clock(c);await Drive(c,signal,false);var clock=new SimClock(signal,8,implementation:ClockImplementation.Managed);Exception? failure=null;
        var thread=new Thread(()=>{try{clock.SetPeriod(2);}catch(Exception error){failure=error;}});thread.Start();thread.Join();Check(failure is InvalidOperationException,"clock update thread guard");
        ulong start=c.Runtime.Now;var owner=c.StartSoon(clock.Run);await RisingEdge(signal);Check(c.Runtime.Now==start+4,"foreign update left timing unchanged");owner.Cancel();await owner.Complete;
    }
    [SimTest] public static async Task R039_CancelBeforeFirstClockEdgePreventsLateToggle(TestContext c)
    {
        var signal=Clock(c);await Drive(c,signal,false);var clock=new SimClock(signal,20,implementation:ClockImplementation.Managed);var owner=c.StartSoon(clock.Run);
        await Timer(2);owner.Cancel();await owner.Complete;
        int before=c.Runtime.PendingCallbacks;
        await Fails<TimeoutException>(()=>WithTimeout(ValueChange(signal),25).AsTask());
        Check(!signal.ReadBit()&&c.Runtime.PendingCallbacks==before,"no late clock transition or leaked observer after cancellation");
    }
    [SimTest] public static async Task R040_MinimumClockPeriodCountsAllEdges(TestContext c)
    {
        var signal=Clock(c);await Drive(c,signal,false);var clock=new SimClock(signal,2,implementation:ClockImplementation.Managed);ulong start=c.Runtime.Now;var owner=c.StartSoon(clock.Run);
        await ClockCycles(signal,20);Check(c.Runtime.Now==start+39,"period2 has no lost rising edge");owner.Cancel();await owner.Complete;
    }
}
