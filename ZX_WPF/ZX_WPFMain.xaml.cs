using Speccy;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ZX_sharp;

namespace ZX_WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class ZX_WPFMain : Window
    {
        private Computer _speccy;
        private DispatcherTimer _renderTimer;
        private DispatcherTimer _machineTimer;
        private Thread _machineThread;
        private Keys[] _keyArray;
        private WriteableBitmap _writeableBitmap;

        public ZX_WPFMain(Computer speccy)
        {
            InitializeComponent();
            _speccy = speccy;

            Initialize();
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


        private BitmapImage ImageSourceForImageControl(Bitmap bitmap)
        {
            BitmapImage bitmapimage = new BitmapImage();
            using (var memory = new MemoryStream())
            {

                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

        public System.Windows.Media.Color ToColor(int rgb)
        {
            return System.Windows.Media.Color.FromArgb(0xFF,
                                  (byte)((rgb & 0xff0000) >> 0x10),
                                  (byte)((rgb & 0xff00) >> 8),
                                  (byte)(rgb & 0xff));
        }
        private void DrawPixel(int X, int Y, int color)
        {
            var rectangle = new System.Windows.Shapes.Rectangle
            {
                Stroke = new SolidColorBrush(ToColor(color)),
                Fill = new SolidColorBrush(ToColor(color)),
                Width = 2,
                Height = 2
            };

            Canvas.SetLeft(rectangle, X);
            Canvas.SetTop(rectangle, Y);

        }



    }
}


