using System;

namespace Speccy.Z80_CPU
{
    public interface IBus16Bit
    {
        byte ReadByte(int address);
        void WriteByte(int address, byte data);

        ushort ReadWord(int address);
        void WriteWord(int address, ushort data);
    }
}
