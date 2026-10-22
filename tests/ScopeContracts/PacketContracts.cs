using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    readonly record struct PacketObservation(bool Full,bool Incoming,bool Outgoing);
    sealed class PacketModel(TestContext c)
    {
        readonly Queue<(byte Data,bool Last,byte Sequence)> expected=new();
        readonly List<byte> packet=[];
        byte sequence;
        public int Accepted{get;private set;}
        public int Delivered{get;private set;}
        public int Discarded{get;private set;}
        public int Count=>expected.Count;
        public readonly List<(byte Data,bool Last,byte Sequence)> Results=[];
        void Verify()
        {
            Check((N(c,"m_valid")!=0)==(expected.Count!=0),"native output valid vs transaction queue");
            if(expected.TryPeek(out var next))Check(N(c,"m_data")==next.Data&&(N(c,"m_last")!=0)==next.Last&&N(c,"m_sequence")==next.Sequence,"native output vs independent packet history");
            Check(Accepted==Delivered+Discarded+expected.Count,"transaction conservation");
        }
        public void VerifyObserved()=>Verify();
        public async Task Step(bool valid,int data,bool last,bool ready,CoveragePoint<PacketObservation>? coverage=null)
        {
            Put(c,"s_valid",valid?1:0);Put(c,"s_data",data);Put(c,"s_last",last?1:0);Put(c,"m_ready",ready?1:0);await Timer(1);Verify();
            bool full=expected.Count!=0;bool nativeReady=N(c,"s_ready")!=0;Check(nativeReady==(!full||ready),"native ready vs queue capacity");
            bool incoming=valid&&nativeReady,outgoing=full&&ready;coverage?.Sample(new(full,incoming,outgoing));
            if(outgoing){var item=expected.Dequeue();Results.Add(item);Delivered++;}
            if(incoming)
            {
                packet.Add((byte)data);byte checksum=0;foreach(byte value in packet)checksum^=value;
                expected.Enqueue((checksum,last,sequence));Accepted++;
                if(last){packet.Clear();sequence=unchecked((byte)(sequence+1));}
            }
            await Tick(c);Verify();
        }
        public async Task ResetState()
        {Discarded+=expected.Count;expected.Clear();packet.Clear();sequence=0;await Reset(c);Verify();}
        public async Task Drain(){await Step(false,0,false,true);Check(Count==0,"drain transaction queue");}
    }
    [SimTest] public static async Task R051_ResetLeavesChecksumStageEmpty(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();Check(N(c,"m_data")==0&&N(c,"m_sequence")==0&&N(c,"m_last")==0,"reset payload metadata");await m.Step(false,255,true,true);Check(m.Accepted==0&&m.Delivered==0,"invalid input ignored after reset");}
    [SimTest] public static async Task R052_SingleWordPacketTransportsLiteralChecksum(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();await m.Step(true,0xA5,true,true);await m.Drain();Check(m.Results.SequenceEqual(new[]{((byte)0xA5,true,(byte)0)}),"literal single packet");}
    [SimTest] public static async Task R053_MultiwordPacketEmitsCumulativeChecksum(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();await m.Step(true,0x12,false,true);await m.Step(true,0x34,false,true);await m.Step(true,0x56,true,true);await m.Drain();Check(m.Results.Select(x=>x.Data).SequenceEqual(new byte[]{0x12,0x26,0x70}),"independent literal cumulative vector");}
    [SimTest] public static async Task R054_InvalidInputCannotAdvancePacketHistory(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();await m.Step(true,10,false,true);for(int i=0;i<8;i++)await m.Step(false,255-i,true,true);await m.Step(true,3,true,true);await m.Drain();Check(m.Results[^1]==((byte)9,true,(byte)0)&&m.Accepted==2,"invalid gaps cannot alter checksum/sequence");}
    [SimTest] public static async Task R055_StalledOutputRetainsPayloadAndMetadata(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();await m.Step(true,55,true,false);for(int i=0;i<12;i++)await m.Step(false,i,false,false);Check(m.Count==1&&m.Delivered==0,"held output remains pending");await m.Drain();}
    [SimTest] public static async Task R056_FullStageRejectsChangingSourcePayloads(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();await m.Step(true,56,false,false);for(int i=0;i<16;i++)await m.Step(true,i,true,false);Check(m.Accepted==1,"blocked input cannot enter transaction queue");await m.Drain();await m.Step(true,1,true,true);await m.Drain();Check(m.Results[^1].Data==57&&m.Results[^1].Sequence==0,"blocked last flags cannot terminate packet");}
    [SimTest] public static async Task R057_ReadyFullStageReplacesOutputInSameCycle(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();await m.Step(true,1,true,false);await m.Step(true,2,true,true);Check(m.Accepted==2&&m.Delivered==1&&m.Count==1,"replacement conservation");await m.Drain();Check(m.Results.Select(x=>x.Sequence).SequenceEqual(new byte[]{0,1}),"replacement retains packet order");}
    [SimTest] public static async Task R058_DrainedBubbleDoesNotRepeatPriorPacket(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();await m.Step(true,58,true,true);await m.Drain();for(int i=0;i<6;i++)await m.Step(false,58,true,true);Check(m.Delivered==1&&m.Count==0,"no phantom repeated output");}
    [SimTest] public static async Task R059_LastBoundaryRestartsChecksumForNextPacket(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();await m.Step(true,1,false,true);await m.Step(true,2,true,true);await m.Step(true,4,false,true);await m.Step(true,8,true,true);await m.Drain();Check(m.Results.Select(x=>x.Data).SequenceEqual(new byte[]{1,3,4,12})&&m.Results[^1].Sequence==1,"checksum reset on accepted packet end");}
    [SimTest] public static async Task R060_ConsecutiveOneWordPacketsAdvanceSequence(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();for(int i=0;i<32;i++)await m.Step(true,i,true,true);await m.Drain();Check(m.Results.Count==32&&m.Results.Select(x=>(int)x.Sequence).SequenceEqual(Enumerable.Range(0,32)),"no bubbles needed between packets");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R061_PacketSequenceWrapsAtNativeByteBoundary(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();for(int i=0;i<260;i++)await m.Step(true,i&255,true,true);await m.Drain();Check(m.Results[255].Sequence==255&&m.Results[256].Sequence==0&&m.Results[259].Sequence==3,"independent sequence wrap literals");}
    [SimTest] public static async Task R062_ResetDiscardsPendingOutputExactlyOnce(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();await m.Step(true,62,true,false);await m.ResetState();await m.Drain();Check(m.Accepted==1&&m.Discarded==1&&m.Delivered==0,"reset pending transaction conservation");}
    [SimTest] public static async Task R063_ResetMidPacketClearsAccumulatedHistory(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();await m.Step(true,255,false,true);await m.Drain();await m.ResetState();await m.Step(true,63,true,true);await m.Drain();Check(m.Results[^1]==((byte)63,true,(byte)0),"reset forgets partial packet and sequence");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R064_SeededBurstyTrafficPreservesEveryAcceptedTransaction(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();for(int i=0;i<400;i++)await m.Step(c.Random.Next(4)!=0,c.Random.Next(256),c.Random.Next(5)==0,c.Random.Next(3)!=0);await m.Drain();Check(m.Accepted>100&&m.Delivered==m.Accepted&&m.Discarded==0,"seeded stress nonvacuity and conservation");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R065_AllByteValuesContributeToOnePacket(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();for(int i=0;i<256;i++)await m.Step(true,i,i==255,true);await m.Drain();Check(m.Delivered==256&&m.Results[^1].Data==0&&m.Results[^1].Last,"xor of all byte values is zero");}
    [SimTest] public static async Task R066_ChecksumFaultIsDetectedBeforeConsumptionAndResetRecovers(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();await m.Step(true,0x66,true,false);Put(c,"m_data",0x67);Check(N(c,"m_data")==0x67,"negative control actually corrupted native output");var error=Throws<InvalidDataException>(m.VerifyObserved);Check(error.Message.Contains("native output vs independent packet history",StringComparison.Ordinal),"oracle detects the intended payload fault");Check(m.Count==1&&m.Accepted==1&&m.Delivered==0,"failed observation cannot consume a transaction");await m.ResetState();await m.Step(true,0x77,true,true);await m.Drain();Check(m.Discarded==1&&m.Delivered==1&&m.Results[^1]==((byte)0x77,true,(byte)0),"reset recovers after independently detected native corruption");}
    [SimTest] public static async Task R067_RepeatedPayloadPairCancelsChecksumBits(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();await m.Step(true,0xC7,false,true);await m.Step(true,0xC7,true,true);await m.Drain();Check(m.Results[0].Data==0xC7&&m.Results[1].Data==0,"xor pair independent oracle");}
    [SimTest] public static async Task R068_LastFlagAndSequenceRemainStableUnderBackpressure(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();await m.Step(true,1,true,true);await m.Step(true,2,true,true);for(int i=0;i<10;i++)await m.Step(true,3,false,false);Check(N(c,"m_last")==1&&N(c,"m_sequence")==1,"held last metadata");await m.Drain();Check(m.Accepted==2&&m.Results[^1].Sequence==1,"rejected traffic cannot consume sequence");}
    [SimTest] public static async Task R069_ResetDominatesSimultaneousValidReadyInputs(TestContext c)
    {await Reset(c);Put(c,"s_valid",1);Put(c,"s_last",1);Put(c,"s_data",255);Put(c,"m_ready",1);Put(c,"rst",1);await Tick(c);Check(N(c,"m_valid")==0&&N(c,"checksum")==0&&N(c,"sequence_number")==0,"reset dominates transfer");Put(c,"rst",0);Put(c,"s_valid",0);await Tick(c);Check(N(c,"m_valid")==0,"reset-time valid cannot leak deferred transaction");}
    [SimTest] public static async Task R070_ThreeAxisHandshakeCoverageExcludesImpossibleTransitions(TestContext c)
    {
        CoverageBin<PacketObservation>[][] axes=[[new("empty",x=>!x.Full),new("full",x=>x.Full)],[new("no-in",x=>!x.Incoming),new("in",x=>x.Incoming)],[new("no-out",x=>!x.Outgoing),new("out",x=>x.Outgoing)]];
        var p=c.Coverage.AddCross("handshake",axes,excluded:[["empty","no-in","out"],["empty","in","out"],["full","in","no-out"]]);var m=new PacketModel(c);await m.ResetState();await m.Step(false,0,false,false,p);await m.Step(true,1,false,false,p);await m.Step(false,0,false,false,p);await m.Step(true,2,true,true,p);await m.Step(false,0,false,true,p);Check(p.Snapshot().Percent==100&&p.Snapshot().Samples==5&&p.Snapshot().Unmatched==0,"all five feasible independently modeled native transitions");
    }
    [SimTest] public static async Task R071_CancelledMonitorCannotLosePendingPacket(TestContext c)
    {var m=new PacketModel(c);await m.ResetState();await m.Step(true,71,true,false);bool cleaned=false;var ready=new SimEvent();var monitor=c.StartSoon(async token=>{try{ready.Set();await Timer(1000);}finally{cleaned=true;}});await ready.Wait();monitor.Cancel();try{await monitor;throw new InvalidDataException("monitor should cancel");}catch(OperationCanceledException){}Check(cleaned&&m.Count==1&&N(c,"m_data")==71,"joined monitor cancellation preserves hardware packet");await m.Drain();Check(m.Delivered==1,"pending packet still delivered after monitor failure path");}
}
