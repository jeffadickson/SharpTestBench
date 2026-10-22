using SharpTestBench;
using static SharpTestBench.Triggers;
using System.Runtime.InteropServices;
public static partial class Entry
{
    [UnmanagedCallersOnly] public static int Initialize(nint api,int size)=>Regression.Initialize(typeof(Entry).Assembly);
    static void Check(bool value,string message){if(!value)throw new InvalidDataException("SCOPE200: "+message);}
    static T Throws<T>(Action action) where T:Exception {try{action();}catch(T error){return error;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    static Signal S(TestContext c,string name)=>c.Dut.Child(name).Signal();
    static int N(TestContext c,string name)=>Convert.ToInt32(S(c,name).ReadBits(),2);
    static void Put(TestContext c,string name,int value)=>S(c,name).WriteBits(Convert.ToString(value,2).PadLeft(S(c,name).Width,'0'),WriteAction.Immediate);
    static async Task Tick(TestContext c){Put(c,"clk",0);await Timer(2);Put(c,"clk",1);await Timer(2);}
    static async Task Reset(TestContext c){Put(c,"s_valid",0);Put(c,"m_ready",0);Put(c,"rst",1);await Tick(c);Put(c,"rst",0);await Timer(1);}
    static async Task Held(TestContext c,string name,string original,string replacement)
    {
        var obj=c.Dut.Child(name);var signal=obj.Signal();signal.WriteBits(original,WriteAction.Immediate);
        try{obj.Freeze();signal.WriteBits(replacement);await Timer(1);Check(signal.ReadBits()==original,"freeze hold "+name);}
        finally{signal.Release();}
        signal.WriteBits(replacement);await Timer(1);Check(signal.ReadBits()==replacement,"release restores writes "+name);
    }
    static CoverageBin<int> B(string name,Func<int,bool> matches)=>new(name,matches);
    static CoverageBin<int>[][] Axes(int rank)=>Enumerable.Range(0,rank).Select(axis=>new[]{B("zero",value=>(value&(1<<axis))==0),B("one",value=>(value&(1<<axis))!=0)}).ToArray();
    static int NativeSample(TestContext c,int value){Put(c,"f_byte",value);return N(c,"f_byte");}
}
