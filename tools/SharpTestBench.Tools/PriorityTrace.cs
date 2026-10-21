using System.Numerics;
using System.Text.Json;

namespace SharpTestBench.Tools;

internal static class PriorityTrace
{
    public static void Validate(JsonElement metrics)
    {
        FixtureContracts.Fields(metrics, "trace", "vectors", "configurations");
        FixtureContracts.Integer(metrics.GetProperty("vectors"), 453, 453, "vectors");
        FixtureContracts.Integer(metrics.GetProperty("configurations"), 12, 12, "configurations");
        var trace = metrics.GetProperty("trace");
        if (trace.ValueKind != JsonValueKind.Array || trace.GetArrayLength() != 5436)
            throw new InvalidDataException("Incomplete priority trace");
        int n = 0;
        uint mask = 0;
        int[] widths = [2, 3, 5, 8, 17, 32];
        foreach (var row in trace.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != 6)
                throw new InvalidDataException("Priority trace rows need six integers");
            int configuration = n++ % 12;
            FixtureContracts.Integer(row[0], (ulong)widths[configuration / 2], (ulong)widths[configuration / 2], "width");
            FixtureContracts.Integer(row[1], (ulong)(configuration % 2), (ulong)(configuration % 2), "priority direction");
            uint current = (uint)FixtureContracts.Integer(row[2], 0, uint.MaxValue, "request mask");
            if (configuration == 0) mask = current;
            else if (mask != current) throw new InvalidDataException("Configuration stimulus differs");
            ulong valid = FixtureContracts.Integer(row[3], 0, 1, "valid");
            if (row[4].ValueKind != JsonValueKind.Number || !row[4].TryGetInt32(out int index) ||
                (valid == 0 ? index != -1 : index < 0 || index >= widths[configuration / 2]))
                throw new InvalidDataException("Invalid encoded priority index");
            ulong onehot = FixtureContracts.Integer(row[5], 0, uint.MaxValue, "onehot");
            if (valid == 0 ? onehot != 0 : onehot != 1UL << index)
                throw new InvalidDataException("Invalid priority onehot mask");
            // Recompute independently of the simulator scoreboard's linear scan.
            uint requests = current & (uint)FixtureContracts.Mask(widths[configuration / 2]);
            int winner = requests == 0 ? -1 : configuration % 2 == 0
                ? 31 - BitOperations.LeadingZeroCount(requests)
                : BitOperations.TrailingZeroCount(requests);
            if (valid != (winner < 0 ? 0UL : 1UL) || index != winner)
                throw new InvalidDataException("Priority trace disagrees with request mask or direction");
        }
    }
}
