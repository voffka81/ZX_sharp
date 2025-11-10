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

        public bool TapeLoading = false;

        private Beeper _beeper;
        private Kempston _joystick;
        private TapePlayer _tapeDevice;

        private readonly Display _display;

        public Bus16Bit(Beeper beeper, Kempston joystick, TapePlayer tapeDevice, Display display)
        {
            _beeper = beeper;
            _joystick = joystick;
            _tapeDevice = tapeDevice;
            _display = display;
        }

        public byte ReadByte(int port)
        {
            int result = 0xFF;

            byte line = (byte)(port >> 8);
            if ((port & 0xFF) == 0xFE)
            {
                result = GetKeyboardLineStatus(line);

                // Default the EAR bit high (no signal) and override below when the tape plays
                result |= TAPE_BIT;

                if (_tapeDevice.IsPlaying)
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
                    _beeper.ProcessEarBitValue(true, ear.Ear); // tape EAR influences audio during load
                    if (ear != null)
                    {
                        if (ear.Pulse == PulseTypeEnum.Stop)
                        {
                            _tapeDevice.IsPlaying = false;
                            firstread = true;
                            tapeposition = 0;
                        }
                        else
                        {
                            if (ear.Ear)
                                result |= TAPE_BIT;
                            else
                                result &= ~TAPE_BIT;
                        }
                    }

                    if (_tapeDevice.CurrentTstate > _tapeDevice.TotalTstates)
                    {
                        firstread = true;
                        _tapeDevice.IsPlaying = false;
                        tapeposition = 0;
                    }
                }
            }

            if ((port & 0xff) == 0x1f)
            {
                result &= _joystick.GetJoystikState(port);
            }


            return (byte)result;
        }

        public long TotalTstates = 0;

        EarValue ear;
        bool firstread = true;
        int tapeposition = 0;
        private byte TAPE_BIT = 0x40;
        private bool _lastTapeEarBit = false;

        // Advance tape EAR state based on current tape T-states, independent of port reads
        public void AdvanceTapeEar()
        {
            if (!_tapeDevice.IsPlaying) return;

            if (firstread)
            {
                tapeposition = 0;
                _tapeDevice.CurrentTstate = 0;
                firstread = false;
            }

            // Move forward through pulse list as time advances
            for (; tapeposition < _tapeDevice.EarValues.Count - 1;)
            {
                if (_tapeDevice.EarValues[tapeposition + 1].TState < _tapeDevice.CurrentTstate)
                {
                    tapeposition++;
                }
                else break;
            }

            ear = _tapeDevice.EarValues[tapeposition];
            if (ear != null)
            {
                if (ear.Pulse == PulseTypeEnum.Stop)
                {
                    _tapeDevice.IsPlaying = false;
                    firstread = true;
                    tapeposition = 0;
                }
                else
                {
                    // Generate audio on ear edge changes
                    if (ear.Ear != _lastTapeEarBit)
                    {
                        _beeper.ProcessEarBitValue(true, ear.Ear);
                        _lastTapeEarBit = ear.Ear;
                    }
                }
            }

            if (_tapeDevice.CurrentTstate > _tapeDevice.TotalTstates)
            {
                firstread = true;
                _tapeDevice.IsPlaying = false;
                tapeposition = 0;
            }
        }

        public void WriteByte(int address, byte data)
        {
            // Only even addresses address the ULA
            if ((address & 0x0001) == 0)
            {
                // border
                BorderColor = (byte)(data & 0x07);
                _display.RecordBorderChange(_beeper.CpuTacts, BorderColor);

                // sound
                _beeper.ProcessEarBitValue(false, (data & 0x10) != 0); // speaker bit

                // tape
                //_beeper.ProcessEarBitValue(false, (data & 0x08) != 0);
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
