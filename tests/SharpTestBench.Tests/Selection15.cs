using SharpTestBench;
using Xunit;
public sealed partial class SelectionContractTests
{
    [Fact] public void S011_NullPatternSequenceIsAnArgumentFailure()
    {Assert.Equal("patterns",Assert.Throws<ArgumentNullException>(()=>Semantic(null!)).ParamName);}
    [Fact] public void S012_NullPatternEntryRejectsAnOtherwiseValidSequence()
    {Assert.Equal("patterns",Assert.Throws<ArgumentException>(()=>Semantic(["^ok$",null!])).ParamName);}
    [Fact] public void S013_PatternLimitStopsAnUnboundedEnumerator()
    {int yielded=0;IEnumerable<string> Source(){while(true){yielded++;if(yielded>65)throw new InvalidDataException("enumeration exceeded rejection boundary");yield return "ok";}}Assert.Throws<ArgumentException>(()=>Semantic(Source()));Assert.Equal(65,yielded);}
    [Fact] public void S014_AllSixtyFourPatternsRemainEffective()
    {var s=Semantic(Enumerable.Range(0,64).Select(n=>$"^Case{n}$"));Assert.Equal(64,s.Patterns.Count);Assert.True(s.Matches("Case0"));Assert.True(s.Matches("Case63"));Assert.False(s.Matches("Case64"));}
    [Fact] public void S015_ExactPatternLengthBoundaryIsAccepted()
    {string value=new('a',4096);var s=Semantic([value]);Assert.Equal(4096,s.Patterns[0].Length);Assert.True(s.Matches(value));}
}
