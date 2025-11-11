using Microsoft.Win32;
using Speccy;
using SpectrumPC.Hardware;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ZX_WPF.Audio;
using ZX_WPF.Keyboard;

namespace ZX_WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindowViewModel : INotifyPropertyChanged
    {
        public Computer Speccy { get; set; }
        private DispatcherTimer _renderTimer;
        private Task? _keyboardListenerTask;
        private CancellationTokenSource? _keyboardLoopCts;
        private WriteableBitmap _writeableBitmap;
        private SpectrumKeyCode[] _keyArray;
        private bool[] _keyStates;
        private BeeperProvider _soundDevice;
        private CancellationTokenSource? _machineLoopCts;
        private Task? _machineLoopTask;
        private readonly object _displayLock = new();
        private volatile bool _isWindowActive = true;
        private volatile bool _resetKeyState;
        private Window? _hostWindow;
        private static readonly bool[] _noJoystickButtons = new bool[5];
        private double _volume = 1.0;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ImageSource Screen => _writeableBitmap;

        public ICommand OpenFileCommand { get; set; }
        public ICommand ResetCommand { get; set; }
        public ICommand PlayTapeCommand { get; set; }
        public ICommand StopTapeCommand { get; set; }

        public MainWindowViewModel()
        {
            _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            _keyArray = Enum.GetValues(typeof(SpectrumKeyCode)).Cast<SpectrumKeyCode>().ToArray();
            _keyStates = new bool[_keyArray.Length];
            _writeableBitmap = new WriteableBitmap(
             (int)352,
             (int)296,
             96,
             96,
             PixelFormats.Bgr32,
             null);

            Speccy = new Computer();
            _soundDevice = new BeeperProvider();
            _soundDevice.SetVolume((float)_volume);

            OpenFileCommand = new RelayCommand(o => OpenFile());
            ResetCommand = new RelayCommand(o => ResetPC());
            PlayTapeCommand = new RelayCommand(o => PlayTape());
            StopTapeCommand = new RelayCommand(o => StopTape());

            Initialize();
        }

        private void OpenFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog() { Filter = "All supported files|*.z80;*.tap;|*.tap snapshots (*.z80)|*.z80|tape file (*.tap)|*.tap" };
            if (openFileDialog.ShowDialog() == true)
                Speccy.TapeInput(openFileDialog.FileName);
        }

        private void ResetPC()
        {
            Speccy.TapeDevice.Stop();
            Speccy.Reset();
        }
        private void PlayTape() => Speccy.TapeDevice.Play();
        private void StopTape() => Speccy.TapeDevice.Stop();


        private void Initialize()
        {
            _renderTimer.Tick += RenderBuffer;
            _renderTimer.Start();

            AttachToWindowLifetime();

            _keyboardLoopCts = new CancellationTokenSource();
            _keyboardListenerTask = Task.Run(() => KeyboardLoopAsync(_keyboardLoopCts.Token), _keyboardLoopCts.Token);

            _machineLoopCts = new CancellationTokenSource();
            _machineLoopTask = Task.Factory.StartNew(() => MachineLoop(_machineLoopCts.Token),
                _machineLoopCts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private async Task KeyboardLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_resetKeyState)
                {
                    ReleaseAllKeys();
                    Speccy.Joystik.PressButtons(_noJoystickButtons);
                    _resetKeyState = false;
                }

                if (!_isWindowActive)
                {
                    try
                    {
                        await Task.Delay(20, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    continue;
                }

                for (int k = 0; k < _keyArray.Length; k++)
                {
                    var key = _keyArray[k];
                    bool isDown = KeyboardInput.IsKeyDown(key);
                    if (isDown != _keyStates[k])
                    {
                        _keyStates[k] = isDown;
                        Speccy.KeyInput(key, isDown);
                    }
                }

                Speccy.Joystik.PressButtons(KeyboardInput.IsArrowKeysDown());

                try
                {
                    await Task.Delay(2, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private void MachineLoop(CancellationToken token)
        {
            const int maxBufferedSamples = Beeper.SamplesPerFrame * 16; // ~320ms of audio
            var spinWait = new SpinWait();

            while (!token.IsCancellationRequested)
            {
                var availableSamples = _soundDevice.AvailableSamples;

                if (availableSamples < maxBufferedSamples)
                {
                    do
                    {
                        Speccy.ExecuteCycle();
                        lock (_displayLock)
                        {
                            Speccy.DisplayUnit.GetDisplayBuffer();
                        }
                        _soundDevice.AddSoundFrame(Speccy.AudioSamples);
                        availableSamples = _soundDevice.AvailableSamples;
                        spinWait.Reset();
                    }
                    while (!token.IsCancellationRequested && availableSamples < maxBufferedSamples);

                    continue;
                }

                if (availableSamples > maxBufferedSamples)
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    spinWait.SpinOnce();
                }
            }
        }

        private void AttachToWindowLifetime()
        {
            if (_hostWindow != null)
            {
                return;
            }

            if (Application.Current?.MainWindow == null)
            {
                Application.Current?.Dispatcher.BeginInvoke(new Action(AttachToWindowLifetime));
                return;
            }

            _hostWindow = Application.Current.MainWindow;
            _isWindowActive = _hostWindow?.IsActive ?? true;

            if (_hostWindow != null)
            {
                _hostWindow.Activated += OnWindowActivated;
                _hostWindow.Deactivated += OnWindowDeactivated;
                _hostWindow.Closing += Window_Closing;
            }
        }

        private void OnWindowActivated(object? sender, EventArgs e)
        {
            _isWindowActive = true;
        }

        private void OnWindowDeactivated(object? sender, EventArgs e)
        {
            _isWindowActive = false;
            _resetKeyState = true;
        }

        private void ReleaseAllKeys()
        {
            for (int k = 0; k < _keyArray.Length; k++)
            {
                if (_keyStates[k])
                {
                    _keyStates[k] = false;
                    Speccy.KeyInput(_keyArray[k], false);
                }
            }
        }

        public double Volume
        {
            get => _volume;
            set
            {
                var clamped = Math.Clamp(value, 0.0, 1.0);
                if (Math.Abs(clamped - _volume) < 0.0001)
                {
                    return;
                }

                _volume = clamped;
                _soundDevice?.SetVolume((float)_volume);
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void RenderBuffer(object? sender, EventArgs e)
        {
            _writeableBitmap.Lock();
            try
            {
                lock (_displayLock)
                {
                    var sourcePixels = Speccy.DisplayUnit.PixelBuffer;
                    var width = Display.Width;
                    var height = Display.Height;
                    const int bytesPerPixel = 4;
                    var sourceStride = width * bytesPerPixel;
                    var destStride = _writeableBitmap.BackBufferStride;

                    unsafe
                    {
                        fixed (int* sourcePtr = sourcePixels)
                        {
                            var srcBase = (byte*)sourcePtr;
                            var destBase = (byte*)_writeableBitmap.BackBuffer;

                            if (sourceStride == destStride)
                            {
                                var totalBytes = (long)sourceStride * height;
                                Buffer.MemoryCopy(srcBase, destBase, totalBytes, totalBytes);
                            }
                            else
                            {
                                for (var row = 0; row < height; row++)
                                {
                                    var srcRow = srcBase + (row * sourceStride);
                                    var destRow = destBase + (row * destStride);
                                    Buffer.MemoryCopy(srcRow, destRow, destStride, sourceStride);
                                }
                            }
                        }
                    }

                    _writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                }
            }
            finally
            {
                _writeableBitmap.Unlock();
            }
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _machineLoopCts?.Cancel();
            try
            {
                _machineLoopTask?.Wait(200);
            }
            catch (AggregateException)
            {
                // Ignore cancellation exceptions while shutting down.
            }
            _machineLoopCts?.Dispose();
            _keyboardLoopCts?.Cancel();
            try
            {
                _keyboardListenerTask?.Wait(100);
            }
            catch (AggregateException)
            {
                // Ignore cancellation exceptions while shutting down.
            }
            _keyboardLoopCts?.Dispose();
            _renderTimer.Stop();

            if (_hostWindow != null)
            {
                _hostWindow.Activated -= OnWindowActivated;
                _hostWindow.Deactivated -= OnWindowDeactivated;
                _hostWindow.Closing -= Window_Closing;
                _hostWindow = null;
            }
        }
    }
}


