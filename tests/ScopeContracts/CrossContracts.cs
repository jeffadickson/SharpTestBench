using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    static async Task<int> Observe(TestContext c,int value)
    {Put(c,"f_byte",value);await Timer(1);int actual=N(c,"f_byte");Check(actual==value,"independent native byte readback");Check(N(c,"f_complement")==255-value,"complement native alias");int parity=0;for(int b=0;b<8;b++)parity^=(value>>b)&1;Check(N(c,"f_parity")==parity,"parity native alias");return actual;}
    static void Counts(CoveragePoint<int> p,ulong samples,ulong unmatched,int bins,int covered)
    {var s=p.Snapshot();Check(s.Samples==samples&&s.Unmatched==unmatched&&s.Bins.Length==bins&&s.Bins.Count(b=>b.Hits>=b.Goal)==covered,"native-sampled cross counters");}
    [SimTest] public static async Task R026_ThreeNativeBitsPopulateEightCells(TestContext c)
    {var p=c.Coverage.AddCross("native-three",Axes(3));for(int i=0;i<8;i++)p.Sample(await Observe(c,i));Counts(p,8,0,8,8);Check(p.Snapshot().Bins.All(b=>b.Hits==1),"one native hit per cell");}
    [SimTest] public static async Task R027_FourNativeBitsPopulateSixteenCells(TestContext c)
    {var p=c.Coverage.AddCross("native-four",Axes(4));for(int i=0;i<16;i++)p.Sample(await Observe(c,i));Counts(p,16,0,16,16);}
    [SimTest] public static async Task R028_UnequalNativePartitionsUseIndependentFields(TestContext c)
    {CoverageBin<int>[][] axes=[[B("lo",x=>(x&1)==0),B("hi",x=>(x&1)!=0)],[B("r0",x=>x%3==0),B("r1",x=>x%3==1),B("r2",x=>x%3==2)],[B("low",x=>x<6),B("high",x=>x>=6)]];var p=c.Coverage.AddCross("unequal",axes);for(int i=0;i<12;i++)p.Sample(await Observe(c,i));Counts(p,12,0,12,12);}
    [SimTest] public static async Task R029_CorrelatedNativeAliasesKeepImpossibleCellsUncovered(TestContext c)
    {CoverageBin<(int Byte,int Complement,int Parity)>[][] axes=[[new("lo",x=>x.Byte<128),new("hi",x=>x.Byte>=128)],[new("lo",x=>x.Complement<128),new("hi",x=>x.Complement>=128)],[new("even",x=>x.Parity==0),new("odd",x=>x.Parity==1)]];var p=c.Coverage.AddCross("aliases",axes);foreach(int v in new[]{0,1,128,129}){int b=await Observe(c,v);p.Sample((b,N(c,"f_complement"),N(c,"f_parity")));}Check(p.Snapshot().Bins.Length==8&&p.Snapshot().Percent==50,"impossible alias cells remain visible");}
    [SimTest] public static async Task R030_ExcludedNativeCoordinateCountsUnmatched(TestContext c)
    {var p=c.Coverage.AddCross("excluded",Axes(3),excluded:[["one","one","one"]]);for(int i=0;i<8;i++)p.Sample(await Observe(c,i));Counts(p,8,1,7,7);}
    [SimTest] public static async Task R031_OverlappingNativeMasksCreditEveryMatchingCell(TestContext c)
    {CoverageBin<int>[][] axes=[[B("all",_=>true),B("low",x=>x<16)],[B("all",_=>true),B("even",x=>(x&1)==0)],[B("all",_=>true),B("nonzero",x=>x!=0)]];var p=c.Coverage.AddCross("overlap",axes);p.Sample(await Observe(c,2));Counts(p,1,0,8,8);Check(p.Snapshot().Bins.Sum(b=>(decimal)b.Hits)==8,"single native sample overlaps eight cells");}
    [SimTest(Simulators=["icarus"])] public static async Task R032_UnknownNativeSymbolsRemainExplicitClassifierInputs(TestContext c)
    {var s=S(c,"f_byte");s.WriteBits("XZ000001");await Timer(1);CoverageBin<string>[][] axes=[[new("x",x=>x[0]=='X')],[new("z",x=>x[1]=='Z')],[new("one",x=>x[^1]=='1')]];var p=c.Coverage.AddCross("symbols",axes);p.Sample(s.ReadBits());Check(p.Snapshot().Percent==100&&p.Snapshot().Samples==1,"four-state native text classified without numeric resolution");}
    [SimTest] public static async Task R033_WideNativeSampleIncludesHighestBit(TestContext c)
    {var s=S(c,"f_wide");s.WriteBits("1"+new string('0',94)+"1");await Timer(1);CoverageBin<string>[][] axes=[[new("top",x=>x.Length==96&&x[0]=='1')],[new("middle",x=>x[47]=='0')],[new("bottom",x=>x[^1]=='1')]];var p=c.Coverage.AddCross("wide",axes);p.Sample(s.ReadBits());Check(p.Snapshot().Percent==100,"96bit classifier no native-long truncation");}
    [SimTest(Simulators=["icarus"])] public static async Task R034_IntegerSignAndLowBitsComeFromNativeReadback(TestContext c)
    {var s=S(c,"f_integer");s.WriteBits("10000000000000000000000000000001");await Timer(1);CoverageBin<string>[][] axes=[[new("negative",x=>x[0]=='1')],[new("odd",x=>x[^1]=='1')],[new("width32",x=>x.Length==32)]];var p=c.Coverage.AddCross("integer",axes);p.Sample(s.ReadBits());Check(p.Snapshot().Percent==100,"signed native pattern classification");}
    [SimTest] public static async Task R035_RealNativeFractionClassifiesSharedSample(TestContext c)
    {var real=c.Dut.Child("f_real").Real();real.Write(-3.25);await Timer(1);CoverageBin<double>[][] axes=[[new("negative",x=>x<0)],[new("fraction",x=>x!=Math.Truncate(x))],[new("bounded",x=>Math.Abs(x)<4)]];var p=c.Coverage.AddCross("real",axes);p.Sample(real.Read());Check(p.Snapshot().Percent==100,"real shared native sample");}
    [SimTest] public static async Task R036_EnumNativeEncodingIsClassifiedWithoutNameInference(TestContext c)
    {var s=S(c,"f_enum");s.WriteBits("111");await Timer(1);CoverageBin<string>[][] axes=[[new("top",x=>x[0]=='1')],[new("mid",x=>x[1]=='1')],[new("low",x=>x[2]=='1')]];var p=c.Coverage.AddCross("enum-encoding",axes);p.Sample(s.ReadBits());Check(p.Snapshot().Percent==100,"enum encoded native signal, no name inference");}
    [SimTest(Simulators=["Verilator"])] public static async Task R037_StringNativeReadbackSupportsUserClassifiers(TestContext c)
    {var s=c.Dut.Child("f_text").String();s.Write("packet37");await Timer(1);CoverageBin<string>[][] axes=[[new("packet",x=>x.StartsWith("packet",StringComparison.Ordinal))],[new("length8",x=>x.Length==8)],[new("suffix37",x=>x.EndsWith("37",StringComparison.Ordinal))]];var p=c.Coverage.AddCross("string",axes);p.Sample(s.Read());Check(p.Snapshot().Percent==100,"native string immutable sample");}
    [SimTest] public static async Task R038_ReadOnlyAllowsSamplingButStillRejectsSignalWrites(TestContext c)
    {await Observe(c,6);var p=c.Coverage.AddCross("readonly",Axes(3));await ReadOnly();p.Sample(N(c,"f_byte"));Throws<InvalidOperationException>(()=>Put(c,"f_byte",0));Counts(p,1,0,8,1);Check(N(c,"f_byte")==6,"sample cannot relax native write phase guard");}
    [SimTest] public static async Task R039_QueuedDepositDoesNotAppearInCommittedCoverageSample(TestContext c)
    {await Observe(c,1);var p=c.Coverage.AddCross("committed",Axes(3));S(c,"f_byte").WriteBits("00000111");p.Sample(N(c,"f_byte"));await Timer(1);p.Sample(N(c,"f_byte"));Counts(p,2,0,8,2);Check(p.Snapshot().Bins.Single(b=>b.Name=="3:one4:zero4:zero").Hits==1,"first sample committed old native value");}
    [SimTest] public static async Task R040_ResetWindowRetainsPreviouslySavedNativeSnapshot(TestContext c)
    {var p=c.Coverage.AddCross("windows",Axes(3));p.Sample(await Observe(c,0));var saved=p.Snapshot();p.Reset();p.Sample(await Observe(c,7));Counts(p,1,0,8,1);Check(saved.Samples==1&&saved.Bins[0].Hits==1&&p.Snapshot().Bins[0].Hits==0,"native sampling windows remain independent");}
    [SimTest] public static async Task R041_MergeMapsNativeSampleCellsAcrossReorderedBins(TestContext c)
    {var p=c.Coverage.AddCross("merged-native",Axes(3));p.Sample(await Observe(c,0));var axes=Axes(3);foreach(var a in axes)Array.Reverse(a);var other=new FunctionalCoverage();other.AddCross("merged-native",axes).Sample(await Observe(c,7));c.Coverage.Merge(other.Snapshot());Counts(p,2,0,8,2);}
    [SimTest] public static async Task R042_GoalCoverageWaitsForRepeatedNativeObservations(TestContext c)
    {var p=c.Coverage.AddCross("goal",Axes(3),goal:2);for(int i=0;i<8;i++)p.Sample(await Observe(c,i));Counts(p,8,0,8,0);p.Sample(await Observe(c,3));Counts(p,9,0,8,1);}
    [SimTest] public static async Task R043_InvalidCrossConstructionLeavesNativeCallbacksUnchanged(TestContext c)
    {await Observe(c,43);int before=c.Runtime.PendingCallbacks;Throws<ArgumentOutOfRangeException>(()=>c.Coverage.AddCross("valid-later",Axes(4),maximumBins:8));Check(c.Runtime.PendingCallbacks==before&&N(c,"f_byte")==43,"managed construction cannot mutate native state");var p=c.Coverage.AddCross("valid-later",Axes(3));p.Sample(N(c,"f_byte"));Counts(p,1,0,8,1);}
    [SimTest] public static async Task R044_ClassifierFailureRollsBackNativeObservedSample(TestContext c)
    {bool fail=false;var axes=Axes(3);axes[2][1]=B("one",x=>fail?throw new FormatException("classifier44"):(x&4)!=0);var p=c.Coverage.AddCross("recovery",axes);p.Sample(await Observe(c,0));fail=true;int rejected=await Observe(c,7);Throws<FormatException>(()=>p.Sample(rejected));Counts(p,1,0,8,1);fail=false;p.Sample(await Observe(c,7));Counts(p,2,0,8,2);}
    [SimTest] public static async Task R045_ExcludedPlaneDoesNotInvokeUnreachableClassifiers(TestContext c)
    {int forbidden=0;var axes=Axes(3);axes[0][1]=B("one",_=>{forbidden++;throw new InvalidDataException("excluded classifier");});var p=c.Coverage.AddCross("excluded-plane",axes,excluded:[["one","zero","zero"],["one","zero","one"],["one","one","zero"],["one","one","one"]]);for(int i=0;i<8;i++)p.Sample(await Observe(c,i));Counts(p,8,4,4,4);Check(forbidden==0,"excluded plane pruned before matcher creation");}
    [SimTest] public static async Task R046_ForeignSamplerCannotMutateNativeCoverageState(TestContext c)
    {var p=c.Coverage.AddCross("thread",Axes(3));int sample=await Observe(c,6);Exception? error=null;var t=new Thread(()=>{try{p.Sample(sample);}catch(Exception e){error=e;}});t.Start();t.Join();Check(error is InvalidOperationException,"foreign sampler guard");Counts(p,0,0,8,0);p.Sample(sample);Counts(p,1,0,8,1);}
    [SimTest] public static async Task R047_ColonNamesRetainDistinctNativeCoordinateIdentity(TestContext c)
    {CoverageBin<int>[][] axes=[[B("a:b",x=>(x&1)==0),B("a",x=>(x&1)!=0)],[B("b:c",x=>(x&2)==0),B("c",x=>(x&2)!=0)],[B("z:tail",_=>true)]];var p=c.Coverage.AddCross("names",axes);for(int i=0;i<4;i++)p.Sample(await Observe(c,i));Counts(p,4,0,4,4);Check(p.Snapshot().Bins.Select(b=>b.Name).Distinct(StringComparer.Ordinal).Count()==4,"encoded identities remain distinct");}
    [SimTest] public static async Task R048_FunctionalExportWeightsNativeCrossAndOrdinaryBins(TestContext c)
    {var p=c.Coverage.AddCross("export-cross",Axes(3));var q=c.Coverage.AddPoint("export-point",new CoverageBin<int>("all",_=>true));int sample=await Observe(c,48);p.Sample(sample);q.Sample(sample);Check(c.Coverage.Snapshot().Percent==100d*2/9,"native export aggregate weights bins");}
    [SimTest] public static async Task R049_AllNativeByteValuesPopulateEightAxisCross(TestContext c)
    {var p=c.Coverage.AddCross("all-byte-values",Axes(8),maximumBins:256);for(int i=255;i>=0;i--)p.Sample(await Observe(c,i));Counts(p,256,0,256,256);Check(p.Snapshot().Bins.All(b=>b.Hits==1),"exhaustive native byte bijection");}
    [SimTest] public static async Task R050_NativeSamplesOutsideDefinedDomainRemainUnmatched(TestContext c)
    {CoverageBin<int>[][] axes=[[B("small",x=>x<4)],[B("even",x=>(x&1)==0)],[B("low",x=>x<128)]];var p=c.Coverage.AddCross("domain",axes);foreach(int v in new[]{0,1,2,4,128,255})p.Sample(await Observe(c,v));Counts(p,6,4,1,1);Check(p.Snapshot().Bins[0].Hits==2,"unmatched counts once per native sample");}
}
