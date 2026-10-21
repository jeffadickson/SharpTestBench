namespace SharpTestBench.Fixtures;

internal sealed record FrameConfig(int Depth,int Pipeline,bool OutputFifo)
{
    internal string Top => $"frame_d{Depth}_p{Pipeline}_o{(OutputFifo?1:0)}";
    public void Validate()
    {
        if (Depth is <4 or >64 || (Depth&(Depth-1))!=0 || Pipeline is <0 or >3)
            throw new InvalidDataException("Frame FIFO requires power-of-two depth4..64 and pipeline0..3.");
    }
}
internal sealed record FrameWord(int Data,int Keep,bool Last,int Id,int Dest,int User);
internal sealed record FramePlan(FrameWord[][] Frames,bool[] Ready)
{
    public void Validate(FrameConfig config)
    {
        config.Validate();
        if (Frames is null || Frames.Length is <1 or >32 || Ready is null || Ready.Length is <3 or >4000)
            throw new InvalidDataException("Invalid frame plan dimensions.");
        int total=0;
        foreach(var frame in Frames)
        {
            if(frame is null || frame.Length<1 || frame.Length>=config.Depth) throw new InvalidDataException("Frame must fit within FIFO RAM.");
            total+=frame.Length;
            for(int index=0;index<frame.Length;index++)
            {
                var word=frame[index];
                if(word is null || word.Data is <0 or >65535 || word.Keep is <1 or >3 || word.Id is <0 or >15 || word.Dest is <0 or >15 || word.User is <0 or >3 || word.Last!=(index==frame.Length-1))
                    throw new InvalidDataException("Invalid frame payload or last boundary.");
            }
        }
        if(total>256 || Ready[0] || Ready.TakeLast(config.Depth+80).Any(value=>!value))
            throw new InvalidDataException("Frame plan lacks bounded final drain.");
    }
}
// A transaction reference, independent of RTL pointers and pipeline latency.
internal sealed class FrameScoreboard(FramePlan plan)
{
    readonly FrameWord[] source=plan.Frames.SelectMany(frame=>frame).ToArray();
    readonly Queue<FrameWord> committed=new();
    readonly List<FrameWord> assembling=[];
    FrameWord? held;
    public int Accepted {get;private set;}
    public int Consumed {get;private set;}
    public int Stalls {get;private set;}
    public int IncompleteHidden {get;private set;}
    public FrameWord? Input => Accepted<source.Length ? source[Accepted] : null;
    public void Observe(bool take,bool valid,bool ready,FrameWord? output)
    {
        if(held is not null && (!valid || output!=held)) throw new InvalidDataException("FRAME_MISMATCH stalled output changed");
        if(assembling.Count>0 && committed.Count==0 && !valid) IncompleteHidden++;
        if(valid)
        {
            if(committed.Count==0 || committed.Peek()!=output) throw new InvalidDataException("FRAME_MISMATCH incomplete frame or payload order");
            if(ready) { committed.Dequeue();Consumed++; } else Stalls++;
        }
        else if(output is not null) throw new InvalidDataException("FRAME_MISMATCH invalid output payload");
        if(take)
        {
            var input=Input ?? throw new InvalidDataException("FRAME_MISMATCH phantom acceptance");
            assembling.Add(input);Accepted++;
            if(input.Last) { foreach(var word in assembling) committed.Enqueue(word);assembling.Clear(); }
        }
        held=valid&&!ready ? output : null;
    }
    public void Complete()
    {
        if(Accepted!=source.Length || Consumed!=source.Length || assembling.Count!=0 || committed.Count!=0 || held is not null)
            throw new InvalidDataException("FRAME_DRAIN incomplete transaction workload");
    }
}
