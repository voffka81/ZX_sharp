using System;
using NAudio.Wave;
using Speccy;
using SpectrumPC.Hardware;

namespace ZX_WPF.Audio
{
    public class BeeperProvider : ISampleProvider
    {
        public AudioProcessor PlaybackEngine { get; }
        public WaveFormat WaveFormat { get; }

        private const int AudioSampleRate = Beeper.SampleRate;
        private const int FRAMES_BUFFERED = 120;
        private const int INITIAL_PREFILL_FRAMES = 1;
        private const int SamplesPerFrame = Beeper.SamplesPerFrame;

        public float[] _waveBuffer;
        public int _bufferLength;
        public long _writeIndex;
        public long _readIndex;
        private int _availableSamples;
        private readonly object _bufferLock = new();
        private float _lastSample;
        private bool _playbackActive;
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
            _bufferLength = SamplesPerFrame * FRAMES_BUFFERED;
            _waveBuffer = new float[_bufferLength];
            _writeIndex = 0;
            _readIndex = 0;
            _availableSamples = 0;
            _lastSample = 0f;
            _playbackActive = false;

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
            var startThreshold = SamplesPerFrame * INITIAL_PREFILL_FRAMES;

            if (!_playbackActive)
            {
                lock (_bufferLock)
                {
                    if (_availableSamples >= startThreshold)
                    {
                        _playbackActive = true;
                    }
                }
            }

            if (!_playbackActive)
            {
                var fallback = _lastSample;
                for (var i = 0; i < count; i++)
                {
                    buffer[offset + i] = fallback;
                }

                return count;
            }

            lock (_bufferLock)
            {
                for (var i = 0; i < count; i++)
                {
                    if (_availableSamples > 0)
                    {
                        var sample = _waveBuffer[_readIndex++];
                        if (_readIndex >= _bufferLength) _readIndex = 0;
                        _availableSamples--;
                        _lastSample = sample;
                        buffer[offset + i] = sample;
                    }
                    else
                    {
                        buffer[offset + i] = _lastSample;
                    }
                }

                if (_availableSamples == 0)
                {
                    _playbackActive = false;
                }
            }
            return count;
        }

        public void SetVolume(float volume)
        {
            PlaybackEngine.Volume = Math.Clamp(volume, 0f, 1f);
        }

        public float Volume => PlaybackEngine.Volume;

    }

}
