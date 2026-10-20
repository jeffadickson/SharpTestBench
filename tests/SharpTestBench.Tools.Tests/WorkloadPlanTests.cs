using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

public sealed class WorkloadPlanTests
{
    [Theory]
    [InlineData("axi", 0, 0UL)]
    [InlineData("axi", 0, 17UL)]
    [InlineData("axi", 0, 4294967296UL)]
    [InlineData("axi", 0, 18446744073709551615UL)]
    [InlineData("axi", 1, 0UL)]
    [InlineData("axi", 1, 17UL)]
    [InlineData("axi", 1, 4294967296UL)]
    [InlineData("axi", 1, 18446744073709551615UL)]
    [InlineData("axi", 31, 0UL)]
    [InlineData("axi", 31, 17UL)]
    [InlineData("axi", 31, 4294967296UL)]
    [InlineData("axi", 31, 18446744073709551615UL)]
    [InlineData("axi", 32, 0UL)]
    [InlineData("axi", 32, 17UL)]
    [InlineData("axi", 32, 4294967296UL)]
    [InlineData("axi", 32, 18446744073709551615UL)]
    [InlineData("axi", 257, 0UL)]
    [InlineData("axi", 257, 17UL)]
    [InlineData("axi", 257, 4294967296UL)]
    [InlineData("axi", 257, 18446744073709551615UL)]
    [InlineData("dsp", 0, 0UL)]
    [InlineData("dsp", 0, 17UL)]
    [InlineData("dsp", 0, 4294967296UL)]
    [InlineData("dsp", 0, 18446744073709551615UL)]
    [InlineData("dsp", 1, 0UL)]
    [InlineData("dsp", 1, 17UL)]
    [InlineData("dsp", 1, 4294967296UL)]
    [InlineData("dsp", 1, 18446744073709551615UL)]
    [InlineData("dsp", 31, 0UL)]
    [InlineData("dsp", 31, 17UL)]
    [InlineData("dsp", 31, 4294967296UL)]
    [InlineData("dsp", 31, 18446744073709551615UL)]
    [InlineData("dsp", 32, 0UL)]
    [InlineData("dsp", 32, 17UL)]
    [InlineData("dsp", 32, 4294967296UL)]
    [InlineData("dsp", 32, 18446744073709551615UL)]
    [InlineData("dsp", 257, 0UL)]
    [InlineData("dsp", 257, 17UL)]
    [InlineData("dsp", 257, 4294967296UL)]
    [InlineData("dsp", 257, 18446744073709551615UL)]
    [InlineData("cpu", -2147483648, 0UL)]
    [InlineData("cpu", -1, 0UL)]
    [InlineData("cpu", 0, 0UL)]
    [InlineData("cpu", 1, 0UL)]
    [InlineData("cpu", 2, 0UL)]
    [InlineData("cpu", 255, 0UL)]
    [InlineData("cpu", 32766, 0UL)]
    [InlineData("cpu", 32767, 0UL)]
    [InlineData("cpu", 32768, 0UL)]
    [InlineData("cpu", 2147483647, 0UL)]
    public void WorkloadPlansRespectTheirProtocols(string kind, int count, ulong seed)
    {
        if (kind == "cpu")
        {
            if (count < 1 || count > 32767) { Assert.Throws<ArgumentOutOfRangeException>(() => WorkloadPlans.Cpu(count)); return; }
            var program = WorkloadPlans.Cpu(count);
            Assert.Equal(15, program.Length);
            Assert.Equal((uint)count, program[1] & 65535);
            Assert.Equal(1U, program[1] >> 28);
            Assert.Equal(15U, program[^1] >> 28);
            Assert.Equal(65528U, program[^2] & 65535); // Loop branch offset -8.
            return;
        }
        if (kind == "dsp")
        {
            var plan = WorkloadPlans.Dsp(count, seed);
            Assert.Equal(count + 101, plan.Length);
            Assert.Equal(plan, WorkloadPlans.Dsp(count, seed));
            Assert.True(plan[0].Reset);
            Assert.All(plan, row => Assert.InRange(row.Sample, -32768, 32767));
            Assert.All(plan.TakeLast(4), row => { Assert.True(row.Ready); Assert.False(row.Offer); Assert.False(row.Reset); });
            for (int index = 0; index < count; index++) Assert.Equal(index % 257 == 256, plan[97 + index].Reset);
            return;
        }
        var transactions = WorkloadPlans.Axi(count, seed);
        Assert.Equal(2 * count + 2 * (count / 32) + 8, transactions.Length);
        Assert.Equal(transactions, WorkloadPlans.Axi(count, seed));
        Assert.All(transactions, row => { Assert.InRange(row.AwDelay, 0, 5); Assert.InRange(row.WDelay, 0, 5); Assert.InRange(row.ReadyDelay, 8, 19); Assert.InRange(row.Strobe, 0, 15); });
        Assert.Equal(new[] { "reset_aw", "read", "reset_w", "read", "reset_b", "read", "reset_r", "read" }, transactions.TakeLast(8).Select(row => row.Op));
        var accesses = transactions.Where(row => row.Op == "write").ToArray(); Assert.Equal(count, accesses.Length);
        for (int index = 0; index < accesses.Length; index++)
        {
            var row = accesses[index]; Assert.Equal(index % 16, row.Strobe);
            if (index % 23 == 0) Assert.Equal(256, row.Address);
            else if (index % 29 == 0) Assert.Equal(3, row.Address);
            else { Assert.InRange(row.Address, 0, 252); Assert.Equal(0, row.Address % 4); }
        }
    }
}
