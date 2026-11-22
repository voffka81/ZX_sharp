using Speccy.Z80_CPU;
using Xunit;

namespace ZXSharp.Tests;

public class RegistersTests
{
    [Theory]
    [InlineData(0x0000)]
    [InlineData(0x1234)]
    [InlineData(0xFFFF)]
    public void Word_SetterAndGetter_RoundTrip(ushort value)
    {
        var register = new Register { w = value };
        Assert.Equal((byte)(value >> 8), register.h.Value);
        Assert.Equal((byte)(value & 0xFF), register.l.Value);
        Assert.Equal(value, register.w);
    }

    [Fact]
    public void Swap_ExchangesBothHalves()
    {
        var first = new Register { w = 0x1234 };
        var second = new Register { w = 0xABCD };
        first.Swap(second);
        Assert.Equal((ushort)0xABCD, first.w);
        Assert.Equal((ushort)0x1234, second.w);
    }
}
