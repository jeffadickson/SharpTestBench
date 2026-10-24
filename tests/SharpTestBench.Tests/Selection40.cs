using SharpTestBench;
using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;
public sealed partial class SelectionContractTests
{
    [Fact] public void S031_ParameterizedCaseSuffixCanBeSelectedPrecisely()
    {var s=Semantic(["\\[1\\]$"]);Assert.True(s.Matches("Suite.Case[1]"));Assert.False(s.Matches("Suite.Case[0]"));Assert.False(s.Matches("Suite.Case[11]"));}
    [Fact] public void S032_UnanchoredSearchAndAnchoredPrefixAreDistinct()
    {var search=Semantic(["Case"]);var prefix=Semantic(["^Case"]);Assert.True(search.Matches("Suite.Case"));Assert.False(prefix.Matches("Suite.Case"));Assert.True(prefix.Matches("Case.Suite"));}
    [Fact] public void S033_LookaheadUsesTheDocumentedDotNetRegexDialect()
    {var s=Semantic(["^Suite\\.(?=Case)Case[0-9]$"]);Assert.True(s.Matches("Suite.Case1"));Assert.False(s.Matches("Suite.Other1"));Assert.False(s.Matches("Suite.Case12"));}
    [Fact] public void S034_LookbehindSearchDoesNotStripQualification()
    {var s=Semantic(["(?<=Suite\\.)Case$"]);Assert.True(s.Matches("Suite.Case"));Assert.False(s.Matches("Other.Case"));}
    [Fact] public void S035_NamedBackreferencesRemainSupportedWithinTheBudget()
    {var s=Semantic(["^(?<part>[a-z]+)\\.\\k<part>$"]);Assert.True(s.Matches("same.same"));Assert.False(s.Matches("same.other"));}
    [Fact] public void S036_NamedGroupsDoNotLeakStateAcrossCandidates()
    {var s=Semantic(["^(?<name>A|B)\\.\\k<name>$"]);Assert.True(s.Matches("A.A"));Assert.False(s.Matches("B.A"));Assert.True(s.Matches("B.B"));}
    [Fact] public void S037_UnicodeQualifiedNamesRemainSearchable()
    {var s=Semantic(["^Δοκιμή\\.Ω_.*$"]);Assert.True(s.Matches("Δοκιμή.Ω_Case"));Assert.False(s.Matches("Other.Ω_Case"));}
    [Fact] public void S038_UnicodeNormalizationIsNotImplicitlyChanged()
    {var s=Semantic(["^café$"]);Assert.True(s.Matches("café"));Assert.False(s.Matches("cafe\u0301"));}
    [Fact] public void S039_InlineIgnoreCaseUsesInvariantCultureUnderTurkishLocale()
    {var original=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("tr-TR");var s=Semantic(["(?i)^I$"]);Assert.True(s.Matches("i"));Assert.False(s.Matches("ı"));}finally{CultureInfo.CurrentCulture=original;}}
    [Fact] public void S040_ChangingCultureAfterConstructionCannotChangeTheMatcher()
    {var original=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;var s=Semantic(["(?i)^I$"]);CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("tr-TR");Assert.True(s.Matches("i"));Assert.False(s.Matches("ı"));}finally{CultureInfo.CurrentCulture=original;}}
}
