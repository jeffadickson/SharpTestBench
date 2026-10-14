using SharpTestBench;
using System.Runtime.InteropServices;
using static SharpTestBench.Triggers;

public static class Entry
{
    [SimTest]
    public static async Task VhdlNamedStruct(TestContext c)
    {
        var data = c.Dut.Child("data").Signal<W8>();
        var fields = data.Struct(new PackedStructLayout(new PackedMember("high", 4), new PackedMember("low", 4)));
        data.Write(new UInt<W8>(0));
        fields.Field<W4>("high").Write(new UInt<W4>(0xD));
        fields.Field<W4>("low").Write(new UInt<W4>(5));
        await ReadOnly();
        Check(data.ReadUInt().Value == 0xD5, "portable named struct overlay");
    }

    [SimTest]
    public static async Task VhdlDiscovery(TestContext c)
    {
        var data = c.Dut.Child("data").Signal<W8>();
        foreach (var discovery in new[] { HdlObject.Roots(), HdlObject.Packages(), data.Drivers(), data.Loads() })
        {
            if (c.Runtime.Simulator.Contains("NVC"))
                Check(discovery.Status == DiscoveryStatus.Unsupported, "VHPI unsupported discovery is explicit");
            Console.WriteLine($"DISCOVERY {discovery.Status}: {string.Join(",", discovery.Objects.Select(o => o.FullName))}; {discovery.Explanation}");
        }
        Check(data.DiscoverPackedLayout().Layout!.BitOffset(7) == 7, "VHDL vector inferred range");
        data.Write(new UInt<W8>(0));
        data.PackedBits()[7].Write(new UInt<W1>(1));
        await ReadOnly();
        Check(data.ReadUInt().Value == 128, "VHDL inferred bit write");
    }

    [UnmanagedCallersOnly]
    public static int Initialize(nint p, int n) => Regression.Initialize(typeof(Entry).Assembly);
    [SimTest]
    public static async Task VhdlPackedFields(TestContext c)
    {
        var data = c.Dut.Child("data").Signal<W8>();
        var high = data.Field<W4>(4);
        var low = data.Field<W4>(0);
        data.Write(new UInt<W8>(0));
        await ReadWrite();
        high.Write(new UInt<W4>(0xA));
        low.Write(new UInt<W4>(0xB));
        await ReadOnly();
        Check(data.ReadUInt().Value == 0xAB, "VHDL ReadWrite field composition");
        await Timer(1);
        data.Write(new UInt<W8>(0x12));
        await ReadWrite();
        high.Write(new UInt<W4>(0xC));
        await ReadOnly();
        Check(data.ReadUInt().Value == 0xC2, "VHDL whole deposit followed by ReadWrite field update");
    }

    [SimTest]
    public static async Task VhdlResolvedDrivers(TestContext c)
    {
        const string states = "UX01ZWLH-";
        // IEEE std_logic_1164 resolution table, checked against the installed GHDL source.
        string[] table =
        {
            "UUUUUUUUU",
            "UXXXXXXXX",
            "UX0X0000X",
            "UXX11111X",
            "UX01ZWLHX",
            "UX01WWWWX",
            "UX01LWLWX",
            "UX01HWWHX",
            "UXXXXXXXX"
        };
        var a = c.Dut.Child("driver_a").Signal();
        var b = c.Dut.Child("driver_b").Signal();
        var bus = c.Dut.Child("resolved_bus").Signal();
        for (int i = 0; i < 9; i++)
            for (int j = 0; j < 9; j++)
            {
                ulong now = c.Runtime.Now;
                a.WriteBits(states[i].ToString());
                b.WriteBits(states[j].ToString());
                await ReadOnly();
                Check(c.Runtime.Now == now && bus.ReadBits() == table[i][j].ToString(), $"resolution {states[i]} + {states[j]}: {bus.ReadBits()}");
                await Timer(1);
            }
    }

    [SimTest]
    public static async Task VhdlDeltaChain(TestContext c)
    {
        var source = c.Dut.Child("delta_source").Signal<W8>();
        var first = c.Dut.Child("delta1").Signal<W8>();
        var last = c.Dut.Child("delta3").Signal<W8>();
        source.Write(new UInt<W8>(0));
        await Timer(1);
        for (int i = 1; i <= 20; i++)
        {
            ulong now = c.Runtime.Now;
            source.Write(new UInt<W8>(i));
            source.Write(new UInt<W8>(i + 32));
            await WithTimeout(Combine(ValueChange(first), ValueChange(last)), 10);
            await ReadOnly();
            Check(c.Runtime.Now == now && last.ReadUInt().Value == ((i + 32) ^ 7), "VHDL multi-delta settle at same timestamp");
            await Timer(1);
        }
    }

    [SimTest]
    public static Task VhdlObjectMetadata(TestContext c)
    {
        var obj = c.Dut.Child("data");
        if (!obj.FullName.Contains("data", StringComparison.OrdinalIgnoreCase) || obj.TypeName.Length == 0)
            throw new Exception("VHDL object metadata missing");
        Console.WriteLine($"CS_METADATA {obj.FullName} type={obj.TypeName} definition={c.Dut.DefinitionName} file={c.Dut.DefinitionFile}");
        return Task.CompletedTask;
    }

    [SimTest]
    public static async Task VhdlSignalAndDeltaCycle(TestContext c)
    {
        var d = c.Dut.Child("data").Signal<W8>();
        var clk = c.Dut.Child("clk").Signal<W1>();
        var q = c.Dut.Child("sampled").Signal<W8>();
        d.Write(new UInt<W8>(93));
        clk.WriteBit(false);
        await Timer(2);
        clk.WriteBit(true);
        await ReadOnly();
        if (q.ReadUInt().Value != 93)
            throw new Exception("VHDL registered transfer failed");
    }

    [SimTest]
    public static async Task VhdlUnknowns(TestContext c)
    {
        var d = c.Dut.Child("logic_data").Signal<W4>();
        d.Write(LogicVector<W4>.Parse("10XZ"));
        await ReadOnly();
        if (d.Read().ToString() != "10XZ")
            throw new Exception("VHDL XZ roundtrip failed");
    }

    [SimTest]
    public static async Task VhdlClockTasks(TestContext c)
    {
        var clk = c.Dut.Child("clk").Signal();
        var clock = c.StartSoon(new SimClock(clk, 10).Run);
        await ClockCycles(clk, 10, c.Cancellation);
        clock.Cancel();
        await Timer(1);
    }

    public readonly struct W9 : IWidth
    {
        public static int Bits => 9;
    }

    public enum Mode
    {
        Idle,
        Active,
        Done
    }

    static void Check(bool ok, string why)
    {
        if (!ok)
            throw new Exception(why);
    }

    [SimTest]
    public static async Task VhdlNineStatesAndRange(TestContext c)
    {
        var nine = c.Dut.Child("nine").Logic<W9>();
        nine.Write(new LogicArray("UX01ZWLH-"));
        await ReadOnly();
        var actual = nine.Read();
        try
        {
            _ = nine.Raw.Read();
            throw new Exception("Four-state transport accepted nine-state symbols");
        }
        catch (FormatException)
        {
        }

        Check(actual.ToString() == "UX01ZWLH-", "native nine-state preservation: " + actual);
        Check(actual.Range == HdlRange.To(2, 10) && actual.At(9) == Logic.H, "ascending native range");
    }

    [SimTest(Simulators = new[] { "nvc" })]
    public static async Task VhdlIntegerRealStringEnum(TestContext c)
    {
        var number = c.Dut.Child("integer_data").Integer();
        var text = c.Dut.Child("text_data").String();
        var mode = c.Dut.Child("mode_data").Enum<Mode>();
        var real = c.Dut.Child("real_data").Real();
        Check(number.Read() == -17 && mode.Read() == Mode.Idle, "initial integer and enum");
        number.Write(-2147483647);
        text.Write("WORLD");
        mode.Write(Mode.Done);
        real.Write(-3.125);
        await ReadOnly();
        Check(number.Read() == -2147483647, "signed integer roundtrip");
        Check(text.Read() == "WORLD", "fixed string roundtrip: " + text.Read());
        Check(mode.Read() == Mode.Done, "enum mapping");
        Check(real.Read() == -3.125, "real roundtrip");
    }

    [SimTest(Simulators = new[] { "nvc" })]
    public static async Task VhdlArraysRecordsAndGenerics(TestContext c)
    {
        var words = c.Dut.Child("words");
        Check(words.Range == HdlRange.To(2, 4), "array range");
        words.At(3).Signal<W8>().Write(new UInt<W8>(123));
        var grid = c.Dut.Child("grid").Array(row => row.Array(word => word.Signal<W8>()));
        grid[2][4].Write(new UInt<W8>(167));
        var packet = c.Dut.Child("packet");
        packet.Child("valid").Signal().WriteBit(true);
        packet.Child("payload").Signal<W8>().Write(new UInt<W8>(231));
        var generic = c.Dut.Child("count").Integer();
        Check(generic.IsConstant && generic.Read() == 37, "generic value and constant metadata");
        bool rejected = false;
        try
        {
            generic.Write(2);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Check(rejected, "generic write guard");
        await ReadOnly();
        Check(words.At(3).Signal<W8>().ReadUInt().Value == 123, "array indexed write");
        Check(grid[2][4].ReadUInt().Value == 167, "nested HDL array indices");
        Check(packet.Child("valid").Signal().ReadBit() && packet.Child("payload").Signal<W8>().ReadUInt().Value == 231, "record write");
    }

    [SimTest(Simulators = new[] { "nvc" })]
    public static async Task VhdlNullRangeAndBindings(TestContext c)
    {
        var empty = c.Dut.Child("empty_vector").Signal();
        Check(empty.Width == 0 && empty.Range == HdlRange.To(3, 2), "native null vector");
        Check(empty.ReadLogic().Length == 0, "native empty logic read");
        var words = c.Dut.Child("words").Array(x => x.Signal<W8>());
        Check(words.Count == 3 && words.Range == HdlRange.To(2, 4), "typed VHDL array range");
        words[3].Write(new UInt<W8>(123));
        await Timer(1);
        Check(words[3].ReadUInt().Value == 123, "typed indexed array read");
        string generated = Bindings.Generate(c.Dut, "VhdlBindings", namespaceName: "GeneratedVhdl");
        Check(generated.Contains("StringSignal") && generated.Contains("IntegerSignal") && generated.Contains("payload", StringComparison.OrdinalIgnoreCase), "record and scalar binding generation");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Environment.GetEnvironmentVariable("CS_BINDINGS"))!, "VhdlBindings.cs"), generated);
        await Timer(1);
    }

    [SimTest(Simulators = new[] { "nvc" })]
    public static async Task VhdlStringBytes(TestContext c)
    {
        var text = c.Dut.Child("text_data").String();
        text.Write("AéZ!? ".TrimEnd());
        await Timer(1);
        Check(text.Read() == "AéZ!?", "Latin-1 byte string");
        text.Write("A\0Bé!");
        await ReadOnly();
        Check(text.Read() == "A\0Bé!", "embedded NUL fixed string: " + text.Read());
    }

    [SimTest(Simulators = new[] { "nvc" })]
    public static async Task VhdlForceReleaseGuard(TestContext c)
    {
        Check(!c.Runtime.SupportsForceRelease, "force/deposit hold contract unverified on NVC");
        bool rejected = false;
        try
        {
            c.Dut.Child("data").Signal<W8>().Force(LogicVector<W8>.From(new UInt<W8>(77)));
        }
        catch (NotSupportedException)
        {
            rejected = true;
        }

        Check(rejected, "unsupported force cannot silently produce wrong behavior");
        await Timer(1);
    }
}
