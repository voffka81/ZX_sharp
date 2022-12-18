using Microsoft.Win32;
using Speccy;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ZX_WPF.Audio;
using ZX_WPF.Keyboard;

namespace ZX_sharp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Computer _speccy;
        private DispatcherTimer _renderTimer;
        private DispatcherTimer _machineTimer;
        private Task _keyboardListenerTask;
        private WriteableBitmap _writeableBitmap;
        private SpectrumKeyCode[] _keyArray;
        BeeperProvider _soundDevice;

        public MainWindow()
        {
            InitializeComponent();
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

            _speccy = new Computer();
            _soundDevice = new BeeperProvider();
            Initialize();
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog() { Filter = "All supported files|*.z80;*.tap;|*.tap snapshots (*.z80)|*.z80|tape file (*.tap)|*.tap" };
            if (openFileDialog.ShowDialog() == true)
                _speccy.TapeInput(openFileDialog.FileName);
            screenImage.Focus();
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            _speccy.Reset();
        }
        private void btnPlayTape_Click(object sender, RoutedEventArgs e)
        {
            _speccy.TapeDevice.Play();
        }


        private void Initialize()
        {
            _renderTimer.Tick += RenderBuffer;
            _renderTimer.Start();


            _machineTimer.Tick += ExecuteMachineCycle;
            _machineTimer.Start();

            _keyboardListenerTask = Task.Run(() => { GetKeyboardInput(); });

            screenImage.Source = _writeableBitmap;


        }

        private void GetKeyboardInput()
        {
            while (true)
            {
                for (int k = 0; k < _keyArray.Length; k++)
                {

                    if (KeyboardInput.IsKeyDown(_keyArray[k]))
                    {
                        _speccy.KeyInput(_keyArray[k], true);
                        Dispatcher.Invoke(() => { keyIndicator.Fill = System.Windows.Media.Brushes.Green; });
                    }
                }

                _speccy.Joystik.PressButtons(KeyboardInput.IsArrowKeysDown());
            }
        }

        private void ExecuteMachineCycle(object? sender, EventArgs e)
        {
            _speccy.ExecuteCycle();
            _speccy.DisplayUnit.GetDisplayBuffer();
            _soundDevice.AddSoundFrame(_speccy.AudioSamples);
            for (int k = 0; k < _keyArray.Length; k++)
            {
                _speccy.KeyInput(_keyArray[k], false);
                keyIndicator.Fill = Brushes.Red;
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
                        *((int*)pBackBuffer) = _speccy.DisplayUnit.pixelBuffer[column, row];
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


