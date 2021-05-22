using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Speccy;

namespace ZX_WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class ZX_WPFMain : Window
    {
        Computer _speccy;
        Thread MachineThread;

        public ZX_WPFMain()
        {
            InitializeComponent();
            _speccy = new Computer();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
            timer.Tick += Timer_Tick;
            timer.Start();

            MachineThread = new Thread(BeginEmulation);

            MachineThread.Start();

            MainGrid.Children.Add(img);
            MainGrid.Children.Add(image);
        }

        private void BeginEmulation()
        {
            _speccy.ExecuteCycle();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MachineThread.Abort();
        }

        System.Windows.Controls.Image img = new System.Windows.Controls.Image();
        private void Timer_Tick(object sender, System.EventArgs e)
        {
             var bitmap = _speccy.DisplayUnit.GetDisplayImage();
            //bitmap.Save("screenshot.bmp");
            img.Source = ImageSourceForImageControl(bitmap);
            //DrawRubbish();
        }
        
        private BitmapImage ImageSourceForImageControl(Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                BitmapImage bitmapimage = new BitmapImage();
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }
        System.Windows.Controls.Image image = new System.Windows.Controls.Image();
        private void DrawRubbish()
        {
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                for (var countX = 0; countX < Display.Width; countX++)
                {
                    for (var countY = 0; countY < Display.Height; countY++)
                        dc.DrawRectangle(new SolidColorBrush(ToColor(_speccy.DisplayUnit.pixelBuffer[countX, countY])), null, new Rect(countX, countY, 1, 1));
                }

                dc.Close();
            }

            RenderTargetBitmap rtb = new RenderTargetBitmap(352, 303, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            image.Source = rtb;

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

            canvas.Children.Add(rectangle);
        }

        public void MainGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F12)
            {
                _speccy.Reset();
            }
            _speccy.KeyInput(Map(e.Key), true);
        }

        private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            _speccy.KeyInput(Map(e.Key), false);
        }

        public SpectrumKeyCode Map(Key key)
        {
            switch (key)
            {
                case Key.D1:
                    return SpectrumKeyCode.N1;
                case Key.D2:
                    return SpectrumKeyCode.N2;
                case Key.D3:
                    return SpectrumKeyCode.N3;
                case Key.D4:
                    return SpectrumKeyCode.N4;
                case Key.D5:
                    return SpectrumKeyCode.N5;
                case Key.D6:
                    return SpectrumKeyCode.N6;
                case Key.D7:
                    return SpectrumKeyCode.N7;
                case Key.D8:
                    return SpectrumKeyCode.N8;
                case Key.D9:
                    return SpectrumKeyCode.N9;
                case Key.D0:
                    return SpectrumKeyCode.N0;

                case Key.Q:
                    return SpectrumKeyCode.Q;
                case Key.W:
                    return SpectrumKeyCode.W;
                case Key.E:
                    return SpectrumKeyCode.E;
                case Key.R:
                    return SpectrumKeyCode.R;
                case Key.T:
                    return SpectrumKeyCode.T;
                case Key.Y:
                    return SpectrumKeyCode.Y;
                case Key.U:
                    return SpectrumKeyCode.U;
                case Key.I:
                    return SpectrumKeyCode.I;
                case Key.O:
                    return SpectrumKeyCode.O;
                case Key.P:
                    return SpectrumKeyCode.P;

                case Key.A:
                    return SpectrumKeyCode.A;
                case Key.S:
                    return SpectrumKeyCode.S;
                case Key.D:
                    return SpectrumKeyCode.D;
                case Key.F:
                    return SpectrumKeyCode.F;
                case Key.G:
                    return SpectrumKeyCode.G;
                case Key.H:
                    return SpectrumKeyCode.H;
                case Key.J:
                    return SpectrumKeyCode.J;
                case Key.K:
                    return SpectrumKeyCode.K;
                case Key.L:
                    return SpectrumKeyCode.L;

                case Key.Z:
                    return SpectrumKeyCode.Z;
                case Key.X:
                    return SpectrumKeyCode.X;
                case Key.C:
                    return SpectrumKeyCode.C;
                case Key.V:
                    return SpectrumKeyCode.V;
                case Key.B:
                    return SpectrumKeyCode.B;
                case Key.N:
                    return SpectrumKeyCode.N;
                case Key.M:
                    return SpectrumKeyCode.M;

                case Key.LeftShift:
                    return SpectrumKeyCode.SShift;
                case Key.RightShift:
                    return SpectrumKeyCode.CShift;
                case Key.Space:
                    return SpectrumKeyCode.Space;

                default:
                    return SpectrumKeyCode.Enter;
            }
        }

        
    }
}


