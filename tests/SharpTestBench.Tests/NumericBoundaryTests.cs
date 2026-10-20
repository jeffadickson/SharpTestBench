using System.Numerics;
using SharpTestBench;
using Xunit;

public sealed class NumericBoundaryTests
{
    [Fact]
    public void Unsigned1Bits()
    {
        var range = HdlRange.To(-13, -13);
        BigInteger modulus = BigInteger.One << 1;
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
    [Fact]
    public void Signed1Bits()
    {
        var range = HdlRange.To(-13, -13);
        BigInteger modulus = BigInteger.One << 1;
        BigInteger limit = modulus >> 1;
        foreach (BigInteger value in new[] { -limit, -BigInteger.One, BigInteger.Zero, limit - 1 })
        {
            var actual = LogicArray.FromSigned(value, range);
            Assert.Equal(value, actual.ToSigned());
            Assert.Equal(value & (modulus - 1), actual.ToUnsigned());
            Assert.Equal(range, actual.Range);
        }
    }
    [Fact]
    public void UnsignedOverflow1Bits()
    {
        var range = HdlRange.To(-13, -13);
        BigInteger modulus = BigInteger.One << 1;
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(modulus, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range, wrap: true));
        Assert.Equal(BigInteger.Zero, LogicArray.FromUnsigned(modulus, range, wrap: true).ToUnsigned());
        Assert.Equal(modulus - 1, LogicArray.FromUnsigned(3 * modulus - 1, range, wrap: true).ToUnsigned());
    }
    [Fact]
    public void SignedOverflow1Bits()
    {
        var range = HdlRange.To(-13, -13);
        BigInteger modulus = BigInteger.One << 1;
        BigInteger limit = modulus >> 1;
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(-limit - 1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(limit, range));
        Assert.Equal(limit - 1, LogicArray.FromSigned(-limit - 1, range, wrap: true).ToSigned());
        Assert.Equal(-limit, LogicArray.FromSigned(limit, range, wrap: true).ToSigned());
    }
    [Fact]
    public void Unsigned7Bits()
    {
        var range = HdlRange.To(-13, -7);
        BigInteger modulus = BigInteger.One << 7;
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
    [Fact]
    public void Signed7Bits()
    {
        var range = HdlRange.To(-13, -7);
        BigInteger modulus = BigInteger.One << 7;
        BigInteger limit = modulus >> 1;
        foreach (BigInteger value in new[] { -limit, -BigInteger.One, BigInteger.Zero, limit - 1 })
        {
            var actual = LogicArray.FromSigned(value, range);
            Assert.Equal(value, actual.ToSigned());
            Assert.Equal(value & (modulus - 1), actual.ToUnsigned());
            Assert.Equal(range, actual.Range);
        }
    }
    [Fact]
    public void UnsignedOverflow7Bits()
    {
        var range = HdlRange.To(-13, -7);
        BigInteger modulus = BigInteger.One << 7;
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(modulus, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range, wrap: true));
        Assert.Equal(BigInteger.Zero, LogicArray.FromUnsigned(modulus, range, wrap: true).ToUnsigned());
        Assert.Equal(modulus - 1, LogicArray.FromUnsigned(3 * modulus - 1, range, wrap: true).ToUnsigned());
    }
    [Fact]
    public void SignedOverflow7Bits()
    {
        var range = HdlRange.To(-13, -7);
        BigInteger modulus = BigInteger.One << 7;
        BigInteger limit = modulus >> 1;
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(-limit - 1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(limit, range));
        Assert.Equal(limit - 1, LogicArray.FromSigned(-limit - 1, range, wrap: true).ToSigned());
        Assert.Equal(-limit, LogicArray.FromSigned(limit, range, wrap: true).ToSigned());
    }
    [Fact]
    public void Unsigned8Bits()
    {
        var range = HdlRange.To(-13, -6);
        BigInteger modulus = BigInteger.One << 8;
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
    [Fact]
    public void Signed8Bits()
    {
        var range = HdlRange.To(-13, -6);
        BigInteger modulus = BigInteger.One << 8;
        BigInteger limit = modulus >> 1;
        foreach (BigInteger value in new[] { -limit, -BigInteger.One, BigInteger.Zero, limit - 1 })
        {
            var actual = LogicArray.FromSigned(value, range);
            Assert.Equal(value, actual.ToSigned());
            Assert.Equal(value & (modulus - 1), actual.ToUnsigned());
            Assert.Equal(range, actual.Range);
        }
    }
    [Fact]
    public void UnsignedOverflow8Bits()
    {
        var range = HdlRange.To(-13, -6);
        BigInteger modulus = BigInteger.One << 8;
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(modulus, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range, wrap: true));
        Assert.Equal(BigInteger.Zero, LogicArray.FromUnsigned(modulus, range, wrap: true).ToUnsigned());
        Assert.Equal(modulus - 1, LogicArray.FromUnsigned(3 * modulus - 1, range, wrap: true).ToUnsigned());
    }
    [Fact]
    public void SignedOverflow8Bits()
    {
        var range = HdlRange.To(-13, -6);
        BigInteger modulus = BigInteger.One << 8;
        BigInteger limit = modulus >> 1;
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(-limit - 1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(limit, range));
        Assert.Equal(limit - 1, LogicArray.FromSigned(-limit - 1, range, wrap: true).ToSigned());
        Assert.Equal(-limit, LogicArray.FromSigned(limit, range, wrap: true).ToSigned());
    }
    [Fact]
    public void Unsigned9Bits()
    {
        var range = HdlRange.To(-13, -5);
        BigInteger modulus = BigInteger.One << 9;
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
    [Fact]
    public void Signed9Bits()
    {
        var range = HdlRange.To(-13, -5);
        BigInteger modulus = BigInteger.One << 9;
        BigInteger limit = modulus >> 1;
        foreach (BigInteger value in new[] { -limit, -BigInteger.One, BigInteger.Zero, limit - 1 })
        {
            var actual = LogicArray.FromSigned(value, range);
            Assert.Equal(value, actual.ToSigned());
            Assert.Equal(value & (modulus - 1), actual.ToUnsigned());
            Assert.Equal(range, actual.Range);
        }
    }
    [Fact]
    public void UnsignedOverflow9Bits()
    {
        var range = HdlRange.To(-13, -5);
        BigInteger modulus = BigInteger.One << 9;
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(modulus, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range, wrap: true));
        Assert.Equal(BigInteger.Zero, LogicArray.FromUnsigned(modulus, range, wrap: true).ToUnsigned());
        Assert.Equal(modulus - 1, LogicArray.FromUnsigned(3 * modulus - 1, range, wrap: true).ToUnsigned());
    }
    [Fact]
    public void SignedOverflow9Bits()
    {
        var range = HdlRange.To(-13, -5);
        BigInteger modulus = BigInteger.One << 9;
        BigInteger limit = modulus >> 1;
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(-limit - 1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(limit, range));
        Assert.Equal(limit - 1, LogicArray.FromSigned(-limit - 1, range, wrap: true).ToSigned());
        Assert.Equal(-limit, LogicArray.FromSigned(limit, range, wrap: true).ToSigned());
    }
    [Fact]
    public void Unsigned15Bits()
    {
        var range = HdlRange.To(-13, 1);
        BigInteger modulus = BigInteger.One << 15;
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
    [Fact]
    public void Signed15Bits()
    {
        var range = HdlRange.To(-13, 1);
        BigInteger modulus = BigInteger.One << 15;
        BigInteger limit = modulus >> 1;
        foreach (BigInteger value in new[] { -limit, -BigInteger.One, BigInteger.Zero, limit - 1 })
        {
            var actual = LogicArray.FromSigned(value, range);
            Assert.Equal(value, actual.ToSigned());
            Assert.Equal(value & (modulus - 1), actual.ToUnsigned());
            Assert.Equal(range, actual.Range);
        }
    }
    [Fact]
    public void UnsignedOverflow15Bits()
    {
        var range = HdlRange.To(-13, 1);
        BigInteger modulus = BigInteger.One << 15;
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(modulus, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range, wrap: true));
        Assert.Equal(BigInteger.Zero, LogicArray.FromUnsigned(modulus, range, wrap: true).ToUnsigned());
        Assert.Equal(modulus - 1, LogicArray.FromUnsigned(3 * modulus - 1, range, wrap: true).ToUnsigned());
    }
    [Fact]
    public void SignedOverflow15Bits()
    {
        var range = HdlRange.To(-13, 1);
        BigInteger modulus = BigInteger.One << 15;
        BigInteger limit = modulus >> 1;
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(-limit - 1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(limit, range));
        Assert.Equal(limit - 1, LogicArray.FromSigned(-limit - 1, range, wrap: true).ToSigned());
        Assert.Equal(-limit, LogicArray.FromSigned(limit, range, wrap: true).ToSigned());
    }
    [Fact]
    public void Unsigned16Bits()
    {
        var range = HdlRange.To(-13, 2);
        BigInteger modulus = BigInteger.One << 16;
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
    [Fact]
    public void Signed16Bits()
    {
        var range = HdlRange.To(-13, 2);
        BigInteger modulus = BigInteger.One << 16;
        BigInteger limit = modulus >> 1;
        foreach (BigInteger value in new[] { -limit, -BigInteger.One, BigInteger.Zero, limit - 1 })
        {
            var actual = LogicArray.FromSigned(value, range);
            Assert.Equal(value, actual.ToSigned());
            Assert.Equal(value & (modulus - 1), actual.ToUnsigned());
            Assert.Equal(range, actual.Range);
        }
    }
    [Fact]
    public void UnsignedOverflow16Bits()
    {
        var range = HdlRange.To(-13, 2);
        BigInteger modulus = BigInteger.One << 16;
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(modulus, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range, wrap: true));
        Assert.Equal(BigInteger.Zero, LogicArray.FromUnsigned(modulus, range, wrap: true).ToUnsigned());
        Assert.Equal(modulus - 1, LogicArray.FromUnsigned(3 * modulus - 1, range, wrap: true).ToUnsigned());
    }
    [Fact]
    public void SignedOverflow16Bits()
    {
        var range = HdlRange.To(-13, 2);
        BigInteger modulus = BigInteger.One << 16;
        BigInteger limit = modulus >> 1;
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(-limit - 1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(limit, range));
        Assert.Equal(limit - 1, LogicArray.FromSigned(-limit - 1, range, wrap: true).ToSigned());
        Assert.Equal(-limit, LogicArray.FromSigned(limit, range, wrap: true).ToSigned());
    }
    [Fact]
    public void Unsigned17Bits()
    {
        var range = HdlRange.To(-13, 3);
        BigInteger modulus = BigInteger.One << 17;
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
    [Fact]
    public void Signed17Bits()
    {
        var range = HdlRange.To(-13, 3);
        BigInteger modulus = BigInteger.One << 17;
        BigInteger limit = modulus >> 1;
        foreach (BigInteger value in new[] { -limit, -BigInteger.One, BigInteger.Zero, limit - 1 })
        {
            var actual = LogicArray.FromSigned(value, range);
            Assert.Equal(value, actual.ToSigned());
            Assert.Equal(value & (modulus - 1), actual.ToUnsigned());
            Assert.Equal(range, actual.Range);
        }
    }
    [Fact]
    public void UnsignedOverflow17Bits()
    {
        var range = HdlRange.To(-13, 3);
        BigInteger modulus = BigInteger.One << 17;
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(modulus, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range, wrap: true));
        Assert.Equal(BigInteger.Zero, LogicArray.FromUnsigned(modulus, range, wrap: true).ToUnsigned());
        Assert.Equal(modulus - 1, LogicArray.FromUnsigned(3 * modulus - 1, range, wrap: true).ToUnsigned());
    }
    [Fact]
    public void SignedOverflow17Bits()
    {
        var range = HdlRange.To(-13, 3);
        BigInteger modulus = BigInteger.One << 17;
        BigInteger limit = modulus >> 1;
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(-limit - 1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(limit, range));
        Assert.Equal(limit - 1, LogicArray.FromSigned(-limit - 1, range, wrap: true).ToSigned());
        Assert.Equal(-limit, LogicArray.FromSigned(limit, range, wrap: true).ToSigned());
    }
    [Fact]
    public void Unsigned31Bits()
    {
        var range = HdlRange.To(-13, 17);
        BigInteger modulus = BigInteger.One << 31;
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
    [Fact]
    public void Signed31Bits()
    {
        var range = HdlRange.To(-13, 17);
        BigInteger modulus = BigInteger.One << 31;
        BigInteger limit = modulus >> 1;
        foreach (BigInteger value in new[] { -limit, -BigInteger.One, BigInteger.Zero, limit - 1 })
        {
            var actual = LogicArray.FromSigned(value, range);
            Assert.Equal(value, actual.ToSigned());
            Assert.Equal(value & (modulus - 1), actual.ToUnsigned());
            Assert.Equal(range, actual.Range);
        }
    }
    [Fact]
    public void UnsignedOverflow31Bits()
    {
        var range = HdlRange.To(-13, 17);
        BigInteger modulus = BigInteger.One << 31;
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(modulus, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range, wrap: true));
        Assert.Equal(BigInteger.Zero, LogicArray.FromUnsigned(modulus, range, wrap: true).ToUnsigned());
        Assert.Equal(modulus - 1, LogicArray.FromUnsigned(3 * modulus - 1, range, wrap: true).ToUnsigned());
    }
    [Fact]
    public void SignedOverflow31Bits()
    {
        var range = HdlRange.To(-13, 17);
        BigInteger modulus = BigInteger.One << 31;
        BigInteger limit = modulus >> 1;
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(-limit - 1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(limit, range));
        Assert.Equal(limit - 1, LogicArray.FromSigned(-limit - 1, range, wrap: true).ToSigned());
        Assert.Equal(-limit, LogicArray.FromSigned(limit, range, wrap: true).ToSigned());
    }
    [Fact]
    public void Unsigned32Bits()
    {
        var range = HdlRange.To(-13, 18);
        BigInteger modulus = BigInteger.One << 32;
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
    [Fact]
    public void Signed32Bits()
    {
        var range = HdlRange.To(-13, 18);
        BigInteger modulus = BigInteger.One << 32;
        BigInteger limit = modulus >> 1;
        foreach (BigInteger value in new[] { -limit, -BigInteger.One, BigInteger.Zero, limit - 1 })
        {
            var actual = LogicArray.FromSigned(value, range);
            Assert.Equal(value, actual.ToSigned());
            Assert.Equal(value & (modulus - 1), actual.ToUnsigned());
            Assert.Equal(range, actual.Range);
        }
    }
    [Fact]
    public void UnsignedOverflow32Bits()
    {
        var range = HdlRange.To(-13, 18);
        BigInteger modulus = BigInteger.One << 32;
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(modulus, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range, wrap: true));
        Assert.Equal(BigInteger.Zero, LogicArray.FromUnsigned(modulus, range, wrap: true).ToUnsigned());
        Assert.Equal(modulus - 1, LogicArray.FromUnsigned(3 * modulus - 1, range, wrap: true).ToUnsigned());
    }
    [Fact]
    public void SignedOverflow32Bits()
    {
        var range = HdlRange.To(-13, 18);
        BigInteger modulus = BigInteger.One << 32;
        BigInteger limit = modulus >> 1;
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(-limit - 1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(limit, range));
        Assert.Equal(limit - 1, LogicArray.FromSigned(-limit - 1, range, wrap: true).ToSigned());
        Assert.Equal(-limit, LogicArray.FromSigned(limit, range, wrap: true).ToSigned());
    }
    [Fact]
    public void Unsigned33Bits()
    {
        var range = HdlRange.To(-13, 19);
        BigInteger modulus = BigInteger.One << 33;
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
    [Fact]
    public void Signed33Bits()
    {
        var range = HdlRange.To(-13, 19);
        BigInteger modulus = BigInteger.One << 33;
        BigInteger limit = modulus >> 1;
        foreach (BigInteger value in new[] { -limit, -BigInteger.One, BigInteger.Zero, limit - 1 })
        {
            var actual = LogicArray.FromSigned(value, range);
            Assert.Equal(value, actual.ToSigned());
            Assert.Equal(value & (modulus - 1), actual.ToUnsigned());
            Assert.Equal(range, actual.Range);
        }
    }
    [Fact]
    public void UnsignedOverflow33Bits()
    {
        var range = HdlRange.To(-13, 19);
        BigInteger modulus = BigInteger.One << 33;
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(modulus, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range, wrap: true));
        Assert.Equal(BigInteger.Zero, LogicArray.FromUnsigned(modulus, range, wrap: true).ToUnsigned());
        Assert.Equal(modulus - 1, LogicArray.FromUnsigned(3 * modulus - 1, range, wrap: true).ToUnsigned());
    }
    [Fact]
    public void SignedOverflow33Bits()
    {
        var range = HdlRange.To(-13, 19);
        BigInteger modulus = BigInteger.One << 33;
        BigInteger limit = modulus >> 1;
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(-limit - 1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(limit, range));
        Assert.Equal(limit - 1, LogicArray.FromSigned(-limit - 1, range, wrap: true).ToSigned());
        Assert.Equal(-limit, LogicArray.FromSigned(limit, range, wrap: true).ToSigned());
    }
    [Fact]
    public void Unsigned63Bits()
    {
        var range = HdlRange.To(-13, 49);
        BigInteger modulus = BigInteger.One << 63;
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
    [Fact]
    public void Signed63Bits()
    {
        var range = HdlRange.To(-13, 49);
        BigInteger modulus = BigInteger.One << 63;
        BigInteger limit = modulus >> 1;
        foreach (BigInteger value in new[] { -limit, -BigInteger.One, BigInteger.Zero, limit - 1 })
        {
            var actual = LogicArray.FromSigned(value, range);
            Assert.Equal(value, actual.ToSigned());
            Assert.Equal(value & (modulus - 1), actual.ToUnsigned());
            Assert.Equal(range, actual.Range);
        }
    }
    [Fact]
    public void UnsignedOverflow63Bits()
    {
        var range = HdlRange.To(-13, 49);
        BigInteger modulus = BigInteger.One << 63;
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(modulus, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range, wrap: true));
        Assert.Equal(BigInteger.Zero, LogicArray.FromUnsigned(modulus, range, wrap: true).ToUnsigned());
        Assert.Equal(modulus - 1, LogicArray.FromUnsigned(3 * modulus - 1, range, wrap: true).ToUnsigned());
    }
    [Fact]
    public void SignedOverflow63Bits()
    {
        var range = HdlRange.To(-13, 49);
        BigInteger modulus = BigInteger.One << 63;
        BigInteger limit = modulus >> 1;
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(-limit - 1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(limit, range));
        Assert.Equal(limit - 1, LogicArray.FromSigned(-limit - 1, range, wrap: true).ToSigned());
        Assert.Equal(-limit, LogicArray.FromSigned(limit, range, wrap: true).ToSigned());
    }
    [Fact]
    public void Unsigned64Bits()
    {
        var range = HdlRange.To(-13, 50);
        BigInteger modulus = BigInteger.One << 64;
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
    [Fact]
    public void Signed64Bits()
    {
        var range = HdlRange.To(-13, 50);
        BigInteger modulus = BigInteger.One << 64;
        BigInteger limit = modulus >> 1;
        foreach (BigInteger value in new[] { -limit, -BigInteger.One, BigInteger.Zero, limit - 1 })
        {
            var actual = LogicArray.FromSigned(value, range);
            Assert.Equal(value, actual.ToSigned());
            Assert.Equal(value & (modulus - 1), actual.ToUnsigned());
            Assert.Equal(range, actual.Range);
        }
    }
    [Fact]
    public void UnsignedOverflow64Bits()
    {
        var range = HdlRange.To(-13, 50);
        BigInteger modulus = BigInteger.One << 64;
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(modulus, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromUnsigned(-1, range, wrap: true));
        Assert.Equal(BigInteger.Zero, LogicArray.FromUnsigned(modulus, range, wrap: true).ToUnsigned());
        Assert.Equal(modulus - 1, LogicArray.FromUnsigned(3 * modulus - 1, range, wrap: true).ToUnsigned());
    }
    [Fact]
    public void SignedOverflow64Bits()
    {
        var range = HdlRange.To(-13, 50);
        BigInteger modulus = BigInteger.One << 64;
        BigInteger limit = modulus >> 1;
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(-limit - 1, range));
        Assert.Throws<OverflowException>(() => LogicArray.FromSigned(limit, range));
        Assert.Equal(limit - 1, LogicArray.FromSigned(-limit - 1, range, wrap: true).ToSigned());
        Assert.Equal(-limit, LogicArray.FromSigned(limit, range, wrap: true).ToSigned());
    }
}
