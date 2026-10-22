using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest] public static async Task R076_InvalidLogicTextCannotReplacePendingWrite(TestContext c)
    {
        var data=c.Dut.Child("data").Signal();data.WriteBits("01010101");Throws<FormatException>(()=>data.WriteBits("0000000Q"));await ReadOnly();Check(data.ReadBits()=="01010101","invalid logic leaves pending valid write");
    }
    [SimTest] public static async Task R077_ReadOnlyRejectsImmediateAndDeferredWrites(TestContext c)
    {
        var data=c.Dut.Child("data").Signal();data.WriteBits("10011001");await ReadOnly();int before=c.Runtime.PendingCallbacks;
        Throws<InvalidOperationException>(()=>data.WriteBits("00000000"));Throws<InvalidOperationException>(()=>data.WriteBits("11111111",WriteAction.Immediate));Check(data.ReadBits()=="10011001"&&before==c.Runtime.PendingCallbacks,"ReadOnly mutation/registration refusal");
    }
    [SimTest] public static async Task R078_DeferredWritesCoalesceAndReachCombinationalLogic(TestContext c)
    {
        var data=c.Dut.Child("data").Signal();data.WriteBits("00000001");data.WriteBits("00111100");data.WriteBits("10100101");await ReadOnly();
        Check(data.ReadBits()=="10100101"&&c.Dut.Child("mirrored").Signal().ReadBits()=="10100101","last deposit wins and HDL combinational result settles");
    }
    [SimTest] public static async Task R079_ImmediateWriteIsVisibleBeforeAwait(TestContext c)
    {
        var data=c.Dut.Child("data").Signal();ulong time=c.Runtime.Now;data.WriteBits("01101001",WriteAction.Immediate);Check(data.ReadBits()=="01101001"&&c.Runtime.Now==time,"immediate write visible without scheduler advance");await Timer(1);
    }
    [SimTest] public static async Task R080_InvalidWriteActionPreservesQueuedDeposit(TestContext c)
    {
        var data=c.Dut.Child("data").Signal();data.WriteBits("10101010");Throws<ArgumentOutOfRangeException>(()=>data.WriteBits("11110000",(WriteAction)99));await ReadOnly();Check(data.ReadBits()=="10101010","invalid action cannot replace queued deposit");
    }
}
