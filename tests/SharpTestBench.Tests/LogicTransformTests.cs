using SharpTestBench;
using System.Text.Json;
using Xunit;

public sealed class LogicTransformTests
{
    [Fact]
    public void PinnedNineStateVectorTransforms()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "logic-reference.json")));
        Assert.Equal(81, doc.RootElement.GetProperty("transforms").GetArrayLength());
        foreach (var row in doc.RootElement.GetProperty("transforms").EnumerateArray())
        {
            var left = new LogicArray(row.GetProperty("left").GetString()!, HdlRange.To(-4, 5));
            var right = new LogicArray(row.GetProperty("right").GetString()!, HdlRange.DownTo(20, 11));
            foreach (var (key, result) in new[] { ("and_bits", left & right), ("or_bits", left | right), ("xor_bits", left ^ right), ("not_bits", ~left) })
            {
                Assert.Equal(row.GetProperty(key).GetString(), result.ToString());
                Assert.Equal(left.Range, result.Range);
                Assert.NotSame(left, result);
            }
            Assert.Equal(9, row.GetProperty("result_bounds")[0].GetInt32()); // pinned Python normalizes bounds
            Assert.Equal(0, row.GetProperty("result_bounds")[1].GetInt32());
            Assert.Equal(row.GetProperty("left").GetString(), left.ToString());
            Assert.Equal(row.GetProperty("right").GetString(), right.ToString());
        }
    }

    [Fact]
    public void PinnedLogicCollectionsAndSliceReplacement()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "logic-reference.json")));
        const string states = "UX01ZWLH-";
        Assert.Equal(2, doc.RootElement.GetProperty("collections").GetArrayLength());
        foreach (var row in doc.RootElement.GetProperty("collections").EnumerateArray())
        {
            var range = new HdlRange(row.GetProperty("left").GetInt32(), row.GetProperty("right").GetInt32(), row.GetProperty("ascending").GetBoolean());
            var value = new LogicArray(row.GetProperty("bits").GetString()!, range);
            IReadOnlyList<Logic> list = value;
            Assert.Equal(value.Length, list.Count);
            Assert.Equal(value.ToString(), new string(value.Select(v => v.Symbol()).ToArray()));
            Assert.Equal(row.GetProperty("reversed").GetString(), new string(value.Reverse().Select(v => v.Symbol()).ToArray()));
            for (int i = 0; i < value.Length; i++) Assert.Equal(list[i], value.At(range.Index(i)));
            for (int i = 0; i < states.Length; i++)
            {
                Logic state = LogicOps.Parse(states[i]);
                Assert.True(value.TryIndexOf(state, out int index));
                Assert.Equal(row.GetProperty("found")[i].GetInt32(), index);
                Assert.Equal(row.GetProperty("counts")[i].GetInt32(), value.CountOf(state));
            }
            foreach (var slice in row.GetProperty("slices").EnumerateArray())
            {
                var bounds = new HdlRange(slice.GetProperty("left").GetInt32(), slice.GetProperty("right").GetInt32(), range.Ascending);
                var changed = value.WithSlice(bounds, new LogicArray(slice.GetProperty("replacement").GetString()!));
                Assert.Equal(slice.GetProperty("extracted").GetString(), value.Slice(bounds).ToString());
                Assert.Equal(slice.GetProperty("result").GetString(), changed.ToString());
                Assert.Equal(range, changed.Range);
                Assert.Equal(row.GetProperty("bits").GetString(), value.ToString());
            }
        }
    }

    [Fact]
    public void ImmutableReplacementValidatesBeforeReturning()
    {
        var original = new LogicArray("01XZLH", HdlRange.DownTo(1, -4));
        Assert.Equal("HLZX10", original.WithSlice(original.Range, original.Reverse()).ToString());
        Assert.Equal("01XZLH", original.WithSlice(original.Range, original).ToString());
        Assert.Equal("0XXZLH", original.WithElement(0, Logic.X).ToString());
        Assert.Equal("0ZLZLH", original.WithSlice(HdlRange.DownTo(0, -1), original.Slice(HdlRange.DownTo(-2, -3))).ToString());
        IEnumerable<Logic> Fails() { yield return Logic.One; throw new FormatException(); }
        Assert.Throws<FormatException>(() => original.WithSlice(HdlRange.DownTo(0, -1), Fails()));
        Assert.Throws<ArgumentException>(() => original.WithSlice(HdlRange.DownTo(0, -1), [Logic.One]));
        Assert.Throws<ArgumentException>(() => original.WithSlice(HdlRange.To(-1, 0), [Logic.One, Logic.One]));
        Assert.Throws<IndexOutOfRangeException>(() => original.WithSlice(HdlRange.DownTo(1, -5), []));
        Assert.Throws<ArgumentOutOfRangeException>(() => original.WithSlice(HdlRange.DownTo(0, 0), [(Logic)99]));
        Assert.Throws<ArgumentOutOfRangeException>(() => original.WithElement(0, (Logic)99));
        Assert.Throws<IndexOutOfRangeException>(() => original.WithElement(9, Logic.One));
        Assert.Throws<ArgumentNullException>(() => original.WithSlice(original.Range, null!));
        Assert.Equal("01XZLH", original.ToString());
    }

    [Fact]
    public void EmptyRangesSearchAndOperandFailures()
    {
        var value = new LogicArray("0L0H", HdlRange.To(-2, 1));
        Assert.True(value.TryIndexOf(Logic.L, out int index)); Assert.Equal(-1, index);
        Assert.True(value.TryIndexOf(Logic.Zero, out index, HdlRange.To(-1, 1))); Assert.Equal(0, index);
        Assert.False(value.TryIndexOf(Logic.One, out index)); Assert.Equal(0, index);
        Assert.Equal(2, value.CountOf(Logic.Zero)); Assert.Equal(1, value.CountOf(Logic.L));
        Assert.False(value.TryIndexOf(Logic.Zero, out _, HdlRange.To(20, 19)));
        Assert.Equal(value.ToString(), value.WithSlice(HdlRange.To(20, 19), []).ToString());
        Assert.Throws<ArgumentException>(() => value.WithSlice(HdlRange.To(20, 19), [Logic.One]));
        Assert.Throws<ArgumentException>(() => value.TryIndexOf(Logic.One, out _, HdlRange.DownTo(0, 1)));
        Assert.Throws<IndexOutOfRangeException>(() => value.TryIndexOf(Logic.Zero, out _, HdlRange.To(-3, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => value.TryIndexOf((Logic)99, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => value.CountOf((Logic)99));
        var empty = new LogicArray("", HdlRange.To(3, 2));
        Assert.Empty(empty); Assert.Empty(~empty);
        Assert.Equal(empty.Range, (empty & new LogicArray("")).Range);
        Assert.False(empty.TryIndexOf(Logic.X, out _)); Assert.Equal(0, empty.CountOf(Logic.X));
        Assert.Empty(empty.WithSlice(empty.Range, []));
        Assert.Throws<ArgumentException>(() => value & empty);
        Assert.Throws<ArgumentException>(() => value | empty);
        Assert.Throws<ArgumentException>(() => value ^ empty);
        Assert.Throws<ArgumentNullException>(() => value & null!);
        Assert.Throws<ArgumentNullException>(() => null! | value);
        Assert.Throws<ArgumentNullException>(() => ~((LogicArray)null!));
    }
}
