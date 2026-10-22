using SharpTestBench.Tools;
using Xunit;
public class ClosureSuiteSelectionTests
{
    [Fact] public void OriginalSuiteMappingRemainsStable()=>Assert.Equal("RequirementContracts",ContractsCiCommand.ContractSuite("requirements").Project);
    [Fact] public void ClosureSuiteMappingSelectsIndependentAssembly()=>Assert.Equal("ClosureContracts",ContractsCiCommand.ContractSuite("closure").Project);
    [Fact] public void UnknownSuiteCannotBecomeAnArbitraryPath()=>Assert.Throws<ArgumentException>(()=>ContractsCiCommand.ContractSuite("../Integration"));
    [Fact] public void SuiteDefaultsDescribeActualFixtureLimits(){Assert.Equal(70,ContractsCiCommand.ContractSuite("closure").Count);Assert.Equal(100,ContractsCiCommand.ContractSuite("requirements").Count);Assert.Equal("closure_top",ContractsCiCommand.ContractSuite("closure").Top);}
    [Fact] public void SuiteNamesRequireExactCase()=>Assert.Throws<ArgumentException>(()=>ContractsCiCommand.ContractSuite("Closure"));
}
