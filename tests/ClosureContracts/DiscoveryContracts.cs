using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest] public static Task R061_HierarchyEnumerationFindsActualFifoInstance(TestContext c)
    {var child=c.Dut.Child("fifo");Check(c.Dut.Kind==ObjectKind.Module&&c.Dut.Children().Any(x=>x.Name=="fifo")&&child.Children().Any(x=>x.Name=="count"),"hierarchy includes FIFO instance and output port");return Task.CompletedTask;}
    [SimTest] public static Task R062_RootDiscoveryObservesCurrentTop(TestContext c)
    {var roots=HdlObject.Roots();Check(roots.Status==DiscoveryStatus.Observed&&roots.Objects.Any(x=>x.FullName==c.Dut.FullName),"read-only root discovery includes current top");return Task.CompletedTask;}
    [SimTest] public static Task R063_PackedDiscoveryReportsCheckedWidthAndProvenance(TestContext c)
    {var layout=c.Dut.Child("packed_word").DiscoverPackedLayout();Check(layout.Available&&layout.Layout!.TotalBits==32&&layout.Source is PackedLayoutSource.GpiBitRange or PackedLayoutSource.NativePackedRanges&&!string.IsNullOrWhiteSpace(layout.Explanation),"observable packed layout width and provenance");return Task.CompletedTask;}
    [SimTest] public static Task R064_ScalarLayoutDoesNotInventVectorDimensions(TestContext c)
    {var layout=c.Dut.Child("clk").DiscoverPackedLayout();Check(!layout.Available&&layout.Source==PackedLayoutSource.Unavailable&&!string.IsNullOrWhiteSpace(layout.Explanation),"scalar cannot establish a packed vector layout");return Task.CompletedTask;}
    [SimTest] public static Task R065_ForeignDiscoveryCannotReachNativeIterators(TestContext c)
    {var signal=c.Dut.Child("packed_word");Exception? drivers=null,loads=null;var thread=new Thread(()=>{try{signal.Drivers();}catch(Exception e){drivers=e;}try{signal.Loads();}catch(Exception e){loads=e;}});thread.Start();thread.Join();Check(drivers is InvalidOperationException&&loads is InvalidOperationException&&signal.Signal().Width==32,"driver/load discovery thread guard and subsequent owner read");return Task.CompletedTask;}
    [SimTest] public static async Task R066_WidthMismatchCannotDisturbPendingNativeWrite(TestContext c)
    {var obj=c.Dut.Child("packed_word");obj.Signal<WordWidth>().Write(new UInt<WordWidth>(0xCAFEBABE));int before=c.Runtime.PendingCallbacks;Throws<ArgumentException>(()=>obj.Signal<ByteWidth>());Check(before==c.Runtime.PendingCallbacks,"wrong-width conversion added no callback");await ReadOnly();Check(obj.Signal<WordWidth>().ReadUInt().Value==0xCAFEBABE,"width validation preserves pending value");}
    [SimTest] public static async Task R067_ConstantSubfieldWriteCannotMutateParameter(TestContext c)
    {var constant=c.Dut.Child("CONSTANT").Signal<ByteWidth>();Throws<InvalidOperationException>(()=>constant.Field<NibbleWidth>(0).Write(new UInt<NibbleWidth>(0)));await Timer(1);Check(constant.ReadUInt().Value==37,"constant subfield preserves parameter");}
    [SimTest] public static Task R068_FailedArrayConverterDoesNotPoisonCachedElement(TestContext c)
    {int calls=0;bool fail=true;var memory=c.Dut.Child("memory").Array(x=>{calls++;if(fail)throw new FormatException("converter");return x.Signal<ByteWidth>();});Throws<FormatException>(()=>{_ = memory[1];});fail=false;var valid=memory[1];Check(ReferenceEquals(valid,memory[1])&&calls==2,"failed conversion retried once and success cached");return Task.CompletedTask;}
    [SimTest] public static Task R069_ForeignArrayIndexCannotInvokeCallerConverter(TestContext c)
    {int calls=0;var memory=c.Dut.Child("memory").Array(x=>{calls++;return x.Signal<ByteWidth>();});Exception? error=null;var thread=new Thread(()=>{try{_ = memory[0];}catch(Exception e){error=e;}});thread.Start();thread.Join();Check(error is InvalidOperationException&&calls==0,"foreign index refused before user conversion");_ = memory[0];Check(calls==1,"valid owner conversion still available");return Task.CompletedTask;}
    [SimTest] public static async Task R070_NullLogicTextCannotReplaceQueuedDeposit(TestContext c)
    {var word=c.Dut.Child("packed_word").Signal<WordWidth>();word.Write(new UInt<WordWidth>(0xAABBCCDD));int before=c.Runtime.PendingCallbacks;var error=Throws<ArgumentNullException>(()=>word.WriteBits(null!));Check(error.ParamName=="value"&&before==c.Runtime.PendingCallbacks,"null logic input validation");await ReadOnly();Check(word.ReadUInt().Value==0xAABBCCDD,"null text preserves queued valid write");}
}
