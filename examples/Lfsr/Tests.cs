using SharpTestBench;
using System.Runtime.InteropServices;
using System.Text.Json;
using static SharpTestBench.Triggers;

/// <summary>Independent bit-serial model for the combinational upstream LFSR.</summary>
public static class Entry
{
    /// <summary>Host entry point.</summary>
    [UnmanagedCallersOnly]
    public static int Initialize(nint p,int n) => Regression.Initialize(typeof(Entry).Assembly);
    /// <summary>Two independent output corruption controls.</summary>
    public readonly struct W2 : IWidth { public static int Bits => 2; }
    /// <summary>Serial register configuration.</summary>
    public sealed record Settings(int Width,ulong Polynomial,int DataWidth,string Topology,bool Reverse,bool FeedForward);
    /// <summary>Input and independently calculated expected outputs.</summary>
    public sealed record Vector(ulong State,ulong Data,ulong ExpectedState,ulong ExpectedData);
    /// <summary>Deterministic stimulus.</summary>
    public sealed record Stimulus(Settings Config,ulong Seed,Vector[] Vectors);

    static bool[] Bits(ulong value,int width) => Enumerable.Range(0,width).Select(i => ((value>>i)&1)!=0).ToArray();
    static ulong Number(bool[] bits)
    {
        ulong value=0;
        for(int i=0;i<bits.Length;i++) if(bits[i]) value|=1UL<<i;
        return value;
    }
    static (ulong State,ulong Data) Reference(Settings c,ulong state,ulong data)
    {
        var register=Bits(state,c.Width); var input=Bits(data,c.DataWidth); var output=new bool[c.DataWidth];
        if(c.Reverse) { Array.Reverse(register); Array.Reverse(input); }
        for(int bit=c.DataWidth-1;bit>=0;bit--)
        {
            bool feedback=register[^1]^input[bit];
            if(c.Topology=="FIBONACCI")
                for(int tap=1;tap<c.Width;tap++) if(((c.Polynomial>>tap)&1)!=0) feedback^=register[tap-1];
            output[bit]=feedback;
            bool injected=c.FeedForward?input[bit]:feedback;
            for(int i=register.Length-1;i>0;i--) register[i]=register[i-1];
            register[0]=injected;
            if(c.Topology=="GALOIS")
                for(int tap=1;tap<c.Width;tap++) if(((c.Polynomial>>tap)&1)!=0) register[tap]^=injected;
        }
        if(c.Reverse) { Array.Reverse(register); Array.Reverse(output); }
        return (Number(register),Number(output));
    }
    /// <summary>Check every bit of both outputs, including basis and random inputs.</summary>
    [SimTest(TimeoutSteps=10000000)]
    public static async Task SerialScoreboard(TestContext c)
    {
        var plan=JsonSerializer.Deserialize<Stimulus>(File.ReadAllText(Environment.GetEnvironmentVariable("LFSR_PLAN")!))!;
        var state=c.Dut.Child("state_in").Signal<W64>(); var data=c.Dut.Child("data_in").Signal<W64>();
        var next=c.Dut.Child("state_out").Signal<W64>(); var output=c.Dut.Child("data_out").Signal<W64>();
        var fault=c.Dut.Child("fault").Signal<W2>();
        var trace=new List<ulong[]>();
        int mode=int.Parse(Environment.GetEnvironmentVariable("LFSR_FAULT")??"0");
        for(int index=0;index<plan.Vectors.Length;index++)
        {
            var row=plan.Vectors[index];
            var expected=Reference(plan.Config,row.State,row.Data);
            if(expected.State!=row.ExpectedState || expected.Data!=row.ExpectedData)
                throw new Exception($"LFSR_PLAN_MISMATCH vector={index}");
            state.Write(new UInt<W64>(row.State)); data.Write(new UInt<W64>(row.Data)); fault.Write(new UInt<W2>(mode));
            await Timer(1); await ReadOnly();
            ulong actualState=(ulong)next.ReadUInt().Value, actualData=(ulong)output.ReadUInt().Value;
            if(actualState!=expected.State || actualData!=expected.Data)
                throw new Exception($"LFSR_MISMATCH vector={index} state={actualState:x}/{expected.State:x} data={actualData:x}/{expected.Data:x}");
            trace.Add([(ulong)index,row.State,row.Data,actualState,actualData]);
            await Timer(1);
        }
        if(trace.Count==0) throw new Exception("LFSR_EMPTY");
        File.WriteAllText(Environment.GetEnvironmentVariable("LFSR_REPORT")!,JsonSerializer.Serialize(new { trace, vectors=trace.Count }));
    }
}
