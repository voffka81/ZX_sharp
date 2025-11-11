namespace SpectrumPC.Hardware
{
    public class Display
    {
        private const int PixelRamStart = 0x4000;
        private const int PixelRamEnd = 0x5800;
        private const int FrameTStates = 69888;
        private const int LinesPerFrame = 312;
        private const int TStatesPerLine = FrameTStates / LinesPerFrame;

        public const int AttributeWidth = 32;
        public const int AttributeHeight = 24;
        public const int PixelWidth = 8 * AttributeWidth;
        private const int PixelHeight = 8 * AttributeHeight;
        public const int AttributeOffset = PixelWidth * AttributeHeight;
        private readonly ushort[] _lookupY = new ushort[PixelHeight];

        private readonly struct BorderEvent
        {
            public BorderEvent(int tact, byte colour)
            {
                Tact = tact;
                Colour = colour;
            }

            public int Tact { get; }
            public byte Colour { get; }
        }

        private readonly List<BorderEvent> _borderTimeline = new();
        private readonly int[] _lineStartTStates = new int[Height];
        private readonly int[] _columnTStates = new int[Width];
        private byte _currentBorderColour;

        public byte BorderColor;
        private bool _flashReversed = false;

        private readonly Memory _ram;
        public static int Width => 352;
        public static int Height => 296;
        public int[] PixelBuffer { get; } = new int[Width * Height]; //Left border * right border = 48 , 256 working area //top=55,bottom=56,working area =192
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
            PrepareBorderTimings();
            BeginFrame(BorderColor);
        }

        private void PrepareSpectrumDisplay()
        {
            ushort pos = 0;
            for (var third = 0; third < 3; third++)
                for (var line = 0; line < 8; line++)
                    for (var y = 0; y < 63; y += 8)
                    {
                        _lookupY[y + line + third * 64] = pos;
                        pos += 32;
                    }
        }
        public void ReverseFlash()
        {
            _flashReversed = !_flashReversed;
        }

        public void BeginFrame(byte initialColour)
        {
            _borderTimeline.Clear();
            _currentBorderColour = initialColour;
            BorderColor = initialColour;
            _borderTimeline.Add(new BorderEvent(0, initialColour));
        }

        public void RecordBorderChange(long cpuTacts, byte colour)
        {
            if (colour == _currentBorderColour)
                return;

            int normalized = NormalizeTacts(cpuTacts);

            if (_borderTimeline.Count > 0)
            {
                int lastIndex = _borderTimeline.Count - 1;
                if (normalized <= _borderTimeline[lastIndex].Tact)
                {
                    normalized = _borderTimeline[lastIndex].Tact + 1;
                    if (normalized >= FrameTStates)
                    {
                        normalized = FrameTStates - 1;
                    }
                }
            }

            _borderTimeline.Add(new BorderEvent(normalized, colour));
            _currentBorderColour = colour;
            BorderColor = colour;
        }

        public void GetDisplayBuffer()
        {
            if (_borderTimeline.Count == 0)
            {
                _borderTimeline.Add(new BorderEvent(0, BorderColor));
            }

            int eventIndex = 0;
            for (int row = 0; row < Height; row++)
            {
                int rowBase = _lineStartTStates[row];
                for (int col = 0; col < Width; col++)
                {
                    int pixelTstate = rowBase + _columnTStates[col];
                    while (eventIndex + 1 < _borderTimeline.Count && _borderTimeline[eventIndex + 1].Tact <= pixelTstate)
                    {
                        eventIndex++;
                    }
                    PixelBuffer[row * Width + col] = _ulaColours[_borderTimeline[eventIndex].Colour];
                }
            }
            for (var ay = 0; ay < AttributeHeight; ay++)
                for (var ax = 0; ax < AttributeWidth; ax++)
                {
                    var attribute = _ram.ReadByte(PixelRamEnd + ay * AttributeWidth + ax);
                    var bright = (byte)((attribute & 64) >> 3);
                    var foreColor = _ulaColours[attribute & 7 | bright];
                    var backColor = _ulaColours[(attribute & 56) >> 3 | bright];

                    if (_flashReversed && (attribute & 0x80) != 0)
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
                            PixelBuffer[(48 + y) * Width + 48 + x] = (pixels & a) != 0 ? foreColor : backColor;
                        }
                    }
                }
        }

        private void PrepareBorderTimings()
        {
            for (int row = 0; row < Height; row++)
            {
                long lineIndex = (long)row * LinesPerFrame / Height;
                if (lineIndex >= LinesPerFrame)
                {
                    lineIndex = LinesPerFrame - 1;
                }

                _lineStartTStates[row] = (int)(lineIndex * TStatesPerLine);
            }

            for (int col = 0; col < Width; col++)
            {
                long columnOffset = (long)col * TStatesPerLine / Width;
                if (columnOffset >= TStatesPerLine)
                {
                    columnOffset = TStatesPerLine - 1;
                }

                _columnTStates[col] = (int)columnOffset;
            }
        }

        private static int NormalizeTacts(long cpuTacts)
        {
            long value = cpuTacts % FrameTStates;
            if (value < 0)
            {
                value += FrameTStates;
            }

            return (int)value;
        }
    }
}
