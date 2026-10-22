using SharpTestBench;
using Xunit;
public sealed class ConformanceWidthTests
{
    public readonly struct ChangingWidth:IWidth
    {public static int Calls;public static int Bits=>Interlocked.Increment(ref Calls)<=2?8:0;}
    public readonly struct OversizedFirstWidth:IWidth
    {public static int Calls;public static int Bits=>Interlocked.Increment(ref Calls)==1?1048577:8;}
    [Fact] public void C041_WidthGetterIsSampledOnceAndSharedAcrossValueTypes()
    {Assert.Equal(8,UInt<ChangingWidth>.Bits);Assert.Equal(8,SInt<ChangingWidth>.Bits);Assert.Equal(8,LogicVector<ChangingWidth>.Bits);Assert.Equal(1,ChangingWidth.Calls);Assert.Equal("00000001",new LogicVector<ChangingWidth>(1).ToString());Assert.Equal(1,ChangingWidth.Calls);}
    [Fact] public void C042_IllegalInitialWidthCannotBeRescuedByLaterGetterValues()
    {var failure=Assert.Throws<TypeInitializationException>(()=>{_ = UInt<OversizedFirstWidth>.Bits;});Assert.IsType<ArgumentOutOfRangeException>(failure.InnerException);Assert.Equal(1,OversizedFirstWidth.Calls);}
}
