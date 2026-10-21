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
    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(4u)]
    [InlineData(8u)]
    [InlineData(16u)]
    [InlineData(32u)]
    [InlineData(128u)]
    [InlineData(256u)]
    [InlineData(0x10000u)]
    [InlineData(0x20000u)]
    [InlineData(0x80000000u)]
    [InlineData(0x80000001u)]
    [InlineData(0xaaaaaaaaU)]
    [InlineData(0x55555555u)]
    public void AcceptMaskedRequestsInBothDirections(uint mask) =>
        PriorityTrace.Validate(JsonSerializer.SerializeToElement(Valid(mask)));

    [Theory]
    [InlineData("missing-valid")]
    [InlineData("spurious-valid")]
    [InlineData("unrequested-bit")]
    [InlineData("opposite-priority")]
    public void SelfConsistentButIncorrectOutputsCannotQualify(string mutation)
    {
        for (int configuration = 0; configuration < 12; configuration++)
        {
            uint mask = mutation switch { "spurious-valid" => 0u, "unrequested-bit" => 1u, _ => 3u };
            var metrics = Valid(mask);
            var row = (JsonArray)metrics["trace"]![configuration]!;
            int wrong = mutation switch {
                "missing-valid" => -1,
                "spurious-valid" => 0,
                "unrequested-bit" => 1,
                "opposite-priority" => configuration % 2 == 0 ? 0 : 1,
                _ => throw new ArgumentException(mutation)
            };
            row[3] = wrong < 0 ? 0 : 1;
            row[4] = wrong;
            row[5] = wrong < 0 ? 0u : 1u << wrong;
            var error = Assert.Throws<InvalidDataException>(() => PriorityTrace.Validate(JsonSerializer.SerializeToElement(metrics)));
            Assert.Contains("request mask or direction", error.Message);
        }
    }
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
