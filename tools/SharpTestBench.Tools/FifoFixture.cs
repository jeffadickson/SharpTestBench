using System.Text.Json;

namespace SharpTestBench.Tools;

internal sealed record FifoConfiguration(int Depth, int Pipeline, bool OutputFifo, bool Sidebands)
{
    public string Top => $"fifo_d{Depth}_p{Pipeline}_o{(OutputFifo ? 1 : 0)}_s{(Sidebands ? 1 : 0)}";
}
internal static class FifoFixture
{
    internal static FifoConfiguration Configuration(int depth, int pipeline, bool outputFifo, bool sidebands)
    {
        if (depth is < 2 or > 64 || (depth & (depth - 1)) != 0 || pipeline is < 0 or > 3)
            throw new InvalidDataException("FIFO depth must be a power of two from2..64 beats; pipeline must be0..3.");
        return new(depth, pipeline, outputFifo, sidebands);
    }
    internal static int DrainCycles(FifoConfiguration config) => config.Depth + 76;
    internal static AxisRow[] Plan(FifoConfiguration config, ulong seed, int count = 128, int stall = 7, int? resetAt = 64, string profile = "random")
    {
        Configuration(config.Depth, config.Pipeline, config.OutputFifo, config.Sidebands);
        if (count is < 1 or > 2048) throw new InvalidDataException("FIFO plan count must be1..2048.");
        if (profile is not ("random" or "saturation")) throw new InvalidDataException("Unknown FIFO stimulus profile.");
        if (profile == "saturation" && count < 2 * (config.Depth + 64))
            throw new InvalidDataException("FIFO saturation needs at least twice(depth+64) active cycles.");
        var rows = AxisFixture.Plan(seed, count, stall, resetAt).ToList();
        if (profile == "saturation")
            for (int index = 0; index < count; index++)
                rows[index + 1] = rows[index + 1] with { Ready = index >= count / 2, Offer = !rows[index + 1].Reset };
        rows.AddRange(Enumerable.Repeat(new AxisRow(false, true, false, 0, 0, false, 0, 0, 0), config.Depth + 64));
        return rows.ToArray();
    }
    internal static AxisRow[] ValidatePlan(FifoConfiguration config, JsonElement value)
    {
        Configuration(config.Depth, config.Pipeline, config.OutputFifo, config.Sidebands);
        var rows = AxisFixture.ValidatePlan(value);
        if (rows.Length < DrainCycles(config) + 2 || rows.Length > 2049 + DrainCycles(config) || rows.TakeLast(DrainCycles(config)).Any(row => row.Reset || row.Offer || !row.Ready))
            throw new InvalidDataException("FIFO plan lacks capacity-bounded drain.");
        return rows;
    }
    internal static string Wrapper(FifoConfiguration config)
    {
        Configuration(config.Depth, config.Pipeline, config.OutputFifo, config.Sidebands);
        int enabled = config.Sidebands ? 1 : 0;
        int words = config.Sidebands ? config.Depth * 2 : config.Depth;
        return $$"""
`timescale 1ns/1ps
module {{config.Top}} (
input wire clk, rst, fault,
input wire [15:0] s_axis_tdata, input wire [1:0] s_axis_tkeep,
input wire s_axis_tvalid, output wire s_axis_tready, input wire s_axis_tlast,
input wire [3:0] s_axis_tid, s_axis_tdest, input wire [1:0] s_axis_tuser,
output wire [15:0] m_axis_tdata, output wire [1:0] m_axis_tkeep,
output wire m_axis_tvalid, input wire m_axis_tready, output wire m_axis_tlast,
output wire [3:0] m_axis_tid, m_axis_tdest, output wire [1:0] m_axis_tuser);
wire [15:0] raw_data;
assign m_axis_tdata = raw_data ^ {15'b0,fault};
axis_fifo #(.DEPTH({{words}}), .DATA_WIDTH(16), .KEEP_ENABLE({{enabled}}), .KEEP_WIDTH(2),
 .LAST_ENABLE({{enabled}}), .ID_ENABLE({{enabled}}), .ID_WIDTH(4), .DEST_ENABLE({{enabled}}),
 .DEST_WIDTH(4), .USER_ENABLE({{enabled}}), .USER_WIDTH(2), .RAM_PIPELINE({{config.Pipeline}}),
 .OUTPUT_FIFO_ENABLE({{(config.OutputFifo ? 1 : 0)}}), .FRAME_FIFO(0), .DROP_OVERSIZE_FRAME(0)) dut (
 .clk(clk), .rst(rst), .s_axis_tdata(s_axis_tdata), .s_axis_tkeep(s_axis_tkeep),
 .s_axis_tvalid(s_axis_tvalid), .s_axis_tready(s_axis_tready), .s_axis_tlast(s_axis_tlast),
 .s_axis_tid(s_axis_tid), .s_axis_tdest(s_axis_tdest), .s_axis_tuser(s_axis_tuser),
 .m_axis_tdata(raw_data), .m_axis_tkeep(m_axis_tkeep), .m_axis_tvalid(m_axis_tvalid),
 .m_axis_tready(m_axis_tready), .m_axis_tlast(m_axis_tlast), .m_axis_tid(m_axis_tid),
 .m_axis_tdest(m_axis_tdest), .m_axis_tuser(m_axis_tuser), .pause_req(1'b0), .pause_ack(),
 .status_depth(), .status_depth_commit(), .status_overflow(), .status_bad_frame(), .status_good_frame());
endmodule
""" + "\n";
    }
    internal static JsonElement Metrics(FifoConfiguration config, JsonElement value, JsonElement stimulus)
    {
        var rows = ValidatePlan(config, stimulus); AxisFixture.Metrics(value);
        var trace = value.GetProperty("trace");
        if (trace.GetArrayLength() != rows.Length) throw new InvalidDataException("FIFO trace and stimulus lengths differ.");
        var queue = new Queue<int[]>(); int[]? pending = null;
        for (int index = 0; index < rows.Length; index++)
        {
            var source = rows[index]; var data = trace[index].EnumerateArray().Select(item => item.GetInt32()).ToArray();
            if (data[1] != (source.Reset ? 1 : 0) || data[4] != (source.Ready ? 1 : 0))
                throw new InvalidDataException("FIFO trace control differs from requested stimulus.");
            if (source.Reset) { queue.Clear(); pending = null; continue; }
            if (pending is null && source.Offer)
                pending = [source.Data, config.Sidebands ? source.Keep : 3, config.Sidebands ? source.Last ? 1 : 0 : 1,
                    config.Sidebands ? source.Id : 0, config.Sidebands ? source.Dest : 0, config.Sidebands ? source.User : 0];
            if (data[2] != 0)
            {
                if (pending is null) throw new InvalidDataException("FIFO accepted a beat the source did not offer.");
                queue.Enqueue(pending); pending = null;
            }
            if (data[3] != 0)
            {
                if (queue.Count == 0 || !queue.Peek().SequenceEqual(data[5..])) throw new InvalidDataException("FIFO output differs from accepted stimulus order.");
                if (source.Ready) queue.Dequeue();
            }
        }
        if (queue.Count != 0 || pending is not null) throw new InvalidDataException("FIFO stimulus did not drain.");
        return value.Clone();
    }

    // A witnessed workload property, not an estimate of exact hardware capacity.
    internal static Dictionary<string, int> SaturationWitness(FifoConfiguration config, JsonElement value, JsonElement stimulus)
    {
        Metrics(config, value, stimulus);
        var rows = ValidatePlan(config, stimulus); var trace = value.GetProperty("trace");
        int backlog = 0, maximum = 0, blocked = 0, blockedAtDepth = 0, simultaneous = 0; bool pending = false;
        for (int index = 0; index < rows.Length; index++)
        {
            var source = rows[index]; var data = trace[index];
            if (source.Reset) { backlog = 0; pending = false; continue; }
            pending |= source.Offer;
            bool take = data[2].GetInt32() != 0, consume = data[3].GetInt32() != 0 && source.Ready;
            if (pending && !take)
            {
                blocked++;
                if (!source.Ready && backlog >= config.Depth) blockedAtDepth++;
            }
            if (take) { backlog++; pending = false; }
            if (consume) backlog--;
            if (take && consume) simultaneous++;
            maximum = Math.Max(maximum, backlog);
        }
        if (maximum < config.Depth || blockedAtDepth == 0)
            throw new InvalidDataException("FIFO saturation was not witnessed: require a blocked source while RAM-depth backlog is held.");
        return new() { ["maximumBacklog"] = maximum, ["blockedSourceCycles"] = blocked, ["blockedAtDepthCycles"] = blockedAtDepth, ["simultaneousTransfers"] = simultaneous };
    }
}
