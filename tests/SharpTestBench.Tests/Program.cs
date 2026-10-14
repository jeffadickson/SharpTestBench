using SharpTestBench;
using System.Numerics;

int passed = 0;
void Check(bool b, string name)
{
    if (!b)
        throw new Exception(name);
    passed++;
}

void Throws<T>(Action a, string name)
    where T : Exception
{
    try
    {
        a();
    }
    catch (T)
    {
        passed++;
        return;
    }

    throw new Exception(name);
}

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
for (int i = 0; i < 256; i++)
{
    var u = new UInt<W8>(i);
    Check(LogicVector<W8>.Parse(LogicVector<W8>.From(u).ToString()).ToUInt() == u, "round trip");
    Check(SInt<W8>.FromBits(u).RawBits == u, "signed round trip");
}

string symbols = "01XZ";
foreach (char a in symbols)
    foreach (char b in symbols)
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
var random1 = new SimRandom(71);
var random2 = new SimRandom(71);
for (int i = 0; i < 1000; i++)
    Check(random1.NextUInt64() == random2.NextUInt64(), "deterministic RNG");
foreach (var line in File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "nine-state-reference.txt")))
{
    var a = LogicOps.Parse(line[0]);
    var b = LogicOps.Parse(line[1]);
    Check(NineState.And(a, b).Symbol() == line[2], "upstream nine-state and");
    Check(NineState.Or(a, b).Symbol() == line[3], "upstream nine-state or");
    Check(NineState.Xor(a, b).Symbol() == line[4], "upstream nine-state xor");
}

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
Console.WriteLine($"UNIT_PASS {passed}");
