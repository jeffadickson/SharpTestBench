using SharpTestBench;
using static SharpTestBench.Triggers;
using System.Runtime.InteropServices;
public static partial class Entry
{
    [UnmanagedCallersOnly] public static int Initialize(nint api,int size)=>Regression.Initialize(typeof(Entry).Assembly);
    static void Check(bool value,string message){if(!value)throw new InvalidDataException("CONFORMANCE200: "+message);}
    static T Throws<T>(Action action) where T:Exception {try{action();}catch(T error){return error;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    static async Task<T> Fails<T>(Func<Task> action) where T:Exception {try{await action();}catch(T error){return error;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    static Signal Bit(TestContext c,string name)=>c.Dut.Child(name).Signal();
    static Signal<W8> Byte(TestContext c,string name)=>c.Dut.Child(name).Signal<W8>();
    static Signal<W32> Word(TestContext c,string name)=>c.Dut.Child(name).Signal<W32>();
    static async Task Tick(TestContext c){Bit(c,"clk").WriteBit(false);await Timer(2);Bit(c,"clk").WriteBit(true);await Timer(2);}
    static async Task Reset(TestContext c)
    {
        Bit(c,"rst").WriteBit(true);Bit(c,"p_svalid").WriteBit(false);Bit(c,"p_mready").WriteBit(false);Bit(c,"rb_write").WriteBit(false);Bit(c,"a_enable").WriteBit(false);Bit(c,"a_load").WriteBit(false);Bit(c,"a_clear").WriteBit(false);
        await Tick(c);Bit(c,"rst").WriteBit(false);await Timer(1);
    }
    sealed class PipeModel(TestContext context)
    {
        // The two-stage model checks cycle timing; the queue independently checks transport.
        readonly Queue<byte> transactions=new();
        bool v0,v1;byte d0,d1;public int Accepted{get;private set;}public int Discarded{get;private set;}public List<byte> Received{get;}=[];
        public int Count=>(v0?1:0)+(v1?1:0);
        public bool OutputValid=>v1;public byte Output=>d1;
        public async Task Step(bool valid,byte data,bool ready,CoveragePoint<(int Occupancy,int Transfer)>? coverage=null)
        {
            Bit(context,"p_svalid").WriteBit(valid);Byte(context,"p_sdata").Write(new UInt<W8>(data));Bit(context,"p_mready").WriteBit(ready);await Timer(1);
            bool r1=!v1||ready,r0=!v0||r1;
            bool actualReady=Bit(context,"p_sready").ReadBit(),actualValid=Bit(context,"p_mvalid").ReadBit();
            Check(actualReady==r0,"elastic ready/timing model mismatch");Check(actualValid==v1,"elastic valid/timing model mismatch");
            int occupancy=Convert.ToInt32(context.Dut.Child("p_count").Signal().ReadBits(),2);Check(occupancy==Count,"elastic occupancy/model mismatch");
            Check(occupancy==transactions.Count,"native occupancy differs from handshake queue");
            bool incoming=valid&&actualReady,outgoing=actualValid&&ready;
            byte actualOutput=(byte)Byte(context,"p_mdata").ReadUInt().Value;
            if(actualValid)Check(transactions.Count>0&&transactions.Peek()==actualOutput,"native output differs from oldest accepted transaction");
            if(v1)Check((byte)Byte(context,"p_mdata").ReadUInt().Value==d1,"held elastic output differs from timing model");
            coverage?.Sample((occupancy,(incoming?1:0)|(outgoing?2:0)));
            if(outgoing){transactions.Dequeue();Received.Add(actualOutput);}
            if(incoming){transactions.Enqueue(data);Accepted++;}
            Check(Accepted==Received.Count+Discarded+transactions.Count,"handshake queue conservation");
            bool nextV1=r1?v0:v1;byte nextD1=r1&&v0?d0:d1;
            bool nextV0=r0?valid:v0;byte nextD0=r0&&valid?data:d0;
            await Tick(context);v0=nextV0;v1=nextV1;d0=nextD0;d1=nextD1;
            Check(Bit(context,"p_mvalid").ReadBit()==v1,"elastic next-state valid");if(v1)Check((byte)Byte(context,"p_mdata").ReadUInt().Value==d1,"elastic next-state payload");
        }
        public async Task ResetState(){Discarded+=transactions.Count;transactions.Clear();await Reset(context);v0=v1=false;d0=d1=0;Check(Accepted==Received.Count+Discarded,"reset discard conservation");}
        public async Task Drain(){for(int i=0;i<4&&Count!=0;i++)await Step(false,0,true);Check(Count==0,"elastic drain left an item");}
    }
}
