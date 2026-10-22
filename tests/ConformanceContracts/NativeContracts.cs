using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest(TimeoutSteps=10000000)] public static async Task R081_RealQueuedWritesCommitOnlyFinalNativeValue(TestContext c)
    {var real=c.Dut.Child("real_value").Real();real.Write(1.25);real.Write(-7.5);await Timer(1);Check(real.Read()==-7.5,"coalesced real deposit commits last value");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R082_RealImmediateWriteDoesNotEraseQueuedDeposit(TestContext c)
    {var real=c.Dut.Child("real_value").Real();real.Write(3.25);real.Write(-2.5,WriteAction.Immediate);Check(real.Read()==-2.5,"immediate real applies now");await Timer(1);Check(real.Read()==3.25,"older queued deposit retains scheduled application");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R083_ReadOnlyRealWriteFailsWithoutChangingCommittedValue(TestContext c)
    {var real=c.Dut.Child("real_value").Real();real.Write(83.5);await ReadOnly();int before=c.Runtime.PendingCallbacks;Throws<InvalidOperationException>(()=>real.Write(99));Check(real.Read()==83.5&&c.Runtime.PendingCallbacks==before,"readonly real rejection before native write/subscription");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R084_InvalidRealActionPreservesValidQueuedDeposit(TestContext c)
    {var real=c.Dut.Child("real_value").Real();real.Write(-84.25);int before=c.Runtime.PendingCallbacks;Throws<ArgumentOutOfRangeException>(()=>real.Write(7,(WriteAction)123));Check(c.Runtime.PendingCallbacks==before,"invalid action adds no callback");await Timer(1);Check(real.Read()==-84.25,"invalid real action cannot replace pending valid write");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R085_ForeignRealAccessCannotChangePendingNativeValue(TestContext c)
    {var real=c.Dut.Child("real_value").Real();real.Write(85.125);Exception? read=null,write=null;var thread=new Thread(()=>{try{real.Read();}catch(Exception error){read=error;}try{real.Write(-1);}catch(Exception error){write=error;}});thread.Start();thread.Join();Check(read is InvalidOperationException&&write is InvalidOperationException,"foreign real read and write rejected");await Timer(1);Check(real.Read()==85.125,"foreign attempt leaves owning native write intact");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R086_WrongRealConversionCannotLoseQueuedPackedWrite(TestContext c)
    {Word(c,"rb_wdata").Write(new UInt<W32>(0x86C5A23D));Throws<ArgumentException>(()=>c.Dut.Child("rb_wdata").Real());await Timer(1);Check(Word(c,"rb_wdata").ReadUInt().Value==0x86C5A23D,"failed typed view leaves queued packed bits intact");}
    [SimTest(TimeoutSteps=10000000,Simulators=["icarus"])] public static async Task R087_IntegerEndpointsMatchIndependentNativePackedBits(TestContext c)
    {var integer=c.Dut.Child("signed_value").Integer();foreach(int value in new[]{int.MinValue,-1,0,int.MaxValue}){integer.Write(value);await Timer(1);Check(Word(c,"signed_alias").ReadUInt().Value==unchecked((uint)value),"native int32 endpoint bit representation");Check(integer.Read()==value,"selected signed native integer endpoints");}}
    [SimTest(TimeoutSteps=10000000,Simulators=["icarus"])] public static async Task R088_IntegerCoalescingUpdatesAliasWithFinalSignedValue(TestContext c)
    {var integer=c.Dut.Child("signed_value").Integer();integer.Write(17);integer.Write(-88);await Timer(1);Check(integer.Read()==-88&&Word(c,"signed_alias").ReadUInt().Value==unchecked((uint)-88),"integer coalescing agrees with independent packed alias");}
    [SimTest(TimeoutSteps=10000000,Simulators=["icarus"])] public static async Task R089_InvalidIntegerActionCannotReplacePendingAssignment(TestContext c)
    {var integer=c.Dut.Child("signed_value").Integer();integer.Write(-89);int before=c.Runtime.PendingCallbacks;Throws<ArgumentOutOfRangeException>(()=>integer.Write(90,(WriteAction)123));Check(c.Runtime.PendingCallbacks==before,"invalid integer action adds no subscription");await Timer(1);Check(integer.Read()==-89,"valid integer assignment survives invalid action");}
    [SimTest(TimeoutSteps=10000000,Simulators=["icarus"])] public static async Task R090_ReadOnlyIntegerWriteCannotMutateAlias(TestContext c)
    {var integer=c.Dut.Child("signed_value").Integer();integer.Write(-90);await ReadOnly();Throws<InvalidOperationException>(()=>integer.Write(90));Check(integer.Read()==-90&&Word(c,"signed_alias").ReadUInt().Value==unchecked((uint)-90),"readonly rejection preserves native signed payload");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R091_ConstantParameterWriteFailsWithoutReplacingNeighborDeposit(TestContext c)
    {var id=c.Dut.Child("DEVICE_ID");Check(id.IsConstant,"device ID declaration reports constant");Word(c,"rb_wdata").Write(new UInt<W32>(91));int before=c.Runtime.PendingCallbacks;Throws<InvalidOperationException>(()=>id.Signal<W32>().Write(new UInt<W32>(0)));Check(c.Runtime.PendingCallbacks==before,"constant rejection cannot acquire extra callback");await Timer(1);Check(id.Signal<W32>().ReadUInt().Value==0xC0DE2026&&Word(c,"rb_wdata").ReadUInt().Value==91,"constant and unrelated queued write remain correct");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R092_ConstantMetadataDoesNotMakeReadonlyDataMuxConstant(TestContext c)
    {await Reset(c);Check(c.Dut.Child("DEVICE_ID").IsConstant&&!c.Dut.Child("rb_rdata").IsConstant,"native constness follows HDL declaration rather than current read-only bus role");await BankWrite(c,0,92);Check(await BankRead(c,0)==92&&await BankRead(c,3)==0xC0DE2026,"mux observes mutable bank and immutable parameter");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R093_AscendingRangeRetainsDeclaredCoordinatesDuringNativeWrites(TestContext c)
    {var signal=Byte(c,"ascending");var range=signal.Range;Check(range.Left==0&&range.Right==7&&range.Ascending&&range.Length==8,"native ascending declaration");signal.Write(new UInt<W8>(0x92));await Timer(1);Check(signal.ReadBits()=="10010010"&&Bit(c,"ascending_left").ReadBit()&&!Bit(c,"ascending_right").ReadBit(),"text significance and declared endpoints agree");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R094_AscendingPackedBitViewTargetsDeclaredEndpoint(TestContext c)
    {var signal=Byte(c,"ascending");signal.Write(new UInt<W8>(0));await Timer(1);var bits=signal.PackedBits();bits[0].Write(new UInt<W1>(1));await Timer(1);Check(signal.ReadUInt().Value==128&&Bit(c,"ascending_left").ReadBit()&&!Bit(c,"ascending_right").ReadBit(),"packed index zero means declared left endpoint");bits[7].Write(new UInt<W1>(1));await Timer(1);Check(signal.ReadUInt().Value==129&&Bit(c,"ascending_right").ReadBit(),"right endpoint read-modify-write preserves left endpoint");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R095_BankArrayBindsThreeDeclaredWordsWithoutAlias(TestContext c)
    {await Reset(c);var bank=c.Dut.Child("bank").Array(obj=>obj.Signal<W32>());Check(bank.Count==3&&bank.Range.Left==0&&bank.Range.Right==2,"declared memory range");for(int i=0;i<3;i++)await BankWrite(c,i,(uint)(0x9500+i));for(int i=0;i<3;i++)Check(bank[i].ReadUInt().Value==0x9500+i&&ReferenceEquals(bank[i],bank[i]),"cached typed array binding observes correct bank word");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R096_InvalidBankIndexCannotAliasAValidStoredWord(TestContext c)
    {await Reset(c);await BankWrite(c,2,96);var bank=c.Dut.Child("bank").Array(obj=>obj.Signal<W32>());Throws<IndexOutOfRangeException>(()=>{_ = bank[3];});Throws<IndexOutOfRangeException>(()=>{_ = bank[-1];});Check(bank[2].ReadUInt().Value==96&&Stored(c,0)==0,"invalid declared indices do not alias native elements");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R097_RawIndexedAndNamedAliasesObserveSameNativeMemoryCommit(TestContext c)
    {await Reset(c);var root=c.Dut.Child("bank");var indexed=root.At(1);Check(ReferenceEquals(indexed,root.At(1)),"indexed object cache identity");await BankWrite(c,1,0x97A4C812);Check(indexed.Signal<W32>().ReadUInt().Value==Stored(c,1)&&Stored(c,1)==0x97A4C812,"raw index and RTL alias observe identical committed word");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R098_WideScalarMisuseCannotDiscardQueuedBusWrite(TestContext c)
    {var signal=Word(c,"rb_wdata");signal.Write(new UInt<W32>(0x98C7B6A5));int before=c.Runtime.PendingCallbacks;Throws<InvalidOperationException>(()=>signal.ReadBit());Throws<InvalidOperationException>(()=>signal.WriteBit(true));Check(c.Runtime.PendingCallbacks==before,"scalar misuse registers nothing");await Timer(1);Check(signal.ReadUInt().Value==0x98C7B6A5,"wide queued write survives scalar misuse");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R099_WrongWidthNativeBindingCannotChangeExistingWrite(TestContext c)
    {var signal=Byte(c,"p_sdata");signal.Write(new UInt<W8>(0xC9));int before=c.Runtime.PendingCallbacks;Throws<ArgumentException>(()=>c.Dut.Child("p_sdata").Signal<W32>());Check(c.Runtime.PendingCallbacks==before,"wrong-width view allocates no callback");await Timer(1);Check(signal.ReadBits()=="11001001","original native byte survives failed reinterpretation");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R100_SeededRegisterTransactionsMatchIndependentByteStore(TestContext c)
    {
        await Reset(c);byte[][] model=[new byte[4],new byte[4],new byte[4]];
        for(int i=0;i<200;i++){
            if(i%47==0){await Reset(c);foreach(var row in model)Array.Clear(row);}else{
                int address=c.Random.Next(8),enables=c.Random.Next(16);uint value=(uint)c.Random.NextUInt64();bool error=await BankWrite(c,address,value,enables);Check(error==(address>=3),"write error follows mapped write permission");
                if(address<3){byte[] incoming=[(byte)value,(byte)(value>>8),(byte)(value>>16),(byte)(value>>24)];for(int lane=0;lane<4;lane++)if((enables&(1<<lane))!=0)model[address][lane]=incoming[lane];}
            }
            for(int address=0;address<3;address++){uint expected=System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(model[address]);Check(Stored(c,address)==expected&&await BankRead(c,address)==expected,"native alias and read mux match independent byte store");}Check(await BankRead(c,3)==0xC0DE2026,"device ID survives randomized bus traffic");
        }
    }
}
