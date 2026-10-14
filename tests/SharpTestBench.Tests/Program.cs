using SharpTestBench;
using System.Numerics;
using Xunit;

public sealed class ManagedTests
{
    static void Check(bool condition, string name) => Assert.True(condition, name);
    static void Throws<T>(Action action, string name) where T : Exception
    {
        var error = Record.Exception(action);
        Assert.True(error is T, $"{name}: expected {typeof(T).Name}, received {error?.GetType().Name ?? "no exception"}");
    }
    [Fact]
    public void IntegerBoundaries()
    {
        Check(new UInt<W8>(255).Value == 255, "uint maximum");
        Throws<OverflowException>(() => new UInt<W8>(256), "uint overflow");
        Throws<OverflowException>(() => new UInt<W8>(-1), "unsigned negative");
        Check(UInt<W8>.Wrap(-1).Value == 255, "wrap negative");
        Check(new UInt<W8>(255).AddWrap(new(1)).Value == 0, "add wrap");
        Throws<OverflowException>(() =>
        {
            _ = new UInt<W8>(255) + new UInt<W8>(1);
        }, "checked add");
        Check(new SInt<W8>(-128).RawBits.Value == 128, "signed min raw");
        Check(SInt<W8>.FromBits(new(255)).Value == -1, "signed decode");
        Throws<OverflowException>(() => new SInt<W8>(128), "signed overflow");
        Check(new SInt<W8>(127).AddWrap(new(1)).Value == -128, "signed wrap");
    }

    public static IEnumerable<object[]> ByteValues => Enumerable.Range(0, 256).Select(i => new object[] { i });
    public static IEnumerable<object[]> LogicPairs => from a in "01XZ" from b in "01XZ" select new object[] { a, b };

    [Theory]
    [MemberData(nameof(ByteValues))]
    public void IntegerRoundTrips(int i)
    {
        var u = new UInt<W8>(i);
        Check(LogicVector<W8>.Parse(LogicVector<W8>.From(u).ToString()).ToUInt() == u, "round trip");
        Check(SInt<W8>.FromBits(u).RawBits == u, "signed round trip");
    }

    [Theory]
    [MemberData(nameof(LogicPairs))]
    public void FourStateTruthTable(char a, char b)
    {
        var av = LogicVector<W1>.Parse(a.ToString());
        var bv = LogicVector<W1>.Parse(b.ToString());
        char and = a == '0' || b == '0' ? '0' : a == '1' && b == '1' ? '1' : 'X';
        char or = a == '1' || b == '1' ? '1' : a == '0' && b == '0' ? '0' : 'X';
        char xor = (a == '0' || a == '1') && (b == '0' || b == '1') ? (a == b ? '0' : '1') : 'X';
        Check((av & bv).ToString() == and.ToString(), "and truth table");
        Check((av | bv).ToString() == or.ToString(), "or truth table");
        Check((av ^ bv).ToString() == xor.ToString(), "xor truth table");
    }

    [Fact]
    public void LogicAndRanges()
    {
        Check((~LogicVector<W4>.Parse("01XZ")).ToString() == "10XX", "not unknowns");
        Throws<InvalidOperationException>(() => LogicVector<W4>.Parse("01XZ").ToUInt(), "reject unresolved");
        Check(LogicVector<W8>.Parse("10XZ0101").Slice<W4>(4).ToString() == "10XZ", "slice");
        Throws<ArgumentOutOfRangeException>(() => LogicVector<W8>.Parse("10000000").Slice<W4>(5), "slice bounds");
        Check(LogicVector<W256>.From(new UInt<W256>((BigInteger.One << 255) | 19)).ToUInt().Value == ((BigInteger.One << 255) | 19), "256 bits");
        Check(new LogicArray("uX01ZwLH-").ToString() == "UX01ZWLH-", "nine states");
        Throws<FormatException>(() => new LogicArray("Q"), "bad logic");
        Check(Logic.H.ToBool() && !Logic.L.ToBool(), "weak resolution");
        Check(new HdlRange(15, 8).Offset(12) == 3, "descending range");
        Check(new HdlRange(4, 11).Index(3) == 7, "ascending range");
        Throws<IndexOutOfRangeException>(() => new HdlRange(7, 0).Offset(8), "range bounds");
    }

    [Fact]
    public void DeterministicRandom()
    {
        var random1 = new SimRandom(71);
        var random2 = new SimRandom(71);
        for (int i = 0; i < 1000; i++)
            Check(random1.NextUInt64() == random2.NextUInt64(), "deterministic RNG");
    }

    public static IEnumerable<object[]> ReferenceRows => File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "nine-state-reference.txt")).Select(line => new object[] { line });

    [Theory]
    [MemberData(nameof(ReferenceRows))]
    public void NineStateReference(string line)
    {
        var a = LogicOps.Parse(line[0]);
        var b = LogicOps.Parse(line[1]);
        Check(NineState.And(a, b).Symbol() == line[2], "upstream nine-state and");
        Check(NineState.Or(a, b).Symbol() == line[3], "upstream nine-state or");
        Check(NineState.Xor(a, b).Symbol() == line[4], "upstream nine-state xor");
    }

    [Fact]
    public void ArraysAndNullRanges()
    {
        var array = new ValueArray<int>([5, 6, 7, 8], new HdlRange(7, 4));
        Check(array[6] == 6, "HDL array label");
        Check(array.Slice(new HdlRange(6, 5)).SequenceEqual(new[] { 6, 7 }), "HDL array slice");
        Check(HdlRange.To(3, 2).Length == 0, "ascending null range");
        Check(HdlRange.DownTo(2, 3).IsNull, "descending null range");
        Check(HdlRange.DownTo(2, 2).Length == 1, "singleton direction retained");
        Throws<IndexOutOfRangeException>(() => HdlRange.To(3, 2).Offset(3), "null range cannot index");
        Throws<OverflowException>(() =>
        {
            _ = HdlRange.To(int.MinValue, int.MaxValue).Length;
        }, "huge range checked");
        Check(new ValueArray<int>([], HdlRange.DownTo(-1, 0)).Length == 0, "null value array");
        Check(new LogicArray("").Length == 0, "empty logic array");
        var nine = new LogicArray("UX01ZWLH-", HdlRange.To(2, 10));
        Check(nine.At(9) == Logic.H, "nine-state HDL index");
        Check(nine.Slice(HdlRange.To(6, 8)).ToString() == "ZWL", "nine-state slice");
        Check(nine.Slice(HdlRange.To(4, 3)).Length == 0, "nine-state empty slice");
    }

    [Fact]
    public void PackedLayouts()
    {
        var shape = new PackedLayout(8, HdlRange.To(-1, 0), HdlRange.DownTo(3, 1));
        Check(shape.TotalBits == 48 && shape.BitOffset(-1, 3) == 40 && shape.BitOffset(0, 1) == 0, "mixed direction packed shape");
        Throws<ArgumentException>(() => shape.BitOffset(0), "packed rank");
        Throws<IndexOutOfRangeException>(() => shape.BitOffset(0, 0), "packed bounds");
        Throws<ArgumentException>(() => new PackedLayout(8, HdlRange.To(2, 1)), "packed null dimension");
        var dims = new[]
        {
            HdlRange.DownTo(1, 0)
        };
        var immutableShape = new PackedLayout(8, dims);
        dims[0] = HdlRange.To(9, 10);
        Check(immutableShape.BitOffset(1) == 8, "packed dimensions copied");
        var packetLayout = new PackedStructLayout(new PackedMember("tag", 4), new PackedMember("payload", 8), new PackedMember("flags", 4));
        Check(packetLayout.TotalBits == 16 && packetLayout["payload"].BitOffset == 4 && packetLayout["tag"].BitOffset == 12, "named struct offsets");
        Throws<ArgumentException>(() => new PackedStructLayout(new PackedMember("x", 4), new PackedMember("x", 4)), "duplicate struct field");
        Throws<ArgumentOutOfRangeException>(() => new PackedStructLayout(new PackedMember("x", 0)), "zero field width");
        Throws<ArgumentException>(() => new PackedStructLayout(), "empty struct");
        Throws<KeyNotFoundException>(() => { _ = packetLayout["missing"]; }, "missing struct field");
    }

    [Fact]
    public void FunctionalCoverage()
    {
        var coverage = new FunctionalCoverage();
        Check(coverage.Snapshot().Percent == null, "empty coverage has no misleading percentage");
        var bins = new[] { new CoverageBin<int>("zero", x => x == 0), new CoverageBin<int>("even", x => x % 2 == 0, 3) };
        var point = coverage.AddPoint("values", bins);
        bins[0] = new CoverageBin<int>("changed", x => true);
        point.Sample(0); point.Sample(2); point.Sample(1);
        var snapshot = point.Snapshot();
        Check(snapshot.Samples == 3 && snapshot.Unmatched == 1, "coverage samples and unmatched");
        Check(snapshot.Bins[0].Hits == 1 && snapshot.Bins[1].Hits == 2 && snapshot.Percent == 50, "overlapping bins and goals");
        point.Sample(4);
        Check(point.Snapshot().Percent == 100 && snapshot.Percent == 50, "coverage snapshot detached from counters");
        Throws<ArgumentException>(() => coverage.AddPoint("values", bins), "duplicate coverage point");
        Throws<ArgumentException>(() => coverage.AddPoint<int>("empty"), "empty bins rejected");
        Throws<ArgumentException>(() => coverage.AddPoint("bad", new CoverageBin<int>("x", _ => true, 0)), "zero goal rejected");
        Throws<ArgumentException>(() => coverage.AddPoint("bad", new CoverageBin<int>("x", _ => true), new CoverageBin<int>("x", _ => false)), "duplicate bins rejected");
        var failing = coverage.AddPoint("failing", new CoverageBin<int>("first", _ => true), new CoverageBin<int>("throws", _ => throw new FormatException()));
        Throws<FormatException>(() => failing.Sample(1), "predicate failure propagates");
        Check(failing.Snapshot().Samples == 0 && failing.Snapshot().Bins.All(b => b.Hits == 0), "predicate failure cannot partially count");
        CoveragePoint<int>? recursive = null;
        recursive = coverage.AddPoint("recursive", new CoverageBin<int>("r", x => { recursive!.Sample(x); return true; }));
        Throws<InvalidOperationException>(() => recursive.Sample(1), "recursive sampling rejected");
        Check(recursive.Snapshot().Samples == 0, "recursive sampling does not count");
        Exception? coverageThreadError = null;
        var coverageThread = new Thread(() => { try { point.Sample(0); } catch (Exception e) { coverageThreadError = e; } });
        coverageThread.Start(); coverageThread.Join();
        Check(coverageThreadError is InvalidOperationException, "coverage thread ownership");
        var coverageJson = System.Text.Json.JsonSerializer.Serialize(coverage.Snapshot());
        using (var parsedCoverage = System.Text.Json.JsonDocument.Parse(coverageJson))
            Check(parsedCoverage.RootElement.GetProperty("Points")[0].GetProperty("Bins")[1].GetProperty("Hits").GetUInt64() == 3, "coverage JSON preserves exact counts");
    }

    [Fact]
    public void CapabilityFamilies()
    {
        foreach (var (product, family) in new[] { ("Icarus Verilog 12", SimulatorFamily.Icarus), (" verilator 5.048 ", SimulatorFamily.Verilator), ("GHDL", SimulatorFamily.Ghdl), ("NVC(1.23)", SimulatorFamily.Nvc), ("NotVerilator", SimulatorFamily.Unknown), ("VerilatorClone", SimulatorFamily.Unknown), ("", SimulatorFamily.Unknown) })
        {
            var caps = SimulatorCapabilities.Describe(product, 3);
            Check(caps.Family == family, "capability product boundary " + product);
            Check(caps.All.Count == Enum.GetValues<SimulatorFeature>().Length && caps.All.All(x => x.Explanation.Length > 0 && x.Evidence.Length > 0), "complete explained catalogue");
            Check(caps[SimulatorFeature.ForceRelease].IsSupported == (family == SimulatorFamily.Icarus), "force policy");
            if (family == SimulatorFamily.Unknown)
                foreach (var feature in new[] { SimulatorFeature.FourStateWrites, SimulatorFeature.NineStateWrites, SimulatorFeature.ForceRelease })
                {
                    Check(caps[feature].Status == CapabilityStatus.Unverified, "unknown remains unverified");
                    Throws<NotSupportedException>(() => caps.Require(feature), "unverified rejected");
                }
            Throws<NotSupportedException>(() => caps.Require(SimulatorFeature.DynamicStringNul), "dynamic NUL rejected");
            Check(caps[SimulatorFeature.PackedPlaneTransport].IsSupported, "ABI transport independent of product");
        }
    }

    [Fact]
    public void CapabilityAbiGuards()
    {
        var noFeatures = SimulatorCapabilities.Describe("Verilator", 0);
        Check(noFeatures[SimulatorFeature.PackedPlaneTransport].Status == CapabilityStatus.Unsupported && noFeatures[SimulatorFeature.NativePackedRanges].Status == CapabilityStatus.Unsupported, "absent ABI features");
        Throws<NotSupportedException>(() => SimulatorCapabilities.Describe("Verilator", 3).Require(SimulatorFeature.NativePackedRanges), "conditional requires per-object probe");
        Throws<ArgumentNullException>(() => SimulatorCapabilities.Describe(null!, 0), "null product rejected");
    }
}
