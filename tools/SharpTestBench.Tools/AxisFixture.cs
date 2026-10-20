using System.Text.Json;

namespace SharpTestBench.Tools;

internal sealed record AxisRow(bool Reset, bool Ready, bool Offer, int Data, int Keep, bool Last, int Id, int Dest, int User);
internal static class AxisFixture
{
    static readonly AxisRow Initial = new(true, false, false, 0, 0, false, 0, 0, 0);
    public static AxisRow[] Plan(ulong seed, int count = 64, int stall = 3, int? resetAt = null)
    {
        if (count < 1 || count > 4096 || stall < 0 || stall > 32 || resetAt is not null && (resetAt < 1 || resetAt >= count)) throw new InvalidDataException("Invalid stream plan parameters");
        var random = new PythonRandom(seed); var rows = new List<AxisRow> { Initial };
        for (int index = 0; index < count; index++) rows.Add(new(index == resetAt, index % (stall + 1) == stall, index % 5 != 2, random.Below(65536), random.Below(4), index % 7 == 6, index % 16, index * 3 % 16, random.Below(4)));
        rows.AddRange(Enumerable.Repeat(Initial with { Reset = false, Ready = true }, 12));
        return rows.ToArray();
    }
    public static AxisRow Row(JsonElement value)
    {
        FixtureContracts.Fields(value, "Reset", "Ready", "Offer", "Data", "Keep", "Last", "Id", "Dest", "User");
        bool Flag(string key) => FixtureContracts.Boolean(value.GetProperty(key), key);
        int Number(string key, int maximum) => (int)FixtureContracts.Integer(value.GetProperty(key), 0, (ulong)maximum, key);
        return new(Flag("Reset"), Flag("Ready"), Flag("Offer"), Number("Data", 65535), Number("Keep", 3), Flag("Last"), Number("Id", 15), Number("Dest", 15), Number("User", 3));
    }
    public static AxisRow[] ValidatePlan(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() < 3 || value.GetArrayLength() > 4110) throw new InvalidDataException("Invalid stream plan length");
        var rows = value.EnumerateArray().Select(Row).ToArray(); ValidateRows(rows); return rows;
    }
    static void ValidateRows(AxisRow[] rows)
    {
        if (rows.Length < 3 || rows.Length > 4110 || !rows[0].Reset) throw new InvalidDataException("Stream must start in reset");
        if (rows.TakeLast(12).Any(row => row.Reset || row.Offer || !row.Ready)) throw new InvalidDataException("Stream must drain for twelve cycles");
        if (!rows.Any(row => row.Offer && !row.Reset)) throw new InvalidDataException("Empty stream");
    }
    public static string Configuration(int mode, bool sidebands = true)
    {
        if (mode < 0 || mode > 2 || !sidebands && mode != 2) throw new InvalidDataException("Invalid stream mode/sidebands");
        return sidebands ? "axis_mode" + mode : "axis_disabled";
    }
    public static int[] Project(JsonElement value, bool sidebands = true)
    {
        var row = Row(value); ValidateRows([Initial, row, .. Enumerable.Repeat(Initial with { Reset = false, Ready = true }, 12)]);
        return [row.Data, sidebands ? row.Keep : 3, sidebands ? row.Last ? 1 : 0 : 1, sidebands ? row.Id : 0, sidebands ? row.Dest : 0, sidebands ? row.User : 0];
    }
    public static JsonElement Metrics(JsonElement value)
    {
        FixtureContracts.Fields(value, "trace", "accepted", "consumed", "flushed", "stalls", "resets", "cycles");
        var counters = new Dictionary<string, int>();
        foreach (var key in new[] { "accepted", "consumed", "flushed", "stalls", "resets", "cycles" }) counters[key] = (int)FixtureContracts.Integer(value.GetProperty(key), 0, 100000, key);
        var trace = value.GetProperty("trace");
        if (trace.ValueKind != JsonValueKind.Array || trace.GetArrayLength() < 3 || trace.GetArrayLength() > 4110 || counters["cycles"] != trace.GetArrayLength()) throw new InvalidDataException("Invalid trace length");
        int accepted = 0, consumed = 0, flushed = 0, stalls = 0, resets = 0, backlog = 0;
        int[]? held = null;
        int[] maxima = [4109, 1, 1, 1, 1, 65535, 3, 1, 15, 15, 3];
        for (int index = 0; index < trace.GetArrayLength(); index++)
        {
            var row = trace[index];
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != 11) throw new InvalidDataException("Invalid trace row");
            var data = row.EnumerateArray().Select((item, column) => (int)FixtureContracts.Integer(item, 0, (ulong)maxima[column], "trace column " + column)).ToArray();
            int cycle = data[0], reset = data[1], take = data[2], valid = data[3], ready = data[4]; var word = data[5..];
            if (cycle != index || index == 0 && reset == 0) throw new InvalidDataException("Noncontiguous cycles or missing initial reset");
            if (reset != 0)
            {
                if (take != 0 || valid != 0) throw new InvalidDataException("Reset transfers are forbidden");
                flushed += backlog; backlog = 0; held = null; resets++;
            }
            else
            {
                if (held is not null && (valid == 0 || !word.SequenceEqual(held))) throw new InvalidDataException("Unstable stalled output");
                accepted += take; backlog += take;
                if (valid != 0 && ready != 0 && backlog == 0) throw new InvalidDataException("Unsolicited output transfer");
                if (valid != 0 && ready != 0) { consumed++; backlog--; }
                if (valid != 0 && ready == 0) stalls++;
                held = valid != 0 && ready == 0 ? word : null;
            }
            if (valid == 0 && word.Any(item => item != 0)) throw new InvalidDataException("Invalid payload must be normalized");
        }
        if (backlog != 0 || held is not null || consumed == 0) throw new InvalidDataException("Incomplete stream");
        if (counters["accepted"] != accepted || counters["consumed"] != consumed || counters["flushed"] != flushed || counters["stalls"] != stalls || counters["resets"] != resets) throw new InvalidDataException("Trace counter mismatch");
        return value.Clone();
    }
}
