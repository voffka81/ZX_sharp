using SpectrumPC.Hardware;
using Speccy.Z80_CPU;
using Xunit;

namespace ZXSharp.Tests;

public class KeyboardTests
{
    [Fact]
    public void UnpressedMatrix_ReadsAllKeysReleased()
    {
        var bus = CreateBus();
        Assert.Equal((byte)0xFF, bus.ReadByte(0xFEFE));
    }

    [Fact]
    public void PressedKey_ClearsItsActiveLowMatrixBit()
    {
        var bus = CreateBus();
        bus.keyLine[0] = 0x01; // CAPS SHIFT, first key in row 0
        Assert.Equal((byte)0xFE, bus.ReadByte(0xFEFE));
        Assert.Equal((byte)0xFF, bus.ReadByte(0xFDFE)); // Other row unaffected
    }

    [Fact]
    public void SelectingMultipleRows_CombinesPressedKeys()
    {
        var bus = CreateBus();
        bus.keyLine[0] = 0x01;
        bus.keyLine[1] = 0x02;
        Assert.Equal((byte)0xFC, bus.ReadByte(0xFCFE));
    }

    private static Bus16Bit CreateBus()
    {
        var memory = new Memory();
        return new Bus16Bit(new Beeper(), new Kempston(), new TapePlayer(), new Display(memory));
    }
}
