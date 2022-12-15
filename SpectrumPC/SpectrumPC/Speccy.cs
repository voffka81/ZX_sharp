using Speccy.Filetypes;
using Speccy.Z80_CPU;

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
        public readonly Kempston _joystik;

        public bool ComputerRunning;

        public float[] AudioSamples { get; private set; }

        public Computer()
        {
            ComputerRunning = true;
            _beeperDevice = new Beeper();
            _joystik = new Kempston();

            _displayUnit = new Display(_ram);
            _IOdataBus = new Bus16Bit(_beeperDevice, _joystik);

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
            _beeperDevice.Reset();
            while (_z80.tstates < _z80.event_next_event)
            {
                _beeperDevice.cpuTacts = _z80.tstates;
                _z80.Cycle();
            }

            _z80.tstates -= _z80.event_next_event;
            _z80.Interrupt();

            _flashCount++;
            if (_flashCount >= 50)
            {

                _flashCount = 0;
                _displayUnit.ReverseFlash();

            }

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
            var z80snap = Z80File.LoadZ80(tapePath);
            _displayUnit.BorderColor = z80snap.BORDER;
            _z80.ApplyZ80Snapshot(z80snap);

        }

        public void Reset()
        {
            _beeperDevice.Reset();
            _z80.Reset();
        }
    }
}

