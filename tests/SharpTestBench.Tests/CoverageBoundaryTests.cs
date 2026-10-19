using SharpTestBench;
using Xunit;

public sealed class CoverageBoundaryTests
{
    [Fact]
    public void Goal1BelowBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 1));
        for (int i = 0; i < 0; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)0, snapshot.Samples);
        Assert.Equal((ulong)0, snapshot.Bins[0].Hits);
        Assert.False(snapshot.Bins[0].Covered);
        Assert.Equal(0d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal1AtBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 1));
        for (int i = 0; i < 1; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)1, snapshot.Samples);
        Assert.Equal((ulong)1, snapshot.Bins[0].Hits);
        Assert.True(snapshot.Bins[0].Covered);
        Assert.Equal(100d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal1AboveBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 1));
        for (int i = 0; i < 2; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)2, snapshot.Samples);
        Assert.Equal((ulong)2, snapshot.Bins[0].Hits);
        Assert.True(snapshot.Bins[0].Covered);
        Assert.Equal(100d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal2BelowBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 2));
        for (int i = 0; i < 1; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)1, snapshot.Samples);
        Assert.Equal((ulong)1, snapshot.Bins[0].Hits);
        Assert.False(snapshot.Bins[0].Covered);
        Assert.Equal(0d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal2AtBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 2));
        for (int i = 0; i < 2; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)2, snapshot.Samples);
        Assert.Equal((ulong)2, snapshot.Bins[0].Hits);
        Assert.True(snapshot.Bins[0].Covered);
        Assert.Equal(100d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal2AboveBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 2));
        for (int i = 0; i < 3; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)3, snapshot.Samples);
        Assert.Equal((ulong)3, snapshot.Bins[0].Hits);
        Assert.True(snapshot.Bins[0].Covered);
        Assert.Equal(100d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal3BelowBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 3));
        for (int i = 0; i < 2; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)2, snapshot.Samples);
        Assert.Equal((ulong)2, snapshot.Bins[0].Hits);
        Assert.False(snapshot.Bins[0].Covered);
        Assert.Equal(0d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal3AtBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 3));
        for (int i = 0; i < 3; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)3, snapshot.Samples);
        Assert.Equal((ulong)3, snapshot.Bins[0].Hits);
        Assert.True(snapshot.Bins[0].Covered);
        Assert.Equal(100d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal3AboveBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 3));
        for (int i = 0; i < 4; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)4, snapshot.Samples);
        Assert.Equal((ulong)4, snapshot.Bins[0].Hits);
        Assert.True(snapshot.Bins[0].Covered);
        Assert.Equal(100d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal5BelowBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 5));
        for (int i = 0; i < 4; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)4, snapshot.Samples);
        Assert.Equal((ulong)4, snapshot.Bins[0].Hits);
        Assert.False(snapshot.Bins[0].Covered);
        Assert.Equal(0d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal5AtBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 5));
        for (int i = 0; i < 5; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)5, snapshot.Samples);
        Assert.Equal((ulong)5, snapshot.Bins[0].Hits);
        Assert.True(snapshot.Bins[0].Covered);
        Assert.Equal(100d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal5AboveBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 5));
        for (int i = 0; i < 6; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)6, snapshot.Samples);
        Assert.Equal((ulong)6, snapshot.Bins[0].Hits);
        Assert.True(snapshot.Bins[0].Covered);
        Assert.Equal(100d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal8BelowBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 8));
        for (int i = 0; i < 7; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)7, snapshot.Samples);
        Assert.Equal((ulong)7, snapshot.Bins[0].Hits);
        Assert.False(snapshot.Bins[0].Covered);
        Assert.Equal(0d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal8AtBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 8));
        for (int i = 0; i < 8; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)8, snapshot.Samples);
        Assert.Equal((ulong)8, snapshot.Bins[0].Hits);
        Assert.True(snapshot.Bins[0].Covered);
        Assert.Equal(100d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal8AboveBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 8));
        for (int i = 0; i < 9; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)9, snapshot.Samples);
        Assert.Equal((ulong)9, snapshot.Bins[0].Hits);
        Assert.True(snapshot.Bins[0].Covered);
        Assert.Equal(100d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal13BelowBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 13));
        for (int i = 0; i < 12; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)12, snapshot.Samples);
        Assert.Equal((ulong)12, snapshot.Bins[0].Hits);
        Assert.False(snapshot.Bins[0].Covered);
        Assert.Equal(0d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal13AtBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 13));
        for (int i = 0; i < 13; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)13, snapshot.Samples);
        Assert.Equal((ulong)13, snapshot.Bins[0].Hits);
        Assert.True(snapshot.Bins[0].Covered);
        Assert.Equal(100d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal13AboveBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 13));
        for (int i = 0; i < 14; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)14, snapshot.Samples);
        Assert.Equal((ulong)14, snapshot.Bins[0].Hits);
        Assert.True(snapshot.Bins[0].Covered);
        Assert.Equal(100d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal21BelowBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 21));
        for (int i = 0; i < 20; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)20, snapshot.Samples);
        Assert.Equal((ulong)20, snapshot.Bins[0].Hits);
        Assert.False(snapshot.Bins[0].Covered);
        Assert.Equal(0d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal21AtBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 21));
        for (int i = 0; i < 21; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)21, snapshot.Samples);
        Assert.Equal((ulong)21, snapshot.Bins[0].Hits);
        Assert.True(snapshot.Bins[0].Covered);
        Assert.Equal(100d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void Goal21AboveBoundary()
    {
        var coverage = new FunctionalCoverage();
        var point = coverage.AddPoint<int>("point", new CoverageBin<int>("match", value => value == 7, 21));
        for (int i = 0; i < 22; i++) point.Sample(7);
        var snapshot = point.Snapshot();
        Assert.Equal((ulong)22, snapshot.Samples);
        Assert.Equal((ulong)22, snapshot.Bins[0].Hits);
        Assert.True(snapshot.Bins[0].Covered);
        Assert.Equal(100d, coverage.Snapshot().Percent);
        Assert.Equal(0UL, snapshot.Unmatched);
    }
    [Fact]
    public void InvalidPointName0()
    {
        var coverage = new FunctionalCoverage();
        Assert.ThrowsAny<ArgumentException>(() => coverage.AddPoint<int>(null!, new CoverageBin<int>("bin", _ => true)));
        Assert.Empty(coverage.Snapshot().Points);
    }
    [Fact]
    public void InvalidPointName1()
    {
        var coverage = new FunctionalCoverage();
        Assert.ThrowsAny<ArgumentException>(() => coverage.AddPoint<int>("", new CoverageBin<int>("bin", _ => true)));
        Assert.Empty(coverage.Snapshot().Points);
    }
    [Fact]
    public void InvalidPointName2()
    {
        var coverage = new FunctionalCoverage();
        Assert.ThrowsAny<ArgumentException>(() => coverage.AddPoint<int>(" ", new CoverageBin<int>("bin", _ => true)));
        Assert.Empty(coverage.Snapshot().Points);
    }
    [Fact]
    public void InvalidPointName3()
    {
        var coverage = new FunctionalCoverage();
        Assert.ThrowsAny<ArgumentException>(() => coverage.AddPoint<int>("\t", new CoverageBin<int>("bin", _ => true)));
        Assert.Empty(coverage.Snapshot().Points);
    }
    [Fact]
    public void InvalidPointName4()
    {
        var coverage = new FunctionalCoverage();
        Assert.ThrowsAny<ArgumentException>(() => coverage.AddPoint<int>("\r\n", new CoverageBin<int>("bin", _ => true)));
        Assert.Empty(coverage.Snapshot().Points);
    }
    [Fact]
    public void InvalidBinName0()
    {
        var coverage = new FunctionalCoverage();
        Assert.ThrowsAny<ArgumentException>(() => coverage.AddPoint<int>("point", new CoverageBin<int>(null!, _ => true)));
        Assert.Empty(coverage.Snapshot().Points);
    }
    [Fact]
    public void InvalidBinName1()
    {
        var coverage = new FunctionalCoverage();
        Assert.ThrowsAny<ArgumentException>(() => coverage.AddPoint<int>("point", new CoverageBin<int>("", _ => true)));
        Assert.Empty(coverage.Snapshot().Points);
    }
    [Fact]
    public void InvalidBinName2()
    {
        var coverage = new FunctionalCoverage();
        Assert.ThrowsAny<ArgumentException>(() => coverage.AddPoint<int>("point", new CoverageBin<int>(" ", _ => true)));
        Assert.Empty(coverage.Snapshot().Points);
    }
    [Fact]
    public void InvalidBinName3()
    {
        var coverage = new FunctionalCoverage();
        Assert.ThrowsAny<ArgumentException>(() => coverage.AddPoint<int>("point", new CoverageBin<int>("\t", _ => true)));
        Assert.Empty(coverage.Snapshot().Points);
    }
    [Fact]
    public void InvalidBinName4()
    {
        var coverage = new FunctionalCoverage();
        Assert.ThrowsAny<ArgumentException>(() => coverage.AddPoint<int>("point", new CoverageBin<int>("\r\n", _ => true)));
        Assert.Empty(coverage.Snapshot().Points);
    }
    [Fact]
    public void EmptySnapshot()
    {
        Assert.Null(new FunctionalCoverage().Snapshot().Percent);
    }
    [Fact]
    public void NullBins()
    {
        Assert.Throws<ArgumentNullException>(() => new FunctionalCoverage().AddPoint<int>("p", null!));
    }
    [Fact]
    public void EmptyBins()
    {
        Assert.Throws<ArgumentException>(() => new FunctionalCoverage().AddPoint<int>("p", []));
    }
    [Fact]
    public void NullBin()
    {
        Assert.Throws<ArgumentNullException>(() => new FunctionalCoverage().AddPoint<int>("p", new CoverageBin<int>[] { null! }));
    }
    [Fact]
    public void NullPredicate()
    {
        Assert.Throws<ArgumentNullException>(() => new FunctionalCoverage().AddPoint<int>("p", new CoverageBin<int>("b", null!)));
    }
    [Fact]
    public void ZeroGoal()
    {
        Assert.Throws<ArgumentException>(() => new FunctionalCoverage().AddPoint<int>("p", new CoverageBin<int>("b", _ => true, 0)));
    }
    [Fact]
    public void DuplicateBins()
    {
        Assert.Throws<ArgumentException>(() => new FunctionalCoverage().AddPoint<int>("p", new CoverageBin<int>("b", _ => true), new CoverageBin<int>("b", _ => false)));
    }
    [Fact]
    public void DuplicatePoint()
    {
        var c = new FunctionalCoverage();
        c.AddPoint<int>("p", new CoverageBin<int>("b", _ => true));
        Assert.Throws<ArgumentException>(() => c.AddPoint<int>("p", new CoverageBin<int>("other", _ => false)));
    }
    [Fact]
    public void UnmatchedSample()
    {
        var p = new FunctionalCoverage().AddPoint<int>("p", new CoverageBin<int>("b", x => x > 0));
        p.Sample(0);
        Assert.Equal(1UL, p.Snapshot().Unmatched);
        Assert.Equal(0UL, p.Snapshot().Bins[0].Hits);
    }
    [Fact]
    public void OverlappingBins()
    {
        var p = new FunctionalCoverage().AddPoint<int>("p", new CoverageBin<int>("a", _ => true), new CoverageBin<int>("b", _ => true));
        p.Sample(7);
        Assert.All(p.Snapshot().Bins, b => Assert.Equal(1UL, b.Hits));
        Assert.Equal(1UL, p.Snapshot().Samples);
    }
    [Fact]
    public void PredicateFailureAtomic()
    {
        var p = new FunctionalCoverage().AddPoint<int>("p", new CoverageBin<int>("a", _ => true), new CoverageBin<int>("b", _ => throw new FormatException()));
        Assert.Throws<FormatException>(() => p.Sample(7));
        Assert.All(p.Snapshot().Bins, b => Assert.Equal(0UL, b.Hits));
        Assert.Equal(0UL, p.Snapshot().Samples);
    }
}
