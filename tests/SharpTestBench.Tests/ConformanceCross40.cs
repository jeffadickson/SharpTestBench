using SharpTestBench;
using Xunit;
using System.Text.Json;
public sealed partial class ConformanceCrossTests
{
    [Fact] public void C031_ReentrantCrossSampleFailsWithoutPoisoningLaterSample()
    {bool recurse=true;CoveragePoint<(int Left,int Right)>? p=null;p=new FunctionalCoverage().AddCross("p",new CoverageBin<int>[] {new("l",x=>{if(recurse)p!.Sample((1,2));return true;})},new CoverageBin<int>[] {new("r",x=>true)});Assert.Throws<InvalidOperationException>(()=>p.Sample((1,2)));Assert.Equal(0UL,p.Snapshot().Samples);recurse=false;p.Sample((1,2));Assert.Equal(1UL,p.Snapshot().Bins[0].Hits);}
    [Fact] public void C032_ActiveCrossCannotResetItsOwnCounters()
    {bool reset=true;CoveragePoint<(int Left,int Right)>? p=null;p=new FunctionalCoverage().AddCross("p",new CoverageBin<int>[] {new("l",x=>{if(reset)p!.Reset();return true;})},new CoverageBin<int>[] {new("r",x=>true)});Assert.Throws<InvalidOperationException>(()=>p.Sample((1,2)));Assert.Equal(0UL,p.Snapshot().Samples);reset=false;p.Sample((1,2));Assert.Equal(1UL,p.Snapshot().Bins[0].Hits);}
    [Fact] public void C033_EmptySuiteMergeStillRejectsActiveCrossSampling()
    {var c=new FunctionalCoverage();bool merge=true;var p=c.AddCross("p",new CoverageBin<int>[] {new("l",x=>{if(merge)c.Merge(new([]));return true;})},new CoverageBin<int>[] {new("r",x=>true)});Assert.Throws<InvalidOperationException>(()=>p.Sample((1,2)));Assert.Equal(0UL,p.Snapshot().Samples);merge=false;p.Sample((1,2));Assert.Equal(1UL,p.Snapshot().Samples);}
    [Fact] public void C034_SnapshotDuringClassificationObservesOnlyCommittedCounters()
    {CoveragePointSnapshot? observed=null;CoveragePoint<(int Left,int Right)>? p=null;p=new FunctionalCoverage().AddCross("p",new CoverageBin<int>[] {new("l",x=>{observed=p!.Snapshot();return true;})},new CoverageBin<int>[] {new("r",x=>true)});p.Sample((1,2));Assert.Equal(0UL,observed!.Samples);Assert.Equal(0UL,observed.Bins[0].Hits);p.Sample((1,2));Assert.Equal(1UL,observed.Samples);Assert.Equal(2UL,p.Snapshot().Samples);}
    [Fact] public void C035_OverlappingCrossMergeAcceptsMoreHitsThanSampleCount()
    {var all=new CoverageBin<int>[] {new("a",x=>true),new("b",x=>true)};var source=new FunctionalCoverage().AddCross("p",all,all);source.Sample((1,2));var c=new FunctionalCoverage();var target=c.AddCross("p",all,all);c.Merge(new([source.Snapshot()]));Assert.Equal(1UL,target.Snapshot().Samples);Assert.Equal(0UL,target.Snapshot().Unmatched);Assert.All(target.Snapshot().Bins,b=>Assert.Equal(1UL,b.Hits));}
    [Fact] public void C036_ExclusionsCannotEvadeTheFullProductBudget()
    {var c=new FunctionalCoverage();var omitted=new[]{("zero","zero"),("zero","one"),("one","zero")};Assert.Throws<ArgumentOutOfRangeException>(()=>c.AddCross("p",Axis(),Axis(),maximumBins:1,excluded:omitted));Assert.Empty(c.Snapshot().Points);Assert.Single(c.AddCross("p",Axis(),Axis(),maximumBins:4,excluded:omitted).Snapshot().Bins);}
    [Fact] public void C037_SuitePercentWeightsEveryCrossCellInsteadOfPointAverages()
    {var c=new FunctionalCoverage();var ordinary=c.AddPoint<int>("ordinary",new CoverageBin<int>("all",x=>true));var p=Cross(c);ordinary.Sample(1);p.Sample((0,0));Assert.Equal(100d,ordinary.Snapshot().Percent);Assert.Equal(25d,p.Snapshot().Percent);Assert.Equal(40d,c.Snapshot().Percent);}
    [Fact] public void C038_DuplicatePointNameCannotReplaceExistingOrdinaryCoverage()
    {var c=new FunctionalCoverage();var ordinary=c.AddPoint<int>("p",new CoverageBin<int>("all",x=>true));ordinary.Sample(3);Assert.Throws<ArgumentException>(()=>c.AddCross("p",Axis(),Axis()));Assert.Single(c.Snapshot().Points);Assert.Equal("all",c.Snapshot().Points[0].Bins[0].Name);Assert.Equal(1UL,ordinary.Snapshot().Samples);}
    [Fact] public void C039_UnicodeAndControlNamesSurviveSnapshotJsonRoundtrip()
    {var c=new FunctionalCoverage();var p=c.AddCross("λ",new CoverageBin<int>[] {new("λ",x=>true)},new CoverageBin<int>[] {new("line\n\"\\",x=>true)});p.Sample((1,2));var copy=JsonSerializer.Deserialize<CoverageSnapshot>(JsonSerializer.Serialize(c.Snapshot()))!;Assert.Equal("λ",copy.Points[0].Name);Assert.Equal(p.Snapshot().Bins[0],copy.Points[0].Bins[0]);Assert.Equal(1UL,copy.Points[0].Samples);}
    [Fact] public void C040_ReferencePayloadsReachClassifiersWithoutEqualityOrStringConversion()
    {var l=new HostilePayload();var r=new HostilePayload();var p=new FunctionalCoverage().AddCross("p",new CoverageBin<HostilePayload>[] {new("l",x=>ReferenceEquals(x,l))},new CoverageBin<HostilePayload>[] {new("r",x=>ReferenceEquals(x,r))});p.Sample((l,r));Assert.Equal(1UL,p.Snapshot().Bins[0].Hits);}
    sealed class HostilePayload {public override bool Equals(object? other)=>throw new IOException();public override int GetHashCode()=>throw new IOException();public override string ToString()=>throw new IOException();}
}
