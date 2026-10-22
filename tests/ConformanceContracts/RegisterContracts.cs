using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    static async Task BankDrive(TestContext c,int address,uint value,int enables,bool write=true)
    {Bit(c,"rb_write").WriteBit(write);Bit(c,"rb_addr").WriteBits(Convert.ToString(address,2).PadLeft(3,'0'));c.Dut.Child("rb_be").Signal<W4>().Write(new UInt<W4>(enables));Word(c,"rb_wdata").Write(new UInt<W32>(value));await Timer(1);}
    static async Task<bool> BankWrite(TestContext c,int address,uint value,int enables=15)
    {await BankDrive(c,address,value,enables);bool error=Bit(c,"rb_error").ReadBit();await Tick(c);Bit(c,"rb_write").WriteBit(false);await Timer(1);return error;}
    static async Task<uint> BankRead(TestContext c,int address)
    {Bit(c,"rb_write").WriteBit(false);Bit(c,"rb_addr").WriteBits(Convert.ToString(address,2).PadLeft(3,'0'));await Timer(1);return (uint)Word(c,"rb_rdata").ReadUInt().Value;}
    static uint Stored(TestContext c,int address)=>(uint)Word(c,"rb_word"+address).ReadUInt().Value;
    [SimTest(TimeoutSteps=10000000)] public static async Task R016_RegisterResetClearsEveryWritableWord(TestContext c)
    {await Reset(c);Check(Stored(c,0)==0&&Stored(c,1)==0&&Stored(c,2)==0,"reset bank storage");Check(await BankRead(c,3)==0xC0DE2026&&!Bit(c,"rb_error").ReadBit(),"reset preserves device identifier");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R017_RegisterFullWriteMatchesIndependentBitPattern(TestContext c)
    {await Reset(c);Check(!await BankWrite(c,0,0xC396A55A),"valid full write");Check(Stored(c,0)==0xC396A55A&&Word(c,"rb_word0").ReadBits()=="11000011100101101010010101011010","full write actual native bit order");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R018_RegisterEachByteEnablePreservesOtherLanes(TestContext c)
    {await Reset(c);for(int lane=0;lane<4;lane++){await BankWrite(c,0,0x96A55A3C);await BankWrite(c,0,0x74737271,1<<lane);uint mask=255U<<(lane*8),expected=(0x96A55A3CU&~mask)|(0x74737271U&mask);Check(Stored(c,0)==expected,"individual lane mask "+lane);}}
    [SimTest(TimeoutSteps=10000000)] public static async Task R019_RegisterZeroByteEnableCannotMutateStorage(TestContext c)
    {await Reset(c);await BankWrite(c,1,0x12345678);await BankWrite(c,1,0xFFFFFFFF,0);await BankWrite(c,1,0,0);Check(Stored(c,1)==0x12345678,"zero strobes are a no-op");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R020_RegisterSparseLaneWritesComposeWithoutLostBytes(TestContext c)
    {await Reset(c);await BankWrite(c,0,0x9ABCDEF0);await BankWrite(c,0,0x12345678,5);Check(Stored(c,0)==0x9A34DE78,"first sparse update");await BankWrite(c,0,0x13579BDF,10);Check(Stored(c,0)==0x13349B78,"complementary sparse update");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R021_RegisterUnmappedAddressesCannotAliasWritableWords(TestContext c)
    {await Reset(c);for(int i=0;i<3;i++)await BankWrite(c,i,(uint)(i+1));for(int address=4;address<8;address++){Check(await BankRead(c,address)==0&&Bit(c,"rb_error").ReadBit(),"unmapped read");Check(await BankWrite(c,address,0xBAD0BAD0),"unmapped write error");}Check(Stored(c,0)==1&&Stored(c,1)==2&&Stored(c,2)==3,"unmapped writes did not alias storage");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R022_RegisterReadonlyIdentifierRejectsWritesInHardware(TestContext c)
    {await Reset(c);Check(await BankWrite(c,3,0xFFFFFFFF),"readonly write error");Check(await BankRead(c,3)==0xC0DE2026&&!Bit(c,"rb_error").ReadBit(),"readonly identifier is stable and remains readable");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R023_RegisterReadMuxTracksAddressWithoutClockEdge(TestContext c)
    {await Reset(c);await BankWrite(c,0,0x11335577);await BankWrite(c,1,0xEECCAA88);Check(await BankRead(c,0)==0x11335577&&await BankRead(c,1)==0xEECCAA88&&await BankRead(c,0)==0x11335577,"combinational read address switching");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R024_RegisterTargetWritePreservesBothNeighbourWords(TestContext c)
    {await Reset(c);await BankWrite(c,0,0xABCD1234);await BankWrite(c,2,0x1234ABCD);await BankWrite(c,1,0x98765432);Check(Stored(c,0)==0xABCD1234&&Stored(c,1)==0x98765432&&Stored(c,2)==0x1234ABCD,"decoded write isolation");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R025_RegisterBackToBackAddressWritesNeedNoStrobeBubble(TestContext c)
    {await Reset(c);await BankDrive(c,0,11,15);await Tick(c);await BankDrive(c,1,22,15);Check(Bit(c,"rb_write").ReadBit(),"write strobe stays asserted");await Tick(c);await BankDrive(c,2,33,15);await Tick(c);Check(Stored(c,0)==11&&Stored(c,1)==22&&Stored(c,2)==33,"consecutive address writes");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R026_RegisterLevelWriteStrobeSamplesEachNewCycleValue(TestContext c)
    {await Reset(c);for(uint value=1;value<=5;value++){await BankDrive(c,1,value*0x10203,15);await Tick(c);Check(Stored(c,1)==value*0x10203,"level strobe must not be edge-detected");}}
    [SimTest(TimeoutSteps=10000000)] public static async Task R027_RegisterReadActivityCannotWriteOnClockEdges(TestContext c)
    {await Reset(c);await BankWrite(c,2,0xCAFEBABE);for(int i=0;i<6;i++){await BankDrive(c,i%3,(uint)i,15,write:false);await Tick(c);}Check(Stored(c,0)==0&&Stored(c,1)==0&&Stored(c,2)==0xCAFEBABE,"read-only cycles cannot store bus data");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R028_RegisterResetDominatesAConcurrentWrite(TestContext c)
    {await Reset(c);await BankWrite(c,0,17);await BankWrite(c,1,18);await BankDrive(c,0,99,15);Bit(c,"rst").WriteBit(true);await Tick(c);Check(Stored(c,0)==0&&Stored(c,1)==0,"reset dominates active write");Bit(c,"rst").WriteBit(false);Bit(c,"rb_write").WriteBit(false);await Tick(c);Check(Stored(c,0)==0,"reset write does not replay after reset");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R029_RegisterNativeByteAliasesEstablishEndianCoordinates(TestContext c)
    {await Reset(c);await BankWrite(c,0,0xD2698E31);await BankRead(c,0);Check(Byte(c,"rb_low").ReadBits()=="00110001"&&Byte(c,"rb_high").ReadBits()=="11010010","independent asymmetric byte aliases");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R030_RegisterWriteCannotCommitBeforeARisingClock(TestContext c)
    {await Reset(c);await BankWrite(c,0,7);Bit(c,"clk").WriteBit(false);await Timer(1);await BankDrive(c,0,99,15);await Timer(20);Check(Stored(c,0)==7,"no asynchronous write-through");await Tick(c);Check(Stored(c,0)==99,"pending bus value commits at edge");}
}
