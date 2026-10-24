using SharpTestBench;
using System.Text.RegularExpressions;
using Xunit;
public sealed partial class SelectionContractTests
{
    [Fact] public void S046_EnumeratorFailuresPreserveTheOriginalException()
    {var expected=new IOException("enumeration failed");IEnumerable<string> Source(){yield return "ok";throw expected;}Assert.Same(expected,Assert.Throws<IOException>(()=>Semantic(Source())));}
    [Fact] public void S047_TimeoutValidationPrecedesEnumeration()
    {int calls=0;IEnumerable<string> Source(){calls++;yield return "ok";}Assert.Throws<ArgumentOutOfRangeException>(()=>new RegressionSelection(Source(),matchTimeout:TimeSpan.Zero));Assert.Equal(0,calls);}
    [Fact] public void S048_DuplicatePatternsAreNotSilentlyRewritten()
    {var s=Semantic(["^one$","^one$"]);Assert.Equal(2,s.Patterns.Count);Assert.True(s.Matches("one"));Assert.False(s.Matches("two"));}
    [Fact] public void S049_PatternLengthBudgetCountsUtf16CodeUnits()
    {string boundary=string.Concat(Enumerable.Repeat("😀",2048));Assert.Equal(4096,boundary.Length);var s=Semantic([boundary]);Assert.True(s.Matches(boundary));Assert.Throws<ArgumentException>(()=>Semantic([boundary+"😀"]));}
    [Fact] public async Task S050_ImmutableMatchingCanBeUsedOutsideTheSimulatorThread()
    {var s=Semantic(["^Suite\\.Case[0-9]$"]);var tasks=Enumerable.Range(0,8).Select(n=>Task.Run(()=>{for(int i=0;i<100;i++){if(!s.Matches($"Suite.Case{n}")||s.Matches("Other.Case"))return false;}return true;},Xunit.TestContext.Current.CancellationToken)).ToArray();Assert.All(await Task.WhenAll(tasks),value=>Assert.True(value));}
}
