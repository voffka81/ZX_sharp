using NAudio.Wave;
using Speccy;

namespace ZX_WPF.Audio
{
    internal class AudioProcessor
    {
        private WaveOut waveOut;

        public void StartStopSineWave(Computer speccy)
        {
            if (waveOut == null)
            {
                var sineWaveProvider = new SineWaveProvider32(speccy);
                sineWaveProvider.SetWaveFormat(43750, 1); // 16kHz mono
                waveOut = new WaveOut();
                waveOut.Init(sineWaveProvider);
                waveOut.Play();
            }
            else
            {
                waveOut.Stop();
                waveOut.Dispose();
                waveOut = null;
            }
        }
    }
}
