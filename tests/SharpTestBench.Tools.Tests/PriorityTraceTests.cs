using System.Text.Json;
using System.Text.Json.Nodes;
using SharpTestBench.Tools;
using Xunit;

public sealed class PriorityTraceTests
{
    static JsonObject Valid(uint mask = 0)
    {
        var rows = new JsonArray();
        for (int vector = 0; vector < 453; vector++)
            foreach (int width in new[] { 2, 3, 5, 8, 17, 32 })
                for (int low = 0; low < 2; low++)
                {
                    int winner = Entry.Winner(mask, width, low == 1);
                    rows.Add(new JsonArray(width, low, mask, winner >= 0 ? 1 : 0,
                        winner, winner >= 0 ? 1u << winner : 0u));
                }
        return new JsonObject { ["trace"] = rows, ["vectors"] = 453, ["configurations"] = 12 };
    }

    [Theory]
    [InlineData("missing-field")]
    [InlineData("extra-field")]
    [InlineData("vector-count")]
    [InlineData("configuration-count")]
    [InlineData("empty-trace")]
    [InlineData("row-type")]
    [InlineData("row-length")]
    [InlineData("width")]
    [InlineData("direction")]
    [InlineData("negative-mask")]
    [InlineData("overflow-mask")]
    [InlineData("boolean-valid")]
    [InlineData("invalid-index")]
    [InlineData("invalid-onehot")]
    [InlineData("different-stimulus")]
    public void MalformedReportCannotQualify(string mutation)
    {
        var metrics = Valid();
        var trace = (JsonArray)metrics["trace"]!;
        var row = (JsonArray)trace[0]!;
        switch (mutation)
        {
            case "missing-field": metrics.Remove("vectors"); break;
            case "extra-field": metrics["extra"] = true; break;
            case "vector-count": metrics["vectors"] = 452; break;
            case "configuration-count": metrics["configurations"] = 11; break;
            case "empty-trace": metrics["trace"] = new JsonArray(); break;
            case "row-type": trace[0] = null; break;
            case "row-length": row.RemoveAt(5); break;
            case "width": row[0] = 32; break;
            case "direction": row[1] = 1; break;
            case "negative-mask": row[2] = -1; break;
            case "overflow-mask": row[2] = 4294967296L; break;
            case "boolean-valid": row[3] = true; break;
            case "invalid-index": row[4] = 0; break;
            case "invalid-onehot": row[5] = 1; break;
            case "different-stimulus": ((JsonArray)trace[1]!)[2] = 1; break;
            default: throw new ArgumentException(mutation);
        }
        Assert.Throws<InvalidDataException>(() => PriorityTrace.Validate(JsonSerializer.SerializeToElement(metrics)));
    }

    [Fact] public void AcceptZeroRequestTrace() => PriorityTrace.Validate(JsonSerializer.SerializeToElement(Valid()));
    [Fact] public void AcceptWinnersIncludingBit31() => PriorityTrace.Validate(JsonSerializer.SerializeToElement(Valid(uint.MaxValue)));
    [Fact] public void RejectOutOfRangeValidIndex()
    {
        var metrics = Valid(uint.MaxValue);
        ((JsonArray)metrics["trace"]![0]!)[4] = 2;
        Assert.Throws<InvalidDataException>(() => PriorityTrace.Validate(JsonSerializer.SerializeToElement(metrics)));
    }
    [Fact] public void RejectValidOnehotMismatch()
    {
        var metrics = Valid(uint.MaxValue);
        ((JsonArray)metrics["trace"]![0]!)[5] = 1;
        Assert.Throws<InvalidDataException>(() => PriorityTrace.Validate(JsonSerializer.SerializeToElement(metrics)));
    }
    [Fact] public void RejectLaterReorderedConfiguration()
    {
        var metrics = Valid();
        ((JsonArray)metrics["trace"]![100]!)[0] = 32;
        Assert.Throws<InvalidDataException>(() => PriorityTrace.Validate(JsonSerializer.SerializeToElement(metrics)));
    }
}
