using System.Globalization;
using System.Text;

namespace SharpTestBench;

public sealed partial class FunctionalCoverage
{
    /// <summary>Create a bounded Cartesian cross of one to 32 classifier axes over the same sample type.</summary>
    /// <remarks>Each axis classifies the whole sample. Predicates must be pure and may be evaluated repeatedly. The full product is bounded before exclusions. Coordinates identify bins in caller-supplied axis order; snapshots retain the flat existing schema.</remarks>
    public CoveragePoint<T> AddCross<T>(string name, CoverageBin<T>[][] axes,
        ulong goal = 1, int maximumBins = 4096, params string[][] excluded)
    {
        CheckThread();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (points.ContainsKey(name)) throw new ArgumentException("Duplicate coverage point", nameof(name));
        ArgumentNullException.ThrowIfNull(axes);
        ArgumentNullException.ThrowIfNull(excluded);
        if (goal == 0) throw new ArgumentOutOfRangeException(nameof(goal));
        if (maximumBins < 1) throw new ArgumentOutOfRangeException(nameof(maximumBins));
        if (axes.Length is < 1 or > 32) throw new ArgumentException("Cross requires one to 32 axes", nameof(axes));
        var copied = (CoverageBin<T>[][])axes.Clone();
        var names = new HashSet<string>[copied.Length];
        for (int axis = 0; axis < copied.Length; axis++)
        {
            if (copied[axis] is null || copied[axis].Length == 0) throw new ArgumentException("Cross axes must not be null or empty", nameof(axes));
            copied[axis] = (CoverageBin<T>[])copied[axis].Clone();
            names[axis] = new(StringComparer.Ordinal);
            foreach (var bin in copied[axis])
                if (bin is null || string.IsNullOrWhiteSpace(bin.Name) || bin.Matches is null || bin.Goal == 0 || !names[axis].Add(bin.Name))
                    throw new ArgumentException("Axes need unique named predicates and positive goals", nameof(axes));
        }
        long product = 1;
        foreach (var axis in copied)
        {
            product *= axis.Length; // Previous product <= Int32 maximum; no Int64 overflow.
            if (product > maximumBins) throw new ArgumentOutOfRangeException(nameof(maximumBins), "Full cross product exceeds the bin limit");
        }
        static string Identity(IEnumerable<string> coordinate)
        {
            var value = new StringBuilder();
            foreach (string item in coordinate) value.Append(item.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(item);
            return value.ToString();
        }
        var omitted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var coordinate in excluded)
        {
            if (coordinate is null || coordinate.Length != copied.Length) throw new ArgumentException("Exclusion rank must match axis count", nameof(excluded));
            var snapshot = (string[])coordinate.Clone();
            for (int axis = 0; axis < snapshot.Length; axis++)
                if (snapshot[axis] is null || !names[axis].Contains(snapshot[axis])) throw new ArgumentException("Unknown excluded coordinate", nameof(excluded));
            if (!omitted.Add(Identity(snapshot))) throw new ArgumentException("Duplicate excluded coordinate", nameof(excluded));
        }
        var indices = new int[copied.Length];
        var cells = new List<CoverageBin<T>>();
        for (long cell = 0; cell < product; cell++)
        {
            var selected = new CoverageBin<T>[copied.Length];
            for (int axis = 0; axis < selected.Length; axis++) selected[axis] = copied[axis][indices[axis]];
            string identity = Identity(selected.Select(bin => bin.Name));
            if (!omitted.Contains(identity))
            {
                bool Matches(T sample)
                {
                    foreach (var bin in selected) if (!bin.Matches(sample)) return false;
                    return true;
                }
                cells.Add(new(identity, Matches, goal));
            }
            for (int axis = indices.Length - 1; axis >= 0; axis--)
            {
                if (++indices[axis] < copied[axis].Length) break;
                indices[axis] = 0;
            }
        }
        if (cells.Count == 0) throw new ArgumentException("Exclusions remove every cell", nameof(excluded));
        return AddPoint(name, cells.ToArray());
    }
}
