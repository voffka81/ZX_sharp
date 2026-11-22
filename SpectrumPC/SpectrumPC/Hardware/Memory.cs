using System;
using Speccy.Z80_CPU;

namespace SpectrumPC.Hardware
{
    public class Memory : IBus16Bit
    {
        private readonly ROM _rom = new ROM();
        private readonly byte[] _ram = new byte[65536];

        public byte ReadByte(int addr)
        {
            // Z80 addresses are 16-bit, including when an instruction crosses 0xFFFF.
            addr &= 0xFFFF;
            return addr < _rom.Rom.Length ? _rom.Rom[addr] : _ram[addr - _rom.Rom.Length];
        }

        public void WriteByte(int addr, byte val)
        {
            addr &= 0xFFFF;
            if (addr >= _rom.Rom.Length)
            {
                _ram[addr - _rom.Rom.Length] = val;
            }
        }

        private LEWord ReadMemoryWord(int address)
        {
            return new LEWord(ReadByte(address), ReadByte(address + 1));
        }

        private void WriteMemoryWord(int address, LEWord data)
        {
            WriteByte(address, data.Low);
            WriteByte(address + 1, data.High);
        }

        public ushort ReadWord(int address)
        {
            return ReadMemoryWord(address);
        }

        public void WriteWord(int address, ushort data)
        {
            WriteMemoryWord(address, data);
        }
    }
}
