using SharpTestBench;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using static SharpTestBench.Triggers;

/// <summary>Instruction retirement and memory protocol scoreboard for the custom CPU fixture.</summary>
public static class Entry
{
    /// <summary>Load this example through the native simulation host.</summary>
    [UnmanagedCallersOnly]
    public static int Initialize(nint p, int n) => Regression.Initialize(typeof(Entry).Assembly);

    /// <summary>Execute a shared binary program against an independent software register/memory model.</summary>
    [SimTest(TimeoutSteps = 10000000)]
    public static async Task CpuScoreboard(TestContext c)
    {
        uint[] program = JsonSerializer.Deserialize<uint[]>(File.ReadAllText(Environment.GetEnvironmentVariable("CPU_PLAN")!))!;
        Signal<W1> Bit(string name) => c.Dut.Child(name).Signal<W1>();
        Signal<W32> Word(string name) => c.Dut.Child(name).Signal<W32>();
        var clk = Bit("clk"); var reset = Bit("reset"); var enable = Bit("enable"); var ready = Bit("mem_ready");
        var instruction = Word("instruction"); var rdata = Word("mem_rdata"); var pcSignal = Word("pc");
        var halted = Bit("halted"); var retired = Bit("retired"); var write = Bit("retire_write");
        var rdSignal = c.Dut.Child("retire_rd").Signal<W8>(); var dataSignal = Word("retire_data");
        var memValid = Bit("mem_valid"); var memWrite = Bit("mem_write");
        var addressSignal = c.Dut.Child("mem_address").Signal<W8>(); var wdata = Word("mem_wdata");
        var fault = c.Dut.Child("fault").Signal<W8>();
        uint[] regs = new uint[8], memory = new uint[256];
        var trace = new List<long[]>();
        int pc = 0, commits = 0, memoryStalls = 0, pauses = 0;
        uint random = 0x12345678;
        int mode = int.Parse(Environment.GetEnvironmentVariable("CPU_FAULT") ?? "0");
        clk.WriteBit(false); reset.WriteBit(true); enable.WriteBit(false); ready.WriteBit(false);
        instruction.Write(new UInt<W32>(0)); rdata.Write(new UInt<W32>(0)); fault.Write(new UInt<W8>(mode));
        await Timer(1); clk.WriteBit(true); await Timer(1); await ReadOnly(); await Timer(1);
        long allocated = GC.GetTotalAllocatedBytes(true);
        int[] gc = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
        var timer = Stopwatch.StartNew();
        bool done = false;
        for (int cycle = 0; cycle < 2000000; cycle++)
        {
            if ((uint)pc >= program.Length) throw new Exception("CPU_MISMATCH: PC outside program");
            uint ins = program[pc]; int op = (int)(ins >> 28), rd = (int)(ins >> 25) & 7;
            int rs = (int)(ins >> 22) & 7, rt = (int)(ins >> 19) & 7, imm = (short)(ins & 65535);
            random = unchecked(random * 1664525 + 1013904223);
            bool run = (random & 7) != 0, available = (random & 24) != 0;
            bool mem = op is 4 or 5, fire = run && (!mem || available), writes = fire && op is >= 1 and <= 4 && rd != 0;
            int address = (int)unchecked(regs[rs] + (uint)imm) & 255;
            clk.WriteBit(false); reset.WriteBit(false); enable.WriteBit(run); ready.WriteBit(available);
            instruction.Write(new UInt<W32>(ins)); rdata.Write(new UInt<W32>(memory[address]));
            await Timer(1); await ReadOnly();
            if (pcSignal.ReadUInt().Value != pc || memValid.ReadBit() != (run && mem)
                || (run && mem && (memWrite.ReadBit() != (op == 5) || addressSignal.ReadUInt().Value != address
                    || (op == 5 && wdata.ReadUInt().Value != regs[rt]))))
                throw new Exception($"CPU_MISMATCH: request cycle={cycle} pc={pc}");
            uint value = op switch { 1 => unchecked((uint)imm), 2 => unchecked(regs[rs] + regs[rt]),
                3 => regs[rs] ^ regs[rt], 4 => memory[address], _ => 0 };
            int next = fire ? (op == 6 && regs[rs] != regs[rt] ? pc + imm : pc + 1) : pc;
            done = fire && op == 15;
            if (run && mem && !available) memoryStalls++;
            if (!run) pauses++;
            if (fire)
            {
                if (op == 5) memory[address] = regs[rt];
                if (writes) regs[rd] = value;
                commits++;
            }
            await Timer(1); clk.WriteBit(true); await Timer(1); await ReadOnly();
            if (retired.ReadBit() != fire || write.ReadBit() != writes || pcSignal.ReadUInt().Value != next
                || halted.ReadBit() != done || (writes && (rdSignal.ReadUInt().Value != rd || dataSignal.ReadUInt().Value != value)))
                throw new Exception($"CPU_MISMATCH: retirement cycle={cycle} pc={pc}");
            trace.Add([cycle, pc, fire ? 1 : 0, writes ? rd : 0, writes ? value : 0, next, run && mem ? 1 : 0, available ? 1 : 0]);
            pc = next;
            await Timer(1);
            if (done) break;
        }
        if (!done || memoryStalls == 0 || pauses == 0) throw new Exception("CPU_TIMEOUT: missing halt or stalls");
        timer.Stop();
        File.WriteAllText(Environment.GetEnvironmentVariable("CPU_REPORT")!, JsonSerializer.Serialize(new
        {
            trace, commits, memoryStalls, pauses, memory, cycles = trace.Count, seconds = timer.Elapsed.TotalSeconds,
            allocated_bytes = GC.GetTotalAllocatedBytes(false) - allocated,
            gc_collections = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - gc[i]).ToArray()
        }));
    }
}
