using System.Reflection;
using SharpTestBench;
using Xunit;

public sealed class CoverageSafetyTests
{
    static void Set(object point, string field, object value)
    {
        var info = point.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!;
        if (info.GetValue(point) is ulong[] hits && value is ulong[] desired) Array.Copy(desired, hits, hits.Length);
        else info.SetValue(point, value);
    }
    [Fact]
    public void HitOverflow()
    {
        var p = new FunctionalCoverage().AddPoint<int>("p", new CoverageBin<int>("b", x => x == 1));
        Set(p, "hits", new ulong[] { ulong.MaxValue });
        var before = p.Snapshot();
        Assert.Throws<OverflowException>(() => p.Sample(1));
        var after = p.Snapshot();
        Assert.Equal(before.Samples, after.Samples);
        Assert.Equal(before.Unmatched, after.Unmatched);
        Assert.Equal(before.Bins, after.Bins);
    }
    [Fact]
    public void SampleOverflow()
    {
        var p = new FunctionalCoverage().AddPoint<int>("p", new CoverageBin<int>("b", x => x == 1));
        Set(p, "samples", ulong.MaxValue);
        var before = p.Snapshot();
        Assert.Throws<OverflowException>(() => p.Sample(1));
        var after = p.Snapshot();
        Assert.Equal(before.Samples, after.Samples);
        Assert.Equal(before.Unmatched, after.Unmatched);
        Assert.Equal(before.Bins, after.Bins);
    }
    [Fact]
    public void UnmatchedOverflow()
    {
        var p = new FunctionalCoverage().AddPoint<int>("p", new CoverageBin<int>("b", x => x == 1));
        Set(p, "unmatched", ulong.MaxValue);
        var before = p.Snapshot();
        Assert.Throws<OverflowException>(() => p.Sample(0));
        var after = p.Snapshot();
        Assert.Equal(before.Samples, after.Samples);
        Assert.Equal(before.Unmatched, after.Unmatched);
        Assert.Equal(before.Bins, after.Bins);
    }
    [Fact]
    public void CollectionSnapshotThread()
    {
        var c = new FunctionalCoverage();
        var p = c.AddPoint<int>("p", new CoverageBin<int>("b", _ => true));
        Exception? error = null;
        var thread = new Thread(() => { try { c.Snapshot(); } catch (Exception caught) { error = caught; } });
        thread.Start(); thread.Join();
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(0UL, p.Snapshot().Samples);
    }
    [Fact]
    public void PointSnapshotThread()
    {
        var c = new FunctionalCoverage();
        var p = c.AddPoint<int>("p", new CoverageBin<int>("b", _ => true));
        Exception? error = null;
        var thread = new Thread(() => { try { p.Snapshot(); } catch (Exception caught) { error = caught; } });
        thread.Start(); thread.Join();
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(0UL, p.Snapshot().Samples);
    }
    [Fact]
    public void AddPointThread()
    {
        var c = new FunctionalCoverage();
        var p = c.AddPoint<int>("p", new CoverageBin<int>("b", _ => true));
        Exception? error = null;
        var thread = new Thread(() => { try { c.AddPoint<int>("other", new CoverageBin<int>("b", _ => true)); } catch (Exception caught) { error = caught; } });
        thread.Start(); thread.Join();
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(0UL, p.Snapshot().Samples);
    }
    [Fact]
    public void BinsAreCloned()
    {
        CoverageBin<int>[] bins = [new("original", x => x == 1)];
        var p = new FunctionalCoverage().AddPoint("p", bins);
        bins[0] = new("replacement", _ => false);
        p.Sample(1);
        Assert.Equal("original", p.Snapshot().Bins[0].Name);
        Assert.Equal(1UL, p.Snapshot().Bins[0].Hits);
    }
    [Fact]
    public void SnapshotsDoNotAliasCounters()
    {
        var p = new FunctionalCoverage().AddPoint<int>("p", new CoverageBin<int>("b", _ => true));
        var first = p.Snapshot();
        first.Bins[0] = new("tampered", 99, 1);
        p.Sample(0);
        Assert.Equal("b", p.Snapshot().Bins[0].Name);
        Assert.Equal(1UL, p.Snapshot().Bins[0].Hits);
        Assert.Equal(0UL, first.Samples);
    }
    [Fact]
    public void RecoverAfterPredicateFailure()
    {
        bool fail = true;
        var p = new FunctionalCoverage().AddPoint<int>("p", new CoverageBin<int>("b", _ => fail ? throw new FormatException() : true));
        Assert.Throws<FormatException>(() => p.Sample(0));
        fail = false;
        p.Sample(0);
        Assert.Equal(1UL, p.Snapshot().Samples);
        Assert.Equal(1UL, p.Snapshot().Bins[0].Hits);
    }
    [Fact]
    public void RecoverAfterRecursiveFailure()
    {
        bool recurse = true;
        CoveragePoint<int>? p = null;
        p = new FunctionalCoverage().AddPoint<int>("p", new CoverageBin<int>("b", x => { if (recurse) p!.Sample(x); return true; }));
        Assert.Throws<InvalidOperationException>(() => p.Sample(0));
        recurse = false;
        p.Sample(0);
        Assert.Equal(1UL, p.Snapshot().Samples);
        Assert.Equal(1UL, p.Snapshot().Bins[0].Hits);
    }
    [Fact]
    public void OwnDisposesResource()
    {
        var scope = new CleanupScope();
        var stream = new MemoryStream();
        Assert.Same(stream, scope.Own(stream));
        Assert.Throws<ArgumentNullException>(() => scope.Own<MemoryStream>(null!));
        var close = scope.CloseAsync(Xunit.TestContext.Current.CancellationToken);
        Assert.True(close.IsCompletedSuccessfully);
        Assert.False(stream.CanRead);
    }
    [Fact]
    public void OwnAsyncDisposesResource()
    {
        var scope = new CleanupScope();
        var stream = new MemoryStream();
        Assert.Same(stream, scope.OwnAsync(stream));
        Assert.Throws<ArgumentNullException>(() => scope.OwnAsync<MemoryStream>(null!));
        var close = scope.CloseAsync(Xunit.TestContext.Current.CancellationToken);
        Assert.True(close.IsCompletedSuccessfully);
        Assert.False(stream.CanRead);
    }
}
