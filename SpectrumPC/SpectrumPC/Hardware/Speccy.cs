using Speccy.Filetypes;
using Speccy.Z80_CPU;
using SpectrumPC.Z80_CPU;

namespace SpectrumPC.Hardware
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
            AudioSamples = Array.Empty<float>();
            _beeperDevice = new Beeper();
            _joystik = new Kempston();
            _tapeDevice = new TapePlayer();
            _displayUnit = new Display(_ram);
            _IOdataBus = new Bus16Bit(_beeperDevice, _joystik, _tapeDevice, _displayUnit);
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
            var bus = _IOdataBus as Bus16Bit;
            if (bus != null)
            {
                _displayUnit.BeginFrame(bus.BorderColor);
                _displayUnit.BorderColor = bus.BorderColor;
            }
            var busForTape = _IOdataBus as Bus16Bit;
            while (_z80.TicksCount < _z80.NextEvent)
            {
                _z80.Cycle();
                _beeperDevice.SetCpuTacts(_z80.TicksCount); // update after cycle so TicksCount advanced
                _tapeDevice.AddTStates(_z80.TStateValue);
                busForTape?.AdvanceTapeEar();
            }

            // Finalize audio for this frame before resetting CPU t-states
            if (_z80.TicksCount >= _z80.NextEvent)
            {
                _beeperDevice.FinalizeFrame(_z80.TicksCount);
                AudioSamples = _beeperDevice.AudioSamples;
                _z80.ResetTStates();
            }
            _z80.Interrupt();

            _flashCount++;
            if (_flashCount >= 50)
            {

                _flashCount = 0;
                _displayUnit.ReverseFlash();

            }
            // Audio samples already finalized above
        }


        public void KeyInput(SpectrumKeyCode key, bool isPressed)
        {
            if (key == SpectrumKeyCode.Invalid)
                return;
            var bus = _IOdataBus as Bus16Bit;
            if (bus == null) return;
            var lineIndex = (byte)key / 5;
            var lineMask = 1 << (byte)key % 5;
            bus.keyLine[lineIndex] = isPressed
                ? (byte)(bus.keyLine[lineIndex] | lineMask)
                : (byte)(bus.keyLine[lineIndex] & ~lineMask);
        }

        public void TapeInput(string tapePath)
        {
            if (tapePath.ToLower().EndsWith(".tap"))
            {
                using (MemoryStream ms = new())
                using (FileStream file = new(tapePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] bytes = new byte[file.Length];
                    file.ReadExactly(bytes, 0, (int)file.Length);
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

