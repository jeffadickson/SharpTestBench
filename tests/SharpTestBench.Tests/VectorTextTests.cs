using System.Numerics;
using SharpTestBench;
using Xunit;

/// <summary>Independent bit-position oracles for stack/pool, partial-byte and four-state conversions.</summary>
public sealed class VectorTextTests
{
    readonly struct W33 : IWidth { public static int Bits => 33; }
    readonly struct W4096 : IWidth { public static int Bits => 4096; }
    readonly struct W8193 : IWidth { public static int Bits => 8193; }
    readonly struct WMaximum : IWidth { public static int Bits => 1048576; }
    /// <summary>Compare decoded planes and formatted text against independently accumulated bit masks.</summary>
    static void Check<W>() where W : struct, IWidth
    {
        var random = new Random(53 + W.Bits);
        for (int sample = 0; sample < 8; sample++)
        {
            var text = new char[W.Bits];
            BigInteger value = 0, unknown = 0, highZ = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char symbol = "01xZ"[random.Next(4)]; text[text.Length - 1 - i] = symbol;
                BigInteger bit = BigInteger.One << i;
                if (symbol == '1') value |= bit;
                if (symbol is 'x' or 'Z') unknown |= bit;
                if (symbol == 'Z') highZ |= bit;
            }
            var parsed = LogicVector<W>.Parse(new string(text));
            Assert.Equal(new LogicVector<W>(value, unknown, highZ), parsed);
            Assert.Equal(new string(text).ToUpperInvariant(), parsed.ToString());
            // A smaller value after a large mask must not expose stale pool bytes.
            Assert.Equal(new string('0', W.Bits - 1) + "1", new LogicVector<W>(1).ToString());
            text[random.Next(text.Length)] = 'U';
            Assert.Throws<FormatException>(() => LogicVector<W>.Parse(new string(text)));
        }
    }
    [Fact] public void FourStateTextAcrossStorageBoundaries()
    { Check<W1>(); Check<W8>(); Check<W33>(); Check<W256>(); Check<W4096>(); Check<W8193>(); }
    [Fact] public void MaximumWidthAndArgumentGuards()
    {
        string text = "1" + new string('0', WMaximum.Bits - 2) + "1";
        var expected = new LogicVector<WMaximum>((BigInteger.One << (WMaximum.Bits - 1)) | 1);
        Assert.Equal(expected, LogicVector<WMaximum>.Parse(text));
        Assert.Equal(text, expected.ToString());
        Assert.Throws<ArgumentNullException>(() => LogicVector<W8>.Parse(null!));
        Assert.Throws<ArgumentException>(() => LogicVector<W8>.Parse("0"));
    }
}
