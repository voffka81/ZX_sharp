using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Threading;
using SpectrumPC.Hardware;

namespace ZX_WPF.Audio
{
    public class AudioProcessor
    {
        private readonly IWavePlayer outputDevice;
        /// <summary>
        /// Enables multiple sources to be played at the same time
        /// </summary>
        private readonly MixingSampleProvider mixer;
        /// <summary>
        /// The AudioPlaybackEngine sample rate
        /// </summary>
        private int SampleRate = Beeper.SampleRate;
        /// <summary>
        /// Stereo Output
        /// </summary>
        private int OutputChannels = 2;

        /// <summary>
        /// The incoming Beeper feed
        /// </summary>
        private ISampleProvider BeeperInput;
        private VolumeSampleProvider? _volumeStage;
        private float _volume = 1f;

        public AudioProcessor()
        {
            // init the Mixer
            mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, OutputChannels));
            // set the mixer to always return the number of samples requested by the Read() method
            mixer.ReadFully = true;
            outputDevice = new WaveOutEvent();
            ((WaveOutEvent)outputDevice as WaveOutEvent).DesiredLatency = 120;
            ((WaveOutEvent)outputDevice as WaveOutEvent).NumberOfBuffers = 3;

            outputDevice.Init(mixer);

            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = false;
                Thread.CurrentThread.Priority = ThreadPriority.Highest;
                // set playing
                outputDevice.Play();
            }).Start();

        }

        public void AddBeeperInput(ISampleProvider input)
        {
            // make sure the sample rate is 44100 and convert to stereo
            var resampStage = Ensure44100(input);
            // init volume stage
            _volumeStage = new VolumeSampleProvider(resampStage)
            {
                Volume = _volume
            };
            // save to field
            BeeperInput = _volumeStage;
            // add to the mixer
            mixer.AddMixerInput(BeeperInput);
        }
        private ISampleProvider Ensure44100(ISampleProvider input)
        {
            var resampler = new WdlResamplingSampleProvider(input, SampleRate);
            return resampler.ToStereo();
        }

        public float Volume
        {
            get => _volumeStage?.Volume ?? _volume;
            set
            {
                var clamped = Math.Clamp(value, 0f, 1f);
                _volume = clamped;
                if (_volumeStage != null)
                {
                    _volumeStage.Volume = clamped;
                }
            }
        }
    }
}
