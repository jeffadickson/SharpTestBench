using SharpTestBench.Fixtures;
using System.Text.Json;

namespace SharpTestBench.Tools;

internal static class ArbiterFixture
{
    internal static ArbiterConfig Configuration(JsonElement value)
    {
        FixtureContracts.Fields(value, "Ports", "RoundRobin", "Blocking", "LsbPriority");
        var config = new ArbiterConfig((int)FixtureContracts.Integer(value.GetProperty("Ports"), 2, 8, "ports"),
            FixtureContracts.Boolean(value.GetProperty("RoundRobin"), "round_robin"),
            (int)FixtureContracts.Integer(value.GetProperty("Blocking"), 0, 2, "blocking"),
            FixtureContracts.Boolean(value.GetProperty("LsbPriority"), "lsb_priority"));
        config.Validate(); return config;
    }
    internal static ArbiterRow[] Plan(ArbiterConfig config, ulong seed, int count = 128)
    {
        config.Validate();
        if (count is < 1 or > 2048) throw new InvalidDataException("Arbiter random cycle count must be1..2048.");
        var rows = new List<ArbiterRow> { new(true, 0, 0) };
        for (int index = 0; index < config.Ports * 2; index++) rows.Add(new(false, config.Mask, config.Mask));
        for (int index = 0; index < 3; index++) rows.Add(new(false, config.Mask, 0));
        rows.Add(new(false, 0, 0)); rows.Add(new(false, config.Mask, config.Mask));
        rows.Add(new(true, 0, 0));
        var random = new PythonRandom(seed);
        for (int index = 0; index < count; index++)
            rows.Add(new(index == count / 2, random.Below(config.Mask + 1), random.Below(config.Mask + 1)));
        rows.Add(new(true, 0, 0)); return rows.ToArray();
    }
    internal static ArbiterRow[] ValidatePlan(ArbiterConfig config, JsonElement value)
    {
        config.Validate();
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() is < 3 or > 2100) throw new InvalidDataException("Invalid arbiter plan length.");
        var rows = value.EnumerateArray().Select(row => {
            FixtureContracts.Fields(row, "Reset", "Request", "Acknowledge");
            return new ArbiterRow(FixtureContracts.Boolean(row.GetProperty("Reset"), "reset"),
                (int)FixtureContracts.Integer(row.GetProperty("Request"), 0, (ulong)config.Mask, "request"),
                (int)FixtureContracts.Integer(row.GetProperty("Acknowledge"), 0, (ulong)config.Mask, "acknowledge"));
        }).ToArray();
        if (!rows[0].Reset || !rows[^1].Reset || !rows.Any(row => !row.Reset && row.Request != 0))
            throw new InvalidDataException("Arbiter plan requires initial/final reset and an active request.");
        return rows;
    }
    internal static string Wrapper(ArbiterConfig config)
    {
        config.Validate();
        return $$"""
`timescale 1ns/1ps
module {{config.Top}}(input wire clk,rst,fault,input wire[7:0] request,acknowledge,
output wire[7:0] grant,output wire grant_valid,output wire[7:0] grant_encoded);
wire[{{config.Ports-1}}:0] raw_grant;
wire[$clog2({{config.Ports}})-1:0] raw_encoded;
assign grant = raw_grant ^ ((fault && grant_valid) ? 8'h01 : 8'h00);
assign grant_encoded = raw_encoded;
arbiter #(.PORTS({{config.Ports}}),.ARB_TYPE_ROUND_ROBIN({{(config.RoundRobin ? 1 : 0)}}),
.ARB_BLOCK({{(config.Blocking == 0 ? 0 : 1)}}),.ARB_BLOCK_ACK({{(config.Blocking == 2 ? 1 : 0)}}),
.ARB_LSB_HIGH_PRIORITY({{(config.LsbPriority ? 1 : 0)}})) dut(
.clk(clk),.rst(rst),.request(request[{{config.Ports-1}}:0]),.acknowledge(acknowledge[{{config.Ports-1}}:0]),
.grant(raw_grant),.grant_valid(grant_valid),.grant_encoded(raw_encoded));
endmodule
""" + "\n";
    }
    internal static JsonElement Metrics(ArbiterConfig config, JsonElement value, JsonElement stimulus)
    {
        var rows = ValidatePlan(config, stimulus);
        FixtureContracts.Fields(value, "trace", "cycles", "valid", "idle", "resets", "transitions");
        var trace = value.GetProperty("trace");
        if (trace.ValueKind != JsonValueKind.Array || trace.GetArrayLength() != rows.Length) throw new InvalidDataException("Arbiter trace length differs from plan.");
        var model = new ArbiterModel(config); int valid = 0, resets = 0, transitions = 0, previous = 0;
        for (int index = 0; index < rows.Length; index++)
        {
            var data = trace[index];
            if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() != 7) throw new InvalidDataException("Invalid arbiter trace row.");
            int[] maxima = [2099, 1, config.Mask, config.Mask, config.Mask, 1, config.Ports - 1];
            var actual = data.EnumerateArray().Select((item, column) => (int)FixtureContracts.Integer(item, 0, (ulong)maxima[column], "arbiter trace column")).ToArray();
            var row = rows[index]; var expected = model.Step(row);
            if (actual[0] != index || actual[1] != (row.Reset ? 1 : 0) || actual[2] != row.Request || actual[3] != row.Acknowledge ||
                actual[4] != expected.Grant || actual[5] != (expected.Valid ? 1 : 0) || actual[6] != expected.Encoded)
                throw new InvalidDataException("Arbiter trace differs from requested policy or stimulus.");
            valid += actual[5]; resets += actual[1]; if (actual[4] != previous) transitions++; previous = actual[4];
        }
        var counters = new Dictionary<string, int> { ["cycles"] = rows.Length, ["valid"] = valid, ["idle"] = rows.Length - valid, ["resets"] = resets, ["transitions"] = transitions };
        if (valid == 0 || counters.Any(pair => FixtureContracts.Integer(value.GetProperty(pair.Key), 0, 2100, pair.Key) != (ulong)pair.Value))
            throw new InvalidDataException("Arbiter trace counters differ or workload is empty.");
        return value.Clone();
    }
}
