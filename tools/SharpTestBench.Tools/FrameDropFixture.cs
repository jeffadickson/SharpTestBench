using SharpTestBench.Fixtures;
using System.Text.Json;
namespace SharpTestBench.Tools;

internal static class FrameDropFixture
{
    internal static string Top(FrameConfig config)=>config.Top.Replace("frame_","drop_",StringComparison.Ordinal);
    internal static FramePlan Plan(FrameConfig config,ulong seed,int count=16)
    {
        if(count is <2 or >32)throw new InvalidDataException("Drop qualification needs2..32 mixed frames.");
        var plan=FrameFixture.Plan(config,seed,count);
        for(int index=0;index<plan.Frames.Length;index++)
        {
            var frame=plan.Frames[index];
            for(int beat=0;beat<frame.Length;beat++)
                frame[beat]=frame[beat] with { User=beat==frame.Length-1?(index%2)|((index%3==0)?2:0):1 };
        }
        plan.Validate(config);return plan;
    }
    internal static string Wrapper(FrameConfig config)=>FrameFixture.Wrapper(config)
        .Replace(config.Top,Top(config),StringComparison.Ordinal)
        .Replace(".DROP_OVERSIZE_FRAME(1)",".DROP_OVERSIZE_FRAME(1), .DROP_BAD_FRAME(1)",StringComparison.Ordinal)
        .Replace("output wire [1:0] m_axis_tuser);","output wire [1:0] m_axis_tuser, output wire status_bad_frame, status_good_frame);",StringComparison.Ordinal)
        .Replace(".status_bad_frame(), .status_good_frame()",".status_bad_frame(status_bad_frame), .status_good_frame(status_good_frame)",StringComparison.Ordinal);
    internal static Dictionary<string,int> Metrics(FrameConfig config,JsonElement value,JsonElement stimulus)
    {
        var plan=FrameFixture.ReadPlan(config,stimulus);
        FixtureContracts.Fields(value,"trace","accepted","consumed","discarded","badFrames","goodFrames","stalls","resets","cycles");
        var trace=value.GetProperty("trace");
        if(trace.ValueKind!=JsonValueKind.Array||trace.GetArrayLength()!=plan.Ready.Length)throw new InvalidDataException("Invalid drop trace length.");
        var model=new FrameDropModel(plan);int[] maxima=[3999,1,1,1,1,65535,3,1,15,15,3,1,1];
        for(int cycle=0;cycle<plan.Ready.Length;cycle++)
        {
            var item=trace[cycle];if(item.ValueKind!=JsonValueKind.Array||item.GetArrayLength()!=13)throw new InvalidDataException("Invalid drop trace row.");
            var row=item.EnumerateArray().Select((number,index)=>(int)FixtureContracts.Integer(number,0,(ulong)maxima[index],"drop trace")).ToArray();
            if(row[0]!=cycle||row[1]!=(cycle==0?1:0)||row[4]!=(plan.Ready[cycle]?1:0))throw new InvalidDataException("Drop trace controls differ.");
            if(row[3]==0&&row[5..11].Any(number=>number!=0))throw new InvalidDataException("Drop invalid payload must be normalized.");
            if(cycle==0){if(row[2]!=0||row[3]!=0||row[11]!=0||row[12]!=0)throw new InvalidDataException("Drop reset transfer or pulse.");continue;}
            model.Observe(row[2]!=0,row[3]!=0,plan.Ready[cycle],row[3]==0?null:new(row[5],row[6],row[7]!=0,row[8],row[9],row[10]),row[11]!=0,row[12]!=0);
        }
        model.Complete();
        var counters=new Dictionary<string,int>{["accepted"]=model.Accepted,["consumed"]=model.Consumed,["discarded"]=model.Discarded,
            ["badFrames"]=model.BadFrames,["goodFrames"]=model.GoodFrames,["stalls"]=model.Stalls,["resets"]=1,["cycles"]=plan.Ready.Length};
        foreach(var (name,expected) in counters)
            if(FixtureContracts.Integer(value.GetProperty(name),0,100000,name)!=(ulong)expected)throw new InvalidDataException("Drop metric counter differs: "+name);
        counters["incompleteHidden"]=model.IncompleteHidden;return counters;
    }
}
