using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    static byte AccValue(TestContext c)=>(byte)Byte(c,"a_value").ReadUInt().Value;
    static bool AccOverflow(TestContext c)=>Bit(c,"a_overflow").ReadBit();
    static async Task AccStep(TestContext c,int delta,bool enable=true,bool clear=false,bool load=false,byte value=0)
    {if(delta is <-128 or >127)throw new ArgumentOutOfRangeException(nameof(delta));Bit(c,"a_enable").WriteBit(enable);Bit(c,"a_clear").WriteBit(clear);Bit(c,"a_load").WriteBit(load);Byte(c,"a_delta").Write(new UInt<W8>(unchecked((byte)delta)));Byte(c,"a_load_value").Write(new UInt<W8>(value));await Tick(c);}
    static Task AccLoad(TestContext c,byte value)=>AccStep(c,0,enable:false,load:true,value:value);
    [SimTest(TimeoutSteps=10000000)] public static async Task R031_AccumulatorAddsPositiveStepWithoutPrematureSaturation(TestContext c)
    {await Reset(c);await AccLoad(c,100);await AccStep(c,17);Check(AccValue(c)==117&&!AccOverflow(c),"in-range positive arithmetic");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R032_AccumulatorInterpretsNegativeStepAsSigned(TestContext c)
    {await Reset(c);await AccLoad(c,100);await AccStep(c,-43);Check(Byte(c,"a_delta").ReadSInt().Value==-43&&AccValue(c)==57&&!AccOverflow(c),"signed subtraction with independent expected difference");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R033_AccumulatorClampsUpperOverflowInsteadOfWrapping(TestContext c)
    {await Reset(c);await AccLoad(c,250);await AccStep(c,20);Check(AccValue(c)==255&&AccOverflow(c),"upper clipping");await AccStep(c,1);Check(AccValue(c)==255&&AccOverflow(c),"upper endpoint remains saturated");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R034_AccumulatorClampsLowerOverflowInsteadOfWrapping(TestContext c)
    {await Reset(c);await AccLoad(c,3);await AccStep(c,-7);Check(AccValue(c)==0&&AccOverflow(c),"lower clipping");await AccStep(c,-128);Check(AccValue(c)==0&&AccOverflow(c),"minimum negative step remains clamped");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R035_AccumulatorLoadDominatesEnableAndClearsOldOverflow(TestContext c)
    {await Reset(c);await AccLoad(c,250);await AccStep(c,20);await AccStep(c,127,load:true,value:42);Check(AccValue(c)==42&&!AccOverflow(c),"load priority and status clear");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R036_AccumulatorDisablePreservesValueAndStickyStatus(TestContext c)
    {await Reset(c);await AccLoad(c,250);await AccStep(c,20);foreach(int delta in new[]{-128,-1,0,127})await AccStep(c,delta,enable:false);Check(AccValue(c)==255&&AccOverflow(c),"disabled arithmetic cannot alter state");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R037_AccumulatorOverflowStaysStickyAfterReturningInRange(TestContext c)
    {await Reset(c);await AccLoad(c,250);await AccStep(c,20);await AccStep(c,-10);Check(AccValue(c)==245&&AccOverflow(c),"in-range update preserves historical overflow");await AccStep(c,0);Check(AccOverflow(c),"zero step cannot clear sticky status");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R038_AccumulatorExplicitStatusClearDoesNotChangeHeldValue(TestContext c)
    {await Reset(c);await AccLoad(c,250);await AccStep(c,20);await AccStep(c,-1,enable:false,clear:true);Check(AccValue(c)==255&&!AccOverflow(c),"status-only clear");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R039_AccumulatorNewClippingEventDominatesSimultaneousStatusClear(TestContext c)
    {await Reset(c);await AccLoad(c,255);await AccStep(c,1,clear:true);Check(AccValue(c)==255&&AccOverflow(c),"new overflow is not lost to simultaneous clear");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R040_AccumulatorSeededControlsMatchIndependentIntegerModel(TestContext c)
    {await Reset(c);int expected=0;bool overflow=false;for(int i=0;i<300;i++){if(i%73==0){await Reset(c);expected=0;overflow=false;}else{int delta=c.Random.Next(256)-128;bool enable=c.Random.Next(2)==1,clear=c.Random.Next(7)==0,load=c.Random.Next(8)==0;byte value=(byte)c.Random.Next(256);if(load){expected=value;overflow=false;}else{if(clear)overflow=false;if(enable){int sum=expected+delta;if(sum is <0 or >255)overflow=true;expected=Math.Clamp(sum,0,255);}}await AccStep(c,delta,enable,clear,load,value);}Check(AccValue(c)==expected&&AccOverflow(c)==overflow,"independent integer control/priority model at step "+i);}}
}
