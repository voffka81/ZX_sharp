using Speccy.Filetypes;
using Speccy.Z80_CPU;
using SpectrumPC.Filetypes.ZXBox.Core.Hardware.Input;

namespace Speccy
{

    public class Computer
    {
        protected bool _frameCompleted;

        private const int PixelRamStart = 0x4000;
        private const int PixelRamEnd = 0x5800;
        private const int AttributeRamEnd = 0x5B00;

        public Display DisplayUnit => _displayUnit;
        private readonly Display _displayUnit;

        private readonly Memory _ram = new Memory();
        private readonly Z80CPU _z80;
        private readonly IPorts _IOdataBus;
        private readonly Beeper _beeperDevice;
        private readonly Kempston _joystik;
        private readonly TapePlayer _tapeDevice;

        public Status CpuDebugInfo => _z80.DebugInfo;

        public bool ComputerRunning;
        public Kempston Joystik => _joystik;
        public TapePlayer TapeDevice => _tapeDevice;

        public float[] AudioSamples { get; private set; }

        public Computer()
        {
            ComputerRunning = true;
            _beeperDevice = new Beeper();
            _joystik = new Kempston();

            _tapeDevice = new TapePlayer();

            _displayUnit = new Display(_ram);
            _IOdataBus = new Bus16Bit(_beeperDevice, _joystik, _tapeDevice);

            _z80 = new Z80CPU(_ram, _IOdataBus);
            _z80.Reset();

            TestVideoBuffer();
        }

        private void TestVideoBuffer()
        {
            var rand = new Random();
            for (var index = PixelRamStart; index < PixelRamEnd; ++index)
                _ram.WriteByte(index, (byte)rand.Next(255));
            for (var index = PixelRamEnd; index < AttributeRamEnd; ++index)
                _ram.WriteByte(index, (byte)rand.Next(255));
        }

        int _flashCount = 0;
       
        public void ExecuteCycle()
        {
            _displayUnit.BorderColor = (_IOdataBus as Bus16Bit).BorderColor;
            while (_z80.TicksCount < _z80.NextEvent)
            {
                _beeperDevice.CpuTacts = _z80.TicksCount;
                _z80.Cycle();
                _tapeDevice.AddTStates(_z80.TStateValue);
            }

            _z80.ResetTStates();
            _z80.Interrupt();

            _flashCount++;
            if (_flashCount >= 50)
            {

                _flashCount = 0;
                _displayUnit.ReverseFlash();

            }
            _beeperDevice.FinalizeFrame(); // Fill remaining audio samples for the frame
            AudioSamples = _beeperDevice.AudioSamples;
        }


        public void KeyInput(SpectrumKeyCode key, bool isPressed)
        {
            if (key == SpectrumKeyCode.Invalid)
                return;
            var lineIndex = (byte)key / 5;
            var lineMask = 1 << (byte)key % 5;
            (_IOdataBus as Bus16Bit).keyLine[lineIndex] = isPressed
                ? (byte)((_IOdataBus as Bus16Bit).keyLine[lineIndex] | lineMask)
                : (byte)((_IOdataBus as Bus16Bit).keyLine[lineIndex] & ~lineMask);
        }

        public void TapeInput(string tapePath)
        {
            if (tapePath.ToLower().EndsWith(".tap"))
            {
                using (MemoryStream ms = new())
                using (FileStream file = new(tapePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] bytes = new byte[file.Length];
                    file.Read(bytes, 0, (int)file.Length);
                    ms.Write(bytes, 0, (int)file.Length);
                    _tapeDevice.LoadTape(ms.ToArray());
                }
            }
            else
            {
                var z80snap = Z80File.LoadZ80(tapePath);
                _displayUnit.BorderColor = z80snap.BORDER;
                _z80.ApplyZ80Snapshot(z80snap);
            }

        }

        public void Reset()
        {
            _beeperDevice.Reset();
            _z80.Reset();
        }
    }
}

