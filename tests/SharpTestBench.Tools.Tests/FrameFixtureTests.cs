using SharpTestBench.Fixtures;
using SharpTestBench.Tools;
using System.Text.Json;
using Xunit;

public sealed class FrameFixtureTests
{
    static FrameWord First=>new(41,3,false,1,2,0);
    static FrameWord Last=>new(83,1,true,1,2,3);
    static FramePlan Plan=>new([[First,Last]],Enumerable.Range(0,100).Select(index=>index>0).ToArray());
    [Fact] public void CompleteFramesRemainInvisibleUntilLastHandshakeHasCompleted()
    {
        var model=new FrameScoreboard(Plan);
        model.Observe(true,false,true,null);
        Assert.Throws<InvalidDataException>(()=>model.Observe(true,true,true,First));
    }
    [Fact] public void CommittedFrameKeepsAllSidebandsAndStableStalledOutput()
    {
        var model=new FrameScoreboard(Plan);
        model.Observe(true,false,true,null);model.Observe(true,false,true,null);
        model.Observe(false,true,false,First);model.Observe(false,true,true,First);
        model.Observe(false,true,true,Last);model.Complete();
        Assert.Equal(2,model.Accepted);Assert.Equal(2,model.Consumed);Assert.Equal(1,model.Stalls);Assert.True(model.IncompleteHidden>0);
    }
    [Fact] public void CompletedFrameCannotHideAChangedStalledOutput()
    {
        var model=new FrameScoreboard(Plan);model.Observe(true,false,true,null);model.Observe(true,false,true,null);
        model.Observe(false,true,false,First);
        Assert.Throws<InvalidDataException>(()=>model.Observe(false,true,true,First with { Keep=1 }));
    }
    [Fact] public void PartialFramesAndUnconsumedCommittedFramesFailDrain()
    {
        var partial=new FrameScoreboard(Plan);partial.Observe(true,false,true,null);Assert.Throws<InvalidDataException>(partial.Complete);
        var complete=new FrameScoreboard(Plan);complete.Observe(true,false,true,null);complete.Observe(true,false,true,null);Assert.Throws<InvalidDataException>(complete.Complete);
    }
    [Fact] public void FramePlansCoverValidatedDepthPipelineAndOutputCombinations()
    {
        var tops=new HashSet<string>();
        foreach(int depth in new[]{4,8,16,32,64})foreach(int pipeline in new[]{0,1,2,3})foreach(bool output in new[]{false,true})
        {
            var config=new FrameConfig(depth,pipeline,output);var plan=FrameFixture.Plan(config,ulong.MaxValue);
            plan.Validate(config);Assert.True(tops.Add(config.Top));
            var wrapper=FrameFixture.Wrapper(config);Assert.Contains(".FRAME_FIFO(1)",wrapper);Assert.Contains(".DROP_OVERSIZE_FRAME(1)",wrapper);
            Assert.Contains("module "+config.Top,wrapper);
            Assert.Equal(JsonSerializer.Serialize(plan),JsonSerializer.Serialize(FrameFixture.Plan(config,ulong.MaxValue)));
        }
        Assert.Equal(40,tops.Count);
    }
    [Fact] public void InvalidFrameConfigurationAndBoundariesRejectBeforeCompilation()
    {
        foreach(int depth in new[]{0,2,3,65})Assert.Throws<InvalidDataException>(()=>new FrameConfig(depth,1,false).Validate());
        Assert.Throws<InvalidDataException>(()=>new FrameConfig(8,4,false).Validate());
        var config=new FrameConfig(4,0,false);
        Assert.Throws<InvalidDataException>(()=>(Plan with { Frames=[[First,Last with { Last=false }]] }).Validate(config));
        Assert.Throws<InvalidDataException>(()=>(Plan with { Frames=[[First,First,First,Last]] }).Validate(config));
        Assert.Throws<InvalidDataException>(()=>(Plan with { Ready=[false,true,true] }).Validate(config));
    }
}
