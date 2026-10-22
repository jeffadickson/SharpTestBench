using SharpTestBench;
using Xunit;
public sealed class ClosureCoverageMergeTests
{
    static FunctionalCoverage Suite(out CoveragePoint<int> p){var c=new FunctionalCoverage();p=c.AddPoint<int>("p",new CoverageBin<int>("even",x=>x%2==0,2),new CoverageBin<int>("positive",x=>x>0));return c;}
    static CoverageSnapshot Source(ulong samples=2,ulong unmatched=1,ulong even=1,ulong positive=1,ulong goal=2)=>new([new("p",samples,unmatched,[new("even",even,goal),new("positive",positive,1)])]);
    static void Zero(CoveragePoint<int> p){Assert.Equal(0UL,p.Snapshot().Samples);Assert.All(p.Snapshot().Bins,b=>Assert.Equal(0UL,b.Hits));}
    [Fact] public void C011_MergeAddsSamplesUnmatchedAndOverlappingHits()
    {var c=Suite(out var p);p.Sample(2);c.Merge(Source());var s=p.Snapshot();Assert.Equal(3UL,s.Samples);Assert.Equal(1UL,s.Unmatched);Assert.Equal(new ulong[]{2,2},s.Bins.Select(b=>b.Hits));Assert.Equal(100,s.Percent);}
    [Fact] public void C012_MergeAcceptsReorderedBinsByIdentity()
    {var c=Suite(out var p);var s=Source();Array.Reverse(s.Points[0].Bins);c.Merge(s);Assert.Equal("even",p.Snapshot().Bins[0].Name);Assert.Equal(1UL,p.Snapshot().Bins[0].Hits);}
    [Fact] public void C013_SubsetMergeLeavesAbsentPointsUntouched()
    {var c=Suite(out var p);var sibling=c.AddPoint<int>("other",new CoverageBin<int>("b",x=>true));sibling.Sample(1);c.Merge(Source());Assert.Equal(2UL,p.Snapshot().Samples);Assert.Equal(1UL,sibling.Snapshot().Samples);}
    [Fact] public void C014_EmptyMergeIsNoOpOnExistingCounters()
    {var c=Suite(out var p);p.Sample(2);c.Merge(new([]));Assert.Equal(1UL,p.Snapshot().Samples);Assert.Equal(1UL,p.Snapshot().Bins[0].Hits);}
    [Fact] public void C015_MergedSnapshotDoesNotAliasCallerArrays()
    {var c=Suite(out var p);var s=Source();c.Merge(s);s.Points[0].Bins[0]=new("tampered",99,99);s.Points[0]=new("tampered",99,99,[]);Assert.Equal(2UL,p.Snapshot().Samples);Assert.Equal("even",p.Snapshot().Bins[0].Name);Assert.Equal(1UL,p.Snapshot().Bins[0].Hits);}
    [Fact] public void C016_MergeCannotRewritePreviouslyReturnedSnapshot()
    {var c=Suite(out var p);p.Sample(2);var old=p.Snapshot();c.Merge(Source());Assert.Equal(1UL,old.Samples);Assert.Equal(1UL,old.Bins[0].Hits);}
    [Fact] public void C017_ResetAfterMergeStartsNewSamplingWindow()
    {var c=Suite(out var p);c.Merge(Source());c.Reset();p.Sample(-1);Assert.Equal(1UL,p.Snapshot().Samples);Assert.Equal(1UL,p.Snapshot().Unmatched);Assert.All(p.Snapshot().Bins,b=>Assert.Equal(0UL,b.Hits));}
    [Fact] public void C018_MergeRejectsGoalMismatchWithoutPartialMutation()
    {var c=Suite(out var p);Assert.Throws<ArgumentException>(()=>c.Merge(Source(goal:3)));Zero(p);}
    [Fact] public void C019_MergeRejectsUnknownBinEvenWithCorrectBinCount()
    {var c=Suite(out var p);var s=Source();s.Points[0].Bins[1]=new("missing",1,1);Assert.Throws<ArgumentException>(()=>c.Merge(s));Zero(p);}
    [Fact] public void C020_DuplicateBinsCannotDoubleCountOneCounter()
    {var c=Suite(out var p);var s=Source();s.Points[0].Bins[1]=s.Points[0].Bins[0];Assert.Throws<ArgumentException>(()=>c.Merge(s));Zero(p);}
    [Fact] public void C021_UnknownLaterPointRollsBackEarlierValidPoint()
    {var c=Suite(out var p);var s=Source();Assert.Throws<ArgumentException>(()=>c.Merge(new([s.Points[0],new("missing",0,0,[new("b",0,1)])])));Zero(p);}
    [Fact] public void C022_DuplicatePointCannotApplySameCountsTwice()
    {var c=Suite(out var p);var s=Source();Assert.Throws<ArgumentException>(()=>c.Merge(new([s.Points[0],s.Points[0]])));Zero(p);}
    [Fact] public void C023_SampleOverflowRollsBackHitAdditions()
    {var c=Suite(out var p);c.Merge(Source(ulong.MaxValue,ulong.MaxValue,0,0));Assert.Throws<OverflowException>(()=>c.Merge(Source(1,0,1,1)));Assert.Equal(ulong.MaxValue,p.Snapshot().Samples);Assert.All(p.Snapshot().Bins,b=>Assert.Equal(0UL,b.Hits));}
    [Fact] public void C024_HitOverflowCannotPartiallyUpdateOtherBins()
    {var c=Suite(out var p);c.Merge(Source(ulong.MaxValue,0,ulong.MaxValue,0));Assert.Throws<OverflowException>(()=>c.Merge(Source(1,0,1,1)));Assert.Equal(0UL,p.Snapshot().Bins[1].Hits);Assert.Equal(ulong.MaxValue,p.Snapshot().Bins[0].Hits);}
    [Fact] public void C025_ImpossibleCountersRejectBeforeAnyAddition()
    {var c=Suite(out var p);Assert.Throws<ArgumentException>(()=>c.Merge(Source(1,2,0,0)));Assert.Throws<ArgumentException>(()=>c.Merge(Source(1,0,2,0)));Assert.Throws<ArgumentException>(()=>c.Merge(Source(1,1,1,0)));Assert.Throws<ArgumentException>(()=>c.Merge(Source(2,0,0,0)));Zero(p);}
    [Fact] public void C026_ForeignMergeCannotChangeCounters()
    {var c=Suite(out var p);Exception? error=null;var thread=new Thread(()=>{try{c.Merge(Source());}catch(Exception caught){error=caught;}});thread.Start();thread.Join();Assert.IsType<InvalidOperationException>(error);Zero(p);}
    [Fact] public void C027_MergeFromPredicateRejectsAndSamplingRecovers()
    {var c=new FunctionalCoverage();bool merge=true,siblingOnly=false;var sibling=c.AddPoint<int>("other",new CoverageBin<int>("b",_=>true));var p=c.AddPoint<int>("p",new CoverageBin<int>("b",x=>{if(merge)c.Merge(new([new(siblingOnly?"other":"p",1,0,[new("b",1,1)])]));return true;}));Assert.Throws<InvalidOperationException>(()=>p.Sample(1));Zero(p);siblingOnly=true;Assert.Throws<InvalidOperationException>(()=>p.Sample(1));Zero(p);Zero(sibling);merge=false;p.Sample(1);Assert.Equal(1UL,p.Snapshot().Samples);}
    [Fact] public void C028_NullSnapshotShapesFailBeforeChangingState()
    {var c=Suite(out var p);Assert.Throws<ArgumentNullException>(()=>c.Merge(null!));Assert.Throws<ArgumentNullException>(()=>c.Merge(new(null!)));Assert.Throws<ArgumentNullException>(()=>c.Merge(new([null!])));Assert.Throws<ArgumentNullException>(()=>c.Merge(new([new("p",0,0,null!)])));var s=Source();s.Points[0].Bins[0]=null!;Assert.Throws<ArgumentNullException>(()=>c.Merge(s));Zero(p);}
    [Fact] public void C029_ZeroSnapshotAndContinuedSamplingRespectGoals()
    {var c=Suite(out var p);c.Merge(Source(0,0,0,0));p.Sample(2);Assert.Equal(50,p.Snapshot().Percent);p.Sample(4);Assert.Equal(100,p.Snapshot().Percent);}
    [Fact] public void C030_LaterOverflowCannotCommitEarlierPoint()
    {var c=Suite(out var p);var last=c.AddPoint<int>("last",new CoverageBin<int>("b",x=>true));c.Merge(new([new("last",ulong.MaxValue,ulong.MaxValue,[new("b",0,1)])]));var s=Source();Assert.Throws<OverflowException>(()=>c.Merge(new([s.Points[0],new("last",1,0,[new("b",1,1)])])));Zero(p);Assert.Equal(ulong.MaxValue,last.Snapshot().Samples);}
}
