using System;
using NAudio.Wave;
using Speccy;

namespace ZX_WPF.Audio
{
    public class BeeperProvider : ISampleProvider
    {
        public AudioProcessor PlaybackEngine { get; }
        public WaveFormat WaveFormat { get; }

        private const int AudioSampleRate = Beeper.SampleRate;
        private const int FRAMES_BUFFERED = 90;
        private const int FRAMES_DELAYED = 4;
        private const int SamplesPerFrame = Beeper.SamplesPerFrame;

        private int _frameCount;
        public float[] _waveBuffer;
        public int _bufferLength;
        public long _writeIndex;
        public long _readIndex;
        private int _availableSamples;
        private readonly object _bufferLock = new();
        private float _lastSample;
        public int AvailableSamples
        {
            get
            {
                lock (_bufferLock)
                {
                    return _availableSamples;
                }
            }
        }

        public BeeperProvider()
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(AudioSampleRate, 1);
            _frameCount = 0;
            _bufferLength = SamplesPerFrame * FRAMES_BUFFERED;
            _waveBuffer = new float[_bufferLength];
            _writeIndex = 0;
            _readIndex = 0;
            _availableSamples = 0;
            _lastSample = 0f;

            PlaybackEngine = new AudioProcessor();

            PlaybackEngine.AddBeeperInput(this);
        }

        public void AddSoundFrame(float[] samples)
        {
            if (samples == null) return;
            lock (_bufferLock)
            {
                foreach (var sample in samples)
                {
                    _waveBuffer[_writeIndex++] = sample;
                    if (_writeIndex >= _bufferLength) _writeIndex = 0;

                    if (_availableSamples < _bufferLength)
                    {
                        _availableSamples++;
                    }
                    else
                    {
                        // Buffer full, drop oldest sample to keep up-to-date audio
                        _readIndex++;
                        if (_readIndex >= _bufferLength) _readIndex = 0;
                    }
                    _lastSample = sample;
                }
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
                lock (_bufferLock)
                {
                    for (var i = 0; i < count; i++)
                    {
                        if (_availableSamples > 0)
                        {
                            buffer[offset++] = _waveBuffer[_readIndex++];
                            if (_readIndex >= _bufferLength) _readIndex = 0;
                            _availableSamples--;
                            _lastSample = buffer[offset - 1];
                        }
                        else
                        {
                            buffer[offset++] = _lastSample;
                        }
                    }
                }
            }
            _frameCount++;
            return count;
        }

        public void SetVolume(float volume)
        {
            PlaybackEngine.Volume = Math.Clamp(volume, 0f, 1f);
        }

        public float Volume => PlaybackEngine.Volume;

    }

}
