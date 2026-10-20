using SharpTestBench.Tools;
using Xunit;
public sealed class AdapterPathTests
{
    [Theory]
    [InlineData("C:\\adapter with spaces\\icarus.dll:entry", "C:\\adapter with spaces\\icarus.dll")]
    [InlineData("/adapter/icarus.so:entry", "/adapter/icarus.so")]
    public void NativeImplementationPreservesLibraryPath(string setting, string library)
    {
        var adapter = new CocotbAdapter("version", "python", "libs", "bootstrap", "gpi,entry", setting, null, null); Assert.Equal((library, "entry"), adapter.ImplementationEntry());
    }
    [Theory]
    [InlineData("missing")]
    [InlineData(":entry")]
    [InlineData("library:")]
    public void MissingImplementationComponentsFail(string setting)
    {
        var adapter = new CocotbAdapter("version", "python", "libs", "bootstrap", "gpi,entry", setting, null, null); Assert.Throws<InvalidDataException>(() => adapter.ImplementationEntry());
    }
}
