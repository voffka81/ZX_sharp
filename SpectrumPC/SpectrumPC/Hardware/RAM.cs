namespace SpectrumPC.Hardware
{
    internal class RAM
    {
        private readonly ROM _rom = new();

        private readonly byte[] _ram = new byte[65536];

        public byte Read(int addr)
        {
            byte data = 0;
            data = addr < 0x4000 ? _rom.Rom[addr] : _ram[addr - 0x4000];

            return data;
        }

        public void Write(int addr, byte val)
        {
            if (addr >= 0x4000)//ROM
            {
                _ram[addr - 0x4000] = val;//RAM
            }
        }
    }
}
