namespace SpectrumPC.Hardware
{
    using System;

    public class Beeper
    {
        // ZX Spectrum: 69888 T-states per 50Hz frame
        private const int FrameTStates = 69888;
        public const int FrameRate = 50;
        public const int SamplesPerFrame = 882; // 882 samples * 50 fps = 44.1 kHz output
        public const int SampleRate = FrameRate * SamplesPerFrame;
        private const double TStatesPerSample = (double)FrameTStates / SamplesPerFrame;

        public float[] AudioSamples { get; private set; } = Array.Empty<float>();
        public bool LastEarBit { get; private set; } // Combined speaker OR tape bit
        public long CpuTacts { get; private set; }
        public int NextSampleIndex { get; private set; }
        private double _nextSampleThreshold;

        private bool _speakerBit;
        private bool _tapeEarBit;
        private float _dcLevel;

        public Beeper()
        {
            Initialize();
        }

        public void Initialize()
        {
            _nextSampleThreshold = 0.0;
            NextSampleIndex = 0;
            LastEarBit = false;
            _speakerBit = false;
            _tapeEarBit = false;
            _dcLevel = 0f;
            AudioSamples = new float[SamplesPerFrame];
            CpuTacts = 0;
        }

        public void ProcessEarBitValue(bool fromTape, bool earBit)
        {
            if (fromTape)
            {
                _tapeEarBit = earBit;
            }
            else
            {
                _speakerBit = earBit;
            }
            bool combined = _speakerBit || _tapeEarBit;
            if (combined == LastEarBit) return; // No change

            // Fill samples up to this point with the previous state
            GenerateSamplesUpTo(CpuTacts);
            LastEarBit = combined;
        }


        public void Reset()
        {

            Initialize();
        }

        private void GenerateSamplesUpTo(long currentCpuTacts)
        {
            if (currentCpuTacts < 0) return;

            while (_nextSampleThreshold <= currentCpuTacts && NextSampleIndex < SamplesPerFrame)
            {
                AudioSamples[NextSampleIndex++] = LastEarBit ? 1.0f : -1.0f;
                _nextSampleThreshold += TStatesPerSample;
            }
        }

        public void SetCpuTacts(long tstates)
        {
            CpuTacts = tstates;
            GenerateSamplesUpTo(CpuTacts);
        }

        public void FinalizeFrame(long finalCpuTacts)
        {
            // Ensure we fill any remaining samples for the frame duration
            GenerateSamplesUpTo(FrameTStates);
            while (NextSampleIndex < SamplesPerFrame)
            {
                AudioSamples[NextSampleIndex++] = LastEarBit ? 1.0f : -1.0f;
            }

            // Apply simple high-pass filter to reduce DC and tame amplitude
            for (var i = 0; i < SamplesPerFrame; i++)
            {
                _dcLevel = 0.995f * _dcLevel + 0.005f * AudioSamples[i];
                AudioSamples[i] = (AudioSamples[i] - _dcLevel) * 0.5f;
            }

            // Prepare next frame
            _nextSampleThreshold -= FrameTStates;
            if (_nextSampleThreshold < 0) _nextSampleThreshold = 0.0;
            NextSampleIndex = 0;
            CpuTacts = 0;
        }
    }
}
