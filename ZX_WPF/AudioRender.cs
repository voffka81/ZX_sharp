//using Microsoft.Xna.Framework;
//using Microsoft.Xna.Framework.Audio;
using System;

namespace ZX_sharp
{
    public class AudioRender
    {
        private const int SampleRate = 44100;
        private const int ChannelsCount = 2;
        // private DynamicSoundEffectInstance _instance;

        public const int SamplesPerBuffer = 1000;
        private float[,] _workingBuffer;
        private byte[] _xnaBuffer;

        // public int PendingBufferCount => _instance.PendingBufferCount;

        public AudioRender()
        {
            _workingBuffer = new float[ChannelsCount, SamplesPerBuffer];
            const int bytesPerSample = 2;
            _xnaBuffer = new byte[ChannelsCount * SamplesPerBuffer * bytesPerSample];

            // On LoadContent
            // _instance = new DynamicSoundEffectInstance(SampleRate, (ChannelsCount == 2) ? AudioChannels.Stereo : AudioChannels.Mono);
            //  _instance.Play();
        }

        public void SubmitBuffer(float[] buffer)
        {
            // if (_instance.PendingBufferCount > 3 || buffer == null) return;
            FillWorkingBuffer(buffer);
            ConvertBuffer(_workingBuffer, _xnaBuffer);
            //  _instance.SubmitBuffer(_xnaBuffer);
            for (int i = 0; i < SamplesPerBuffer; i++)
            {
                _workingBuffer[0, i] = 0;
                _workingBuffer[1, i] = 0;
            }
        }

        double SineWave(double time, double frequency)
        {
            return Math.Sin(time * 2 * Math.PI * frequency);
        }

        private double _time = 0.0;
        /// <summary>
        /// Fills the working buffer with values from the oscillators
        /// </summary>
        private void FillWorkingBuffer(float[] buffer)
        {
            for (int i = 0; i < SamplesPerBuffer / 10; i++)
            {
                if (buffer[i] != 0)
                {
                    for (int k = 0; k < 10; k++)
                    {    // Here is where you sample your wave function
                        _workingBuffer[0, k] = (float)SineWave(_time, 440);
                        _workingBuffer[1, k] = (float)SineWave(_time, 440);
                    }
                }
                //_workingBuffer[1, i] = j;// buffer[i];
                //catch (Exception ex) { }
                // _workingBuffer[0, i] = 0.5f;//
                //_workingBuffer[1, i] = (float)SineWave(_time, 380);

                // Advance time passed since beggining
                // Since the amount of samples in a second equals the chosen SampleRate
                // Then each sample should advance the time by 1 / SampleRate
                _time += 1.0 / SampleRate;
            }
        }

        private static void ConvertBuffer(float[,] from, byte[] to)
        {
            const int bytesPerSample = 2;
            int channels = from.GetLength(0);
            int samplesPerBuffer = from.GetLength(1);

            // Make sure the buffer sizes are correct
            System.Diagnostics.Debug.Assert(to.Length == samplesPerBuffer * channels * bytesPerSample, "Buffer sizes are mismatched.");

            for (int i = 0; i < samplesPerBuffer; i++)
            {
                for (int c = 0; c < channels; c++)
                {
                    // First clamp the value to the [-1.0..1.0] range
                    // float floatSample = MathHelper.Clamp(from[c, i], -1.0f, 1.0f);

                    // Convert it to the 16 bit [short.MinValue..short.MaxValue] range
                    // short shortSample = (short)(floatSample >= 0.0f ? floatSample * short.MaxValue : floatSample * short.MinValue * -1);

                    // Calculate the right index based on the PCM format of interleaved samples per channel [L-R-L-R]
                    int index = i * channels * bytesPerSample + c * bytesPerSample;

                    // Store the 16 bit sample as two consecutive 8 bit values in the buffer with regard to endian-ness
                    //if (!BitConverter.IsLittleEndian)
                    //{
                    //    to[index] = (byte)(shortSample >> 8);
                    //    to[index + 1] = (byte)shortSample;
                    //}
                    //else
                    //{
                    //    to[index] = (byte)shortSample;
                    //    to[index + 1] = (byte)(shortSample >> 8);
                    //}
                }
            }
        }
    }
}
