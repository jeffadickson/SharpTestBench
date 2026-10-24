using SharpTestBench;
using static SharpTestBench.Triggers;
using System.Numerics;
using System.Runtime.InteropServices;

/// <summary>Native contracts for locally authored CRC, arbiter and register-bank fixtures.</summary>
public static partial class Entry
{
    [UnmanagedCallersOnly] public static int Initialize(nint api,int size)=>Regression.Initialize(typeof(Entry).Assembly,new RegressionSelection([@"^Entry\.R"],matchTimeout:TimeSpan.FromSeconds(1)));
    static void Check(bool condition,string context){if(!condition)throw new InvalidDataException("PROTOCOL200: "+context);}
    static Signal Signal(TestContext c,string name)=>c.Dut.Child(name).Signal();
    static int Read(TestContext c,string name)=>Convert.ToInt32(Signal(c,name).ReadBits(),2);
    static void Put(TestContext c,string name,int value)=>Signal(c,name).WriteBits(Convert.ToString(value,2).PadLeft(Signal(c,name).Width,'0'),WriteAction.Immediate);
    static async Task Tick(TestContext c){Put(c,"clk",0);await Timer(2);Put(c,"clk",1);await Timer(2);}
    static async Task Reset(TestContext c)
    {
        foreach(string name in new[]{"c_valid","c_last","c_ready","c_data","a_request","a_enable","a_ack","b_read","b_write","b_address","b_mask","b_data"})Put(c,name,0);
        Put(c,"rst",1);await Tick(c);Put(c,"rst",0);await Timer(1);
    }
    // Independent whole-message polynomial long division, rather than the RTL's
    // per-byte shift register recurrence. The remainder of M(x)*x^8 is CRC-8/0x107.
    static int Crc(IReadOnlyList<int> packet)
    {
        BigInteger dividend=0;foreach(int value in packet)dividend=(dividend<<8)|value;
        dividend<<=8;
        while(dividend.GetBitLength()>8){int shift=checked((int)dividend.GetBitLength()-9);dividend^=(BigInteger)0x107<<shift;}
        return (int)dividend;
    }
    sealed record CrcWord(int Data,int Crc,int Length,bool Last);
    sealed class CrcModel
    {
        readonly Queue<CrcWord> pending=new();readonly List<int> packet=[];
        internal int Accepted {get;private set;} internal int Delivered {get;private set;}
        internal void Observe(TestContext c)
        {
            Check(Read(c,"c_out_valid")== (pending.Count==0?0:1),"CRC valid versus accepted transaction queue");
            if(pending.TryPeek(out var word))
            {
                Check(Read(c,"c_out_data")==word.Data,"CRC data conservation");Check(Read(c,"c_out_crc")==word.Crc,"whole-packet polynomial oracle");
                Check(Read(c,"c_out_length")==word.Length,"accepted-byte packet length");Check(Read(c,"c_out_last")== (word.Last?1:0),"packet boundary conservation");
            }
        }
        internal async Task Step(TestContext c,bool valid,int data,bool last,bool ready)
        {
            Put(c,"c_valid",valid?1:0);Put(c,"c_data",data);Put(c,"c_last",last?1:0);Put(c,"c_ready",ready?1:0);await Timer(1);
            Observe(c);
            bool accept=valid&&Read(c,"c_in_ready")==1;
            if(ready&&pending.Count>0){pending.Dequeue();Delivered++;}
            if(accept){packet.Add(data);pending.Enqueue(new(data,Crc(packet),packet.Count&0xffff,last));Accepted++;if(last)packet.Clear();}
            await Tick(c);Observe(c);Check(Accepted-Delivered==pending.Count,"CRC occupancy conservation");
        }
        internal async Task Flush(TestContext c)
        {for(int n=0;n<3;n++)await Step(c,false,0,false,true);Check(Accepted==Delivered,"CRC drain exact accounting");}
    }
    static async Task Stream(TestContext c,int[][] packets,int sourcePeriod=1,int stallPeriod=0)
    {
        await Reset(c);var model=new CrcModel();int cycle=0;
        foreach(var packet in packets)for(int index=0;index<packet.Length;index++)
        {
            bool accepted=false;
            while(!accepted)
            {
                Check(cycle<10000,"bounded stream schedule");bool valid=cycle%sourcePeriod==0;bool ready=stallPeriod==0||cycle%stallPeriod!=0;
                int before=model.Accepted;await model.Step(c,valid,packet[index],index==packet.Length-1,ready);accepted=model.Accepted!=before;cycle++;
            }
        }
        await model.Flush(c);
    }
    // Deposit an incorrect native output between clock edges. Observe must reject
    // it before dequeuing a transaction; restoration then proves model recovery.
    static string FaultMarker(string signal)=>"PROTOCOL200: "+(signal switch
    {
        "c_out_crc"=>"whole-packet polynomial oracle", "c_out_data"=>"CRC data conservation",
        "c_out_length"=>"accepted-byte packet length", "c_out_last"=>"packet boundary conservation",
        "a_pointer"=>"rotating list grant oracle", "b_result"=>"bank read result oracle",
        _=>throw new ArgumentException("Unknown native fault coordinate",nameof(signal))
    });
    // Reject only the intended oracle assertion, not an unrelated read failure.
    // The actual register must change, and restoration runs even on failed checks.
    static async Task Fault(TestContext c,CrcModel model,string signal,int wrong)
    {
        int original=Read(c,signal),accepted=model.Accepted,delivered=model.Delivered;
        string marker=FaultMarker(signal);Check(wrong!=original,"fault must change native output");
        try
        {
            Put(c,signal,wrong);await Timer(1);Check(Read(c,signal)==wrong,"native output fault was deposited");
            bool detected=false;try{model.Observe(c);}catch(InvalidDataException error)when(error.Message==marker){detected=true;}
            Check(detected,"native output fault must fail its specific oracle: "+signal);
            Check(model.Accepted==accepted&&model.Delivered==delivered,"fault check cannot consume a transaction");
        }
        finally{Put(c,signal,original);await Timer(1).WithCancellation(CancellationToken.None);}
        model.Observe(c);
    }
    static async Task NativeFault(TestContext c,string signal,int wrong,Action observe)
    {
        int original=Read(c,signal);string marker=FaultMarker(signal);Check(wrong!=original,"fault must change native register");
        try
        {
            Put(c,signal,wrong);await Timer(1);Check(Read(c,signal)==wrong,"native register fault was deposited");
            bool detected=false;try{observe();}catch(InvalidDataException error)when(error.Message==marker){detected=true;}
            Check(detected,"native fault must reach its specific output oracle: "+signal);
        }
        finally{Put(c,signal,original);await Timer(1).WithCancellation(CancellationToken.None);}
        observe();
    }
    static int[] Bytes(TestContext c,int length)=>Enumerable.Range(0,length).Select(_=>c.Random.Next(256)).ToArray();
    // A list rotation is independent of the RTL's offset/index scan.
    sealed class ArbiterModel
    {
        readonly List<int> order=[0,1,2,3];
        internal int Grant(int request,bool enable)=>!enable?0:order.Where(port=>(request&(1<<port))!=0).Select(port=>1<<port).FirstOrDefault();
        internal void Observe(TestContext c,int request,bool enable)=>Check(Read(c,"a_grant")==Grant(request,enable),"rotating list grant oracle");
        internal async Task Step(TestContext c,int request,bool enable,bool acknowledge)
        {
            Put(c,"a_request",request);Put(c,"a_enable",enable?1:0);Put(c,"a_ack",acknowledge?1:0);await Timer(1);
            int grant=Grant(request,enable);Check(Read(c,"a_grant")==grant,"rotating list grant oracle");
            Check((grant&~request)==0&&(grant&(grant-1))==0,"grant is requested and one-hot-or-zero");
            if(grant!=0&&acknowledge){int winner=BitOperations.TrailingZeroCount((uint)grant);while(order[0]!=winner){int first=order[0];order.RemoveAt(0);order.Add(first);}order.RemoveAt(0);order.Add(winner);}
            await Tick(c);Check(Read(c,"a_grant")==Grant(request,enable),"acknowledged pointer transition");
        }
    }
    sealed class BankModel
    {
        readonly int[] words=new int[8];int result;
        internal void Observe(TestContext c)
        {
            Check(Read(c,"b_result")==result,"bank read result oracle");
            for(int index=0;index<8;index++)Check(Read(c,"b_obs"+index)==words[index],"byte-enable bank isolation at "+index);
        }
        internal async Task Step(TestContext c,int address,int data,int mask,bool write,bool read,bool reset=false)
        {
            Put(c,"b_address",address);Put(c,"b_data",data);Put(c,"b_mask",mask);Put(c,"b_write",write?1:0);Put(c,"b_read",read?1:0);Put(c,"rst",reset?1:0);
            await Timer(1);Check(Read(c,"b_result")==result,"bank output changes only on a clock edge");
            if(reset){Array.Clear(words);result=0;}
            else
            {
                if(read)result=words[address]; // Snapshot before write proves collision policy.
                if(write)for(int lane=0;lane<2;lane++)if((mask&(1<<lane))!=0){int bits=0xff<<(lane*8);words[address]=(words[address]&~bits)|(data&bits);}
            }
            await Tick(c);Check(Read(c,"b_result")==result,"synchronous read/hold/read-before-write oracle");
            for(int index=0;index<8;index++)Check(Read(c,"b_obs"+index)==words[index],"byte-enable bank isolation at "+index);
            Put(c,"rst",0);
        }
    }
}