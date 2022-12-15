using SpectrumPC.Filetypes.ZXBox.Core.Hardware.Input;
using static SpectrumPC.Filetypes.ZXBox.Core.Hardware.Input.TapePlayer;

namespace Speccy.Z80_CPU
{
    public class Bus16Bit : IPorts
    {
        public byte BorderColor = 0x0;


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
        private Kempston _joystick;
        private TapePlayer _tapeDevice;

        public Bus16Bit(Beeper beeper, Kempston joystick, TapePlayer tapeDevice)
        {
            _beeper = beeper;
            _joystick = joystick;
            _tapeDevice = tapeDevice;
        }

        public byte ReadByte(int port)
        {
            int result = 0xFF;

            byte line = (byte)(port >> 8);
            if ((port & 0xFF) == 0xFE)
            {
                result = GetKeyboardLineStatus(line);
            }

            if ((port & 0xff) == 0x1f)
            {
                result &= _joystick.GetJoystikState(port);
            }

            if (_tapeDevice.IsPlaying)
            {
                if ((port & 0xff) == 0xfe)
                {
                    if (firstread)
                    {
                        tapeposition = 0;
                        _tapeDevice.CurrentTstate = 0;
                        firstread = false;
                    }
                    for (; tapeposition < _tapeDevice.EarValues.Count - 1;)
                    {
                        if (_tapeDevice.EarValues[tapeposition + 1].TState < _tapeDevice.CurrentTstate)
                        {
                            tapeposition++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    ear = _tapeDevice.EarValues[tapeposition];
                    _beeper.ProcessEarBitValue(true, ear.Ear);
                    if (ear != null)
                    {
                        if (ear.Pulse == PulseTypeEnum.Stop)
                        {
                            _tapeDevice.IsPlaying = false;
                        }
                        if (ear.Ear)
                            result |= 1 << 6;
                        else
                            result &= ~(1 << 6);
                    }

                    if (_tapeDevice.CurrentTstate > _tapeDevice.TotalTstates)
                    {
                        firstread = true;
                        _tapeDevice.IsPlaying = false;
                    }
                }
            }
            var data = (byte)(result & 191);
            return data;
        }

        public long TotalTstates = 0;

        int returnvalue = 0xff;
        EarValue ear;
        bool firstread = true;
        int tapeposition = 0;
        private byte TAPE_BIT = 0x40;
        private int LoadTape()
        {
            //if (pulseLevel == 0)
            //{
            //    pulseLevel = 1;
            //    result &= ~(TAPE_BIT);    //reset is EAR off
            //}
            //else
            //{
            //    pulseLevel = 0;
            //    result |= (TAPE_BIT); //set is EAR on
            //}
            if (firstread)
            {
                _tapeDevice.CurrentTstate = 0;
                firstread = false;
            }
            for (; tapeposition < _tapeDevice.EarValues.Count - 1;)
            {
                if (_tapeDevice.EarValues[tapeposition + 1].TState < _tapeDevice.CurrentTstate)
                {
                    tapeposition++;
                }
                else
                {
                    break;
                }
            }

            ear = _tapeDevice.EarValues[tapeposition];
            _beeper.ProcessEarBitValue(true, (((ear.Ear ? 1 : 0) << 4) & 0x10) != 0);
            if (ear != null)
            {
                if (ear.Pulse == PulseTypeEnum.Stop)
                {
                    _tapeDevice.IsPlaying = false;
                }
                if (ear.Ear)
                    returnvalue &= ~(TAPE_BIT);
                //return returnvalue |= 1 << 6;
                else
                    return returnvalue |= (TAPE_BIT);
                return returnvalue;
            }

            if (_tapeDevice.CurrentTstate > TotalTstates)
            {
                _tapeDevice.IsPlaying = false;
            }

            return returnvalue;
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
                _beeper.ProcessEarBitValue(false, (data & 0x08) != 0);
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
