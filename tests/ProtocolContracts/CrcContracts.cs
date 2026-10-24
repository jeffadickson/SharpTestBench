using SharpTestBench;
using static SharpTestBench.Triggers;
/// <summary>CRC accepted-transaction, packet-boundary, stall and reset contracts.</summary>
public static partial class Entry
{
 [SimTest] public static Task R001_ZeroBytePacket(TestContext c)=>Stream(c,[[0]]);
 [SimTest] public static Task R002_AllOnesBytePacket(TestContext c)=>Stream(c,[[255]]);
 [SimTest] public static async Task R003_PublishedCheckVector(TestContext c){Check(Crc("123456789".Select(ch=>(int)ch).ToArray())==0xf4,"CRC-8 check vector fixes polynomial conventions");await Stream(c,["123456789".Select(ch=>(int)ch).ToArray()]);}
 [SimTest] public static Task R004_MostSignificantBitPacket(TestContext c)=>Stream(c,[[128]]);
 [SimTest] public static Task R005_LeastSignificantBitPacket(TestContext c)=>Stream(c,[[1]]);
 [SimTest] public static Task R006_EachBitAsItsOwnPacket(TestContext c)=>Stream(c,Enumerable.Range(0,8).Select(bit=>new[]{1<<bit}).ToArray());
 [SimTest] public static Task R007_AllByteValuesOnePacket(TestContext c)=>Stream(c,[Enumerable.Range(0,256).ToArray()]);
 [SimTest] public static Task R008_ZeroRunKeepsPolynomialZero(TestContext c)=>Stream(c,[new int[257]]);
 [SimTest] public static Task R009_OnesRunAccumulatesAcrossBytes(TestContext c)=>Stream(c,[Enumerable.Repeat(255,257).ToArray()]);
 [SimTest] public static Task R010_AlternatingBitOrder(TestContext c)=>Stream(c,[Enumerable.Range(0,128).Select(i=>i%2==0?0xaa:0x55).ToArray()]);
 [SimTest] public static Task R011_AdjacentPacketBoundariesResetAccumulator(TestContext c)=>Stream(c,[[0x12,0x34],[0x12,0x34],[0xff],[0]]);
 [SimTest] public static Task R012_SeededPacketLengthsAndPayloads(TestContext c)=>Stream(c,Enumerable.Range(1,17).Select(length=>Bytes(c,length)).ToArray());
 [SimTest] public static async Task R013_ContinuousAcceptedBurst(TestContext c){await Reset(c);var m=new CrcModel();var data=Bytes(c,513);/* No retry loop: a bubble-inserting stage must fail its throughput contract. */for(int i=0;i<data.Length;i++){await m.Step(c,true,data[i],i==data.Length-1,true);Check(m.Accepted==i+1,"continuous input acceptance each offered cycle");Check(m.Delivered==i,"continuous replacement delivers each prior byte");Check(m.Accepted-m.Delivered==1,"one occupied elastic stage during continuous burst");}await m.Flush(c);}
 [SimTest] public static Task R014_SourceGapsDoNotAdvancePacket(TestContext c)=>Stream(c,[Bytes(c,31)],sourcePeriod:3);
 [SimTest] public static Task R015_SinkBackpressureHoldsCompleteWord(TestContext c)=>Stream(c,[Bytes(c,37)],stallPeriod:3);
 [SimTest] public static Task R016_IndependentSourceAndSinkGaps(TestContext c)=>Stream(c,[Bytes(c,13),Bytes(c,19)],sourcePeriod:3,stallPeriod:4);
 [SimTest] public static async Task R017_LastWordIsHeldUnderBackpressure(TestContext c){await Reset(c);var m=new CrcModel();await m.Step(c,true,0xab,true,false);for(int i=0;i<7;i++)await m.Step(c,false,i,false,false);await m.Flush(c);}
 [SimTest] public static Task R018_LengthCrossesEightBitBoundary(TestContext c)=>Stream(c,[Bytes(c,260)],stallPeriod:5);
 [SimTest] public static async Task R019_ResetEmptyStage(TestContext c){await Reset(c);await Reset(c);new CrcModel().Observe(c);}
 [SimTest] public static async Task R020_ResetDiscardsFullStage(TestContext c){await Reset(c);var m=new CrcModel();await m.Step(c,true,0x57,false,false);await Reset(c);new CrcModel().Observe(c);await Stream(c,[[0x57]]);}
 [SimTest] public static async Task R021_ResetOverridesStalledLast(TestContext c){await Reset(c);var m=new CrcModel();await m.Step(c,true,0xff,true,false);Put(c,"rst",1);await Tick(c);Put(c,"rst",0);new CrcModel().Observe(c);await Stream(c,[[0xff]]);}
 [SimTest] public static Task R022_BackToBackSingleBytePackets(TestContext c)=>Stream(c,Enumerable.Range(0,32).Select(i=>new[]{i}).ToArray());
 [SimTest] public static async Task R023_InvalidLastCannotResetPacket(TestContext c){await Reset(c);var m=new CrcModel();await m.Step(c,true,0x12,false,true);await m.Step(c,false,0,true,true);await m.Step(c,true,0x34,true,true);await m.Flush(c);}
 [SimTest] public static async Task R024_InvalidDataCannotEnterAccumulator(TestContext c){await Reset(c);var m=new CrcModel();for(int i=0;i<8;i++)await m.Step(c,false,1<<i,false,true);await m.Step(c,true,0,true,true);await m.Flush(c);}
 [SimTest] public static async Task R025_BlockedInputChangesCannotCorruptHeldOutput(TestContext c){await Reset(c);var m=new CrcModel();await m.Step(c,true,0x71,false,false);for(int i=0;i<8;i++)await m.Step(c,true,1<<i,i%2==0,false);await m.Step(c,true,0x93,true,true);await m.Flush(c);Check(m.Accepted==2,"only two accepted words");}
 [SimTest] public static async Task R026_CrcOracleRejectsWrongRemainder(TestContext c){await Reset(c);var m=new CrcModel();await m.Step(c,true,0x31,true,false);await Fault(c,m,"c_out_crc",Crc([0x31])^1);await m.Flush(c);}
 [SimTest] public static async Task R027_DataOracleRejectsCorruptedPayload(TestContext c){await Reset(c);var m=new CrcModel();await m.Step(c,true,0xa5,true,false);await Fault(c,m,"c_out_data",0xa4);await m.Flush(c);}
 [SimTest] public static async Task R028_LengthOracleRejectsCountingBubbles(TestContext c){await Reset(c);var m=new CrcModel();await m.Step(c,false,0,false,true);await m.Step(c,true,7,true,false);await Fault(c,m,"c_out_length",2);await m.Flush(c);}
 [SimTest] public static async Task R029_LastOracleRejectsBoundaryLoss(TestContext c){await Reset(c);var m=new CrcModel();await m.Step(c,true,7,true,false);await Fault(c,m,"c_out_last",0);await m.Flush(c);}
 [SimTest] public static async Task R030_NegativeProbeDoesNotPoisonLaterPacket(TestContext c){await Reset(c);var m=new CrcModel();await m.Step(c,true,0xff,true,false);await Fault(c,m,"c_out_crc",Crc([0xff])^1);await m.Step(c,true,0,true,true);await m.Flush(c);}
 [SimTest] public static async Task R031_OneValidPulseProducesExactlyOneDelivery(TestContext c){await Reset(c);var m=new CrcModel();await m.Step(c,true,0x42,true,true);for(int i=0;i<12;i++)await m.Step(c,false,0,false,true);Check(m.Accepted==1&&m.Delivered==1,"no repeated delivery after valid deassertion");}
 [SimTest] public static Task R032_DifferentPacketLengthsRestartAtOne(TestContext c)=>Stream(c,[Bytes(c,1),Bytes(c,255),Bytes(c,2),Bytes(c,256)],sourcePeriod:2,stallPeriod:3);
}
