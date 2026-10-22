using SharpTestBench;
using static SharpTestBench.Triggers;
public static partial class Entry
{
    [SimTest] public static async Task R011_FirstCompletedTieUsesInputOrder(TestContext c)
    {
        Task left=Task.FromResult(1),right=Task.FromResult(2);
        Check(ReferenceEquals(left,await SimTasks.First(left,right,left)),"first completed tie identity");
    }
    [SimTest] public static async Task R012_FirstBorrowsItsLosingJob(TestContext c)
    {
        bool finished=false;var loser=c.StartSoon(async _=>{await Timer(5);finished=true;});
        var winner=c.StartSoon(async _=>await Timer(2));
        Check(ReferenceEquals(await SimTasks.First(loser.Task,winner.Task),winner.Task),"first winner");
        Check(!finished&&!loser.IsCompleted,"loser still pending");await loser;Check(finished,"borrowed loser survives");
    }
    [SimTest] public static async Task R013_FirstCancelledWinnerPreservesLoser(TestContext c)
    {
        var loser=c.StartSoon(async _=>await Timer(3));var cancelled=Task.FromCanceled(new CancellationToken(true));
        await Fails<TaskCanceledException>(()=>SimTasks.First(cancelled,loser.Task));
        Check(!loser.IsCompleted,"cancelled winner did not own loser");await loser;
    }
    [SimTest] public static async Task R014_FirstCancellationOnlyCancelsWaiter(TestContext c)
    {
        using var source=new CancellationTokenSource();var a=c.StartSoon(async _=>await Timer(3));var b=c.StartSoon(async _=>await Timer(5));
        var waiting=SimTasks.First(source.Token,a.Task,b.Task);source.Cancel();await Fails<OperationCanceledException>(()=>waiting);
        Check(!a.IsCompleted&&!b.IsCompleted,"external cancellation preserves inputs");await a;await b;
    }
    [SimTest] public static async Task R015_CombineWaitsForReverseCompletion(TestContext c)
    {
        var order=new List<int>();var first=c.StartSoon(async _=>{await Timer(5);order.Add(1);});var second=c.StartSoon(async _=>{await Timer(2);order.Add(2);});
        await SimTasks.Combine(first.Task,second.Task);Check(order.SequenceEqual(new[]{2,1}),"combine all completion");
    }
}
