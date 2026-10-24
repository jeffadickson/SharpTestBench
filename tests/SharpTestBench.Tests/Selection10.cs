using SharpTestBench;
using System.Collections;
using System.Text.RegularExpressions;
using Xunit;
public sealed partial class SelectionContractTests
{
    // Semantic probes use the maximum finite budget so scheduler preemption does
    // not turn unrelated machine load into an assumed matching guarantee. S019
    // checks the public default; explicit timeout contracts use their own budget.
    static RegressionSelection Semantic(IEnumerable<string> patterns,RegressionMode mode=RegressionMode.Regression)=>new(patterns,mode,TimeSpan.FromSeconds(1));
    static RegressionSelection Semantic()=>new();
    [Fact] public void S001_DefaultSelectionIsUnrestrictedWithoutSkipOverride()
    {var s=Semantic();Assert.True(s.Matches("Suite.Case[0]"));Assert.True(s.Matches(""));Assert.Empty(s.Patterns);Assert.Equal(RegressionMode.Regression,s.Mode);}
    [Fact] public void S002_RegexSearchIncludesTheQualifiedName()
    {var s=Semantic(["Suite"]);Assert.True(s.Matches("Namespace.Suite.Case"));Assert.False(s.Matches("Other.Case"));}
    [Fact] public void S003_AnchorsDistinguishQualifiedIdentities()
    {var s=Semantic(["^One\\.Case$"]);Assert.True(s.Matches("One.Case"));Assert.False(s.Matches("Two.One.Case"));Assert.False(s.Matches("One.Case[0]"));}
    [Fact] public void S004_DefaultMatchingRemainsCaseSensitive()
    {var s=Semantic(["alpha"]);Assert.True(s.Matches("Suite.alpha"));Assert.False(s.Matches("Suite.Alpha"));}
    [Fact] public void S005_MultiplePatternsSelectADisjointUnion()
    {var s=Semantic(["^A\\.","^C\\."]);Assert.True(s.Matches("A.One"));Assert.True(s.Matches("C.Two"));Assert.False(s.Matches("B.One"));}
    [Fact] public void S006_EmptyPatternListDoesNotChangeExplicitMode()
    {var s=Semantic([],RegressionMode.Testcase);Assert.True(s.Matches("Any.Case"));Assert.Equal(RegressionMode.Testcase,s.Mode);}
    [Fact] public void S007_EmptyRegexIsAnExplicitMatchAllPattern()
    {var s=Semantic(["never","" ]);Assert.True(s.Matches("anything"));Assert.Equal(2,s.Patterns.Count);}
    [Fact] public void S008_ConstructorSnapshotsCallerArray()
    {string[] source=["^original$"];var s=Semantic(source);source[0]="replacement";Assert.True(s.Matches("original"));Assert.False(s.Matches("replacement"));Assert.Equal("^original$",s.Patterns[0]);}
    [Fact] public void S009_PatternViewCannotMutateCompiledSelection()
    {var s=Semantic(["^safe$"]);var list=Assert.IsAssignableFrom<IList<string>>(s.Patterns);Assert.Throws<NotSupportedException>(()=>list[0]="unsafe");Assert.True(s.Matches("safe"));Assert.False(s.Matches("unsafe"));}
    [Fact] public void S010_LaterInvalidSyntaxCannotHideBehindAnEarlierMatchAll()
    {Assert.ThrowsAny<ArgumentException>(()=>Semantic(["", "("]));var valid=Semantic(["^safe$"]);Assert.True(valid.Matches("safe"));}
}
