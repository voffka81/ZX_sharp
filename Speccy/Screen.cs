using System.Drawing;

namespace Speccy
{
    public class Display
    {
        private const int PixelRamStart = 0x4000;
        private const int PixelRamEnd = 0x5800;

        public const int AttributeWidth = 32;
        public const int AttributeHeight = 24;
        public const int PixelWidth = 8 * AttributeWidth;
        private const int PixelHeight = 8 * AttributeHeight;
        public const int AttributeOffset = PixelWidth * AttributeHeight;
        private readonly ushort[] _lookupY = new ushort[PixelHeight];

        public int BorderColor;
        private bool _flashReversed = false;
       
        private readonly Memory _ram;
        public static int Width => 352;
        public static int Height => 296;
        //public int[,] pixelBuffer = new int[257, 193]; //Left border * right border = 48 , 256 working area //top=55,bottom=56,working area =192
        public int[,] pixelBuffer = new int[Width, Height]; //Left border * right border = 48 , 256 working area //top=55,bottom=56,working area =192
        private readonly Bitmap _bitmap = new Bitmap(Width, Height);
        private readonly int[] _ulaColours =
        {
            0x000000, // Black
            0x0000AA, // Blue
            0xAA0000, // Red
            0xAA00AA, // Magenta
            0x00AA00, // Green
            0x00AAAA, // Cyan
            0xAAAA00, // Yellow
            0xAAAAAA, // White
            0x000000, // Bright Black
            0x0000FF, // Bright Blue
            0xFF0000, // Bright Red
            0xFF00FF, // Bright Magenta
            0x00FF00, // Bright Green
            0x00FFFF, // Bright Cyan
            0xFFFF00, // Bright Yellow
            0xFFFFFF, // Bright White
        };

        public Display(Memory ram)
        {
            BorderColor = 1;
            _ram = ram;
            PrepareSpectrumDisplay();
        }

        private void PrepareSpectrumDisplay()
        {
            ushort pos = 0;
            for (var third = 0; third < 3; third++)
                for (var line = 0; line < 8; line++)
                    for (var y = 0; y < 63; y += 8)
                    {
                        _lookupY[y + line + (third * 64)] = pos;
                        pos += 32;
                    }
        }

        public Bitmap GetDisplayImage()
        {
            GetDisplayBuffer();
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    _bitmap.SetPixel(x, y, ToColor(pixelBuffer[x, y]));
                }
            }
            return _bitmap;
        }

        public void ReverseFlash()
        {
            _flashReversed = !_flashReversed;
        }

        public void GetDisplayBuffer()
        {          
            for (int i = 0; i < Width * Height; i++) pixelBuffer[i % Width, i / Width] = _ulaColours[BorderColor];
            for (var ay = 0; ay < AttributeHeight; ay++)
                for (var ax = 0; ax < AttributeWidth; ax++)
                {
                    var attribute = _ram.ReadByte(PixelRamEnd + (ay * AttributeWidth + ax));
                    var bright = (byte)((attribute & 64) >> 3);
                    var foreColor = _ulaColours[(attribute & 7) | bright];
                    var backColor = _ulaColours[((attribute & 56) >> 3) | bright];

                    if (_flashReversed && ((attribute & 0x80) != 0))
                    {
                        var tmp = foreColor;
                        foreColor = backColor;
                        backColor = tmp;
                    }

                    for (var py = 0; py < 8; py++)
                    {
                        var y = ay * 8 + py;
                        var pixels = _ram.ReadByte(PixelRamStart + _lookupY[y] + ax);
                        for (var px = 0; px < 8; px++)
                        {
                            var a = 128 >> px;
                            var x = ax * 8 + px;
                            pixelBuffer[48 + x, 48 + y] = (pixels & a) != 0 ? foreColor : backColor;
                        }
                    }
                }
        }
        private Color ToColor(int rgb)
        {
            return Color.FromArgb(0xFF,
                                  (byte)((rgb & 0xff0000) >> 0x10),
                                  (byte)((rgb & 0xff00) >> 8),
                                  (byte)(rgb & 0xff));
        }
    }
}
