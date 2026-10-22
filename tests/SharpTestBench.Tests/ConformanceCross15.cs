using SharpTestBench;
using Xunit;
public sealed partial class ConformanceCrossTests
{
    [Fact] public void C011_ExcludedCellIsUnmatchedWithoutRedistributingHits()
    {var p=new FunctionalCoverage().AddCross("p",Axis(),Axis(),excluded:[("zero","one")]);p.Sample((0,1));p.Sample((1,1));var s=p.Snapshot();Assert.Equal(3,s.Bins.Length);Assert.Equal(2UL,s.Samples);Assert.Equal(1UL,s.Unmatched);Assert.Equal(new ulong[]{0,0,1},s.Bins.Select(b=>b.Hits));}
    [Fact] public void C012_DuplicateExclusionsRejectAtomically()
    {var c=new FunctionalCoverage();Assert.Throws<ArgumentException>(()=>c.AddCross("p",Axis(),Axis(),excluded:[("zero","one"),("zero","one")]));Assert.Empty(c.Snapshot().Points);Assert.NotNull(c.AddCross("p",Axis(),Axis()));}
    [Fact] public void C013_UnknownExclusionCoordinateCannotSilentlyDisappear()
    {var c=new FunctionalCoverage();Assert.Throws<ArgumentException>(()=>c.AddCross("p",Axis(),Axis(),excluded:[("missing","one")]));Assert.Throws<ArgumentException>(()=>c.AddCross("p",Axis(),Axis(),excluded:[("zero","missing")]));Assert.Throws<ArgumentException>(()=>c.AddCross("p",Axis(),Axis(),excluded:[(null!,"one")]));Assert.Throws<ArgumentException>(()=>c.AddCross("p",Axis(),Axis(),excluded:[("zero",null!)]));Assert.Empty(c.Snapshot().Points);}
    [Fact] public void C014_ExcludingTheEntireProductCannotCreateAnEmptyPoint()
    {var c=new FunctionalCoverage();Assert.Throws<ArgumentException>(()=>c.AddCross("p",Axis(),Axis(),excluded:[("zero","zero"),("zero","one"),("one","zero"),("one","one")]));Assert.Empty(c.Snapshot().Points);Assert.NotNull(c.AddCross("p",Axis(),Axis()));}
    [Fact] public void C015_ExcludedAxisRowNeverInvokesItsPredicate()
    {int calls=0;var left=new CoverageBin<int>[] {new("bad",x=>{calls++;throw new IOException();}),new("good",x=>x==0)};var p=new FunctionalCoverage().AddCross("p",left,Axis(),excluded:[("bad","zero"),("bad","one")]);p.Sample((0,0));Assert.Equal(0,calls);Assert.Equal(new ulong[]{1,0},p.Snapshot().Bins.Select(b=>b.Hits));}
}
