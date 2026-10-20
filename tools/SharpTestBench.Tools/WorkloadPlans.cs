namespace SharpTestBench.Tools;

internal sealed record DspPlanRow(bool Reset, bool Ready, bool Offer, int Sample);
internal sealed record AxiPlanRow(string Op, int Address, uint Data, int Strobe, int AwDelay, int WDelay, int ReadyDelay);
internal static class WorkloadPlans
{
    public static DspPlanRow[] Dsp(int count, ulong seed)
    {
        var random = new PythonRandom(seed); var rows = new List<DspPlanRow> { new(true, false, false, 0) };
        int[] extremes = [-32768, 32767, -1, 0, 1, 16384];
        for (int block = 0; block < 4; block++) for (int i = 0; i < 24; i++) rows.Add(new(i == 12 && block % 2 == 0, i < 2 || i >= 12, true, extremes[i % 6]));
        for (int i = 0; i < count; i++) rows.Add(new(i % 257 == 256, random.Below(4) != 0, random.Below(5) != 0, random.Below(65536) - 32768));
        rows.AddRange(Enumerable.Repeat(new DspPlanRow(false, true, false, 0), 4)); return rows.ToArray();
    }
    public static uint[] Cpu(int count)
    {
        static uint Instruction(int op, int rd = 0, int rs = 0, int rt = 0, int imm = 0) => (uint)((op << 28) | (rd << 25) | (rs << 22) | (rt << 19) | (imm & 65535));
        return [Instruction(1, rd: 0, imm: 123), Instruction(1, rd: 1, imm: count), Instruction(1, rd: 2, imm: 0), Instruction(1, rd: 3, imm: -1), Instruction(1, rd: 4, imm: 1), Instruction(5, rs: 2, rt: 3), Instruction(4, rd: 5, rs: 2), Instruction(2, rd: 3, rs: 5, rt: 4), Instruction(3, rd: 6, rs: 3, rt: 5), Instruction(5, rs: 2, rt: 6, imm: 64), Instruction(2, rd: 2, rs: 2, rt: 4), Instruction(1, rd: 7, imm: -1), Instruction(2, rd: 1, rs: 1, rt: 7), Instruction(6, rs: 1, rt: 0, imm: -8), Instruction(15)];
    }
    public static AxiPlanRow[] Axi(int count, ulong seed)
    {
        var random = new PythonRandom(seed); var rows = new List<AxiPlanRow>();
        void Add(string op, int address = 0, uint data = 0, int strobe = 0) => rows.Add(new(op, address, data, strobe, random.Below(6), random.Below(6), random.Below(12) + 8));
        for (int i = 0; i < count; i++)
        {
            int address = random.Below(64) * 4;
            if (i % 23 == 0) address = 256; else if (i % 29 == 0) address = 3;
            Add("write", address, (uint)random.GetBits(32), i % 16); Add("read", address);
            if (i % 32 == 31) { Add("reset_" + new[] { "aw", "w", "b", "r" }[(i / 32) % 4]); Add("read", 0); }
        }
        foreach (var phase in new[] { "aw", "w", "b", "r" }) { Add("reset_" + phase); Add("read", 0); }
        return rows.ToArray();
    }
}
