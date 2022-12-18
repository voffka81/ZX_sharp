namespace Speccy
{
    public class Beeper
    {
        public float[] AudioSamples { get; private set; }
        public bool LastEarBit { get; set; }
        public long cpuTacts;
        public int NextSampleIndex { get; private set; }
        int LastSampleTact = 0;
        private long _frameBegins;
        int _frameTacts = 699;
        int _tactsPerSample = 100;

        public Beeper()
        {
            Initialize();

        }

        public void Initialize()
        {
            LastSampleTact = 0;
            NextSampleIndex = 0;
            AudioSamples = new float[_frameTacts];
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


        public void Reset()
        {
            _frameBegins = cpuTacts;
            LastEarBit = false;
            Initialize();
        }

        private void CreateSamples()
        {
            var nextSampleOffset = LastSampleTact;

            if (cpuTacts > _frameBegins - _frameTacts)
            {
                cpuTacts = _frameBegins - _frameTacts;
            }

            while (nextSampleOffset < cpuTacts)
            {
                AudioSamples[NextSampleIndex++] = LastEarBit ? 1.0f : 0;
                nextSampleOffset += _tactsPerSample;
            }
            LastSampleTact = nextSampleOffset;
        }
    }
}
