using SharpTestBench.Tools;
using SharpTestBench.Fixtures;
using Xunit;

public sealed class FrameDropTests
{
    static FrameWord Word(bool last,int user,int data=41)=>new(data,3,last,1,2,user);
    static FramePlan Plan(params FrameWord[][] frames)=>new(frames,Enumerable.Range(0,100).Select(index=>index>0).ToArray());
    [Fact] public void LastBeatBadMarkerDiscardsEntireFrame()
    {
        var model=new FrameDropModel(Plan([Word(false,0),Word(true,1)]));
        model.Observe(true,false,true,null,false,false);model.Observe(true,false,true,null,false,false);
        model.Observe(false,false,true,null,true,false);model.Complete();
        Assert.Equal(2,model.Discarded);Assert.Equal(0,model.Consumed);Assert.Equal(1,model.BadFrames);
    }
    [Fact] public void NonterminalMarkersAndUnmaskedHighUserBitDoNotDropGoodFrames()
    {
        var first=Word(false,1);var last=Word(true,2,83);var model=new FrameDropModel(Plan([first,last]));
        model.Observe(true,false,true,null,false,false);model.Observe(true,false,true,null,false,false);
        model.Observe(false,true,true,first,false,true);model.Observe(false,true,true,last,false,false);model.Complete();
        Assert.Equal(0,model.Discarded);Assert.Equal(1,model.GoodFrames);
    }
    [Fact] public void DroppedFrameCannotLeakBehindAdjacentGoodFrame()
    {
        var good=Word(true,0);var bad=Word(true,1,99);var model=new FrameDropModel(Plan([good],[bad]));
        model.Observe(true,false,true,null,false,false);model.Observe(true,true,true,good,false,true);
        Assert.Throws<InvalidDataException>(()=>model.Observe(false,true,true,bad,true,false));
    }
    [Fact] public void StatusPulseCannotAppearBeforeOrAfterItsCompletionCycle()
    {
        var plan=Plan([Word(true,1)]);var early=new FrameDropModel(plan);
        Assert.Throws<InvalidDataException>(()=>early.Observe(true,false,true,null,true,false));
        var missing=new FrameDropModel(plan);missing.Observe(true,false,true,null,false,false);
        Assert.Throws<InvalidDataException>(()=>missing.Observe(false,false,true,null,false,false));
    }
    [Fact] public void BackpressuredGoodOutputStaysStableWhileBadFramesComplete()
    {
        var good=Word(true,0);var bad=Word(true,1,99);var model=new FrameDropModel(Plan([good],[bad]));
        model.Observe(true,false,false,null,false,false);model.Observe(true,true,false,good,false,true);
        model.Observe(false,true,true,good,true,false);model.Complete();
        Assert.Equal(1,model.Stalls);Assert.Equal(1,model.Discarded);Assert.Equal(1,model.Consumed);
    }
    [Fact] public void DropWrapperAndPlanKeepNormalFrameSchemasSeparate()
    {
        var config=new FrameConfig(8,2,true);var wrapper=FrameDropFixture.Wrapper(config);
        Assert.Contains(".DROP_BAD_FRAME(1)",wrapper);Assert.Contains(".status_bad_frame(status_bad_frame)",wrapper);
        Assert.Contains(".status_good_frame(status_good_frame)",wrapper);Assert.Contains("module drop_d8_p2_o1",wrapper);
        var plan=FrameDropFixture.Plan(config,83);plan.Validate(config);
        Assert.Equal(8,plan.Frames.Count(frame=>(frame[^1].User&1)!=0));
        Assert.Throws<InvalidDataException>(()=>FrameDropFixture.Plan(config,83,1));
    }
}
