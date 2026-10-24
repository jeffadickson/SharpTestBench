using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SharpTestBench;

/// <summary>Explicit regression policy for declared skips. Backend and isolation guards remain independent.</summary>
public enum RegressionMode
{
    /// <summary>Honor each explicit skip declaration, retaining ordinary regression policy.</summary>
    Regression,
    /// <summary>Execute explicitly skipped declarations when selected; backend and isolation guards still apply.</summary>
    Testcase
}

/// <summary>Immutable, bounded ORed regex selection over qualified case names.</summary>
/// <remarks>Uses the .NET regex dialect with invariant culture and a per-match timeout.
/// This selection intersects existing stage, substring and exact-name selection.
/// Testcase mode overrides only an explicit SimTest.Skip declaration.</remarks>
public sealed class RegressionSelection
{
    /// <summary>Caps constructor enumeration and the number of OR alternatives (STB-SEL-002).</summary>
    public const int MaximumPatterns = 64;
    /// <summary>Per-pattern budget in UTF-16 code units, before regex parsing (STB-SEL-002).</summary>
    public const int MaximumPatternLength = 4096;
    readonly Regex[] expressions;
    /// <summary>Original patterns in input order, exposed through an immutable snapshot.</summary>
    public IReadOnlyList<string> Patterns { get; }
    /// <summary>Declared-skip policy only; this does not authorize unavailable backends.</summary>
    public RegressionMode Mode { get; }
    /// <summary>Finite budget for each regex match, not an aggregate suite deadline.</summary>
    public TimeSpan MatchTimeout { get; }
    /// <summary>Create an unrestricted filter while honoring explicit skips.</summary>
    public RegressionSelection() : this(Array.Empty<string>()) { }
    /// <summary>Copy and validate all patterns before compiling any selection used by a regression.</summary>
    /// <param name="patterns">OR alternatives; an empty sequence is unrestricted and an empty regex matches all.</param>
    /// <param name="mode">Explicit-skip policy, independent of backend and cleanup safeguards.</param>
    /// <param name="matchTimeout">Per-match timeout from one to 1000 milliseconds; default 20 milliseconds.</param>
    public RegressionSelection(IEnumerable<string> patterns, RegressionMode mode = RegressionMode.Regression, TimeSpan? matchTimeout = null)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        var timeout = matchTimeout ?? TimeSpan.FromMilliseconds(20);
        if (timeout < TimeSpan.FromMilliseconds(1) || timeout > TimeSpan.FromSeconds(1))
            throw new ArgumentOutOfRangeException(nameof(matchTimeout), "Regex timeout must be between one and 1000 milliseconds");
        ArgumentNullException.ThrowIfNull(patterns);
        var snapshot = new List<string>();
        foreach (string pattern in patterns)
        {
            if (snapshot.Count == MaximumPatterns) throw new ArgumentException("Too many regex patterns", nameof(patterns));
            if (pattern is null) throw new ArgumentException("Regex patterns must not be null", nameof(patterns));
            if (pattern.Length > MaximumPatternLength) throw new ArgumentException("Regex pattern exceeds the length limit", nameof(patterns));
            snapshot.Add(pattern);
        }
        expressions = snapshot.Select(pattern => new Regex(pattern, RegexOptions.CultureInvariant, timeout)).ToArray();
        Patterns = new ReadOnlyCollection<string>(snapshot.ToArray());
        Mode = mode;
        MatchTimeout = timeout;
    }
    /// <summary>Search a qualified case name, preserving case/Unicode text and propagating regex timeouts.</summary>
    public bool Matches(string qualifiedName)
    {
        ArgumentNullException.ThrowIfNull(qualifiedName);
        return expressions.Length == 0 || expressions.Any(expression => expression.IsMatch(qualifiedName));
    }
}

internal sealed record PlannedTest(MethodInfo Method, object[] Arguments, SimTestAttribute Attribute, TestDescriptor Descriptor);

internal static class RegressionPlanning
{
    internal static List<PlannedTest> Build(IEnumerable<MethodInfo> methods, RegressionSelection selection, ulong seed, Func<TestDescriptor, bool> include)
    {
        ArgumentNullException.ThrowIfNull(methods);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(include);
        var ordered = methods.Where(method => method.IsDefined(typeof(SimTestAttribute)))
            .OrderBy(method => method.GetCustomAttribute<SimTestAttribute>()!.Stage)
            .ThenBy(method => method.DeclaringType!.FullName + "." + method.Name, StringComparer.Ordinal);
        // Complete this plan before any test body is invoked: later selection failures must not leave earlier side effects.
        var planned = new List<PlannedTest>();
        ulong ordinal = 0;
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in ordered)
        {
            var attribute = method.GetCustomAttribute<SimTestAttribute>()!;
            var cases = method.GetCustomAttributes<SimCaseAttribute>().Select(item => item.Arguments).ToArray();
            if (cases.Length == 0) cases = [[]];
            for (int index = 0; index < cases.Length; index++)
            {
                string name = method.Name + (cases.Length > 1 ? $"[{index}]" : "");
                string qualifiedName = method.DeclaringType!.FullName + "." + name;
                if (!identities.Add(qualifiedName)) throw new InvalidOperationException("Duplicate qualified test identity: " + qualifiedName);
                // Allocate before filtering; overflow is intentionally modulo 2^64, matching existing seed semantics.
                ulong effectiveSeed = unchecked(seed + ordinal++);
                var descriptor = new TestDescriptor(name, qualifiedName, attribute.Stage, index,
                    effectiveSeed, attribute.TimeoutSteps, attribute.Skip, (string[])attribute.Simulators.Clone());
                if (include(descriptor) && selection.Matches(qualifiedName))
                    planned.Add(new(method, (object[])cases[index].Clone(), attribute, descriptor));
            }
        }
        return planned;
    }
}
