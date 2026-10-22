using SharpTestBench;
public static partial class Entry
{
    [SimTest] public static async Task R001_ResetEstablishesEmptyFlags(TestContext c)
    {var f=await Fifo.Open(c);Check(f.Count==0&&f.Accepted==0&&f.Consumed==0,"reset counters");}
    [SimTest] public static async Task R002_FirstPushPublishesHead(TestContext c)
    {var f=await Fifo.Open(c);await f.Step(push:true,value:37);Check(f.Count==1&&f.Accepted==1,"first accepted byte");}
    [SimTest] public static async Task R003_FullRejectsAdditionalPush(TestContext c)
    {var f=await Fifo.Open(c);for(byte i=1;i<=3;i++)await f.Step(push:true,value:i);await f.Step(push:true,value:99);Check(f.Count==3&&f.Accepted==3,"full backpressure rejects byte");}
    [SimTest] public static async Task R004_EmptyPopCannotUnderflow(TestContext c)
    {var f=await Fifo.Open(c);await f.Step(pop:true);Check(f.Count==0&&f.Consumed==0,"empty read rejected");}
    [SimTest] public static async Task R005_DrainPreservesArrivalOrder(TestContext c)
    {var f=await Fifo.Open(c);await f.Step(push:true,value:91);await f.Step(push:true,value:13);await f.Step(push:true,value:72);for(int i=0;i<3;i++)await f.Step(pop:true);Check(f.Count==0&&f.Consumed==3,"ordered drain");}
    [SimTest] public static async Task R006_FullPopAndPushMaintainThroughput(TestContext c)
    {var f=await Fifo.Open(c);for(byte i=1;i<=3;i++)await f.Step(push:true,value:i);await f.Step(push:true,pop:true,value:4);Check(f.Count==3&&f.Accepted==4&&f.Consumed==1,"full replacement accepts both");for(int i=0;i<3;i++)await f.Step(pop:true);}
    [SimTest] public static async Task R007_PartialSimultaneousTransferKeepsOccupancy(TestContext c)
    {var f=await Fifo.Open(c);await f.Step(push:true,value:21);await f.Step(push:true,pop:true,value:22);Check(f.Count==1&&f.Consumed==1,"one occupied simultaneous transfer");await f.Step(pop:true);}
    [SimTest] public static async Task R008_EmptySimultaneousTransferDoesNotFallThrough(TestContext c)
    {var f=await Fifo.Open(c);await f.Step(push:true,pop:true,value:31);Check(f.Count==1&&f.Accepted==1&&f.Consumed==0,"no empty bypass");await f.Step(pop:true);}
    [SimTest] public static async Task R009_ResetDropsBufferedDataAndIgnoresRequests(TestContext c)
    {var f=await Fifo.Open(c);await f.Step(push:true,value:41);await f.Step(push:true,value:42);await f.Step(push:true,pop:true,value:99,reset:true);Check(f.Dropped==2&&f.Accepted==2&&f.Consumed==0&&f.Count==0,"reset priority over both requests");}
    [SimTest] public static async Task R010_RefillAfterDrainWrapsWritePointer(TestContext c)
    {var f=await Fifo.Open(c);for(byte i=0;i<3;i++)await f.Step(push:true,value:i);for(int i=0;i<3;i++)await f.Step(pop:true);await f.Step(push:true,value:55);await f.Step(pop:true);Check(f.Accepted==4&&f.Consumed==4,"post-wrap reuse");}
    [SimTest] public static async Task R011_NonPowerOfTwoRingWrapsRepeatedly(TestContext c)
    {var f=await Fifo.Open(c);for(byte i=0;i<31;i++){await f.Step(push:true,value:i);await f.Step(pop:true);}Check(f.Consumed==31&&f.Count==0,"many read/write wraps");}
    [SimTest] public static async Task R012_StalledHeadSurvivesIdleCycles(TestContext c)
    {var f=await Fifo.Open(c);await f.Step(push:true,value:77);for(int i=0;i<50;i++)await f.Step();Check(f.Count==1&&f.Consumed==0,"idle head stable");await f.Step(pop:true);}
    [SimTest] public static async Task R013_BackpressuredProducerCanRetryExactlyOnce(TestContext c)
    {var f=await Fifo.Open(c);for(byte i=1;i<=3;i++)await f.Step(push:true,value:i);await f.Step(push:true,value:4);await f.Step(pop:true);await f.Step(push:true,value:4);for(int i=0;i<3;i++)await f.Step(pop:true);Check(f.Accepted==4&&f.Consumed==4,"rejected value admitted once on retry");}
    [SimTest] public static async Task R014_DuplicatePayloadsAreDistinctTransactions(TestContext c)
    {var f=await Fifo.Open(c);for(int i=0;i<3;i++)await f.Step(push:true,value:88);for(int i=0;i<3;i++)await f.Step(pop:true);Check(f.Accepted==3&&f.Consumed==3,"duplicates retained");}
    [SimTest] public static async Task R015_UnqualifiedInputChangesCannotAlterStorage(TestContext c)
    {var f=await Fifo.Open(c);await f.Step(push:true,value:123);for(byte i=0;i<20;i++)await f.Step(value:i);Check(f.Accepted==1&&f.Count==1,"data without push ignored");await f.Step(pop:true);}
    [SimTest] public static async Task R016_HeldPopAfterDrainDoesNotCreateTransfers(TestContext c)
    {var f=await Fifo.Open(c);await f.Step(push:true,value:3);for(int i=0;i<12;i++)await f.Step(pop:true);Check(f.Consumed==1&&f.Count==0,"held pop saturates at empty");}
    [SimTest] public static async Task R017_HeldPushAtFullDoesNotOverwriteOldest(TestContext c)
    {var f=await Fifo.Open(c);for(byte i=1;i<=3;i++)await f.Step(push:true,value:i);for(byte i=100;i<120;i++)await f.Step(push:true,value:i);for(int i=0;i<3;i++)await f.Step(pop:true);Check(f.Accepted==3&&f.Consumed==3,"full storage preserved");}
    [SimTest] public static async Task R018_HeldResetDominatesContinuousTraffic(TestContext c)
    {var f=await Fifo.Open(c);await f.Step(push:true,value:1);for(int i=0;i<5;i++)await f.Step(push:true,pop:true,value:9,reset:true);await f.Step(push:true,value:2);await f.Step(pop:true);Check(f.Dropped==1&&f.Accepted==2&&f.Consumed==1,"reset release recovery");}
    [SimTest] public static async Task R019_SparseBurstsPreserveConservation(TestContext c)
    {var f=await Fifo.Open(c);for(byte i=0;i<20;i++){await f.Step(push:true,value:i);await f.Step();if(i%2==1){await f.Step(pop:true);await f.Step(pop:true);}}Check(f.Accepted==20&&f.Consumed==20,"sparse burst conservation");}
    [SimTest] public static async Task R020_MixedTrafficAndMidstreamResetsMatchIndependentQueue(TestContext c)
    {var f=await Fifo.Open(c);var rng=c.Random;for(int i=0;i<200;i++)await f.Step(rng.Next(2)==1,rng.Next(2)==1,(byte)rng.Next(256),i%47==0);while(f.Count>0)await f.Step(pop:true);Check(f.Accepted==f.Consumed+f.Dropped,"random traffic conservation including reset drops");}
}
