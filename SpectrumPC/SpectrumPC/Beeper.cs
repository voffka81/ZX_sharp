using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Speccy
{
    public class Beeper
    {
        public float[] AudioSamples { get; private set; }
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
            AudioSamples = new float[100];
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
        int _frameTacts = 1000;
        int _tactsPerSample = 100;

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
                AudioSamples[NextSampleIndex++] = LastEarBit ? 1.0f : 0.0f;
                nextSampleOffset += _tactsPerSample;
            }
            LastSampleTact = nextSampleOffset;
        }
    }
}
