using SharpTestBench;
using Xunit;
public sealed partial class ConformanceCrossTests
{
    [Fact] public void C021_NullConstructionShapesFailBeforeRegistration()
    {var c=new FunctionalCoverage();Assert.Throws<ArgumentNullException>(()=>c.AddCross<int,int>("p",null!,Axis()));Assert.Throws<ArgumentNullException>(()=>c.AddCross<int,int>("p",Axis(),null!));Assert.Throws<ArgumentNullException>(()=>c.AddCross("p",Axis(),Axis(),excluded:null!));Assert.Empty(c.Snapshot().Points);}
    [Fact] public void C022_MutatingSnapshotBinsCannotChangeLiveCrossDefinitions()
    {var p=Cross(new());p.Sample((0,0));var snapshot=p.Snapshot();snapshot.Bins[0]=new("corrupt",999,999);p.Sample((0,0));Assert.Equal("4:zero4:zero",p.Snapshot().Bins[0].Name);Assert.Equal(2UL,p.Snapshot().Bins[0].Hits);}
    [Fact] public void C023_PointResetRetainsExclusionsAndGoals()
    {var p=new FunctionalCoverage().AddCross("p",Axis(),Axis(),goal:2,excluded:[("one","one")]);p.Sample((0,0));p.Reset();p.Sample((0,0));Assert.Equal(1UL,p.Snapshot().Samples);Assert.Equal(3,p.Snapshot().Bins.Length);Assert.Equal(2UL,p.Snapshot().Bins[0].Goal);Assert.False(p.Snapshot().Bins[0].Covered);}
    [Fact] public void C024_SuiteResetPreflightSeesActiveCrossBeforeClearingEarlierPoints()
    {var c=new FunctionalCoverage();var ordinary=c.AddPoint<int>("ordinary",new CoverageBin<int>("all",x=>true));ordinary.Sample(1);bool reset=true;var p=c.AddCross("p",new CoverageBin<int>[] {new("l",x=>{if(reset)c.Reset();return true;})},new CoverageBin<int>[] {new("r",x=>true)});Assert.Throws<InvalidOperationException>(()=>p.Sample((1,2)));Assert.Equal(1UL,ordinary.Snapshot().Samples);Assert.Equal(0UL,p.Snapshot().Samples);reset=false;p.Sample((1,2));Assert.Equal(1UL,p.Snapshot().Samples);}
    [Fact] public void C025_MergeMapsCellsByIdentityAcrossReorderedAxes()
    {var source=new FunctionalCoverage().AddCross("p",Axis().Reverse().ToArray(),Axis().Reverse().ToArray());source.Sample((1,0));var c=new FunctionalCoverage();var target=c.AddCross("p",Axis(),Axis());target.Sample((0,1));c.Merge(new([source.Snapshot()]));Assert.Equal(new ulong[]{0,1,1,0},target.Snapshot().Bins.Select(b=>b.Hits));Assert.Equal(2UL,target.Snapshot().Samples);}
    [Fact] public void C026_SubsetMergeLeavesUnmentionedOrdinaryPointsUntouched()
    {var c=new FunctionalCoverage();var ordinary=c.AddPoint<int>("ordinary",new CoverageBin<int>("all",x=>true));ordinary.Sample(7);var target=Cross(c);var source=Cross(new());source.Sample((1,1));c.Merge(new([source.Snapshot()]));Assert.Equal(1UL,ordinary.Snapshot().Samples);Assert.Equal(1UL,target.Snapshot().Bins[3].Hits);}
    [Fact] public void C027_CrossOverflowPreventsCommittingEarlierPointMerge()
    {var c=new FunctionalCoverage();var ordinary=c.AddPoint<int>("ordinary",new CoverageBin<int>("all",x=>true));var all=new CoverageBin<int>[] {new("zero",x=>true),new("one",x=>true)};var cross=c.AddCross("cross",all,all);c.Merge(new([new("cross",ulong.MaxValue,0,cross.Snapshot().Bins.Select(b=>b with{Hits=ulong.MaxValue}).ToArray())]));Assert.Throws<OverflowException>(()=>c.Merge(new([new("ordinary",1,0,[new("all",1,1)]),new("cross",1,0,cross.Snapshot().Bins.Select(b=>b with{Hits=1}).ToArray())])));Assert.Equal(0UL,ordinary.Snapshot().Samples);Assert.Equal(ulong.MaxValue,cross.Snapshot().Samples);}
    [Fact] public void C028_MismatchedCrossGoalCannotPartiallyMerge()
    {var c=new FunctionalCoverage();var target=Cross(c);var source=new FunctionalCoverage().AddCross("cross",Axis(),Axis(),goal:2);source.Sample((0,0));Assert.Throws<ArgumentException>(()=>c.Merge(new([source.Snapshot()])));Assert.Equal(0UL,target.Snapshot().Samples);Assert.All(target.Snapshot().Bins,b=>Assert.Equal(0UL,b.Hits));}
    [Fact] public void C029_LeftPredicateFailureRollsBackAndRecovers()
    {bool fail=true;var marker=new IOException("left");var p=new FunctionalCoverage().AddCross("p",new CoverageBin<int>[] {new("l",x=>fail?throw marker:true)},Axis());Assert.Same(marker,Assert.Throws<IOException>(()=>p.Sample((1,0))));Assert.Equal(0UL,p.Snapshot().Samples);fail=false;p.Sample((1,0));Assert.Equal(new ulong[]{1,0},p.Snapshot().Bins.Select(b=>b.Hits));}
    [Fact] public void C030_LateRightPredicateFailureCannotCommitEarlierCellHit()
    {bool fail=true;var p=new FunctionalCoverage().AddCross("p",new CoverageBin<int>[] {new("l",x=>true)},new CoverageBin<int>[] {new("early",x=>true),new("late",x=>fail?throw new FormatException():false)});Assert.Throws<FormatException>(()=>p.Sample((1,2)));Assert.Equal(0UL,p.Snapshot().Samples);Assert.All(p.Snapshot().Bins,b=>Assert.Equal(0UL,b.Hits));fail=false;p.Sample((1,2));Assert.Equal(new ulong[]{1,0},p.Snapshot().Bins.Select(b=>b.Hits));}
}
