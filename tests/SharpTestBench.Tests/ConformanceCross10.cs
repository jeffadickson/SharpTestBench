using SharpTestBench;
using Xunit;
public sealed partial class ConformanceCrossTests
{
    static CoverageBin<int>[] Axis() => [new("zero",x=>x==0),new("one",x=>x==1)];
    static string Cell(string l,string r)=>$"{l.Length}:{l}{r.Length}:{r}";
    static CoveragePoint<(int Left,int Right)> Cross(FunctionalCoverage c)=>c.AddCross("cross",Axis(),Axis());
    [Fact] public void C001_CartesianCellsCoverEveryPartitionPair()
    {var p=Cross(new());foreach(int l in new[]{0,1})foreach(int r in new[]{0,1})p.Sample((l,r));var s=p.Snapshot();Assert.Equal(4UL,s.Samples);Assert.Equal(0UL,s.Unmatched);Assert.Equal(100d,s.Percent);Assert.All(s.Bins,b=>Assert.Equal(1UL,b.Hits));}
    [Fact] public void C002_CartesianOrderingIsLeftMajorAndDeterministic()
    {var p=new FunctionalCoverage().AddCross("p",Axis(),new CoverageBin<int>[] {new("a",x=>true),new("b",x=>true),new("c",x=>true)});Assert.Equal(new[]{"4:zero1:a","4:zero1:b","4:zero1:c","3:one1:a","3:one1:b","3:one1:c"},p.Snapshot().Bins.Select(b=>b.Name));}
    [Fact] public void C003_OverlappingAxesHitAllMatchingCellsButOneSample()
    {var p=new FunctionalCoverage().AddCross("p",new CoverageBin<int>[] {new("all",x=>true),new("positive",x=>x>0)},new CoverageBin<int>[] {new("all",x=>true),new("even",x=>x%2==0)});p.Sample((1,2));var s=p.Snapshot();Assert.Equal(1UL,s.Samples);Assert.All(s.Bins,b=>Assert.Equal(1UL,b.Hits));}
    [Fact] public void C004_UnclassifiedCoordinateProducesOneUnmatchedTuple()
    {var p=Cross(new());p.Sample((7,0));p.Sample((0,7));p.Sample((7,7));Assert.Equal(3UL,p.Snapshot().Unmatched);Assert.All(p.Snapshot().Bins,b=>Assert.Equal(0UL,b.Hits));}
    [Fact] public void C005_RightPredicateIsNotCalledForAnUnmatchedLeftCell()
    {int calls=0;var p=new FunctionalCoverage().AddCross("p",new CoverageBin<int>[] {new("left",x=>false)},new CoverageBin<int>[] {new("right",x=>{calls++;throw new IOException();})});p.Sample((1,2));Assert.Equal(0,calls);Assert.Equal(1UL,p.Snapshot().Unmatched);}
    [Fact] public void C006_CrossGoalIsIndependentOfAxisClassifierGoals()
    {Assert.Throws<ArgumentOutOfRangeException>(()=>new FunctionalCoverage().AddCross("invalid",Axis(),Axis(),goal:0));var p=new FunctionalCoverage().AddCross("p",new CoverageBin<int>[] {new("l",x=>true,99)},new CoverageBin<int>[] {new("r",x=>true,33)},goal:2);p.Sample((1,2));Assert.False(p.Snapshot().Bins[0].Covered);p.Sample((1,2));Assert.True(p.Snapshot().Bins[0].Covered);Assert.Equal(2UL,p.Snapshot().Bins[0].Goal);}
    [Fact] public void C007_CallerArrayReplacementCannotChangeClassifiers()
    {var left=Axis();var right=Axis();var p=new FunctionalCoverage().AddCross("p",left,right);left[0]=new("bad",x=>false);right[0]=new("bad",x=>false);p.Sample((0,0));Assert.Equal("4:zero4:zero",p.Snapshot().Bins[0].Name);Assert.Equal(1UL,p.Snapshot().Bins[0].Hits);}
    [Fact] public void C008_EmptyAxisRejectionLeavesPointNameAvailable()
    {var c=new FunctionalCoverage();Assert.Throws<ArgumentException>(()=>c.AddCross("p",Array.Empty<CoverageBin<int>>(),Axis()));Assert.Empty(c.Snapshot().Points);Assert.Equal("p",c.AddCross("p",Axis(),Axis()).Snapshot().Name);}
    [Fact] public void C009_DuplicateAxisNamesRejectWithoutRegistration()
    {var c=new FunctionalCoverage();Assert.Throws<ArgumentException>(()=>c.AddCross("p",new CoverageBin<int>[] {new("a",x=>true),new("a",x=>false)},Axis()));Assert.Empty(c.Snapshot().Points);Assert.NotNull(c.AddCross("p",Axis(),Axis()));}
    [Fact] public void C010_ProductBoundRejectsBeforeExecutingPredicates()
    {int calls=0;var a=new CoverageBin<int>[] {new("a",x=>{calls++;return true;}),new("b",x=>true)};var c=new FunctionalCoverage();Assert.Throws<ArgumentOutOfRangeException>(()=>c.AddCross("p",a,Axis(),maximumBins:0));Assert.Throws<ArgumentOutOfRangeException>(()=>c.AddCross("p",a,Axis(),maximumBins:-1));Assert.Throws<ArgumentOutOfRangeException>(()=>c.AddCross("p",a,Axis(),maximumBins:3));Assert.Equal(0,calls);Assert.Empty(c.Snapshot().Points);Assert.Equal(4,c.AddCross("p",a,Axis(),maximumBins:4).Snapshot().Bins.Length);}
}
