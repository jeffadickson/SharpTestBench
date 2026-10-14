namespace SharpTestBench;

public sealed record CoverageBin<T>(string Name, Func<T, bool> Matches, ulong Goal = 1);
public sealed record CoverageBinSnapshot(string Name, ulong Hits, ulong Goal)
{
    public bool Covered => Hits >= Goal;
}
public sealed record CoveragePointSnapshot(string Name, ulong Samples, ulong Unmatched, CoverageBinSnapshot[] Bins)
{
    public double Percent => 100.0 * Bins.Count(b => b.Covered) / Bins.Length;
}
public sealed record CoverageSnapshot(CoveragePointSnapshot[] Points)
{
    public double? Percent => Points.Length == 0 ? null :
        100.0 * Points.Sum(p => p.Bins.Count(b => b.Covered)) / Points.Sum(p => p.Bins.Length);
}

/// <summary>Explicit functional coverage, with typed samples and caller-defined bins.
/// It measures observed scenarios, not managed or HDL code coverage.</summary>
public sealed class FunctionalCoverage
{
    readonly int owner = Environment.CurrentManagedThreadId;
    readonly Dictionary<string, Func<CoveragePointSnapshot>> points = new(StringComparer.Ordinal);
    void CheckThread()
    {
        if (Environment.CurrentManagedThreadId != owner)
            throw new InvalidOperationException("Coverage must be accessed on its creating thread");
    }

    public CoveragePoint<T> AddPoint<T>(string name, params CoverageBin<T>[] bins)
    {
        CheckThread();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (points.ContainsKey(name)) throw new ArgumentException("Duplicate coverage point", nameof(name));
        var point = new CoveragePoint<T>(name, bins, CheckThread);
        points.Add(name, point.Snapshot);
        return point;
    }

    public CoverageSnapshot Snapshot()
    {
        CheckThread();
        return new(points.Values.Select(snapshot => snapshot()).ToArray());
    }
}

public sealed class CoveragePoint<T>
{
    readonly string name;
    readonly CoverageBin<T>[] bins;
    readonly ulong[] hits;
    readonly bool[] matched;
    readonly Action checkThread;
    ulong samples, unmatched;
    bool sampling;
    internal CoveragePoint(string name, CoverageBin<T>[] bins, Action checkThread)
    {
        ArgumentNullException.ThrowIfNull(bins);
        if (bins.Length == 0) throw new ArgumentException("At least one coverage bin is required", nameof(bins));
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var bin in bins)
        {
            ArgumentNullException.ThrowIfNull(bin);
            ArgumentException.ThrowIfNullOrWhiteSpace(bin.Name);
            ArgumentNullException.ThrowIfNull(bin.Matches);
            if (!names.Add(bin.Name) || bin.Goal == 0)
                throw new ArgumentException("Bins require unique names and positive goals", nameof(bins));
        }
        this.name = name;
        this.bins = (CoverageBin<T>[])bins.Clone();
        this.checkThread = checkThread;
        hits = new ulong[bins.Length];
        matched = new bool[bins.Length];
    }

    /// <summary>Each matching bin receives a hit; overlaps are allowed. Failed predicates
    /// and counter overflow leave all counters unchanged. Predicates must be pure.</summary>
    public void Sample(T value)
    {
        checkThread();
        if (sampling) throw new InvalidOperationException("Recursive sampling of one point is unsupported");
        sampling = true;
        try
        {
            bool any = false;
            for (int i = 0; i < bins.Length; i++)
            {
                matched[i] = bins[i].Matches(value);
                any |= matched[i];
                if (matched[i] && hits[i] == ulong.MaxValue) throw new OverflowException("Coverage counter overflow");
            }
            ulong nextSamples = checked(samples + 1);
            ulong nextUnmatched = any ? unmatched : checked(unmatched + 1);
            for (int i = 0; i < bins.Length; i++) if (matched[i]) hits[i]++;
            samples = nextSamples;
            unmatched = nextUnmatched;
        }
        finally { sampling = false; }
    }

    public CoveragePointSnapshot Snapshot()
    {
        checkThread();
        return new(name, samples, unmatched,
            bins.Select((bin, i) => new CoverageBinSnapshot(bin.Name, hits[i], bin.Goal)).ToArray());
    }
}
