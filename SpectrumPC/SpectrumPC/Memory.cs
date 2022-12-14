using System;
using Speccy.Z80_CPU;

namespace Speccy
{
    public class Memory : IBus16Bit
    {
        private readonly ROM _rom = new ROM();
        private readonly byte[] _ram = new byte[65536];

        public byte ReadByte(int addr)
        {
            byte data = addr < 0x4000 ? _rom.Rom[addr] : _ram[addr - 0x4000];
            return data;
        }

        public void WriteByte(int addr, byte val)
        {
            if (addr >= _rom.Rom.Length && addr< _ram.Length)//ROM
            {
                _ram[addr - _rom.Rom.Length] = val;//RAM
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
