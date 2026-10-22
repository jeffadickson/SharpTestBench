using SharpTestBench;
using Xunit;
public class TriggerFactoryValidationTests
{
    [Fact] public void NullFactoriesFailWithoutAnActiveRuntime()
    {
        Assert.Throws<ArgumentNullException>(()=>Triggers.First((Trigger[])null!));
        Assert.Throws<ArgumentNullException>(()=>Triggers.Combine((Trigger[])null!));
        Assert.Throws<ArgumentNullException>(()=>Triggers.RisingEdge(null!));
        Assert.Throws<ArgumentNullException>(()=>Triggers.FallingEdge(null!));
    }
    [Fact] public void TimeoutRejectsNullTargetBeforeInvalidDeadline()
        =>Assert.Throws<ArgumentNullException>(()=>Triggers.WithTimeout(null!,0));
}
