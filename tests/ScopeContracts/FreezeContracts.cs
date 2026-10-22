using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest(Simulators=["Icarus Verilog"])] public static Task R001_FreezeScalarHoldsUntilRelease(TestContext c)=>Held(c,"f_bit","1","0");
    [SimTest(Simulators=["Icarus Verilog"])] public static Task R002_FreezeBytePreservesAllBits(TestContext c)=>Held(c,"f_byte","10100101","01011010");
    [SimTest(Simulators=["Icarus Verilog"])] public static Task R003_FreezeWideDoesNotTruncateHighWord(TestContext c)=>Held(c,"f_wide","1"+new string('0',94)+"1",new string('0',96));
    [SimTest(Simulators=["Icarus Verilog"])] public static Task R004_FreezeSignedIntegerUsesFullBinaryPattern(TestContext c)=>Held(c,"f_integer","10000000000000000000000000000001","01111111111111111111111111111110");
    [SimTest(Simulators=["Icarus Verilog"])] public static Task R005_FreezeEnumPreservesNativeEncodedValue(TestContext c)=>Held(c,"f_enum","011","111");
    [SimTest(Simulators=["Icarus Verilog"])] public static async Task R006_FreezeRealRetainsFractionAndSign(TestContext c)
    {var obj=c.Dut.Child("f_real");var real=obj.Real();real.Write(-6.125,WriteAction.Immediate);try{obj.Freeze();real.Write(7.5);await Timer(1);Check(real.Read()==-6.125,"real committed hold");}finally{real.Write(0,WriteAction.Release);}real.Write(7.5);await Timer(1);Check(real.Read()==7.5,"real release");}
    [SimTest(Simulators=["Icarus Verilog"])] public static async Task R007_FreezeCapturesCommittedValueBeforeQueuedDeposit(TestContext c)
    {Put(c,"f_byte",7);var obj=c.Dut.Child("f_byte");var s=obj.Signal();s.WriteBits("11001000");int callbacks=c.Runtime.PendingCallbacks;try{obj.Freeze();Check(s.ReadBits()=="00000111"&&c.Runtime.PendingCallbacks==callbacks,"capture committed not preview, retain queued callback");await Timer(1);Check(N(c,"f_byte")==7,"queued deposit cannot defeat force");}finally{s.Release();}}
    [SimTest(Simulators=["Icarus Verilog"])] public static async Task R008_RepeatedFreezeRetainsExistingForcedValue(TestContext c)
    {Put(c,"f_byte",8);var s=S(c,"f_byte");try{s.Freeze();s.WriteBits("11111111");s.Freeze();await Timer(1);Check(N(c,"f_byte")==8,"second capture keeps forced committed value");}finally{s.Release();}}
    [SimTest(Simulators=["Icarus Verilog"])] public static Task R009_FreezePreservesUnknownLogic(TestContext c)=>Held(c,"f_bit","X","0");
    [SimTest(Simulators=["Icarus Verilog"])] public static Task R010_FreezePreservesHighImpedance(TestContext c)=>Held(c,"f_byte","ZZZZZZZZ","00000000");
    [SimTest(Simulators=["Icarus Verilog"])] public static async Task R011_ReleaseRestoresContinuousDriver(TestContext c)
    {Put(c,"f_driver",11);await Timer(1);var s=S(c,"f_driven");try{s.Freeze();Put(c,"f_driver",22);await Timer(1);Check(N(c,"f_driven")==11,"wire force beats active continuous driver");}finally{s.Release();}await Timer(1);Check(N(c,"f_driven")==22,"release resumes continuous driver");}
    [SimTest] public static async Task R012_ReadOnlyFreezeRejectsWithoutCallbacksOrMutation(TestContext c)
    {Put(c,"f_byte",12);await ReadOnly();int before=c.Runtime.PendingCallbacks;Throws<InvalidOperationException>(()=>c.Dut.Child("f_byte").Freeze());Check(N(c,"f_byte")==12&&c.Runtime.PendingCallbacks==before,"readonly freeze guard");}
    [SimTest] public static Task R013_ForeignThreadFreezeCannotReachNativeAccess(TestContext c)
    {var obj=c.Dut.Child("f_byte");Put(c,"f_byte",13);Exception? failure=null;var thread=new Thread(()=>{try{obj.Freeze();}catch(Exception error){failure=error;}});thread.Start();thread.Join();Check(failure is InvalidOperationException&&N(c,"f_byte")==13,"thread refusal and unchanged native data");return Task.CompletedTask;}
    [SimTest] public static Task R014_ModuleFreezeRejectsBeforeIllegalValueRead(TestContext c)
    {int before=c.Runtime.PendingCallbacks;Throws<NotSupportedException>(()=>c.Dut.Freeze());Check(c.Runtime.PendingCallbacks==before,"module kind refusal adds no callback");return Task.CompletedTask;}
    [SimTest] public static Task R015_UnpackedMemoryFreezeIsExplicitlyUnsupported(TestContext c)
    {var obj=c.Dut.Child("f_memory");Check(obj.Kind is ObjectKind.Memory or ObjectKind.Array,"unpacked fixture kind");Throws<NotSupportedException>(()=>obj.Freeze());return Task.CompletedTask;}
    [SimTest(Simulators=["Verilator"])] public static Task R016_StringFreezeIsExplicitlyUnsupported(TestContext c)
    {var obj=c.Dut.Child("f_text");var before=obj.String().Read();var refusal=Throws<NotSupportedException>(()=>obj.Freeze());Check(refusal.Message.Contains("object kind",StringComparison.Ordinal),"string kind guard precedes capability refusal");Check(obj.String().Read()==before,"string refusal preserves payload");return Task.CompletedTask;}
    [SimTest] public static Task R017_ConstantFreezeRejectsWithoutChangingConstant(TestContext c)
    {var obj=c.Dut.Child("F_CONSTANT");Check(obj.IsConstant,"constant metadata");Throws<InvalidOperationException>(()=>obj.Freeze());Check(N(c,"F_CONSTANT")==60,"constant native readback");return Task.CompletedTask;}
    [SimTest(Simulators=["Verilator"])] public static async Task R018_UnsupportedForceBackendPreservesQueuedDeposit(TestContext c)
    {Put(c,"f_byte",18);var s=S(c,"f_byte");s.WriteBits("00100100");int before=c.Runtime.PendingCallbacks;var refusal=Throws<NotSupportedException>(()=>s.Freeze());Check(refusal.Message.Contains("ForceRelease",StringComparison.Ordinal),"capability refusal source");Check(N(c,"f_byte")==18&&c.Runtime.PendingCallbacks==before,"unsupported backend preflight before write/callback");await Timer(1);Check(N(c,"f_byte")==36,"pending deposit survives refusal");}
    [SimTest(Simulators=["Verilator"])] public static async Task R019_UnsupportedKindCannotRetractQueuedStringWrite(TestContext c)
    {var obj=c.Dut.Child("f_text");obj.String().Write("pending19");int before=c.Runtime.PendingCallbacks;var refusal=Throws<NotSupportedException>(()=>obj.Freeze());Check(refusal.Message.Contains("object kind",StringComparison.Ordinal),"string preflight refusal source");Check(c.Runtime.PendingCallbacks==before,"kind refusal preserves callback");await Timer(1);Check(obj.String().Read()=="pending19","kind refusal preserves scheduled string");}
    [SimTest] public static async Task R020_ReadOnlyGuardPrecedesUnsupportedContainerKind(TestContext c)
    {await ReadOnly();var error=Throws<InvalidOperationException>(()=>c.Dut.Freeze());Check(error.Message.Contains("ReadOnly",StringComparison.Ordinal),"phase guard precedes object value access");}
    [SimTest(Simulators=["Icarus Verilog"])] public static Task R021_FreezeWideMixedFourStatePattern(TestContext c)=>Held(c,"f_wide",string.Concat(Enumerable.Repeat("10XZ",24)),new string('1',96));
    [SimTest(Simulators=["Icarus Verilog"])] public static async Task R022_ReleaseThenRecaptureUsesNewCommittedValue(TestContext c)
    {var s=S(c,"f_byte");Put(c,"f_byte",22);try{s.Freeze();Check(N(c,"f_byte")==22,"first force");}finally{s.Release();}Put(c,"f_byte",44);try{s.Freeze();s.WriteBits("00000000");await Timer(1);Check(N(c,"f_byte")==44,"recapture after release");}finally{s.Release();}}
    [SimTest(Simulators=["Icarus Verilog"])] public static async Task R023_CoalescedDepositsDoNotAlterCapturedCommit(TestContext c)
    {Put(c,"f_byte",23);var s=S(c,"f_byte");s.WriteBits("00000001");s.WriteBits("00000010");try{s.Freeze();await Timer(1);Check(N(c,"f_byte")==23,"coalesced preview not committed capture");}finally{s.Release();}s.WriteBits("00000011");await Timer(1);Check(N(c,"f_byte")==3,"write scheduler remains usable");}
    [SimTest(Simulators=["Icarus Verilog"])] public static async Task R024_ReadWritePhaseFreezeCapturesCommittedValue(TestContext c)
    {var s=S(c,"f_byte");Put(c,"f_byte",24);await ReadWrite();Check(c.Runtime.Phase==SimPhase.ReadWrite,"readwrite test phase");try{s.Freeze();s.WriteBits("00011001",WriteAction.Immediate);Check(N(c,"f_byte")==24,"readwrite freeze holds explicitly committed value");}finally{s.Release();}await Timer(1);s.WriteBits("00011001");await Timer(1);Check(N(c,"f_byte")==25,"phase test release restores writes");}
    [SimTest] public static async Task R025_ConstantRefusalPreservesUnrelatedPendingWrite(TestContext c)
    {S(c,"f_byte").WriteBits("00011001");int before=c.Runtime.PendingCallbacks;Throws<InvalidOperationException>(()=>c.Dut.Child("F_CONSTANT").Freeze());Check(c.Runtime.PendingCallbacks==before,"constant refusal cannot steal scheduled write");await Timer(1);Check(N(c,"f_byte")==25,"unrelated pending write retained");}
}
