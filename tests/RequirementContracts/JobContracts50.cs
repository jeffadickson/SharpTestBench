using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest] public static async Task R046_InvalidInitialNameCannotEnqueueAJob(TestContext c)
    {
        bool ran=false;Throws<ArgumentException>(()=>c.StartSoon(_=>{ran=true;return Task.CompletedTask;}," \t"));
        var valid=c.StartSoon(_=>Task.CompletedTask);await valid;Check(!ran&&valid.Name=="job-1","invalid initial name reserves no job or callback");
    }
    [SimTest] public static async Task R047_TaskLocalDistinguishesStoredNullFromMissing(TestContext c)
    {
        var key=new SimTaskLocal<string?>();var job=c.StartSoon(_=>{Check(!key.TryGetValue(out var ignored1)&&key.Value is null,"missing local default");key.Value=null;
            Check(key.TryGetValue(out var value)&&value is null,"stored null is present");Check(key.Clear()&&!key.Clear()&&!key.TryGetValue(out var ignored2),"local Clear presence");return Task.CompletedTask;});await job;
    }
    [SimTest] public static async Task R048_SameTypedLocalKeysRemainIndependent(TestContext c)
    {
        var a=new SimTaskLocal<int>();var b=new SimTaskLocal<int>();var job=c.StartSoon(async _=>{a.Value=7;b.Value=9;await Timer(1);a.Clear();Check(!a.TryGetValue(out var ignored3)&&b.Value==9,"typed keys use identity not type");});await job;
    }
    [SimTest] public static async Task R049_ChildLocalMutationCannotChangeParent(TestContext c)
    {
        var key=new SimTaskLocal<int>();var parent=c.StartSoon(async _=>{key.Value=5;var child=c.StartSoon(async _=>{Check(!key.TryGetValue(out var ignored4),"no local inheritance");key.Value=7;await Timer(1);});await child;Check(key.Value==5,"child local isolation");});await parent;
    }
    [SimTest] public static async Task R050_TaskLocalRejectsForeignThreadAndTestBody(TestContext c)
    {
        var key=new SimTaskLocal<int>();Throws<InvalidOperationException>(()=>_ = key.Value);
        var job=c.StartSoon(_=>{key.Value=50;Exception? failure=null;var thread=new Thread(()=>{try{key.Value=99;}catch(Exception error){failure=error;}});thread.Start();thread.Join();Check(failure is InvalidOperationException&&key.Value==50,"local foreign-thread noninterference");return Task.CompletedTask;});await job;
    }
}
