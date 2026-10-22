using SharpTestBench;
using Xunit;
public sealed class ClosureCoverageResetTests
{
    static CoveragePoint<int> Point(FunctionalCoverage c,string name="point")=>c.AddPoint<int>(name,new CoverageBin<int>("even",x=>x%2==0,2),new CoverageBin<int>("positive",x=>x>0));
    static Exception? Foreign(Action action){Exception? error=null;var thread=new Thread(()=>{try{action();}catch(Exception caught){error=caught;}});thread.Start();thread.Join();return error;}
    [Fact] public void C001_ResetClearsHitsSamplesAndUnmatched()
    {var p=Point(new());p.Sample(2);p.Sample(-1);p.Reset();var s=p.Snapshot();Assert.Equal(0UL,s.Samples);Assert.Equal(0UL,s.Unmatched);Assert.All(s.Bins,b=>Assert.Equal(0UL,b.Hits));}
    [Fact] public void C002_ResetRetainsNamesGoalsAndPredicates()
    {var p=Point(new());p.Sample(2);p.Reset();p.Sample(4);var s=p.Snapshot();Assert.Equal("point",s.Name);Assert.Equal("even",s.Bins[0].Name);Assert.Equal(2UL,s.Bins[0].Goal);Assert.Equal(1UL,s.Bins[0].Hits);Assert.Equal(50,s.Percent);}
    [Fact] public void C003_PointResetDoesNotClearSibling()
    {var c=new FunctionalCoverage();var a=Point(c,"a");var b=Point(c,"b");a.Sample(2);b.Sample(2);a.Reset();Assert.Equal(0UL,a.Snapshot().Samples);Assert.Equal(1UL,b.Snapshot().Samples);}
    [Fact] public void C004_SuiteResetClearsMixedTypedPoints()
    {var c=new FunctionalCoverage();var a=Point(c);var b=c.AddPoint<string>("text",new CoverageBin<string>("word",x=>x=="ok"));a.Sample(2);b.Sample("ok");c.Reset();Assert.All(c.Snapshot().Points,p=>Assert.Equal(0UL,p.Samples));Assert.Equal(0,c.Snapshot().Percent);}
    [Fact] public void C005_EmptySuiteResetPreservesNullCoverage()
    {var c=new FunctionalCoverage();c.Reset();Assert.Null(c.Snapshot().Percent);Assert.Empty(c.Snapshot().Points);}
    [Fact] public void C006_ResetCannotRewritePriorSnapshot()
    {var p=Point(new());p.Sample(2);var old=p.Snapshot();p.Reset();p.Sample(-1);Assert.Equal(1UL,old.Samples);Assert.Equal(1UL,old.Bins[0].Hits);Assert.Equal(0UL,old.Unmatched);Assert.Equal(1UL,p.Snapshot().Unmatched);}
    [Fact] public void C007_ForeignPointResetPreservesCounters()
    {var p=Point(new());p.Sample(2);Assert.IsType<InvalidOperationException>(Foreign(p.Reset));Assert.Equal(1UL,p.Snapshot().Samples);}
    [Fact] public void C008_ForeignSuiteResetPreservesAllPoints()
    {var c=new FunctionalCoverage();var p=Point(c);p.Sample(2);Assert.IsType<InvalidOperationException>(Foreign(c.Reset));Assert.Equal(1UL,p.Snapshot().Bins[0].Hits);}
    [Fact] public void C009_ResetFromPredicateFailsAndSamplingRecovers()
    {bool reset=true;CoveragePoint<int>? p=null;p=new FunctionalCoverage().AddPoint<int>("p",new CoverageBin<int>("b",x=>{if(reset)p!.Reset();return true;}));Assert.Throws<InvalidOperationException>(()=>p.Sample(1));Assert.Equal(0UL,p.Snapshot().Samples);reset=false;p.Sample(1);Assert.Equal(1UL,p.Snapshot().Bins[0].Hits);}
    [Fact] public void C010_ReentrantSuiteResetIsAtomicAcrossEarlierPoints()
    {var c=new FunctionalCoverage();var earlier=Point(c,"earlier");earlier.Sample(2);bool reset=true;var last=c.AddPoint<int>("last",new CoverageBin<int>("b",x=>{if(reset)c.Reset();return true;}));Assert.Throws<InvalidOperationException>(()=>last.Sample(1));Assert.Equal(1UL,earlier.Snapshot().Samples);Assert.Equal(0UL,last.Snapshot().Samples);reset=false;last.Sample(1);c.Reset();Assert.All(c.Snapshot().Points,p=>Assert.Equal(0UL,p.Samples));}
}
