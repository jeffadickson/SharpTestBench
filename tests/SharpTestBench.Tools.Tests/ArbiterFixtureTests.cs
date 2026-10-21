using SharpTestBench.Tools;
using SharpTestBench.Fixtures;
using System.Text.Json;
using Xunit;

public sealed class ArbiterFixtureTests
{
    [Fact] public void PinnedArbiterAndPriorityDependencyHaveExactSourceProvenance()
    {
        var value = FixtureContracts.VerifySources(ToolContext.FindRoot(), "arbiter");
        Assert.Equal("d54d337db4812f927b2e9b5788dfa39f9bd578ad01b7846c5fa7f2610b0923fa",value.GetProperty("files").GetProperty("arbiter.v").GetProperty("sha256").GetString());
        Assert.Equal(3,value.GetProperty("files").EnumerateObject().Count());
    }
    [Fact] public void ChangedPriorityDependencyCannotQualifyArbiterSources()
    {
        var root = Path.Combine(Path.GetTempPath(), "stb-arbiter-source-"+Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(root,"tests/Fixtures/verilog-arbiter"); Directory.CreateDirectory(folder);
        try
        {
            foreach (var file in new[] { "arbiter.v","priority_encoder.v","COPYING","provenance.json" })
                File.Copy(Path.Combine(ToolContext.FindRoot(),"tests/Fixtures/verilog-arbiter",file),Path.Combine(folder,file));
            File.AppendAllText(Path.Combine(folder,"priority_encoder.v"),"\n// altered dependency\n");
            Assert.Throws<InvalidDataException>(() => FixtureContracts.VerifySources(root,"arbiter"));
        }
        finally
        {
            if (Path.GetDirectoryName(Path.GetFullPath(root)) != Path.TrimEndingDirectorySeparator(Path.GetTempPath())) throw new InvalidOperationException("Invalid temporary arbiter root");
            Directory.Delete(root,true);
        }
    }
    [Fact] public void FortyEightArbiterConfigurationsHaveUniqueSafeTopNames()
    {
        var names = new HashSet<string>();
        foreach (int ports in new[] { 2,3,4,8 })
        foreach (bool roundRobin in new[] { false,true })
        foreach (int block in new[] { 0,1,2 })
        foreach (bool lsb in new[] { false,true })
        {
            var config = new ArbiterConfig(ports,roundRobin,block,lsb); config.Validate();
            Assert.True(names.Add(config.Top)); Assert.Matches("^[a-z][a-z0-9_]+$",config.Top);
            Assert.Contains(".PORTS("+ports+")",ArbiterFixture.Wrapper(config));
        }
        Assert.Equal(48,names.Count);
    }
    [Fact] public void InvalidArbiterConfigurationRejectsBeforeModelOrPlanUse()
    {
        foreach (int ports in new[] { -1,0,1,5,9,int.MaxValue })
            Assert.Throws<InvalidDataException>(() => new ArbiterModel(new(ports,false,0,true)));
        foreach (int block in new[] { -1,3,int.MaxValue })
            Assert.Throws<InvalidDataException>(() => ArbiterFixture.Plan(new(4,false,block,true),83));
    }
    [Fact] public void RoundRobinCircularScanWrapsBothDirectionsAtThreePorts()
    {
        foreach (bool lsb in new[] { false,true })
        {
            var model = new ArbiterModel(new(3,true,0,lsb)); model.Step(new(true,0,0));
            Assert.Equal(lsb ? new[] { 0,1,2,0,1,2 } : new[] { 2,1,0,2,1,0 },
                Enumerable.Range(0,6).Select(_ => model.Step(new(false,7,7)).Encoded).ToArray());
        }
    }
    [Fact] public void AcknowledgementBlockHoldsWithdrawnRequestUntilItsOwnAck()
    {
        var model = new ArbiterModel(new(4,false,2,true));
        Assert.Equal(8,model.Step(new(false,8,0)).Grant);
        Assert.Equal(8,model.Step(new(false,1,1)).Grant);
        Assert.Equal(1,model.Step(new(false,1,8)).Grant);
    }
    [Fact] public void RequestBlockIgnoresAckAndNewHigherPriorityRequestUntilRelease()
    {
        var model = new ArbiterModel(new(4,false,1,true));
        Assert.Equal(8,model.Step(new(false,8,0)).Grant);
        Assert.Equal(8,model.Step(new(false,9,8)).Grant);
        Assert.Equal(1,model.Step(new(false,1,0)).Grant);
    }
    [Fact] public void ResetPreemptsHeldGrantAndRestartsRoundRobinPreference()
    {
        var model = new ArbiterModel(new(3,true,2,false));
        Assert.Equal(4,model.Step(new(false,7,0)).Grant);
        Assert.Equal(default,model.Step(new(true,7,0)));
        Assert.Equal(4,model.Step(new(false,7,0)).Grant);
    }
    [Fact] public void IdleCyclePreservesRoundRobinCursorWithoutStaleGrant()
    {
        var model = new ArbiterModel(new(3,true,0,true));
        Assert.Equal(1,model.Step(new(false,7,0)).Grant);
        Assert.Equal(default,model.Step(new(false,0,0)));
        Assert.Equal(2,model.Step(new(false,7,0)).Grant);
    }
    [Fact] public void SeededPlansIncludeResetHoldReleaseAndBoundedRandomMasks()
    {
        var config = new ArbiterConfig(3,true,2,false);
        var rows = ArbiterFixture.Plan(config,ulong.MaxValue,17);
        Assert.Equal(rows,ArbiterFixture.Plan(config,ulong.MaxValue,17));
        Assert.Equal(rows,ArbiterFixture.ValidatePlan(config,JsonSerializer.SerializeToElement(rows)));
        Assert.True(rows[0].Reset); Assert.True(rows[^1].Reset);
        Assert.Contains(rows,row => !row.Reset && row.Request == 7 && row.Acknowledge == 0);
        Assert.Contains(rows,row => !row.Reset && row.Request == 0);
        Assert.All(rows,row => { Assert.InRange(row.Request,0,7); Assert.InRange(row.Acknowledge,0,7); });
    }
    [Fact] public void InvalidArbiterPlanBudgetsAndMasksRejectBeforeNativeExecution()
    {
        var config = new ArbiterConfig(3,true,0,true);
        Assert.Throws<InvalidDataException>(() => ArbiterFixture.Plan(config,83,0));
        Assert.Throws<InvalidDataException>(() => ArbiterFixture.Plan(config,83,2049));
        Assert.Throws<InvalidDataException>(() => new ArbiterModel(config).Step(new(false,8,0)));
        var rows = ArbiterFixture.Plan(config,83,1); rows[0] = rows[0] with { Reset = false };
        Assert.Throws<InvalidDataException>(() => ArbiterFixture.ValidatePlan(config,JsonSerializer.SerializeToElement(rows)));
    }
    [Fact] public void HandAuthoredThreePortTraceBindsPolicyControlsAndCounters()
    {
        var config = new ArbiterConfig(3,true,0,true);
        ArbiterRow[] rows = [new(true,0,0),new(false,7,7),new(false,7,7),new(false,7,7),new(true,0,0)];
        int[][] trace = [[0,1,0,0,0,0,0],[1,0,7,7,1,1,0],[2,0,7,7,2,1,1],[3,0,7,7,4,1,2],[4,1,0,0,0,0,0]];
        var metrics = JsonSerializer.SerializeToElement(new { trace,cycles=5,valid=3,idle=2,resets=2,transitions=4 });
        var plan = JsonSerializer.SerializeToElement(rows);
        Assert.True(JsonElement.DeepEquals(metrics,ArbiterFixture.Metrics(config,metrics,plan)));
        trace[2][4] = 4;
        var bad = JsonSerializer.SerializeToElement(new { trace,cycles=5,valid=3,idle=2,resets=2,transitions=4 });
        Assert.Throws<InvalidDataException>(() => ArbiterFixture.Metrics(config,bad,plan));
    }
}
