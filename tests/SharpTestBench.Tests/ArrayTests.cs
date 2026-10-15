using SharpTestBench;
using System.Text.Json;
using Xunit;

public sealed class ArrayTests
{
    [Fact]
    public void PinnedRangeAndArrayReference()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "array-reference.json")));
        Assert.Equal(98, document.RootElement.GetArrayLength());
        foreach (var row in document.RootElement.EnumerateArray())
        {
            int[] Read(string key) => row.GetProperty(key).EnumerateArray().Select(x => x.GetInt32()).ToArray();
            var range = new HdlRange(row.GetProperty("left").GetInt32(), row.GetProperty("right").GetInt32(), row.GetProperty("ascending").GetBoolean());
            int[] indices = Read("indices");
            Assert.Equal(indices, range.ToArray());
            Assert.Equal(indices.Length, range.Length);
            for (int value = -4; value <= 4; value++) Assert.Equal(indices.Contains(value), range.Contains(value));
            for (int i = 0; i < indices.Length; i++) { Assert.Equal(i, range.Offset(indices[i])); Assert.Equal(indices[i], range.Index(i)); }
            var array = new ValueArray<int>(Read("values"), range);
            int[] counts = Read("counts");
            for (int value = 0; value < 4; value++)
            {
                var found = row.GetProperty("found")[value];
                Assert.Equal(found.ValueKind != JsonValueKind.Null, array.TryIndexOf(value, out int index));
                if (found.ValueKind != JsonValueKind.Null) Assert.Equal(found.GetInt32(), index);
                Assert.Equal(counts[value], array.CountOf(value));
            }
            if (indices.Length >= 2) array.SetSlice(new(indices[0], indices[1], range.Ascending), [7, 8]);
            Assert.Equal(Read("after_slice"), array.ToArray());
            array.Range = HdlRange.To(10, 10 + array.Length - 1);
            Assert.Equal(Read("reindexed"), array.Range.Select(i => array[i]).ToArray());
        }
    }

    [Fact]
    public void SliceAssignmentIsTransactionalAndAliasSafe()
    {
        var array = new ValueArray<int>([1, 2, 3, 4], HdlRange.DownTo(1, -2));
        array.SetSlice(array.Range, array.Reverse());
        Assert.Equal(new[] { 4, 3, 2, 1 }, array.ToArray());
        Assert.Throws<ArgumentException>(() => array.SetSlice(HdlRange.DownTo(0, -1), [9]));
        Assert.Throws<IndexOutOfRangeException>(() => array.SetSlice(HdlRange.DownTo(1, -3), [9, 9, 9, 9, 9]));
        Assert.Throws<ArgumentException>(() => array.SetSlice(HdlRange.To(-1, 0), [9, 9]));
        IEnumerable<int> Fail() { yield return 9; throw new FormatException(); }
        Assert.Throws<FormatException>(() => array.SetSlice(HdlRange.DownTo(0, -1), Fail()));
        Assert.Equal(new[] { 4, 3, 2, 1 }, array.ToArray());
        var before = array.Range;
        Assert.Throws<ArgumentException>(() => array.Range = HdlRange.To(0, 4));
        Assert.Equal(before, array.Range);
        array.SetSlice(HdlRange.DownTo(-1, 0), []);
        Assert.Empty(array.Slice(HdlRange.DownTo(-1, 0)));
        Assert.Throws<ArgumentException>(() => array.SetSlice(HdlRange.DownTo(-1, 0), [1]));
        array.SetSlice(HdlRange.DownTo(0, -1), [8, 9]);
        Assert.Equal(new[] { 4, 8, 9, 1 }, array.ToArray());
    }

    [Fact]
    public void SearchDistinguishesNegativeIndicesAndMissingValues()
    {
        var array = new ValueArray<string?>(["a", null, "A", null], HdlRange.To(-2, 1));
        Assert.True(array.TryIndexOf(null, out int index)); Assert.Equal(-1, index);
        Assert.False(array.TryIndexOf("missing", out index)); Assert.Equal(0, index);
        Assert.True(array.TryIndexOf(null, out index, HdlRange.To(0, 1))); Assert.Equal(1, index);
        Assert.Equal(2, array.CountOf(null));
        Assert.Equal(2, array.CountOf("A", StringComparer.OrdinalIgnoreCase));
        Assert.True(array.TryIndexOf("A", out index, comparer: StringComparer.OrdinalIgnoreCase)); Assert.Equal(-2, index);
        Assert.False(array.TryIndexOf(null, out _, HdlRange.To(1, 0)));
        Assert.Throws<IndexOutOfRangeException>(() => array.TryIndexOf("a", out _, HdlRange.To(-3, 1)));
        var empty = new ValueArray<int>([], HdlRange.To(1, 0));
        Assert.False(empty.TryIndexOf(0, out _)); Assert.Equal(0, empty.CountOf(0));
        empty.Range = HdlRange.DownTo(-2, -1);
        Assert.True(empty.Range.IsNull);
    }

    [Fact]
    public void RangeLimitsAndStructuralEqualityAreExplicit()
    {
        Assert.Equal(new[] { int.MaxValue - 1, int.MaxValue }, HdlRange.To(int.MaxValue - 1, int.MaxValue).ToArray());
        Assert.Equal(new[] { int.MinValue + 1, int.MinValue }, HdlRange.DownTo(int.MinValue + 1, int.MinValue).ToArray());
        var huge = HdlRange.To(int.MinValue, int.MaxValue);
        Assert.True(huge.Contains(0)); Assert.True(huge.Contains(int.MaxValue));
        Assert.Equal(new[] { int.MinValue, int.MinValue + 1 }, huge.Take(2).ToArray());
        Assert.Throws<OverflowException>(() => huge.Length);
        Assert.Throws<OverflowException>(() => huge.Offset(int.MaxValue));
        Assert.Throws<IndexOutOfRangeException>(() => HdlRange.To(0, 1).Index(-1));
        Assert.NotEqual(HdlRange.To(2, 1), HdlRange.To(9, 8));
        Assert.NotEqual(HdlRange.To(1, 1), HdlRange.DownTo(1, 1));
    }
}
