using SharpTestBench;
using Xunit;

public sealed class ClockCycleCompatibilityTests
{
    [Fact] public async Task DefaultLiteralStillSelectsTheOriginalCancellationOverload()
    {
        // This call must compile: an optional third-argument edge overload makes it ambiguous.
        await Assert.ThrowsAsync<ArgumentNullException>(()=>Triggers.ClockCycles(null!,2,default));
    }
}
