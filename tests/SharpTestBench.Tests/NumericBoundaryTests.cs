using System.Numerics;
using SharpTestBench;
using Xunit;

public sealed class NumericBoundaryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    public void UnsignedBoundary(int width)
    {
        var range = HdlRange.To(-13, width - 14);
        BigInteger modulus = BigInteger.One << width;
        foreach (BigInteger value in new[] { BigInteger.Zero, BigInteger.One, modulus - 1 })
        {
            var actual = LogicArray.FromUnsigned(value, range);
            Assert.Equal(value, actual.ToUnsigned());
            Assert.Equal(range, actual.Range);
            Assert.Equal(range.Length, actual.ToString().Length);
            byte[] bytes = actual.ToBytes(ByteOrder.BigEndian);
            Assert.Equal(value, new BigInteger(bytes, true, true));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    public void SignedBoundary(int width)
    {
        var range = HdlRange.To(-13, width - 14);
        BigInteger modulus = BigInteger.One << width;
        BigInteger limit = modulus >> 1;
        foreach (BigInteger value in new[] { -limit, -BigInteger.One, BigInteger.Zero, limit - 1 })
        {
            var actual = LogicArray.FromSigned(value, range);
            Assert.Equal(value, actual.ToSigned());
            Assert.Equal(value & (modulus - 1), actual.ToUnsigned());
            Assert.Equal(range, actual.Range);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    public void UnsignedOverflowBoundary(int width)
    {
        var range = HdlRange.To(-13, width - 14);
        BigInteger modulus = BigInteger.One << width;
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(modulus, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range, wrap: true));
        Assert.Equal(BigInteger.Zero, LogicArray.FromUnsigned(modulus, range, wrap: true).ToUnsigned());
        Assert.Equal(modulus - 1, LogicArray.FromUnsigned(3 * modulus - 1, range, wrap: true).ToUnsigned());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    public void SignedOverflowBoundary(int width)
    {
        var range = HdlRange.To(-13, width - 14);
        BigInteger modulus = BigInteger.One << width;
        BigInteger limit = modulus >> 1;
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(-limit - 1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(limit, range));
        Assert.Equal(limit - 1, LogicArray.FromSigned(-limit - 1, range, wrap: true).ToSigned());
        Assert.Equal(-limit, LogicArray.FromSigned(limit, range, wrap: true).ToSigned());
    }
}
