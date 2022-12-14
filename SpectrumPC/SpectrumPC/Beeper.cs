namespace Speccy
{
    public class Beeper
    {
        public byte[] AudioSamples { get; private set; }
        public bool LastEarBit { get; set; }
        public long cpuTacts;
        public int NextSampleIndex { get; private set; }

        public Beeper()
        {
            Initialize();
        }

        public void Initialize()
        {
            LastSampleTact = 0;
            NextSampleIndex = 0;
            AudioSamples = new byte[1250];
        }

        public void ProcessEarBitValue(bool fromTape, bool earBit)
        {
            //if (!fromTape && _useTapeMode)
            //{
            //    // --- The EAR bit comes from and OUT instruction, but now we're in tape mode
            //    return;
            //}
            if (earBit == LastEarBit)
            {
                // --- The earbit has not changed
                return;
            }
            LastEarBit = earBit;
            CreateSamples();

        }
        int LastSampleTact = 0;
        private long _frameBegins;
        int _frameTacts = 1250;
        int _tactsPerSample = 80;

        public void Reset()
        {
            _frameBegins = cpuTacts;
            LastEarBit = false;
            Initialize();
        }

        private void CreateSamples()
        {
            var nextSampleOffset = LastSampleTact;
            if (cpuTacts > _frameBegins + _frameTacts)
            {
                cpuTacts = _frameBegins + _frameTacts;
            }
            while (nextSampleOffset < cpuTacts)
            {
                AudioSamples[NextSampleIndex++] = LastEarBit ? (byte)0xFF : (byte)0;
                nextSampleOffset += _tactsPerSample;
            }
            LastSampleTact = nextSampleOffset;
        }
    }
}
