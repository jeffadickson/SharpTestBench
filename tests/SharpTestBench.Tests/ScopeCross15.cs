using SharpTestBench;
using Xunit;
public sealed partial class ScopeCrossTests
{
    [Fact] public void S011_FourAxisExcludedPlaneCountsOnlySurvivingCells()
    {string[][] excluded=Enumerable.Range(0,8).Select(i=>new[]{"one",(i&1)==0?"zero":"one",(i&2)==0?"zero":"one",(i&4)==0?"zero":"one"}).ToArray();var p=new FunctionalCoverage().AddCross("four",Axes(4),excluded:excluded);for(int i=0;i<16;i++)p.Sample(i);Assert.Equal(8,p.Snapshot().Bins.Length);Assert.Equal(8UL,p.Snapshot().Unmatched);Assert.Equal(16UL,p.Snapshot().Samples);Assert.All(p.Snapshot().Bins,b=>Assert.Equal(1UL,b.Hits));Assert.Equal(100,p.Snapshot().Percent);}
    [Fact] public void S012_OuterAxisReplacementCannotChangeConstructedClassifiers()
    {var axes=Axes(3);var p=new FunctionalCoverage().AddCross("outer",axes);axes[0]=[B("bad",_=>throw new IOException())];for(int i=0;i<8;i++)p.Sample(i);Assert.Equal(100,p.Snapshot().Percent);Assert.All(p.Snapshot().Bins,b=>Assert.Equal(1UL,b.Hits));}
    [Fact] public void S013_InnerBinReplacementCannotChangeConstructedCells()
    {var axes=Axes(3);var inner=axes[2];var p=new FunctionalCoverage().AddCross("inner",axes);inner[0]=B("replacement",_=>throw new IOException());for(int i=0;i<8;i++)p.Sample(i);Assert.Equal(100,p.Snapshot().Percent);Assert.DoesNotContain(p.Snapshot().Bins,b=>b.Name.Contains("replacement",StringComparison.Ordinal));}
    [Fact] public void S014_ExcludedCoordinateArraysAreSnapshottedDeeply()
    {string[][] omitted=[["one","one","one"]];var p=new FunctionalCoverage().AddCross("copy",Axes(3),excluded:omitted);omitted[0][0]="zero";omitted[0]=["missing"];p.Sample(7);p.Sample(6);Assert.Equal(2UL,p.Snapshot().Samples);Assert.Equal(1UL,p.Snapshot().Unmatched);Assert.Equal(1UL,p.Snapshot().Bins.Aggregate(0UL,(sum,b)=>sum+b.Hits));Assert.Equal(1UL,p.Snapshot().Bins.Single(b=>b.Name=="4:zero3:one3:one").Hits);}
    [Fact] public void S015_InvalidAxisGoalCannotReserveNameOrInvokePredicates()
    {var c=new FunctionalCoverage();int calls=0;CoverageBin<int>[][] axes=[[B("a",_=>{calls++;return true;})],[B("b",_=>true)],[new("c",_=>true,0)]];Assert.Throws<ArgumentException>(()=>c.AddCross("retry",axes));Assert.Empty(c.Snapshot().Points);Assert.Equal(0,calls);axes[2]=[B("c",_=>true)];var p=c.AddCross("retry",axes);p.Sample(0);Assert.Equal(100,p.Snapshot().Percent);}
}
