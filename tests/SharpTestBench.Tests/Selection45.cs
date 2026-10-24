using SharpTestBench;
using System.Text.RegularExpressions;
using Xunit;
public sealed partial class SelectionContractTests
{
    static string Adversarial()=>new string('a',128)+"B";
    [Fact] public void S041_TimeoutIsReportedAndTheSelectionCanStillBeReused()
    {var s=new RegressionSelection(["^safe$","^(a+)+$"],matchTimeout:TimeSpan.FromSeconds(1));var e=Assert.Throws<RegexMatchTimeoutException>(()=>s.Matches(Adversarial()));Assert.Equal("^(a+)+$",e.Pattern);Assert.True(s.Matches("safe"));}
    [Fact] public void S042_OrMatchingShortCircuitsBeforeAnUnneededExpensiveExpression()
    {var s=new RegressionSelection(["^.*$","^(a+)+$"],matchTimeout:TimeSpan.FromSeconds(1));Assert.True(s.Matches(Adversarial()));}
    [Fact] public void S043_TimeoutIsNeverConvertedIntoASilentNonMatch()
    {var s=new RegressionSelection(["^not-this$","^(a+)+$"],matchTimeout:TimeSpan.FromSeconds(1));Assert.Throws<RegexMatchTimeoutException>(()=>s.Matches(Adversarial()));Assert.False(s.Matches("ordinary"));}
    [Fact] public void S044_PatternSourceIsEnumeratedOnlyOnce()
    {int calls=0;IEnumerable<string> Source(){calls++;if(calls!=1)throw new InvalidDataException("second enumeration");yield return "^one$";}var s=Semantic(Source());Assert.True(s.Matches("one"));Assert.False(s.Matches("two"));Assert.Equal(1,calls);}
    [Fact] public void S045_LaterCallerListAppendsCannotWidenSelection()
    {var source=new List<string>{"^one$"};var s=Semantic(source);source.Add("^two$");source.Clear();Assert.True(s.Matches("one"));Assert.False(s.Matches("two"));Assert.Single(s.Patterns);}
}
