using Microsoft.Win32;
using Speccy;
using System;
using System.ComponentModel;
using System.Linq;
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
    public partial class MainWindowViewModel
    {
        public Computer Speccy { get; set; }
        private DispatcherTimer _renderTimer;
        private DispatcherTimer _machineTimer;
        private Task _keyboardListenerTask;
        private WriteableBitmap _writeableBitmap;
        private SpectrumKeyCode[] _keyArray;
        private BeeperProvider _soundDevice;

        public ImageSource Screen => _writeableBitmap;

        public ICommand OpenFileCommand { get; set; }
        public ICommand ResetCommand { get; set; }
        public ICommand PlayTapeCommand { get; set; }

        public MainWindowViewModel()
        {
#if DEBUG
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) return;
#endif
            _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2) };
            _machineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1) };
            _keyArray = Enum.GetValues(typeof(SpectrumKeyCode)).Cast<SpectrumKeyCode>().ToArray();
            _writeableBitmap = new WriteableBitmap(
             (int)352,
             (int)296,
             96,
             96,
             PixelFormats.Bgr32,
             null);

            Speccy = new Computer();
            _soundDevice = new BeeperProvider();

            OpenFileCommand = new RelayCommand(o => OpenFile());
            ResetCommand = new RelayCommand(o => ResetPC());
            PlayTapeCommand = new RelayCommand(o => PlayTape());
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
            Speccy.Reset();
        }
        private void PlayTape()
        {
            Speccy.TapeDevice.Play();
        }


        private void Initialize()
        {
            _renderTimer.Tick += RenderBuffer;
            _renderTimer.Start();


            _machineTimer.Tick += ExecuteMachineCycle;
            _machineTimer.Start();

            _keyboardListenerTask = Task.Run(() => { GetKeyboardInput(); });
        }

        private void GetKeyboardInput()
        {
            while (true)
            {
                for (int k = 0; k < _keyArray.Length; k++)
                {

                    if (KeyboardInput.IsKeyDown(_keyArray[k]))
                    {
                        Speccy.KeyInput(_keyArray[k], true);
                    }
                }

                Speccy.Joystik.PressButtons(KeyboardInput.IsArrowKeysDown());
            }
        }

        private void ExecuteMachineCycle(object? sender, EventArgs e)
        {
            Speccy.ExecuteCycle();
            Speccy.DisplayUnit.GetDisplayBuffer();
            _soundDevice.AddSoundFrame(Speccy.AudioSamples);
            for (int k = 0; k < _keyArray.Length; k++)
            {
                Speccy.KeyInput(_keyArray[k], false);
            }
        }

        private void RenderBuffer(object? sender, EventArgs e)
        {
            _writeableBitmap.Lock();
            for (var column = 0; column < Display.Width; column++)
                for (var row = 0; row < Display.Height; row++)
                {
                    unsafe
                    {
                        // Get a pointer to the back buffer.
                        IntPtr pBackBuffer = _writeableBitmap.BackBuffer;

                        // Find the address of the pixel to draw.
                        pBackBuffer += row * _writeableBitmap.BackBufferStride;
                        pBackBuffer += column * 4;

                        // Assign the color data to the pixel.
                        *((int*)pBackBuffer) = Speccy.DisplayUnit.pixelBuffer[column, row];
                    }

                    // Specify the area of the bitmap that changed.
                    _writeableBitmap.AddDirtyRect(new Int32Rect(column, row, 1, 1));
                }
            // Release the back buffer and make it available for display.
            _writeableBitmap.Unlock();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _machineTimer.Stop();
            _keyboardListenerTask.Dispose();
            _renderTimer.Stop();
        }
    }
}


