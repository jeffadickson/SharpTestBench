using SharpTestBench;
using Xunit;
public sealed partial class ScopeCrossTests
{
    [Fact] public void S016_DuplicatePointCannotReplaceExistingCross()
    {var c=new FunctionalCoverage();var p=c.AddCross("existing",Axes(3));Assert.Throws<ArgumentException>(()=>c.AddCross<int>("existing",null!));p.Sample(0);Assert.Single(c.Snapshot().Points);Assert.Equal(1UL,p.Snapshot().Samples);Assert.Equal(8,p.Snapshot().Bins.Length);}
    [Fact] public void S017_NullNestedAndEmptyAxisShapesAreAtomic()
    {var c=new FunctionalCoverage();Action[] invalid=[()=>c.AddCross<int>("shape",null!),()=>c.AddCross<int>("shape",[null!]),()=>c.AddCross<int>("shape",[[]])];foreach(var attempt in invalid){Assert.ThrowsAny<ArgumentException>(attempt);Assert.Empty(c.Snapshot().Points);}var p=c.AddCross("shape",Axes(3));p.Sample(0);Assert.Equal(1UL,p.Snapshot().Samples);}
    [Fact] public void S018_LateMalformedClassifiersValidateBeforeProductBudget()
    {CoverageBin<int>[] invalid=[null!,B("",_=>true),B(" \t",_=>true),new("null",null!),new("zero-goal",_=>true,0)];var c=new FunctionalCoverage();foreach(var bin in invalid){var axes=Axes(3);axes[2]=[bin];var error=Assert.Throws<ArgumentException>(()=>c.AddCross("definition",axes,maximumBins:1));Assert.Equal("axes",error.ParamName);Assert.Empty(c.Snapshot().Points);}var p=c.AddCross("definition",Axes(3));p.Sample(0);Assert.Equal(1UL,p.Snapshot().Samples);}
    [Fact] public void S019_DuplicateNamesWithinLateAxisRejectBeforeRegistration()
    {var c=new FunctionalCoverage();var axes=Axes(3);axes[2]=[B("same",_=>true),B("same",_=>false)];Assert.Throws<ArgumentException>(()=>c.AddCross("duplicate",axes));Assert.Empty(c.Snapshot().Points);c.AddCross("duplicate",Axes(3));Assert.Single(c.Snapshot().Points);}
    [Fact] public void S020_AxisNamesAreOrdinalAndMayRepeatAcrossDimensions()
    {var axes=Enumerable.Range(0,3).Select(_=>new[]{B("a",_=>true),B("A",_=>true)}).ToArray();var p=new FunctionalCoverage().AddCross("ordinal",axes);p.Sample(0);Assert.Equal(8,p.Snapshot().Bins.Select(b=>b.Name).Distinct(StringComparer.Ordinal).Count());Assert.Equal(100,p.Snapshot().Percent);Assert.All(p.Snapshot().Bins,b=>Assert.Equal(1UL,b.Hits));}
}
