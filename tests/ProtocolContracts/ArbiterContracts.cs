using SharpTestBench;
using static SharpTestBench.Triggers;
/// <summary>Rotating arbitration: admission, acknowledged movement, withdrawal and fairness.</summary>
public static partial class Entry
{
 static async Task Arbiter(TestContext c,params (int Requests,bool Enable,bool Ack)[] steps){await Reset(c);var m=new ArbiterModel();foreach(var s in steps)await m.Step(c,s.Requests,s.Enable,s.Ack);}
 [SimTest] public static Task R033_NoRequestProducesNoGrant(TestContext c)=>Arbiter(c,(0,true,true),(0,true,false));
 [SimTest] public static Task R034_PortZeroCanWinAlone(TestContext c)=>Arbiter(c,(1,true,true),(1,true,true));
 [SimTest] public static Task R035_PortOneCanWinAlone(TestContext c)=>Arbiter(c,(2,true,true),(2,true,true));
 [SimTest] public static Task R036_PortTwoCanWinAlone(TestContext c)=>Arbiter(c,(4,true,true),(4,true,true));
 [SimTest] public static Task R037_PortThreeCanWinAlone(TestContext c)=>Arbiter(c,(8,true,true),(8,true,true));
 [SimTest] public static async Task R038_AllRequestsRotateInLiteralOrder(TestContext c){await Reset(c);var m=new ArbiterModel();for(int i=0;i<12;i++){Put(c,"a_request",15);Put(c,"a_enable",1);await Timer(1);Check(Read(c,"a_grant")==1<<(i%4),"literal four-port round robin");await m.Step(c,15,true,true);}}
 [SimTest] public static Task R039_TwoContendersAlternate(TestContext c)=>Arbiter(c,Enumerable.Repeat((5,true,true),12).ToArray());
 [SimTest] public static Task R040_LastPortWrapsToPortZero(TestContext c)=>Arbiter(c,(8,true,true),(15,true,true));
 [SimTest] public static Task R041_NoAcknowledgeKeepsPriority(TestContext c)=>Arbiter(c,(15,true,false),(15,true,false),(15,true,true),(15,true,false));
 [SimTest] public static Task R042_DisableSuppressesAllGrants(TestContext c)=>Arbiter(c,(15,false,false),(1,false,false),(8,false,false));
 [SimTest] public static Task R043_DisabledAcknowledgeCannotRotate(TestContext c)=>Arbiter(c,(2,true,true),(15,false,true),(15,true,false));
 [SimTest] public static Task R044_EmptyAcknowledgementCannotRotate(TestContext c)=>Arbiter(c,(2,true,true),(0,true,true),(15,true,false));
 [SimTest] public static Task R045_RequestWithdrawalReselectsWithoutOwnerLock(TestContext c)=>Arbiter(c,(15,true,false),(14,true,false),(12,true,false),(8,true,false));
 [SimTest] public static Task R046_LateRequestUsesCurrentPriority(TestContext c)=>Arbiter(c,(4,true,true),(3,true,false),(11,true,true),(15,true,true));
 [SimTest] public static async Task R047_AllRequestMasksAtEveryPointer(TestContext c){await Reset(c);var m=new ArbiterModel();for(int round=0;round<4;round++){for(int mask=0;mask<16;mask++)await m.Step(c,mask,true,false);await m.Step(c,15,true,true);}}
 [SimTest] public static async Task R048_SeededRequestEnableAckInterleaving(TestContext c){await Reset(c);var m=new ArbiterModel();for(int i=0;i<256;i++)await m.Step(c,c.Random.Next(16),c.Random.Next(4)!=0,c.Random.Next(3)!=0);}
 [SimTest] public static async Task R049_PersistentRequestsBoundServiceToFourAcks(TestContext c){await Reset(c);var m=new ArbiterModel();for(int window=0;window<8;window++){int served=0;for(int turn=0;turn<4;turn++){Put(c,"a_request",15);Put(c,"a_enable",1);await Timer(1);int grant=Read(c,"a_grant");Check((served&grant)==0,"no repeated winner before persistent peers");served|=grant;await m.Step(c,15,true,true);}Check(served==15,"all persistent ports serviced");}}
 [SimTest] public static async Task R050_ResetRestartsAtPortZero(TestContext c){await Arbiter(c,(15,true,true),(15,true,true));await Reset(c);await new ArbiterModel().Step(c,15,true,false);}
}
