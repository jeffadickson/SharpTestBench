using SharpTestBench;
using System.Runtime.InteropServices;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [UnmanagedCallersOnly] public static int Initialize(nint api,int size)=>Regression.Initialize(typeof(Entry).Assembly);
    static void Check(bool condition,string message){if(!condition)throw new InvalidDataException("CLOSURE_CONTRACT: "+message);}
    static T Throws<T>(Action action)where T:Exception {try{action();}catch(T error){return error;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    static async Task<T> Fails<T>(Func<Task> action)where T:Exception {try{await action();}catch(T error){return error;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    public readonly struct ByteWidth:IWidth{public static int Bits=>8;}
    public readonly struct HalfWidth:IWidth{public static int Bits=>16;}
    public readonly struct WordWidth:IWidth{public static int Bits=>32;}
    public readonly struct NibbleWidth:IWidth{public static int Bits=>4;}
    sealed class Fifo
    {
        readonly TestContext context;readonly Queue<byte> model=new();
        public int Accepted{get;private set;}public int Consumed{get;private set;}public int Dropped{get;private set;}
        Fifo(TestContext c)=>context=c;
        Signal Signal(string name)=>context.Dut.Child(name).Signal();
        uint Number(string name)=>(uint)context.Dut.Child(name).Signal<WordWidth>().ReadUInt().Value;
        byte Head=>(byte)context.Dut.Child("dout").Signal<ByteWidth>().ReadUInt().Value;
        public int Count=>model.Count;
        public static async Task<Fifo> Open(TestContext c){var f=new Fifo(c);await f.Step(reset:true);await f.Step();return f;}
        public async Task Step(bool push=false,bool pop=false,byte value=0,bool reset=false)
        {
            await Timer(1);Signal("clk").WriteBit(false,WriteAction.Immediate);Signal("rst").WriteBit(reset,WriteAction.Immediate);Signal("push").WriteBit(push,WriteAction.Immediate);Signal("pop").WriteBit(pop,WriteAction.Immediate);context.Dut.Child("din").Signal<ByteWidth>().Write(new UInt<ByteWidth>(value),WriteAction.Immediate);
            await Timer(1);if(!reset)Check(Head==(model.Count==0?0:model.Peek()),"FIFO head before edge");
            bool take=pop&&model.Count>0,put=push&&(model.Count<3||take);
            if(reset){Dropped+=model.Count;model.Clear();}else{if(take){model.Dequeue();Consumed++;}if(put){model.Enqueue(value);Accepted++;}}
            Signal("clk").WriteBit(true,WriteAction.Immediate);await Timer(1);await ReadOnly();
            Check(Number("count")==model.Count,"FIFO occupancy");Check(Signal("full").ReadBit()==(model.Count==3),"FIFO full");Check(Signal("empty").ReadBit()==(model.Count==0),"FIFO empty");Check(Head==(model.Count==0?0:model.Peek()),"FIFO head after edge");
        }
    }
}
