using SharpTestBench.Tools;
using Xunit;

public sealed class WorkloadNegativeCountTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativeCountsCannotBecomePrefixOnlyPlans(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkloadPlans.Dsp(count, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkloadPlans.Axi(count, 1));
    }
}
