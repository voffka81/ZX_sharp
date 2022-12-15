using NAudio.Wave;

namespace ZX_WPF.Audio
{
    public class BeeperProvider : ISampleProvider
    {
        public AudioProcessor _PlaybackEngine;
        public WaveFormat WaveFormat { get; }

        private const int AudioSampleRate = 35000;
        private const int FRAMES_BUFFERED = 50;
        private const int FRAMES_DELAYED = 2;
        private const int SamplesPerFrame = 699;

        private int _frameCount;
        public float[] _waveBuffer;
        public int _bufferLength;
        public long _writeIndex;
        public long _readIndex;

        public BeeperProvider()
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(AudioSampleRate, 1);
            _frameCount = 0;
            _bufferLength = (SamplesPerFrame + 1) * FRAMES_BUFFERED;
            _waveBuffer = new float[_bufferLength];
            _writeIndex = 0;
            _readIndex = 0;

            _PlaybackEngine = new AudioProcessor();

            _PlaybackEngine.AddBeeperInput(this);
        }

        public void AddSoundFrame(float[] samples)
        {
            if (samples == null) { return; }
            // kludgy hack to remove the high frequency artifacts
            // need to look into this, its probably a fault in the code (or some kind of bug in RebelstarII)
            // Until then, if the entire sample array is made up of 1.0f, zero them out
            bool IsNoise = true;
            foreach (var s in samples)
                if (s == 0.0f)
                {
                    IsNoise = false;
                    break;
                }

            foreach (var sample in samples)
            {
                if (IsNoise == false)
                    _waveBuffer[_writeIndex++] = sample;
                else
                    _waveBuffer[_writeIndex++] = 0f;
                if (_writeIndex >= _bufferLength) _writeIndex = 0;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            // --- We set up the initial buffer content for desired latency
            if (_frameCount <= FRAMES_DELAYED)
            {

                for (var i = 0; i < count; i++)
                {
                    buffer[offset++] = 0.0F;
                }

            }
            else
            {
                // --- We use the real samples
                for (var i = 0; i < count; i++)
                {
                    buffer[offset++] = _waveBuffer[_readIndex++];
                    if (_readIndex >= _bufferLength) _readIndex = 0;
                }
            }
            _frameCount++;
            return count;
        }

    }

}
