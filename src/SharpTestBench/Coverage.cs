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
    readonly Dictionary<string, (Func<CoveragePointSnapshot> Snapshot, Action CheckReset, Action Reset, Func<CoveragePointSnapshot, Action> PrepareMerge)> points = new(StringComparer.Ordinal);
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
        points.Add(name, (point.Snapshot, point.CheckReset, point.Reset, point.PrepareMerge));
        return point;
    }

    /// <summary>Create bounded Cartesian bins from two classifier axes. Excluded name pairs are omitted before sampling.</summary>
    /// <remarks>Axis predicates must be pure; axis goals are validated but the independent cross goal is used. Bin names use invariant length prefixes. The full product is bounded before exclusions; at least one cell must remain.</remarks>
    public CoveragePoint<(TLeft Left, TRight Right)> AddCross<TLeft, TRight>(string name,
        CoverageBin<TLeft>[] left, CoverageBin<TRight>[] right, ulong goal = 1,
        int maximumBins = 4096, params (string Left, string Right)[] excluded)
    {
        CheckThread();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (points.ContainsKey(name)) throw new ArgumentException("Duplicate coverage point", nameof(name));
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ArgumentNullException.ThrowIfNull(excluded);
        if (goal == 0) throw new ArgumentOutOfRangeException(nameof(goal));
        if (maximumBins < 1) throw new ArgumentOutOfRangeException(nameof(maximumBins));
        var leftBins = (CoverageBin<TLeft>[])left.Clone();
        var rightBins = (CoverageBin<TRight>[])right.Clone();
        static HashSet<string> Validate<T>(CoverageBin<T>[] bins, string parameter)
        {
            if (bins.Length == 0) throw new ArgumentException("Cross axis must not be empty", parameter);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var bin in bins)
                if (bin is null || string.IsNullOrWhiteSpace(bin.Name) || bin.Matches is null || bin.Goal == 0 || !names.Add(bin.Name))
                    throw new ArgumentException("Cross axis needs unique named predicates and positive goals", parameter);
            return names;
        }
        var leftNames = Validate(leftBins, nameof(left));
        var rightNames = Validate(rightBins, nameof(right));
        if ((long)leftBins.Length * rightBins.Length > maximumBins)
            throw new ArgumentOutOfRangeException(nameof(maximumBins), "Cross product exceeds the bin limit");
        var omitted = new HashSet<(string Left, string Right)>();
        foreach (var pair in excluded)
            if (pair.Left is null || pair.Right is null || !leftNames.Contains(pair.Left) || !rightNames.Contains(pair.Right) || !omitted.Add(pair))
                throw new ArgumentException("Cross exclusions must be unique existing axis pairs", nameof(excluded));
        var bins = new List<CoverageBin<(TLeft Left, TRight Right)>>();
        foreach (var x in leftBins)
            foreach (var y in rightBins)
            {
                if (omitted.Contains((x.Name, y.Name))) continue;
                string cell = string.Concat(x.Name.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), ":", x.Name,
                    y.Name.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), ":", y.Name);
                bins.Add(new(cell, value => x.Matches(value.Left) && y.Matches(value.Right), goal));
            }
        if (bins.Count == 0) throw new ArgumentException("Cross exclusions remove every cell", nameof(excluded));
        return AddPoint(name, bins.ToArray());
    }


    /// <summary>Clear sample counters while retaining definitions. Validation is atomic across all points.</summary>
    public void Reset()
    {
        CheckThread();
        foreach (var point in points.Values) point.CheckReset();
        foreach (var point in points.Values) point.Reset();
    }

    /// <summary>Add a subset of named point snapshots with matching bin names/goals. Validation and overflow checks are atomic.</summary>
    public void Merge(CoverageSnapshot snapshot)
    {
        CheckThread();
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Points);
        foreach (var point in points.Values) point.CheckReset();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var apply = new List<Action>();
        foreach (var source in snapshot.Points)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentException.ThrowIfNullOrWhiteSpace(source.Name);
            if (!seen.Add(source.Name) || !points.TryGetValue(source.Name, out var target))
                throw new ArgumentException("Coverage snapshot has duplicate or unknown points", nameof(snapshot));
            apply.Add(target.PrepareMerge(source));
        }
        foreach (var commit in apply) commit();
    }

    public CoverageSnapshot Snapshot()
    {
        CheckThread();
        return new(points.Values.Select(point => point.Snapshot()).ToArray());
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

    internal void CheckReset()
    {
        checkThread();
        if (sampling) throw new InvalidOperationException("Cannot reset coverage during sampling");
    }

    /// <summary>Clear this point's counters while retaining its bin definitions and goals.</summary>
    public void Reset()
    {
        CheckReset();
        Array.Clear(hits);
        samples = unmatched = 0;
    }

    internal Action PrepareMerge(CoveragePointSnapshot source)
    {
        CheckReset();
        ArgumentNullException.ThrowIfNull(source.Bins);
        if (source.Bins.Length != bins.Length || source.Unmatched > source.Samples)
            throw new ArgumentException("Coverage snapshot has inconsistent counters or bins", nameof(source));
        var byName = new Dictionary<string, CoverageBinSnapshot>(StringComparer.Ordinal);
        ulong unaccountedMatches = source.Samples - source.Unmatched;
        foreach (var bin in source.Bins)
        {
            ArgumentNullException.ThrowIfNull(bin);
            ArgumentException.ThrowIfNullOrWhiteSpace(bin.Name);
            if (!byName.TryAdd(bin.Name, bin) || bin.Hits > source.Samples - source.Unmatched)
                throw new ArgumentException("Coverage snapshot has duplicate bins or impossible hits", nameof(source));
            unaccountedMatches = bin.Hits >= unaccountedMatches ? 0 : unaccountedMatches - bin.Hits;
        }
        if (unaccountedMatches != 0) throw new ArgumentException("Coverage snapshot has unaccounted matched samples", nameof(source));
        var nextHits = new ulong[hits.Length];
        for (int i = 0; i < bins.Length; i++)
        {
            if (!byName.TryGetValue(bins[i].Name, out var bin) || bin.Goal != bins[i].Goal)
                throw new ArgumentException("Coverage snapshot bin names and goals must match", nameof(source));
            nextHits[i] = checked(hits[i] + bin.Hits);
        }
        ulong nextSamples = checked(samples + source.Samples);
        ulong nextUnmatched = checked(unmatched + source.Unmatched);
        return () => { Array.Copy(nextHits, hits, hits.Length); samples = nextSamples; unmatched = nextUnmatched; };
    }

    public CoveragePointSnapshot Snapshot()
    {
        checkThread();
        return new(name, samples, unmatched,
            bins.Select((bin, i) => new CoverageBinSnapshot(bin.Name, hits[i], bin.Goal)).ToArray());
    }
}
