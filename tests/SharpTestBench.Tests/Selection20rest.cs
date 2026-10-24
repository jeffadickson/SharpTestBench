using SharpTestBench;
using Xunit;
public sealed partial class SelectionContractTests
{
    [Fact] public void S016_OversizedPatternRejectsBeforeRegexExecution()
    {Assert.Equal("patterns",Assert.Throws<ArgumentException>(()=>Semantic([new string('a',4097)])).ParamName);}
    [Fact] public void S017_InvalidModeRejectsBeforeEnumeration()
    {var source=Enumerable.Range(0,1).Select<int,string>(_=>throw new InvalidDataException("must not enumerate"));Assert.Equal("mode",Assert.Throws<ArgumentOutOfRangeException>(()=>Semantic(source,(RegressionMode)9)).ParamName);}
    [Fact] public void S018_NegativeUndefinedModeIsNeverTreatedAsRegression()
    {Assert.Throws<ArgumentOutOfRangeException>(()=>Semantic([],(RegressionMode)(-1)));Assert.Throws<ArgumentOutOfRangeException>(()=>Semantic([],(RegressionMode)int.MinValue));}
    [Fact] public void S019_DefaultPerMatchTimeoutIsExplicitAndFinite()
    {var s=new RegressionSelection(["x"]);Assert.Equal(TimeSpan.FromMilliseconds(20),s.MatchTimeout);Assert.NotEqual(System.Text.RegularExpressions.Regex.InfiniteMatchTimeout,s.MatchTimeout);}
    [Fact] public void S020_ZeroTimeoutCannotCreateAnUnboundedMatcher()
    {Assert.Equal("matchTimeout",Assert.Throws<ArgumentOutOfRangeException>(()=>new RegressionSelection([],matchTimeout:TimeSpan.Zero)).ParamName);}
}
