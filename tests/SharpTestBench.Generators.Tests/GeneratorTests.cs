using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using SharpTestBench.Generators;
using Xunit;

public sealed class GeneratorTests
{
    sealed class FileText(string path, string text) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
    static CancellationToken Cancellation => Xunit.TestContext.Current.CancellationToken;
    const string Valid = "<Bindings namespace='Demo' class='Ports'><Signal name='Data' width='8'><Child name='data'/></Signal></Bindings>";
    static CSharpCompilation Compilation(string source = "") => CSharpCompilation.Create("Consumer", [CSharpSyntaxTree.ParseText(source, cancellationToken: Cancellation)],
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Append(typeof(SharpTestBench.IWidth).Assembly.Location)
            .Distinct().Select(path => MetadataReference.CreateFromFile(path)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    static GeneratorDriver Driver(string xml) => CSharpGeneratorDriver.Create([new BindingGenerator().AsSourceGenerator()],
        [new FileText("ports.stb.xml", xml)], driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, true));
    [Fact] public void PositiveAndDeterministic()
    {
        var driver = Driver(Valid).RunGeneratorsAndUpdateCompilation(Compilation("class Check { int Width => Demo.Ports.Width8.Bits; }"), out var output, out var diagnostics, Cancellation);
        Assert.Empty(diagnostics); Assert.DoesNotContain(output.GetDiagnostics(Cancellation), d => d.Severity == DiagnosticSeverity.Error);
        var first = driver.GetRunResult().GeneratedTrees.Single().ToString();
        Assert.Equal(first, Driver(Valid).RunGenerators(Compilation(), Cancellation).GetRunResult().GeneratedTrees.Single().ToString());
        driver = driver.RunGenerators(Compilation(), Cancellation);
        Assert.All(driver.GetRunResult().Results.Single().TrackedSteps["BindingManifest"].SelectMany(s => s.Outputs),
            x => Assert.True(x.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged));
    }
    [Theory]
    [InlineData("0")][InlineData("-1")][InlineData("1048577")][InlineData("eight")]
    public void RejectInvalidWidth(string width) => Assert.Contains(Driver(Valid.Replace("width='8'", $"width='{width}'")).RunGenerators(Compilation(), Cancellation).GetRunResult().Diagnostics, d => d.Id == "STBGEN001");
    [Theory]
    [InlineData("<Bindings namespace='Demo' class='Ports'><Unknown/></Bindings>")]
    [InlineData("<!DOCTYPE Bindings [<!ENTITY x SYSTEM 'file:///not-read'>]><Bindings namespace='Demo' class='Ports'/>")]
    [InlineData("<Bindings namespace='bad-name' class='Ports'/>")]
    public void RejectInvalidManifest(string xml) => Assert.Contains(Driver(xml).RunGenerators(Compilation(), Cancellation).GetRunResult().Diagnostics, d => d.Id == "STBGEN001");
    [Fact] public void WidthMismatchFailsCompilation()
    {
        Driver(Valid).RunGeneratorsAndUpdateCompilation(Compilation("class Check { void F(Demo.Ports p) { p.Data.Write(new SharpTestBench.UInt<SharpTestBench.W16>(1)); } }"), out var output, out _, Cancellation);
        Assert.Contains(output.GetDiagnostics(Cancellation), d => d.Id == "CS1503");
    }
    [Fact] public void ChangesInvalidateOutput()
    {
        var old = new FileText("ports.stb.xml", Valid);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new BindingGenerator().AsSourceGenerator()], [old]);
        driver = driver.RunGenerators(Compilation(), Cancellation).ReplaceAdditionalText(old, new FileText("ports.stb.xml", Valid.Replace("width='8'", "width='16'"))).RunGenerators(Compilation(), Cancellation);
        Assert.Contains("Width16", driver.GetRunResult().GeneratedTrees.Single().ToString());
        Assert.DoesNotContain("Width8", driver.GetRunResult().GeneratedTrees.Single().ToString());
    }
    [Theory]
    [InlineData("Input")][InlineData("Output")][InlineData("InOut")]
    public void DirectionEmitsRuntimeGuard(string direction)
    {
        var driver = Driver(Valid.Replace("width='8'", $"width='8' direction='{direction}'"))
            .RunGeneratorsAndUpdateCompilation(Compilation(), out var output, out var diagnostics, Cancellation);
        Assert.Empty(diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(Cancellation), d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("RequireDirection(global::SharpTestBench.PortDirection." + direction + ")",
            driver.GetRunResult().GeneratedTrees.Single().ToString());
    }
    [Theory]
    [InlineData("Unknown")][InlineData("Driver")][InlineData("input")][InlineData("")]
    public void InvalidDirectionRejected(string direction)
        => Assert.Contains(Driver(Valid.Replace("width='8'", $"width='8' direction='{direction}'"))
            .RunGenerators(Compilation(), Cancellation).GetRunResult().Diagnostics, d => d.Id == "STBGEN001");
}
