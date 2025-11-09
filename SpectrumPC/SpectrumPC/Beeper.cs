namespace Speccy
{
    public class Beeper
    {
        public float[] AudioSamples { get; private set; }
        public bool LastEarBit { get; set; }
        public long CpuTacts { get; set; }
        public int NextSampleIndex { get; private set; }
        int LastSampleTact = 0;
        private long _frameBegins;
        int _frameTacts = 700; //700 samples per frame for35000Hz/50Hz
        int _tactsPerSample = 99; //69888 /700 ≈99.84, so99 is closer

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

            Initialize();
        }

        private void CreateSamples()
        {
            var nextSampleOffset = LastSampleTact;

            // Work with a snapshot of cpuTacts to avoid races
            long currentCpuTacts = CpuTacts;

            // Ensure cpuTacts does not exceed the frame end
            if (currentCpuTacts > _frameBegins + _frameTacts)
            {
                currentCpuTacts = _frameBegins + _frameTacts;
            }

            // Fill samples until we catch up or until buffer is full
            while (nextSampleOffset < currentCpuTacts && NextSampleIndex < AudioSamples.Length)
            {
                AudioSamples[NextSampleIndex++] = LastEarBit ? 1.0f : 0;
                nextSampleOffset += _tactsPerSample;
            }

            // Clamp NextSampleIndex to valid range
            if (NextSampleIndex >= AudioSamples.Length)
            {
                NextSampleIndex = AudioSamples.Length - 1;
            }

            LastSampleTact = nextSampleOffset;
        }

        // --- Added: Fill remaining samples at the end of the frame
        public void FinalizeFrame()
        {
            while (NextSampleIndex < AudioSamples.Length)
            {
                AudioSamples[NextSampleIndex++] = LastEarBit ? 1.0f : 0f;
            }
            LastSampleTact = 0;
            NextSampleIndex = 0;
            _frameBegins = CpuTacts;
        }
    }
}
