using SpectrumPC.Hardware;
using Xunit;

namespace ZXSharp.Tests;

public class MemoryTests
{
    [Theory]
    [InlineData(0x4000)]
    [InlineData(0x5AFF)]
    [InlineData(0xFFFF)]
    public void RamByte_RoundTrips(int address)
    {
        var memory = new Memory();
        memory.WriteByte(address, 0xA5);
        Assert.Equal((byte)0xA5, memory.ReadByte(address));
    }

    [Fact]
    public void Rom_IsReadOnly()
    {
        var memory = new Memory();
        byte before = memory.ReadByte(0x0000);
        memory.WriteByte(0x0000, (byte)(before ^ 0xFF));
        Assert.Equal(before, memory.ReadByte(0x0000));
    }

    [Fact]
    public void Word_LittleEndianInRam()
    {
        var memory = new Memory();
        memory.WriteWord(0x8000, 0x1234);
        Assert.Equal((byte)0x34, memory.ReadByte(0x8000));
        Assert.Equal((byte)0x12, memory.ReadByte(0x8001));
        Assert.Equal((ushort)0x1234, memory.ReadWord(0x8000));
    }

    [Fact]
    public void Word_ReadAtFFFF_WrapsTo0000()
    {
        var memory = new Memory();
        memory.WriteByte(0xFFFF, 0x34);
        byte romFirstByte = memory.ReadByte(0x0000);
        Assert.Equal((ushort)(0x34 | (romFirstByte << 8)), memory.ReadWord(0xFFFF));
    }

    [Fact]
    public void Word_WriteAtFFFF_WrapsAndPreservesRom()
    {
        var memory = new Memory();
        byte romFirstByte = memory.ReadByte(0x0000);
        memory.WriteWord(0xFFFF, 0x1234);
        Assert.Equal((byte)0x34, memory.ReadByte(0xFFFF));
        Assert.Equal(romFirstByte, memory.ReadByte(0x0000));
    }
}
