using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest(TimeoutSteps=10000000)] public static async Task R001_ElasticResetPublishesEmptyReadyState(TestContext c)
    {await Reset(c);Check(Bit(c,"p_sready").ReadBit()&&!Bit(c,"p_mvalid").ReadBit(),"reset ready/valid");Check(Convert.ToInt32(Bit(c,"p_count").ReadBits(),2)==0&&(byte)Byte(c,"p_mdata").ReadUInt().Value==0,"reset occupancy and data");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R002_ElasticSingleItemHasTwoRegisterLatency(TestContext c)
    {var p=new PipeModel(c);await p.ResetState();await p.Step(true,0xA7,false);Check(p.Count==1&&!p.OutputValid,"item cannot bypass both registers");await p.Step(false,0,true);Check(p.OutputValid&&p.Received.Count==0,"second register fills before transfer");await p.Step(false,0,true);Check(p.Received.SequenceEqual(new byte[]{0xA7})&&p.Count==0,"single item delivered once");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R003_ElasticFullStateRefusesAnotherAcceptance(TestContext c)
    {var p=new PipeModel(c);await p.ResetState();await p.Step(true,11,false);await p.Step(true,22,false);Check(p.Count==2&&!Bit(c,"p_sready").ReadBit(),"full elastic storage");await p.Step(true,33,false);Check(p.Accepted==2,"blocked item not accepted");await p.Drain();Check(p.Received.SequenceEqual(new byte[]{11,22}),"full refusal preserved original items");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R004_ElasticOutputRemainsStableThroughoutStall(TestContext c)
    {var p=new PipeModel(c);await p.ResetState();await p.Step(true,0xE1,false);await p.Step(true,0x12,false);for(int i=0;i<8;i++){await p.Step(false,0,false);Check(p.OutputValid&&p.Output==0xE1&&p.Received.Count==0,"held output must not pulse or change");}}
    [SimTest(TimeoutSteps=10000000)] public static async Task R005_ElasticDrainPreservesTwoHeldItemsInOrder(TestContext c)
    {var p=new PipeModel(c);await p.ResetState();await p.Step(true,0x21,false);await p.Step(true,0xB2,false);await p.Step(false,0,true);Check(p.Received.SequenceEqual(new byte[]{0x21})&&p.Count==1,"first held item");await p.Step(false,0,false);Check(p.Received.Count==1,"deasserted ready cannot consume tail");await p.Drain();Check(p.Received.SequenceEqual(new byte[]{0x21,0xB2}),"second held item");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R006_ElasticFullReplacementTransfersBothEnds(TestContext c)
    {var p=new PipeModel(c);await p.ResetState();await p.Step(true,10,false);await p.Step(true,20,false);await p.Step(true,30,true);Check(p.Count==2&&p.Accepted==3&&p.Received.SequenceEqual(new byte[]{10}),"simultaneous transfer keeps full occupancy");await p.Drain();Check(p.Received.SequenceEqual(new byte[]{10,20,30}),"replacement order");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R007_ElasticBubblesDoNotCreatePhantomTransfers(TestContext c)
    {var p=new PipeModel(c);await p.ResetState();await p.Step(true,1,true);await p.Step(false,0,true);await p.Step(true,2,true);await p.Step(false,0,true);await p.Step(false,0,true);await p.Step(false,0,true);Check(p.Accepted==2&&p.Received.SequenceEqual(new byte[]{1,2})&&p.Count==0,"bubbles preserve multiplicity");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R008_ElasticContinuousTrafficSustainsOneTransferPerCycle(TestContext c)
    {var p=new PipeModel(c);await p.ResetState();for(int i=0;i<16;i++){await p.Step(true,(byte)(i+1),true);Check(p.Received.Count==Math.Max(0,i-1),"continuous transfer cadence after two-stage fill");}await p.Drain();Check(p.Received.SequenceEqual(Enumerable.Range(1,16).Select(i=>(byte)i)),"continuous ordered payloads");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R009_ElasticResetDiscardsBothInflightItems(TestContext c)
    {var p=new PipeModel(c);await p.ResetState();await p.Step(true,7,false);await p.Step(true,8,false);await p.ResetState();await p.Step(false,0,true);await p.Step(false,0,true);Check(p.Discarded==2&&p.Received.Count==0&&p.Count==0,"reset cannot leak an old item after ready returns");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R010_ElasticResetDominatesCoincidentValidInput(TestContext c)
    {await Reset(c);Bit(c,"rst").WriteBit(true);Bit(c,"p_svalid").WriteBit(true);Byte(c,"p_sdata").Write(new UInt<W8>(99));Bit(c,"p_mready").WriteBit(true);await Timer(1);Check(Bit(c,"p_svalid").ReadBit(),"valid stimulus present during reset");await Tick(c);Bit(c,"rst").WriteBit(false);Bit(c,"p_svalid").WriteBit(false);await Tick(c);Check(!Bit(c,"p_mvalid").ReadBit(),"reset input not promoted");await Tick(c);Check(!Bit(c,"p_mvalid").ReadBit(),"reset input never emerges later");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R011_ElasticInvalidInputChangesCannotFillStorage(TestContext c)
    {var p=new PipeModel(c);await p.ResetState();for(int i=0;i<12;i++)await p.Step(false,(byte)(17*i),i%2==0);Check(p.Accepted==0&&p.Received.Count==0&&p.Count==0,"invalid payload changes do not count as tokens");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R012_ElasticRejectedPayloadCannotOverwriteTheHiddenStage(TestContext c)
    {var p=new PipeModel(c);await p.ResetState();await p.Step(true,0x53,false);await p.Step(true,0xAC,false);for(int i=0;i<5;i++)await p.Step(true,(byte)i,false);await p.Drain();Check(p.Received.SequenceEqual(new byte[]{0x53,0xAC})&&p.Accepted==2,"rejected producer values cannot overwrite either stored item");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R013_ElasticEqualAndZeroPayloadsRetainMultiplicity(TestContext c)
    {var p=new PipeModel(c);await p.ResetState();byte[] input=[0,0x5A,0x5A,0,0x5A,0];foreach(byte value in input)await p.Step(true,value,true);await p.Drain();Check(p.Accepted==input.Length&&p.Received.SequenceEqual(input),"payload equality must not merge tokens or confuse empty data with valid zero");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R014_ElasticSeededBackpressureAndResetMatchIndependentModel(TestContext c)
    {var p=new PipeModel(c);await p.ResetState();for(int i=0;i<8;i++)await p.Step(true,(byte)i,true);await p.Drain();for(int i=0;i<300;i++){if(i is 63 or 141 or 218){while(p.Count<2)await p.Step(true,(byte)i,false);await p.ResetState();}else await p.Step(c.Random.Next(2)==1,(byte)c.Random.Next(256),c.Random.Next(2)==1);}await p.Drain();Check(p.Discarded==6&&p.Received.Count>0&&p.Accepted==p.Received.Count+p.Discarded,"seeded accounting includes reset discards and final drain");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R015_ElasticNativeOccupancyTransferCrossCoversAllLegalCells(TestContext c)
    {
        CoverageBin<int>[] occupancy=[new("empty",x=>x==0),new("single",x=>x==1),new("full",x=>x==2)];
        CoverageBin<int>[] transfer=[new("none",x=>x==0),new("input",x=>x==1),new("output",x=>x==2),new("both",x=>x==3)];
        var cross=c.Coverage.AddCross("elastic-cross",occupancy,transfer,excluded:[("empty","output"),("empty","both"),("full","input")]);var p=new PipeModel(c);
        for(int state=0;state<4;state++)for(int valid=0;valid<2;valid++)for(int ready=0;ready<2;ready++)
        {await p.ResetState();if(state!=0)await p.Step(true,12,false);if(state==2)await p.Step(false,0,false);if(state==3)await p.Step(true,34,false);await p.Step(valid==1,56,ready==1,cross);}
        var report=cross.Snapshot();Check(report.Samples==16&&report.Unmatched==0&&report.Bins.Length==9&&report.Percent==100,"all legal observed occupancy/transfer cells and only those cells are covered");
    }
}
