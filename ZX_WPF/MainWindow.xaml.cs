using Microsoft.Win32;
using Speccy;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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
        private Thread _machineThread;
        private Keys[] _keyArray;
        private WriteableBitmap _writeableBitmap;

        public MainWindow()
        {
            InitializeComponent();

            _speccy = new Computer();

            Initialize();
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog() { Filter = "snapshots (*.z80)|*.z80" };
            if (openFileDialog.ShowDialog() == true)
                _speccy.TapeInput(openFileDialog.FileName);
        }

        private void Initialize()
        {
            _keyArray = Enum.GetValues(typeof(Keys)).Cast<Keys>().ToArray();

            _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2) };
            _renderTimer.Tick += RenderBuffer;
            _renderTimer.Start();

            _machineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1) };
            _machineTimer.Tick += ExecuteMachineCycle;
            _machineTimer.Start();

            _machineThread = new Thread(EmulationCycle);
            _machineThread.Start();

            _writeableBitmap = new WriteableBitmap(
            (int)352,
               (int)296,
               96,
               96,
               PixelFormats.Bgr32,
               null);

            screenImage.Source = _writeableBitmap;
        }

        private void EmulationCycle()
        {
            while (true)
            {
                for (int k = 0; k < _keyArray.Length; k++)
                {

                    if (KeyboardInput.IsKeyDown(_keyArray[k]))
                    {
                        if (_keyArray[k] == Keys.F12)
                            _speccy.Reset();
                        _speccy.KeyInput(Map(_keyArray[k]), true);
                        Dispatcher.Invoke(() => { keyIndicator.Fill = System.Windows.Media.Brushes.Green; });
                    }
                }
            }
        }

        private void ExecuteMachineCycle(object sender, System.EventArgs e)
        {
            _speccy.ExecuteCycle();
            _speccy.DisplayUnit.GetDisplayBuffer();
            for (int k = 0; k < _keyArray.Length; k++)
            {
                _speccy.KeyInput(Map(_keyArray[k]), false);
                keyIndicator.Fill = System.Windows.Media.Brushes.Red;
            }
        }

        private void RenderBuffer(object sender, System.EventArgs e)
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

                        // Compute the pixel's color.
                        int color_data = 255 << 16; // R
                        color_data |= 128 << 8;   // G
                        color_data |= 255 << 0;   // B

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
            _machineThread.Abort();
            _renderTimer.Stop();
        }
    }
}


