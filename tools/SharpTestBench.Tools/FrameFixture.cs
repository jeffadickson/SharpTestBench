using SharpTestBench.Fixtures;
using System.Text.Json;

namespace SharpTestBench.Tools;

internal static class FrameFixture
{
    internal static FrameConfig Configuration(JsonElement value)
    {
        FixtureContracts.Fields(value,"Depth","Pipeline","OutputFifo");
        var config=new FrameConfig((int)FixtureContracts.Integer(value.GetProperty("Depth"),4,64,"depth"),
            (int)FixtureContracts.Integer(value.GetProperty("Pipeline"),0,3,"pipeline"),FixtureContracts.Boolean(value.GetProperty("OutputFifo"),"output_fifo"));
        config.Validate();return config;
    }
    internal static FramePlan Plan(FrameConfig config,ulong seed,int count=16)
    {
        config.Validate();
        if(count is <1 or >32) throw new InvalidDataException("Frame count must be1..32.");
        var random=new PythonRandom(seed);var frames=new FrameWord[count][];
        for(int index=0;index<count;index++)
        {
            int length=index==0?2:1+random.Below(Math.Min(config.Depth-1,7));
            frames[index]=Enumerable.Range(0,length).Select(beat=>new FrameWord(random.Below(65536),1+random.Below(3),beat==length-1,index%16,(index*3)%16,random.Below(4))).ToArray();
        }
        int cycles=frames.Sum(frame=>frame.Length)*10+config.Depth+100;
        var ready=Enumerable.Range(0,cycles).Select(index=>index>=16 && (index>=cycles-config.Depth-80 || index%5>=2)).ToArray();
        var plan=new FramePlan(frames,ready);plan.Validate(config);return plan;
    }
    internal static FramePlan ReadPlan(FrameConfig config,JsonElement value)
    {
        FixtureContracts.Fields(value,"Frames","Ready");
        var frames=value.GetProperty("Frames");var ready=value.GetProperty("Ready");
        if(frames.ValueKind!=JsonValueKind.Array || ready.ValueKind!=JsonValueKind.Array) throw new InvalidDataException("Invalid frame plan arrays.");
        var words=frames.EnumerateArray().Select(frame=> {
            if(frame.ValueKind!=JsonValueKind.Array) throw new InvalidDataException("Invalid frame array.");
            return frame.EnumerateArray().Select(word=> {
                FixtureContracts.Fields(word,"Data","Keep","Last","Id","Dest","User");
                int Number(string name,int maximum)=>(int)FixtureContracts.Integer(word.GetProperty(name),0,(ulong)maximum,name);
                return new FrameWord(Number("Data",65535),Number("Keep",3),FixtureContracts.Boolean(word.GetProperty("Last"),"last"),Number("Id",15),Number("Dest",15),Number("User",3));
            }).ToArray();
        }).ToArray();
        var plan=new FramePlan(words,ready.EnumerateArray().Select(item=>FixtureContracts.Boolean(item,"ready")).ToArray());plan.Validate(config);return plan;
    }
    internal static string Wrapper(FrameConfig config)
    {
        config.Validate();
        var beat=new FifoConfiguration(config.Depth,config.Pipeline,config.OutputFifo,true);
        return FifoFixture.Wrapper(beat).Replace(beat.Top,config.Top,StringComparison.Ordinal)
            .Replace(".FRAME_FIFO(0)",".FRAME_FIFO(1)",StringComparison.Ordinal)
            .Replace(".DROP_OVERSIZE_FRAME(0)",".DROP_OVERSIZE_FRAME(1)",StringComparison.Ordinal);
    }
    internal static int Metrics(FrameConfig config,JsonElement value,JsonElement stimulus)
    {
        var plan=ReadPlan(config,stimulus);AxisFixture.Metrics(value);
        var rows=value.GetProperty("trace");
        if(rows.GetArrayLength()!=plan.Ready.Length) throw new InvalidDataException("Frame trace length differs.");
        var model=new FrameScoreboard(plan);
        for(int cycle=0;cycle<plan.Ready.Length;cycle++)
        {
            var row=rows[cycle].EnumerateArray().Select(item=>item.GetInt32()).ToArray();
            if(row[1]!=(cycle==0?1:0) || row[4]!=(plan.Ready[cycle]?1:0)) throw new InvalidDataException("Frame trace controls differ.");
            if(cycle==0)continue;
            var word=row[3]==0?null:new FrameWord(row[5],row[6],row[7]!=0,row[8],row[9],row[10]);
            model.Observe(row[2]!=0,row[3]!=0,plan.Ready[cycle],word);
        }
        model.Complete();return model.IncompleteHidden;
    }
}
