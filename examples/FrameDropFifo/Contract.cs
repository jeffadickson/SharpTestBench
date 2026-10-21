namespace SharpTestBench.Fixtures;

internal sealed class FrameDropModel(FramePlan plan)
{
    readonly FrameWord[] source=plan.Frames.SelectMany(frame=>frame).ToArray();
    readonly Queue<FrameWord> committed=new();readonly List<FrameWord> assembling=[];
    FrameWord? held;bool expectedBad,expectedGood;
    public int Accepted{get;private set;}public int Consumed{get;private set;}public int Discarded{get;private set;}
    public int BadFrames{get;private set;}public int GoodFrames{get;private set;}public int Stalls{get;private set;}public int IncompleteHidden{get;private set;}
    public FrameWord? Input=>Accepted<source.Length?source[Accepted]:null;
    public void Observe(bool take,bool valid,bool ready,FrameWord? output,bool bad,bool good)
    {
        if(bad!=expectedBad || good!=expectedGood)throw new InvalidDataException("FRAME_DROP_MISMATCH status pulse");
        expectedBad=expectedGood=false;
        if(held is not null&&(!valid||held!=output))throw new InvalidDataException("FRAME_DROP_MISMATCH stalled output changed");
        if(assembling.Count>0&&committed.Count==0&&!valid)IncompleteHidden++;
        if(valid)
        {
            if(committed.Count==0||committed.Peek()!=output)throw new InvalidDataException("FRAME_DROP_MISMATCH dropped frame leaked or good payload order");
            if(ready){committed.Dequeue();Consumed++;}else Stalls++;
        }
        else if(output is not null)throw new InvalidDataException("FRAME_DROP_MISMATCH invalid output payload");
        if(take)
        {
            var input=Input??throw new InvalidDataException("FRAME_DROP_MISMATCH phantom acceptance");
            Accepted++;assembling.Add(input);
            if(input.Last)
            {
                if((input.User&1)!=0){Discarded+=assembling.Count;BadFrames++;expectedBad=true;}
                else{foreach(var word in assembling)committed.Enqueue(word);GoodFrames++;expectedGood=true;}
                assembling.Clear();
            }
        }
        held=valid&&!ready?output:null;
    }
    public void Complete()
    {
        if(Accepted!=source.Length||Consumed+Discarded!=source.Length||assembling.Count!=0||committed.Count!=0||held is not null||BadFrames+GoodFrames!=plan.Frames.Length||expectedBad||expectedGood)
            throw new InvalidDataException("FRAME_DROP_DRAIN incomplete transaction workload");
    }
}
