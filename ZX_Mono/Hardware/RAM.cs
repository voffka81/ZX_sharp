namespace ZX_sharp.Hardware
{
    public class RAM
    {
        private ROM _rom = new ROM();

        private byte[] _ram = new byte[65536];


        public int Border { get; set; }

        public byte Read(int addr)
        {
            byte data = 0;
            if (addr < 0x4000)//ROM
            {
                data = _rom.Rom[addr];
            }
            else if (addr < 0x5800)//scr_picsel_RAM
            {
                data = _ram[addr - 0x4000];//RAM
            }
            else if (addr < 0x5B00)//scr_atr_RAM
            {
                data = _ram[addr - 0x4000];
            }
            else
            {
                data = _ram[addr - 0x4000];//RAM
            }
            return data;
        }

        public void Write(int addr, byte val)
        {
            if (addr < 0x4000)//ROM
            {
            }
            else if (addr < 0x5800)//scr_picsel_RAM
            {
               _ram[addr - 0x4000] = val;
            }
            else if (addr < 0x5B00)//scr_atr_RAM
            {
                _ram[addr - 0x4000] = val;
            }
            else
            {
                _ram[addr - 0x4000] = val;//RAM
            }
        }

    }
}
