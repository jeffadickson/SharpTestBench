using SharpTestBench;
using System.Numerics;
using System.Text.Json;
using Xunit;

public sealed class LogicConversionTests
{
    static byte[] Bytes(JsonElement row, string key) => row.GetProperty(key).EnumerateArray().Select(x => x.GetByte()).ToArray();
    static BigInteger Number(JsonElement row, string key) => BigInteger.Parse(row.GetProperty(key).GetString()!);

    [Fact]
    public void PinnedScalarAndVectorResolution()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "logic-reference.json")));
        Assert.Equal(9, doc.RootElement.GetProperty("scalar").GetArrayLength());
        foreach (var row in doc.RootElement.GetProperty("scalar").EnumerateArray())
        {
            var value = LogicOps.Parse(row.GetProperty("symbol").GetString()![0]);
            Assert.Equal(row.GetProperty("resolvable").GetBoolean(), value.IsResolvable());
            foreach (var (key, policy) in new[] { ("weak", LogicResolution.Weak), ("zeros", LogicResolution.Zeros), ("ones", LogicResolution.Ones) })
                Assert.Equal(row.GetProperty("resolved").GetProperty(key).GetString(), value.Resolve(policy).Symbol().ToString());
        }
        Assert.Equal(86, doc.RootElement.GetProperty("vectors").GetArrayLength());
        foreach (var row in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            string text = row.GetProperty("bits").GetString()!;
            var value = new LogicArray(text, HdlRange.To(-3, text.Length - 4));
            Assert.Equal(row.GetProperty("resolvable").GetBoolean(), value.IsResolvable);
            foreach (var (key, policy) in new[] { ("weak", LogicResolution.Weak), ("zeros", LogicResolution.Zeros), ("ones", LogicResolution.Ones) })
            {
                var resolved = value.Resolve(policy);
                Assert.Equal(value.Range, resolved.Range);
                Assert.Equal(row.GetProperty("resolved").GetProperty(key).GetString(), resolved.ToString());
            }
            if (text == "-1") Assert.Equal("-1", row.GetProperty("unsigned").GetString()); // retained upstream discrepancy
            if (!value.IsResolvable || value.Length == 0)
            {
                Assert.Throws<InvalidOperationException>(() => value.ToUnsigned());
                Assert.Throws<InvalidOperationException>(() => value.ToSigned());
                Assert.Throws<InvalidOperationException>(() => value.ToBytes(ByteOrder.BigEndian));
            }
            else
            {
                Assert.Equal(Number(row, "unsigned"), value.ToUnsigned());
                Assert.Equal(Number(row, "signed"), value.ToSigned());
                Assert.Equal(Bytes(row, "big"), value.ToBytes(ByteOrder.BigEndian));
                Assert.Equal(Bytes(row, "little"), value.ToBytes(ByteOrder.LittleEndian));
            }
            Assert.Equal(text, value.ToString());
        }
    }

    [Fact]
    public void PinnedCheckedAndWrappingIntegerConversions()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "logic-reference.json")));
        Assert.Equal(256, doc.RootElement.GetProperty("integers").GetArrayLength());
        foreach (var row in doc.RootElement.GetProperty("integers").EnumerateArray())
        {
            int width = row.GetProperty("width").GetInt32();
            HdlRange range = HdlRange.To(-3, width - 4);
            BigInteger number = Number(row, "value");
            bool wrap = row.GetProperty("wrap").GetBoolean();
            LogicArray Make() => row.GetProperty("signed").GetBoolean()
                ? LogicArray.FromSigned(number, range, wrap) : LogicArray.FromUnsigned(number, range, wrap);
            if (row.GetProperty("bits").ValueKind == JsonValueKind.Null) { Assert.Throws<OverflowException>(() => Make()); continue; }
            var value = Make();
            Assert.Equal(range, value.Range);
            Assert.Equal(row.GetProperty("bits").GetString(), value.ToString());
            Assert.Equal(Number(row, "unsigned"), value.ToUnsigned());
            Assert.Equal(Number(row, "signed_value"), value.ToSigned());
            Assert.Equal(Bytes(row, "big"), value.ToBytes(ByteOrder.BigEndian));
            Assert.Equal(Bytes(row, "little"), value.ToBytes(ByteOrder.LittleEndian));
            var descending = value.WithRange(HdlRange.DownTo(width + 2, 3));
            Assert.Equal(value.ToSigned(), descending.ToSigned());
            foreach (var order in new[] { ByteOrder.BigEndian, ByteOrder.LittleEndian })
            {
                byte[] bytes = value.ToBytes(order);
                var roundTrip = LogicArray.FromBytes(bytes, order);
                Assert.Equal(value.ToUnsigned(), roundTrip.ToUnsigned());
                Assert.Equal(bytes.Length * 8, roundTrip.Length); // partial-byte widths deliberately zero-pad
                Assert.Equal(bytes, roundTrip.ToBytes(order));
            }
        }
    }

    sealed class AlternatingRandom : Random
    {
        public int Calls;
        public override int Next(int maxValue) { Assert.Equal(2, maxValue); return Calls++ % 2; }
    }

    [Fact]
    public void RandomResolutionIsExplicitAndLeftToRight()
    {
        var random = new AlternatingRandom();
        var value = new LogicArray("LHUXZW-01", HdlRange.DownTo(12, 4));
        var result = value.Resolve(LogicResolution.Random, random);
        Assert.Equal("010101001", result.ToString());
        Assert.Equal(5, random.Calls);
        Assert.Equal(value.Range, result.Range);
        Assert.True(result.IsResolvable);
        Assert.Equal("LHUXZW-01", value.ToString());
        Assert.Equal(value.Resolve(LogicResolution.Random, new Random(42)).ToString(), value.Resolve(LogicResolution.Random, new Random(42)).ToString());
        Assert.Throws<ArgumentNullException>(() => value.Resolve(LogicResolution.Random));
        Assert.Throws<ArgumentNullException>(() => Logic.One.Resolve(LogicResolution.Random));
        Assert.Throws<ArgumentOutOfRangeException>(() => value.Resolve((LogicResolution)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => ((Logic)999).Resolve(LogicResolution.Zeros));
    }

    [Fact]
    public void ConversionBoundariesAndByteWidths()
    {
        var empty = new LogicArray("");
        Assert.True(empty.IsResolvable);
        Assert.Throws<InvalidOperationException>(() => empty.ToUnsigned());
        Assert.Throws<ArgumentException>(() => LogicArray.FromUnsigned(0, empty.Range));
        Assert.Throws<ArgumentException>(() => LogicArray.FromSigned(0, empty.Range, wrap: true));
        Assert.Throws<ArgumentException>(() => LogicArray.FromBytes([], ByteOrder.BigEndian));
        Assert.Throws<ArgumentException>(() => LogicArray.FromBytes([1], ByteOrder.BigEndian, HdlRange.To(0, 6)));
        Assert.Throws<ArgumentOutOfRangeException>(() => LogicArray.FromBytes([1], (ByteOrder)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogicArray("1").ToBytes((ByteOrder)99));
        Assert.Throws<ArgumentNullException>(() => new LogicArray(null!));
        Assert.Throws<ArgumentNullException>(() => new LogicArray(null!, HdlRange.To(0, 0)));
        var wide = LogicArray.FromBytes([0, 1, 128], ByteOrder.BigEndian, HdlRange.To(-7, 16));
        Assert.Equal(new byte[] { 0, 1, 128 }, wide.ToBytes(ByteOrder.BigEndian));
        Assert.Equal(new byte[] { 128, 1, 0 }, wide.ToBytes(ByteOrder.LittleEndian));
        Assert.Throws<ArgumentException>(() => wide.WithRange(HdlRange.To(0, 24)));
        Assert.Equal(new BigInteger(384), wide.ToUnsigned());
    }
}
