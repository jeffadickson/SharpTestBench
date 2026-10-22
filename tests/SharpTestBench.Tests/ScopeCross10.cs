using SharpTestBench;
using Xunit;
public sealed partial class ScopeCrossTests
{
    static CoverageBin<int> B(string name,Func<int,bool> match)=>new(name,match);
    static CoverageBin<int>[][] Axes(int rank)=>Enumerable.Range(0,rank).Select(axis=>new[]{B("zero",value=>(value&(1<<axis))==0),B("one",value=>(value&(1<<axis))!=0)}).ToArray();
    [Fact] public void S001_ThreeAxesCoverAllIndependentCoordinates()
    {var point=new FunctionalCoverage().AddCross("three",Axes(3));for(int i=0;i<8;i++)point.Sample(i);var s=point.Snapshot();Assert.Equal(8UL,s.Samples);Assert.Equal(8,s.Bins.Length);Assert.All(s.Bins,b=>Assert.Equal(1UL,b.Hits));Assert.Equal(100,s.Percent);Assert.Equal(0UL,s.Unmatched);}
    [Fact] public void S002_UnequalAxisSizesRetainLastAxisFastestOrder()
    {var axes=new[]{new[]{B("a",_=>true),B("b",_=>true)},new[]{B("x",_=>true),B("y",_=>true),B("z",_=>true)},new[]{B("p",_=>true),B("q",_=>true)}};var p=new FunctionalCoverage().AddCross("order",axes);Assert.Equal(new[]{"1:a1:x1:p","1:a1:x1:q","1:a1:y1:p","1:a1:y1:q","1:a1:z1:p","1:a1:z1:q","1:b1:x1:p","1:b1:x1:q","1:b1:y1:p","1:b1:y1:q","1:b1:z1:p","1:b1:z1:q"},p.Snapshot().Bins.Select(b=>b.Name));p.Sample(7);Assert.All(p.Snapshot().Bins,b=>Assert.Equal(1UL,b.Hits));}
    [Fact] public void S003_SingleAxisClassifiesWithoutTupleWrapping()
    {var p=new FunctionalCoverage().AddCross("one",Axes(1));p.Sample(0);p.Sample(1);Assert.Equal(new[]{"4:zero","3:one"},p.Snapshot().Bins.Select(b=>b.Name));Assert.Equal(100,p.Snapshot().Percent);}
    [Fact] public void S004_MaximumRankUsesIterativeSingletonConstruction()
    {var axes=Enumerable.Range(0,32).Select(i=>new[]{B("axis"+i,_=>true)}).ToArray();var p=new FunctionalCoverage().AddCross("deep",axes,maximumBins:1);p.Sample(9);Assert.Single(p.Snapshot().Bins);Assert.Equal(1UL,p.Snapshot().Bins[0].Hits);Assert.Equal(100,p.Snapshot().Percent);}
    [Fact] public void S005_OverlappingClassifiersCanHitEveryCellForOneSample()
    {var axes=Enumerable.Range(0,3).Select(_=>new[]{B("a",_=>true),B("b",_=>true)}).ToArray();var p=new FunctionalCoverage().AddCross("overlap",axes);p.Sample(0);Assert.Equal(1UL,p.Snapshot().Samples);Assert.Equal(8UL,p.Snapshot().Bins.Aggregate(0UL,(sum,b)=>sum+b.Hits));Assert.Equal(0UL,p.Snapshot().Unmatched);}
    [Fact] public void S006_PartialOverlapAndUnmatchedCountsAreIndependent()
    {CoverageBin<int>[][] axes=[[B("all-positive",x=>x>0),B("even-positive",x=>x>0&&x%2==0)],[B("small",x=>x<10)],[B("nonzero",x=>x!=0)]];var p=new FunctionalCoverage().AddCross("partial",axes);p.Sample(2);p.Sample(3);p.Sample(0);p.Sample(20);Assert.Equal(new ulong[]{2,1},p.Snapshot().Bins.Select(b=>b.Hits));Assert.Equal(4UL,p.Snapshot().Samples);Assert.Equal(2UL,p.Snapshot().Unmatched);}
    [Fact] public void S007_EarlierFalseAxisPreventsLaterClassifierInvocation()
    {int calls=0;CoverageBin<int>[][] axes=[[B("false",_=>false)],[B("error",_=>{calls++;throw new IOException();})],[B("tail",_=>true)]];var p=new FunctionalCoverage().AddCross("short",axes);p.Sample(4);Assert.Equal(0,calls);Assert.Equal(1UL,p.Snapshot().Unmatched);Assert.All(p.Snapshot().Bins,b=>Assert.Equal(0UL,b.Hits));}
    [Fact] public void S008_LateClassifierFailureRollsBackEarlierMatchedCells()
    {bool reject=false;var marker=new IOException("last axis");CoverageBin<int>[][] axes=[[B("all",_=>true)],[B("all",_=>true)],[B("first",_=>true),B("last",_=>reject?throw marker:true)]];var p=new FunctionalCoverage().AddCross("atomic",axes);p.Sample(1);reject=true;Assert.Same(marker,Assert.Throws<IOException>(()=>p.Sample(2)));Assert.Equal(1UL,p.Snapshot().Samples);Assert.All(p.Snapshot().Bins,b=>Assert.Equal(1UL,b.Hits));reject=false;p.Sample(3);Assert.Equal(2UL,p.Snapshot().Samples);Assert.All(p.Snapshot().Bins,b=>Assert.Equal(2UL,b.Hits));}
    [Fact] public void S009_ExcludedCoordinateIsUnmatchedWithoutRedistribution()
    {var p=new FunctionalCoverage().AddCross("excluded",Axes(3),excluded:[["one","one","one"]]);for(int i=0;i<8;i++)p.Sample(i);Assert.Equal(7,p.Snapshot().Bins.Length);Assert.Equal(8UL,p.Snapshot().Samples);Assert.Equal(1UL,p.Snapshot().Unmatched);Assert.All(p.Snapshot().Bins,b=>Assert.Equal(1UL,b.Hits));Assert.Equal(100,p.Snapshot().Percent);}
    [Fact] public void S010_FullyExcludedPlaneCannotInvokeItsClassifier()
    {int bad=0;CoverageBin<int>[][] axes=[[B("good",_=>true),B("bad",_=>{bad++;throw new IOException();})],[B("b",_=>true)],[B("c",_=>true)]];var p=new FunctionalCoverage().AddCross("plane",axes,excluded:[["bad","b","c"]]);p.Sample(0);Assert.Equal(0,bad);Assert.Single(p.Snapshot().Bins);Assert.Equal(100,p.Snapshot().Percent);}
}
