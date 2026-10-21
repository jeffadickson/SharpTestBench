using SharpTestBench;
using SharpTestBench.Fixtures;
using System.Runtime.InteropServices;
using System.Text.Json;
using static SharpTestBench.Triggers;

public static class Entry
{
    [UnmanagedCallersOnly]
    public static int Initialize(nint api,int size)=>Regression.Initialize(typeof(Entry).Assembly);
    public readonly struct W1:IWidth { public static int Bits=>1; }
    public readonly struct W2:IWidth { public static int Bits=>2; }
    public readonly struct W4:IWidth { public static int Bits=>4; }
    public readonly struct W16:IWidth { public static int Bits=>16; }
    [SimTest(TimeoutSteps=10000000)]
    public static async Task FrameTransactions(TestContext context)
    {
        var plan=JsonSerializer.Deserialize<FramePlan>(File.ReadAllText(Environment.GetEnvironmentVariable("FRAME_PLAN")!))!;
        var config=JsonSerializer.Deserialize<FrameConfig>(File.ReadAllText(Environment.GetEnvironmentVariable("FRAME_CONFIG")!))!;
        plan.Validate(config);
        var model=new FrameScoreboard(plan);
        var clk=context.Dut.Child("clk").Signal<W1>();var reset=context.Dut.Child("rst").Signal<W1>();
        var fault=context.Dut.Child("fault").Signal<W1>();
        var valid=context.Dut.Child("s_axis_tvalid").Signal<W1>();var ready=context.Dut.Child("m_axis_tready").Signal<W1>();
        var accept=context.Dut.Child("s_axis_tready").Signal<W1>();var output=context.Dut.Child("m_axis_tvalid").Signal<W1>();
        var data=context.Dut.Child("s_axis_tdata").Signal<W16>();var keep=context.Dut.Child("s_axis_tkeep").Signal<W2>();
        var last=context.Dut.Child("s_axis_tlast").Signal<W1>();var id=context.Dut.Child("s_axis_tid").Signal<W4>();
        var dest=context.Dut.Child("s_axis_tdest").Signal<W4>();var user=context.Dut.Child("s_axis_tuser").Signal<W2>();
        var outData=context.Dut.Child("m_axis_tdata").Signal<W16>();var outKeep=context.Dut.Child("m_axis_tkeep").Signal<W2>();
        var outLast=context.Dut.Child("m_axis_tlast").Signal<W1>();var outId=context.Dut.Child("m_axis_tid").Signal<W4>();
        var outDest=context.Dut.Child("m_axis_tdest").Signal<W4>();var outUser=context.Dut.Child("m_axis_tuser").Signal<W2>();
        var trace=new List<int[]>();
        for(int cycle=0;cycle<plan.Ready.Length;cycle++)
        {
            bool rst=cycle==0;var input=rst?null:model.Input;var word=input??new FrameWord(0,0,false,0,0,0);
            clk.WriteBit(false);reset.WriteBit(rst);fault.WriteBit(Environment.GetEnvironmentVariable("FRAME_FAULT")=="1");
            valid.WriteBit(input is not null);ready.WriteBit(plan.Ready[cycle]);
            data.Write(new UInt<W16>(word.Data));keep.Write(new UInt<W2>(word.Keep));last.WriteBit(word.Last);
            id.Write(new UInt<W4>(word.Id));dest.Write(new UInt<W4>(word.Dest));user.Write(new UInt<W2>(word.User));
            await Timer(1);await ReadOnly();
            bool take=!rst&&input is not null&&accept.ReadBit(),hasOutput=!rst&&output.ReadBit();
            FrameWord? actual=hasOutput?new((int)outData.ReadUInt().Value,(int)outKeep.ReadUInt().Value,outLast.ReadBit(),
                (int)outId.ReadUInt().Value,(int)outDest.ReadUInt().Value,(int)outUser.ReadUInt().Value):null;
            if(!rst)model.Observe(take,hasOutput,plan.Ready[cycle],actual);
            trace.Add([cycle,rst?1:0,take?1:0,hasOutput?1:0,plan.Ready[cycle]?1:0,actual?.Data??0,actual?.Keep??0,
                actual?.Last==true?1:0,actual?.Id??0,actual?.Dest??0,actual?.User??0]);
            await Timer(1);clk.WriteBit(true);await Timer(1);await ReadOnly();await Timer(1);
        }
        model.Complete();
        File.WriteAllText(Environment.GetEnvironmentVariable("FRAME_REPORT")!,JsonSerializer.Serialize(new {
            trace,accepted=model.Accepted,consumed=model.Consumed,flushed=0,stalls=model.Stalls,resets=1,cycles=plan.Ready.Length }));
    }
}
