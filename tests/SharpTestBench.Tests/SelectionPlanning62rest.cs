using SharpTestBench;
using System.Reflection;
using Xunit;
public sealed partial class SelectionContractTests
{
    [Fact] public void S061_DuplicateCaseIdentityRejectsEvenWhenAFilterWouldExcludeIt()
    {var methods=typeof(SelectionCollisionFixture).GetMethods(BindingFlags.Public|BindingFlags.Static);var error=Assert.Throws<InvalidOperationException>(()=>Plan(methods,selection:Semantic(["^never$"])));Assert.Contains("Duplicate qualified test identity",error.Message);Assert.Equal(0,SelectionPlanningFixture.BodyInvocations);}
    [Fact] public void S062_InvalidPlannerArgumentsRejectBeforeEnumeration()
    {Assert.Equal("methods",Assert.Throws<ArgumentNullException>(()=>RegressionPlanning.Build(null!,new(),0,_=>true)).ParamName);var source=Enumerable.Range(0,1).Select<int,MethodInfo>(_=>throw new IOException("must not enumerate"));Assert.Equal("selection",Assert.Throws<ArgumentNullException>(()=>RegressionPlanning.Build(source,null!,0,_=>true)).ParamName);Assert.Equal("include",Assert.Throws<ArgumentNullException>(()=>RegressionPlanning.Build(source,new(),0,null!)).ParamName);}
}
