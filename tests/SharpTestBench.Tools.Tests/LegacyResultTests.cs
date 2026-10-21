using System.Text.Json;
using SharpTestBench.Tools;
using Xunit;

public sealed class LegacyResultTests
{
    [Fact] public void DiscoveryAllowsEmptyRowsButAnOrdinaryRunRequiresCases()
    {
        using var rows=JsonDocument.Parse("[]");
        LegacyResults.Validate(rows.RootElement,true);
        Assert.Throws<InvalidDataException>(()=>LegacyResults.Validate(rows.RootElement,false));
    }
    [Fact] public void DiscoveryStillRejectsMalformedCompletionRows()
    {
        using var rows=JsonDocument.Parse("[{\"Name\":\"case\",\"Seed\":1,\"Status\":\"unknown\"}]");
        Assert.Throws<InvalidDataException>(()=>LegacyResults.Validate(rows.RootElement,true));
    }
}
