using SharpTestBench;
using TestContext = SharpTestBench.TestContext;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

/// <summary>Planner contracts isolate metadata and seed behavior from simulator execution.</summary>
public sealed partial class SelectionContractTests
{
    static MethodInfo Method(Type type,string name)=>type.GetMethod(name,BindingFlags.Public|BindingFlags.Static)!;
    static List<PlannedTest> Plan(MethodInfo[] methods,ulong seed=83,RegressionSelection? selection=null,Func<TestDescriptor,bool>? include=null)=>RegressionPlanning.Build(methods,selection??new(),seed,include??(_=>true));
    [Fact] public void S051_PlanUsesStageThenOrdinalQualifiedIdentity()
    {var plan=Plan([Method(typeof(SelectionPlanningFixture),"A_Late"),Method(typeof(SelectionPlanningOther),"A_Middle"),Method(typeof(SelectionPlanningFixture),"Z_First")]);Assert.Equal(new[]{"SelectionPlanningFixture.Z_First","SelectionPlanningOther.A_Middle","SelectionPlanningFixture.A_Late"},plan.Select(p=>p.Descriptor.QualifiedName));}
    [Fact] public void S052_CaseArgumentsKeepTheirDeclarationOrder()
    {var p=Plan([Method(typeof(SelectionPlanningFixture),"C_Cases")]);Assert.Equal(new[]{"C_Cases[0]","C_Cases[1]"},p.Select(x=>x.Descriptor.Name));Assert.Equal(17,p[0].Arguments[0]);Assert.Equal(3,p[1].Arguments[0]);Assert.Equal(new[]{0,1},p.Select(x=>x.Descriptor.CaseIndex));}
    [Fact] public void S053_SeedArithmeticWrapsModuloTwoToSixtyFour()
    {var p=Plan([Method(typeof(SelectionPlanningFixture),"Z_First"),Method(typeof(SelectionPlanningOther),"A_Middle"),Method(typeof(SelectionPlanningFixture),"A_Late")],ulong.MaxValue);Assert.Equal(new[]{ulong.MaxValue,0UL,1UL},p.Select(x=>x.Descriptor.Seed));}
    [Fact] public void S054_IntersectingFiltersDoNotReseedSelectedCases()
    {MethodInfo[] methods=[Method(typeof(SelectionPlanningFixture),"Z_First"),Method(typeof(SelectionPlanningOther),"A_Middle"),Method(typeof(SelectionPlanningFixture),"A_Late")];var full=Plan(methods);var selected=Plan(methods,selection:Semantic(["A_Late$"]),include:d=>d.Stage==2);Assert.Single(selected);Assert.Equal(full[^1].Descriptor.Seed,selected[0].Descriptor.Seed);Assert.Equal(85UL,selected[0].Descriptor.Seed);}
    [Fact] public void S055_UnmarkedHelpersAreNotDiscoveredOrAllocatedSeeds()
    {var p=Plan([Method(typeof(SelectionPlanningFixture),"Helper"),Method(typeof(SelectionPlanningFixture),"Z_First")]);Assert.Single(p);Assert.Equal(83UL,p[0].Descriptor.Seed);}
    [Fact] public void S056_OneCaseRetainsItsLegacyUnsuffixedName()
    {var p=Plan([Method(typeof(SelectionPlanningFixture),"Single")]);Assert.Single(p);Assert.Equal("Single",p[0].Descriptor.Name);Assert.Equal(0,p[0].Descriptor.CaseIndex);Assert.Equal(7,p[0].Arguments[0]);}
    [Fact] public void S057_SeparatePlansDoNotShareArgumentBindings()
    {var m=Method(typeof(SelectionPlanningFixture),"Single");var first=Plan([m]);first[0].Arguments[0]=999;var second=Plan([m]);Assert.Equal(7,second[0].Arguments[0]);Assert.Equal("Single",second[0].Descriptor.Name);}
    [Fact] public void S058_TestcaseModePreservesDeclaredSkipMetadata()
    {var p=Plan([Method(typeof(SelectionPlanningFixture),"Skipped")],selection:Semantic([],RegressionMode.Testcase));Assert.True(p[0].Descriptor.Skip);Assert.True(p[0].Attribute.Skip);}
    [Fact] public void S059_DescriptorBackendMetadataIsAnIndependentSnapshot()
    {var p=Plan([Method(typeof(SelectionPlanningFixture),"Skipped")]);p[0].Attribute.Simulators[0]="changed";Assert.Equal(new[]{"icarus"},p[0].Descriptor.Simulators);}
    [Fact] public void S060_LaterRegexTimeoutCannotInvokeAnEarlierBody()
    {var visited=new List<string>();var s=new RegressionSelection(["^SelectionPlanningFixture\\.Z_First$","^SelectionTimeoutFixture\\.(a+)+$"],matchTimeout:TimeSpan.FromSeconds(1));Assert.Throws<RegexMatchTimeoutException>(()=>Plan([Method(typeof(SelectionPlanningFixture),"Z_First"),typeof(SelectionTimeoutFixture).GetMethods(BindingFlags.Public|BindingFlags.Static).Single()],selection:s,include:d=>{visited.Add(d.Name);return true;}));Assert.Equal("Z_First",visited[0]);Assert.Equal(2,visited.Count);Assert.Equal(0,SelectionPlanningFixture.BodyInvocations);}

}

/// <summary>Reflection-only fixture; invocation would be an observable planner isolation failure.</summary>
public static class SelectionPlanningFixture
{
    public static int BodyInvocations;
    [SimTest(Stage=-2)] public static Task Z_First(TestContext context){BodyInvocations++;return Task.CompletedTask;}
    [SimTest(Stage=2)] public static Task A_Late(TestContext context){BodyInvocations++;return Task.CompletedTask;}
    [SimTest] [SimCase(17)] [SimCase(3)] public static Task C_Cases(TestContext context,int value){BodyInvocations++;return Task.CompletedTask;}
    [SimTest] [SimCase(7)] public static Task Single(TestContext context,int value){BodyInvocations++;return Task.CompletedTask;}
    [SimTest(Skip=true,Simulators=["icarus"])] public static Task Skipped(TestContext context){BodyInvocations++;return Task.CompletedTask;}
    public static Task Helper(TestContext context){BodyInvocations++;return Task.CompletedTask;}
}
/// <summary>A second declaring type demonstrates that input enumeration order does not determine case order.</summary>
public static class SelectionPlanningOther
{
    [SimTest] public static Task A_Middle(TestContext context){SelectionPlanningFixture.BodyInvocations++;return Task.CompletedTask;}
}
/// <summary>Two legal CLR overloads intentionally collide in the existing case-identity schema.</summary>
public static class SelectionCollisionFixture
{
    [SimTest] public static Task Clash(TestContext context){SelectionPlanningFixture.BodyInvocations++;return Task.CompletedTask;}
    [SimTest] [SimCase(1)] public static Task Clash(TestContext context,int value){SelectionPlanningFixture.BodyInvocations++;return Task.CompletedTask;}
}
/// <summary>Long valid CLR identifier supplies a reproducible pathological regex input.</summary>
public static class SelectionTimeoutFixture
{
    [SimTest(Stage=10)] public static Task aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaB(TestContext context){SelectionPlanningFixture.BodyInvocations++;return Task.CompletedTask;}
}
