using SharpTestBench;
using System.Text.RegularExpressions;
using Xunit;
public sealed partial class SelectionContractTests
{
    [Fact] public void S021_NegativeAndInfiniteTimeoutsAreRejected()
    {foreach(var t in new[]{TimeSpan.FromTicks(-1),Regex.InfiniteMatchTimeout})Assert.Throws<ArgumentOutOfRangeException>(()=>new RegressionSelection([],matchTimeout:t));}
    [Fact] public void S022_TimeoutAboveThePublicBudgetIsRejected()
    {Assert.Throws<ArgumentOutOfRangeException>(()=>new RegressionSelection([],matchTimeout:TimeSpan.FromSeconds(1)+TimeSpan.FromTicks(1)));}
    [Fact] public void S023_SubMillisecondTimeoutDoesNotRoundIntoAnUnboundedWait()
    {Assert.Throws<ArgumentOutOfRangeException>(()=>new RegressionSelection([],matchTimeout:TimeSpan.FromTicks(9999)));}
    [Fact] public void S024_MinimumTimeoutIsAcceptedAndRetained()
    {var s=new RegressionSelection(["^Case$"],matchTimeout:TimeSpan.FromMilliseconds(1));Assert.Equal(TimeSpan.FromMilliseconds(1),s.MatchTimeout);Assert.Equal("^Case$",Assert.Single(s.Patterns));}
    [Fact] public void S025_MaximumTimeoutRemainsAnExplicitFiniteBudget()
    {var s=new RegressionSelection(["abc"],matchTimeout:TimeSpan.FromSeconds(1));Assert.True(s.Matches("Suite.abc"));Assert.Equal(TimeSpan.FromSeconds(1),s.MatchTimeout);}
    [Fact] public void S026_NullCandidateIsNotSilentlyIncludedByAnEmptyFilter()
    {Assert.Equal("qualifiedName",Assert.Throws<ArgumentNullException>(()=>Semantic().Matches(null!)).ParamName);}
    [Fact] public void S027_EmptyCandidateUsesNormalRegexSemantics()
    {var s=Semantic(["^$"]);Assert.True(s.Matches(""));Assert.False(s.Matches("Suite.Case"));}
    [Fact] public void S028_WhitespacePatternsAreNotTrimmedOrDiscarded()
    {var s=Semantic([" a "]);Assert.Equal(" a ",s.Patterns[0]);Assert.True(s.Matches("prefix a suffix"));Assert.False(s.Matches("a"));}
    [Fact] public void S029_EscapedQualifiedCaseNamesMatchLiterally()
    {const string name="One.Two+Nested.Case[1]";var s=Semantic(["^"+Regex.Escape(name)+"$"]);Assert.True(s.Matches(name));Assert.False(s.Matches("OneXTwo+NestedXCase1"));}
    [Fact] public void S030_AlternationAndPatternUnionHaveTheSameCaseSet()
    {var a=Semantic(["^A$","^C$"]);var b=Semantic(["^(A|C)$"]);foreach(string name in new[]{"A","B","C","AC",""})Assert.Equal(a.Matches(name),b.Matches(name));}
}
