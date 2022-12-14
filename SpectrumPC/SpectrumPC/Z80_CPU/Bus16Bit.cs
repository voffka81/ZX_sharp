namespace Speccy.Z80_CPU
{
    public class Bus16Bit : IPorts
    {
        public byte BorderColor = 0x0;

        private byte TAPE_BIT = 0x40;

        public byte[] keyLine = new byte[8];
        public byte[] io = new byte[255];

        public byte GetKeyboardLineStatus(byte lines)
        {
            byte status = 0;
            lines = (byte)~lines;

            var lineIndex = 0;
            while (lines > 0)
            {
                if ((lines & 0x01) != 0)
                {
                    status |= keyLine[lineIndex];
                }
                lineIndex++;
                lines >>= 1;
            }
            return (byte)~status;
        }

        int pulseLevel = 0;
        public bool TapeLoading = false;

        private Beeper _beeper;
        public Bus16Bit(Beeper beeper)
        {
            _beeper = beeper;
        }

        public byte ReadByte(int address)
        {
            if ((address & 0x0001) != 0)
                return 0xFF;

            byte line = (byte)(address >> 8);
            int result = GetKeyboardLineStatus(line);

            if (TapeLoading)
            {
                if (pulseLevel == 0)
                {
                    pulseLevel = 1;
                    result &= ~(TAPE_BIT);    //reset is EAR off
                }
                else
                {
                    pulseLevel = 0;
                    result |= (TAPE_BIT); //set is EAR on
                }
            }
            var data = (byte)(result & 191);
            return data;
        }

        public void WriteByte(int address, byte data)
        {
            // Only even addresses address the ULA
            if ((address & 0x0001) == 0)
            {

                // border
                BorderColor = (byte)(data & 0x07);

                // sound
                _beeper.ProcessEarBitValue(false, (data & 0x10) != 0);

                // tape
                //_tapeDevice.ProcessMicBit((data & 0x08) != 0);
            }
        }

        public ushort ReadWord(int address)
        {
            return (ushort)(io[address % 256] * 256 + io[++address % 256]);
        }

        public void WriteWord(int address, ushort data)
        {
            io[address % 256] = (byte)(data / 256);
            io[++address % 256] = (byte)(data % 256);
        }
    }
}
