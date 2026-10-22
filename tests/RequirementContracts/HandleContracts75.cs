using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    public readonly struct Word8:IWidth { public static int Bits=>8; }
    [SimTest] public static Task R071_NamedHandleIdentitySurvivesEnumeration(TestContext c)
    {
        var first=c.Dut.Child("data");var children=c.Dut.Children();Check(ReferenceEquals(first,c.Dut.Child("data"))&&children.Any(child=>ReferenceEquals(child,first)),"named handle cache/enumeration identity");return Task.CompletedTask;
    }
    [SimTest] public static Task R072_FailedArrayIndexCannotPoisonIndexedCache(TestContext c)
    {
        var memory=c.Dut.Child("memory");var first=memory.At(0);Throws<ArgumentException>(()=>memory.At(4));Check(ReferenceEquals(first,memory.At(0))&&!ReferenceEquals(first,memory.At(1)),"indexed cache survives missing handle");return Task.CompletedTask;
    }
    [SimTest] public static Task R073_MissingNamedChildDoesNotAffectValidLookup(TestContext c)
    {
        var first=c.Dut.Child("data");Throws<KeyNotFoundException>(()=>c.Dut.Child("__missing_contract_member__"));Check(ReferenceEquals(first,c.Dut.Child("data")),"failed named lookup preserves valid cache");return Task.CompletedTask;
    }
    [SimTest] public static async Task R074_ConstantWriteCannotReachNativeMutation(TestContext c)
    {
        var constant=c.Dut.Child("CONSTANT").Signal<Word8>();Check(constant.IsConstant,"constant metadata");var before=constant.ReadUInt();
        Throws<InvalidOperationException>(()=>constant.Write(new UInt<Word8>(0),WriteAction.Immediate));await Timer(1);Check(constant.ReadUInt().Value==before.Value,"constant guard preserves native value");
    }
    [SimTest] public static async Task R075_ScalarWriteRejectsVectorWithoutMutation(TestContext c)
    {
        var data=c.Dut.Child("data").Signal();data.WriteBits("00110101");await Timer(1);Throws<InvalidOperationException>(()=>data.WriteBit(false));await Timer(1);Check(data.ReadBits()=="00110101","wide signal scalar guard");
    }
}
