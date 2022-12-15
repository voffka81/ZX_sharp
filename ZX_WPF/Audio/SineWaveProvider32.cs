using NAudio.Wave;
using Speccy;
using System;

namespace ZX_WPF.Audio
{
    public class SineWaveProvider32 : WaveProvider32
    {
        int sample;
        Computer _speccy;
        public SineWaveProvider32(Computer speccy)
        {
            Frequency = 440;
            Amplitude = 0.25f; // let's not hurt our ears
            _speccy = speccy;
        }

        public float Frequency { get; set; }
        public float Amplitude { get; set; }

        public override int Read(float[] buffer, int offset, int sampleCount)
        {
            int sampleRate = WaveFormat.SampleRate;
            //for (int n = 0; n < sampleCount; n++)
            //{
            //    buffer[n + offset] = (float)(Amplitude * Math.Sin((2 * Math.PI * sample * Frequency) / sampleRate));

            //    sample++;
            //    if (sample >= sampleRate) sample = 0;
            //}
            for (int n = 0; n < _speccy.AudioSamples?.Length; n++)
            {
                //if (_speccy.AudioSamples[n] > 0)
                //{
                buffer[n + offset] = (float)(Amplitude * Math.Sin((2 * _speccy.AudioSamples[n] * sample * Frequency) / sampleRate));
                //}
                //else
                //{
                //    buffer[n + offset] = 0;
                //}
                sample++;
                if (sample >= sampleRate) sample = 0;
            }
            return sampleCount;
        }

        void findFrequencies(byte[] ele, int n)
        {
            int freq = 1;
            int idx = 1;
            int element = ele[0];
            while (idx < n)
            {

                if (ele[idx - 1] == ele[idx])
                {
                    freq++;
                    idx++;
                }
                else
                {
                    element = ele[idx];
                    idx++;
                    freq = 1;
                }
            }

        }
    }

}
