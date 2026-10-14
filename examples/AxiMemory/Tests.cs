using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Generated;
using SharpTestBench;
using static SharpTestBench.Triggers;

public static class Entry
{
    [UnmanagedCallersOnly]
    public static int Initialize(nint p, int n) => Regression.Initialize(typeof(Entry).Assembly);

    public sealed record Command(string Op, uint Address, uint Data, uint Strobe, int AwDelay, int WDelay, int ReadyDelay);
    sealed class Inputs
    {
        public bool Reset = true, Aw, W, Ar, BReady, RReady;
        public uint Address, Data, Strobe;
    }
    readonly record struct Outputs(bool AwReady, bool WReady, bool ArReady, bool BValid, uint BResp, bool RValid, uint RData, uint RResp);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Write<W>(Signal<W> signal, uint value) where W : struct, IWidth => signal.Write(new UInt<W>(value));
    static uint Read<W>(Signal<W> signal) where W : struct, IWidth => (uint)signal.ReadUInt().Value;

    sealed class Bus(TestContext context)
    {
        readonly AxiPorts p = new(context.Dut);
        uint random = 0x12345678;
        readonly uint fault = uint.Parse(Environment.GetEnvironmentVariable("AXI_FAULT") ?? "0");
        public int Cycles;
        public int WriteStalls, ReadStalls;
        public async Task<Outputs> Step(Inputs input)
        {
            random = unchecked(random * 1664525 + 1013904223);
            p.clk.WriteBit(false); p.reset_n.WriteBit(input.Reset);
            p.awvalid.WriteBit(input.Aw); p.wvalid.WriteBit(input.W); p.arvalid.WriteBit(input.Ar);
            p.bready.WriteBit(input.BReady); p.rready.WriteBit(input.RReady);
            Write(p.awaddr, input.Address); Write(p.araddr, input.Address);
            Write(p.wdata, input.Data); Write(p.wstrb, input.Strobe);
            p.stall_aw.WriteBit((random & 3) == 0); p.stall_w.WriteBit((random & 12) == 0); p.stall_ar.WriteBit((random & 48) == 0);
            Write(p.fault, fault);
            // Sample stable READY/VALID before the active edge; these are the transfers at that edge.
            await Timer(1); await ReadOnly();
            var result = input.Reset ? new Outputs(p.awready.ReadBit(), p.wready.ReadBit(), p.arready.ReadBit(),
                p.bvalid.ReadBit(), Read(p.bresp), p.rvalid.ReadBit(), Read(p.rdata), Read(p.rresp)) : default;
            if (result.BValid && !input.BReady) WriteStalls++;
            if (result.RValid && !input.RReady) ReadStalls++;
            await Timer(1); p.clk.WriteBit(true);
            await Timer(1); await ReadOnly();
            await Timer(1); Cycles++;
            return result;
        }
        public async Task Reset()
        {
            await Step(new() { Reset = false });
            await Step(new() { Reset = false });
            var result = await Step(new());
            Check(!result.BValid && !result.RValid, "AXI_RESET: stale response after reset");
        }
        public async Task Cut(string phase)
        {
            // Intentionally abandon one half of a write, or an unaccepted response.
            for (int cycle = 0; cycle < 64; cycle++)
            {
                var input = new Inputs { Address = 0, Data = 0xdeadbeef, Strobe = 15,
                    Aw = phase is "aw" or "b", W = phase is "w" or "b", Ar = phase == "r" };
                var o = await Step(input);
                if ((phase == "aw" && o.AwReady) || (phase == "w" && o.WReady) ||
                    (phase == "b" && o.BValid) || (phase == "r" && o.RValid)) { await Reset(); return; }
            }
            throw new Exception("AXI_TIMEOUT: reset phase " + phase);
        }
    }

    [SimTest(TimeoutSteps = 100000000)]
    public static async Task AxiMemoryScoreboard(TestContext c)
    {
        var commands = JsonSerializer.Deserialize<Command[]>(File.ReadAllText(Environment.GetEnvironmentVariable("AXI_PLAN")!))!;
        var bus = new Bus(c);
        var memory = new uint[64];
        var trace = new List<object>();
        long allocated = GC.GetTotalAllocatedBytes(true);
        int[] gc = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        var clock = Stopwatch.StartNew();
        await bus.Reset();
        foreach (var command in commands)
        {
            int start = bus.Cycles;
            if (command.Op.StartsWith("reset_"))
            {
                await bus.Cut(command.Op[6..]); Array.Clear(memory);
                trace.Add(new object[] { command.Op, bus.Cycles - start }); continue;
            }
            bool legal = command.Address < 256 && command.Address % 4 == 0;
            bool aw = false, w = false, ar = false, done = false;
            Outputs? held = null;
            for (int cycle = 0; cycle < 128; cycle++)
            {
                bool writing = command.Op == "write";
                var input = new Inputs { Address = command.Address, Data = command.Data, Strobe = command.Strobe,
                    Aw = writing && !aw && cycle >= command.AwDelay, W = writing && !w && cycle >= command.WDelay,
                    Ar = !writing && !ar && cycle >= command.AwDelay,
                    BReady = writing && cycle >= command.ReadyDelay, RReady = !writing && cycle >= command.ReadyDelay };
                var o = await bus.Step(input);
                if (held is Outputs previous)
                    Check(writing ? o.BValid && o.BResp == previous.BResp : o.RValid && o.RResp == previous.RResp && o.RData == previous.RData,
                        "AXI_STABILITY: response changed under backpressure");
                held = writing ? o.BValid && !input.BReady ? o : null : o.RValid && !input.RReady ? o : null;
                aw |= input.Aw && o.AwReady; w |= input.W && o.WReady; ar |= input.Ar && o.ArReady;
                if (writing ? o.BValid && input.BReady : o.RValid && input.RReady)
                {
                    Check(writing ? aw && w : ar, "AXI_ORDER: response without request");
                    uint response = writing ? o.BResp : o.RResp;
                    Check(response == (legal ? 0u : 2u), "AXI_RESPONSE: wrong response code");
                    if (writing && legal)
                        for (int lane = 0; lane < 4; lane++)
                            if ((command.Strobe & (1u << lane)) != 0)
                            {
                                uint mask = 255u << (lane * 8);
                                memory[command.Address / 4] = (memory[command.Address / 4] & ~mask) | (command.Data & mask);
                            }
                    if (!writing) Check(o.RData == (legal ? memory[command.Address / 4] : 0), "AXI_DATA: scoreboard mismatch");
                    trace.Add(new object[] { command.Op, command.Address, command.Data, command.Strobe, response, writing ? 0u : o.RData, bus.Cycles - start });
                    done = true; break;
                }
            }
            Check(done, "AXI_TIMEOUT: transaction did not complete");
        }
        clock.Stop();
        Check(bus.WriteStalls > 0 && bus.ReadStalls > 0, "AXI_COVERAGE: no response backpressure exercised");
        var report = new { trace, cycles = bus.Cycles, write_stalls = bus.WriteStalls, read_stalls = bus.ReadStalls,
            seconds = clock.Elapsed.TotalSeconds, allocated_bytes = GC.GetTotalAllocatedBytes(true) - allocated,
            gc_collections = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - gc[i]).ToArray() };
        File.WriteAllText(Environment.GetEnvironmentVariable("AXI_REPORT")!, JsonSerializer.Serialize(report));
        c.Log($"AXI checked {commands.Length} commands in {bus.Cycles} cycles");
    }
}
