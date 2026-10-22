using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    static CoverageBin<int>[] Codes(params int[] values)=>values.Select(value=>new CoverageBin<int>(value.ToString(System.Globalization.CultureInfo.InvariantCulture),x=>x==value)).ToArray();
    static CoveragePoint<(int Left,int Right)> NativeCross(TestContext c,string name,int[] left,int[] right,params(string Left,string Right)[] excluded)=>c.Coverage.AddCross(name,Codes(left),Codes(right),excluded:excluded);
    static void Full(CoveragePoint<(int Left,int Right)> point,ulong samples,int bins){var s=point.Snapshot();Check(s.Samples==samples&&s.Unmatched==0&&s.Bins.Length==bins&&s.Percent==100,"observed native cross counters and legal-cell coverage");}
    [SimTest(TimeoutSteps=10000000)] public static async Task R061_RegisterAddressErrorCrossUsesNativeDecode(TestContext c)
    {
        await Reset(c);var point=NativeCross(c,"bank-address-error",[0,1,2,3,4,5,6,7],[0,1],("0","1"),("1","1"),("2","1"),("4","0"),("5","0"),("6","0"),("7","0"));
        for(int address=0;address<8;address++){await BankDrive(c,address,17,15);point.Sample((Convert.ToInt32(Bit(c,"rb_addr").ReadBits(),2),Bit(c,"rb_error").ReadBit()?1:0));}
        await BankDrive(c,3,0,0,write:false);point.Sample((3,Bit(c,"rb_error").ReadBit()?1:0));Full(point,9,9);
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R062_RegisterLaneChangeCrossChecksAllObservedByteCoordinates(TestContext c)
    {
        await Reset(c);var point=NativeCross(c,"lane-change",[0,1,2,3],[0,1]);
        for(int selected=0;selected<4;selected++){await BankWrite(c,0,0);await BankWrite(c,0,uint.MaxValue,1<<selected);uint actual=Stored(c,0);for(int lane=0;lane<4;lane++){int changed=((actual>>(8*lane))&255)!=0?1:0;Check(changed==(lane==selected?1:0),"selected lane alone changes");point.Sample((lane,changed));}}Full(point,16,8);
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R063_AccumulatorBoundaryStickyCrossReachesEveryLegalState(TestContext c)
    {
        await Reset(c);var point=NativeCross(c,"acc-boundary-sticky",[0,1,2],[0,1]);void Sample(){int value=AccValue(c);point.Sample((value==0?0:value==255?2:1,AccOverflow(c)?1:0));}
        foreach(byte value in new byte[]{0,100,255}){await AccLoad(c,value);Sample();}await AccLoad(c,0);await AccStep(c,-1);Sample();await AccLoad(c,255);await AccStep(c,1);Sample();await AccStep(c,-1);Sample();Full(point,6,6);
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R064_AccumulatorDirectionClippingCrossExcludesImpossibleDirections(TestContext c)
    {
        await Reset(c);var point=NativeCross(c,"direction-clip",[-1,0,1],[-1,0,1],("-1","1"),("0","-1"),("0","1"),("1","-1"));
        foreach(var row in new[]{(128,-128),(0,-128),(100,0),(0,127),(250,127)}){await AccLoad(c,(byte)row.Item1);await AccStep(c,row.Item2);int clipped=AccOverflow(c)?(AccValue(c)==0?-1:1):0;point.Sample((Math.Sign(row.Item2),clipped));}Full(point,5,5);
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R065_AccumulatorEnableChangeCrossIncludesEnabledNoop(TestContext c)
    {
        await Reset(c);var point=NativeCross(c,"enable-change",[0,1],[0,1],("0","1"));
        foreach(var row in new[]{(false,17),(true,0),(true,17)}){await AccLoad(c,90);byte before=AccValue(c);await AccStep(c,row.Item2,enable:row.Item1);point.Sample((Bit(c,"a_enable").ReadBit()?1:0,AccValue(c)==before?0:1));}Full(point,3,3);
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R066_AccumulatorLoadOverflowCrossShowsLoadClearsStickyStatus(TestContext c)
    {
        await Reset(c);var point=NativeCross(c,"load-overflow",[0,1],[0,1],("1","1"));
        await AccLoad(c,255);await AccStep(c,1);point.Sample((0,AccOverflow(c)?1:0));await AccStep(c,127,load:true,value:77);point.Sample((Bit(c,"a_load").ReadBit()?1:0,AccOverflow(c)?1:0));await AccStep(c,1);point.Sample((0,AccOverflow(c)?1:0));Full(point,3,3);Check(AccValue(c)==78,"load payload survives enabled saturated input");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R067_RegisterResetCrossSeparatesDataBeforeAndAfterReset(TestContext c)
    {
        await Reset(c);var point=NativeCross(c,"address-nonzero",[0,1,2],[0,1]);for(int address=0;address<3;address++){await BankWrite(c,address,(uint)(address+1));point.Sample((address,Stored(c,address)!=0?1:0));}await Reset(c);for(int address=0;address<3;address++)point.Sample((address,Stored(c,address)!=0?1:0));Full(point,6,6);
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R068_ElasticReadyFullCrossIncludesSimultaneousFullReplacement(TestContext c)
    {
        var point=NativeCross(c,"ready-full",[0,1],[0,1],("0","0"));var p=new PipeModel(c);await p.ResetState();void Sample()=>point.Sample((p.Count==2?1:0,Bit(c,"p_sready").ReadBit()?1:0));Sample();await p.Step(true,1,false);await p.Step(true,2,false);Sample();Bit(c,"p_mready").WriteBit(true);await Timer(1);Sample();int accepted=p.Accepted,consumed=p.Received.Count;await p.Step(true,3,true);Check(p.Accepted==accepted+1&&p.Received.Count==consumed+1&&p.Count==2,"full replacement must actually transfer both ends and remain full");await p.Drain();Check(p.Received.SequenceEqual(new byte[]{1,2,3}),"full replacement preserves accepted order");Full(point,3,3);
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R069_RealSignalSignMagnitudeCrossUsesNativeDoubles(TestContext c)
    {
        var point=NativeCross(c,"real-sign-size",[-1,0,1],[0,1],("0","1"));var signal=c.Dut.Child("real_value").Real();
        foreach(double value in new[]{-10d,-0.25d,0d,0.25d,10d}){signal.Write(value);await Timer(1);double actual=signal.Read();Check(actual==value,"native real transport exact for binary fractions");point.Sample((Math.Sign(actual),Math.Abs(actual)>=1?1:0));}Full(point,5,5);
    }
    [SimTest(TimeoutSteps=10000000,Simulators=["icarus"])] public static async Task R070_NativeIntegerSignCrossUsesIndependentPackedAlias(TestContext c)
    {
        var point=NativeCross(c,"integer-sign",[-1,0,1],[0,1],("-1","0"),("0","1"),("1","1"));var signal=c.Dut.Child("signed_value").Integer();
        foreach(int value in new[]{-17,0,73}){signal.Write(value);await Timer(1);Check(signal.Read()==value,"selected int32 native values");point.Sample((Math.Sign(signal.Read()),Word(c,"signed_alias").ReadBits()[0]=='1'?1:0));}Full(point,3,3);
    }

    [SimTest(TimeoutSteps=10000000)] public static async Task R071_AscendingPackedCrossBindsDeclaredEndpointCoordinates(TestContext c)
    {
        var point=NativeCross(c,"ascending-endpoints",[0,1],[0,1]);foreach(byte value in new byte[]{0,1,128,129}){Byte(c,"ascending").Write(new UInt<W8>(value));await Timer(1);int left=Bit(c,"ascending_left").ReadBit()?1:0,right=Bit(c,"ascending_right").ReadBit()?1:0;Check(left==((value>>7)&1)&&right==(value&1),"ascending declared endpoints follow asymmetric native aliases");point.Sample((left,right));}Full(point,4,4);
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R072_ReadonlyRegisterAccessCrossDistinguishesReadsFromWrites(TestContext c)
    {
        await Reset(c);var point=NativeCross(c,"id-access-error",[0,1],[0,1],("0","1"));await BankDrive(c,3,0,0,write:false);point.Sample((0,Bit(c,"rb_error").ReadBit()?1:0));await BankDrive(c,0,77,15);point.Sample((1,Bit(c,"rb_error").ReadBit()?1:0));await BankDrive(c,3,77,15);point.Sample((1,Bit(c,"rb_error").ReadBit()?1:0));Full(point,3,3);Check(await BankRead(c,3)==0xC0DE2026,"error classification cannot mutate device ID");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R073_CoverageResetCannotResetNativeRegisterStorage(TestContext c)
    {
        await Reset(c);var point=NativeCross(c,"reset-storage-separation",[0,1],[0]);await BankWrite(c,0,73);point.Sample((Stored(c,0)==0?0:1,0));point.Reset();Check(Stored(c,0)==73&&point.Snapshot().Samples==0,"coverage-only reset retains native word");point.Sample((Stored(c,0)==0?0:1,0));await BankWrite(c,0,0);point.Sample((Stored(c,0)==0?0:1,0));Full(point,2,2);
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R074_NativeSamplerClassifierFailurePreservesCommittedCounters(TestContext c)
    {
        await Reset(c);bool reject=false;var marker=new IOException("native classifier");CoverageBin<int>[] first=[new("all",_=>true),new("checked",_=>reject?throw marker:true)];var point=c.Coverage.AddCross("native-sample-atomic",first,Codes(0));
        await BankWrite(c,0,17);point.Sample(((int)Stored(c,0),0));reject=true;await BankWrite(c,0,34);var error=Throws<IOException>(()=>point.Sample(((int)Stored(c,0),0)));var old=point.Snapshot();Check(ReferenceEquals(error,marker)&&old.Samples==1&&old.Bins.All(bin=>bin.Hits==1)&&Stored(c,0)==34,"failed classification rolls back only counters, not completed HDL transaction");reject=false;point.Sample(((int)Stored(c,0),0));Full(point,2,2);
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R075_NativeCrossSnapshotRoundTripMergeDoesNotResampleHardware(TestContext c)
    {
        await Reset(c);var point=NativeCross(c,"native-snapshot",[0,1],[0]);await BankWrite(c,0,75);point.Sample((Stored(c,0)!=0?1:0,0));await BankWrite(c,0,0);point.Sample((Stored(c,0)!=0?1:0,0));string json=System.Text.Json.JsonSerializer.Serialize(c.Coverage.Snapshot());var saved=System.Text.Json.JsonSerializer.Deserialize<CoverageSnapshot>(json)!;c.Coverage.Merge(saved);Full(point,4,2);Check(point.Snapshot().Bins.All(bin=>bin.Hits==2)&&Stored(c,0)==0,"counter merge is additive without HDL replay");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R076_NativeCrossRejectsForeignSampleBeforeClassifierRuns(TestContext c)
    {
        await Reset(c);int classifiers=0;var point=c.Coverage.AddCross("native-owner",new CoverageBin<int>[] {new("all",_=>{classifiers++;return true;})},Codes(0));await BankWrite(c,0,76);int value=(int)Stored(c,0);Exception? error=null;var thread=new Thread(()=>{try{point.Sample((value,0));}catch(Exception failure){error=failure;}});thread.Start();thread.Join();Check(error is InvalidOperationException&&classifiers==0&&point.Snapshot().Samples==0,"foreign sample cannot run classifier or alter counters");point.Sample(((int)Stored(c,0),0));Full(point,1,1);
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R077_ReadOnlyPhaseAllowsCoverageOfCommittedNativeValues(TestContext c)
    {
        await Reset(c);var point=NativeCross(c,"readonly-native-sampling",[0],[1]);await BankWrite(c,0,77);await ReadOnly();point.Sample((0,Stored(c,0)==77?1:0));Full(point,1,1);Throws<InvalidOperationException>(()=>Word(c,"rb_wdata").Write(new UInt<W32>(99)));Check(Stored(c,0)==77,"coverage sampled stable phase without enabling HDL writes");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R078_NativeCrossUnmatchedSamplesRetainObservedBadProtocolTuple(TestContext c)
    {
        await Reset(c);var point=NativeCross(c,"valid-address-only",[0,1,2],[0]);await BankDrive(c,7,0,0,write:false);int observed=Convert.ToInt32(Bit(c,"rb_addr").ReadBits(),2);Check(Bit(c,"rb_error").ReadBit(),"unmapped native address is actual stimulus");point.Sample((observed,0));var s=point.Snapshot();Check(s.Samples==1&&s.Unmatched==1&&s.Bins.All(bin=>bin.Hits==0),"unmatched observation remains counted instead of manufacturing valid coverage");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R079_NativeCrossGoalRequiresRepeatedIndependentTransactions(TestContext c)
    {
        await Reset(c);var point=c.Coverage.AddCross("two-transactions-per-cell",Codes(0,1),Codes(0),goal:2);for(int pass=0;pass<2;pass++){for(int state=0;state<2;state++){await BankWrite(c,0,(uint)state);point.Sample(((int)Stored(c,0),0));}Check(point.Snapshot().Percent==(pass==0?0:100),"one observation cannot satisfy repeated-transaction goal");}Full(point,4,2);Check(point.Snapshot().Bins.All(bin=>bin.Hits==2&&bin.Goal==2),"each cell needs own repeated transactions");
    }
    [SimTest(TimeoutSteps=10000000)] public static async Task R080_NativeCoverageCombinesCrossAndOrdinaryPointsByBinCount(TestContext c)
    {
        await Reset(c);var cross=NativeCross(c,"weighted-native-cross",[0,1],[0,1]);var ordinary=c.Coverage.AddPoint<int>("native-valid",new CoverageBin<int>("seen",x=>x==80));await BankWrite(c,0,80);ordinary.Sample((int)Stored(c,0));cross.Sample((0,0));Check(c.Coverage.Snapshot().Percent==40,"native coverage weights two covered bins out of five, not average point percentages");
    }
}
