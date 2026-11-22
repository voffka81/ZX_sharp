using SpectrumPC.Filetypes;
using SpectrumPC.Hardware;
using Xunit;

namespace ZXSharp.Tests;

public class TapTests
{
    // TAP blocks are prefixed with their length in little-endian form.
    private static readonly byte[] OneBlock = [0x02, 0x00, 0x00, 0xFF];

    [Fact]
    public void Computer_LoadProgram_MountsTapeWithoutAutoPlay()
    {
        var computer = new Computer();
        computer.LoadProgram("demo.TAP", OneBlock);
        Assert.Single(computer.TapeDevice.tf.Blocks);
        Assert.False(computer.TapeDevice.IsPlaying);
        computer.TapeDevice.Play();
        Assert.True(computer.TapeDevice.IsPlaying);
    }

    [Fact]
    public void Computer_LoadProgram_RejectsUnsupportedFile()
    {
        var computer = new Computer();
        Assert.Throws<NotSupportedException>(() => computer.LoadProgram("demo.sna", OneBlock));
    }

    [Fact]
    public void Parse_OneTapBlock()
    {
        var tap = new TapFile();
        tap.ReadFile(OneBlock);
        Assert.Single(tap.Blocks);
        Assert.Equal(new byte[] { 0x00, 0xFF }, tap.Blocks[0].Data);
    }

    [Fact]
    public void ReloadTapFile_ReplacesPreviousBlocks()
    {
        var tap = new TapFile();
        tap.ReadFile(OneBlock);
        tap.ReadFile(OneBlock);
        Assert.Single(tap.Blocks);
    }

    [Fact]
    public void ReloadTapePlayer_ReplacesPreviousPulseTimeline()
    {
        var player = new TapePlayer();
        player.LoadTape(OneBlock);
        int initialCount = player.EarValues.Count;
        Assert.True(initialCount > 0);
        player.LoadTape(OneBlock);
        Assert.Equal(initialCount, player.EarValues.Count);
    }
}
