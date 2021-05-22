using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZX_sharp.Hardware.Display;

namespace ZX_sharp.Hardware
{
    class Screen
    {
        int _screenWidth = 352;

        public static uint[] ULAColours = 
        {
            0xFF000000, // Black
            0xFF0000AA, // Blue
            0xFFAA0000, // Red
            0xFFAA00AA, // Magenta
            0xFF00AA00, // Green
            0xFF00AAAA, // Cyan
            0xFFAAAA00, // Yellow
            0xFFAAAAAA, // White
            0xFF000000, // Bright Black
            0xFF0000FF, // Bright Blue
            0xFFFF0000, // Bright Red
            0xFFFF00FF, // Bright Magenta
            0xFF00FF00, // Bright Green
            0xFF00FFFF, // Bright Cyan
            0xFFFFFF00, // Bright Yellow
            0xFFFFFFFF, // Bright White
        };
        int BorderColour = 0xFF0000;

        protected byte[] _pixelBuffer;
        protected byte _pixelByte1;
        protected byte _pixelByte2;
        protected byte _attrByte1;
        protected byte _attrByte2;
        protected int _xPos;
        protected int _yPos;
        int UlaFrameTStateCount = 69888;
        int ScreenLineTime=224;

        protected RenderingTState[] _renderingTStateTable;

        public bool IsTactInDisplayArea(int line, int tactInLine)
        {
            return line >= 64
                && line <= 255
                && tactInLine >= 64
                && tactInLine < 64 + 128;
        }

        public virtual void InitializeUlaTStateTable()
        {
            // --- Reset the tact information table
            _renderingTStateTable = new RenderingTState[UlaFrameTStateCount];

            // --- Iterate through tacts
            for (var tact = 0; tact < UlaFrameTStateCount; tact++)
            {
                // --- We can put a tact shift logic here in the future
                // ...

                // --- calculate screen line and tact in line values here
                var line = tact / ScreenLineTime;
                var tactInLine = tact % ScreenLineTime;

                // --- Default tact description
                var tactItem = new RenderingTState
                {
                    Phase = ScreenRenderingPhase.None,
                    ContentionDelay = 0
                };

               // if (ScreenConfiguration.IsTactVisible(line, tactInLine))
                {
                    // --- Calculate the pixel positions of the area
                    tactItem.XPos = (ushort)((tactInLine - 40) * 2);
                    tactItem.YPos = (ushort)(line - 8 - 8);

                    // --- The current tact is in a visible screen area (border or display area)
                    if (!IsTactInDisplayArea(line, tactInLine))
                    {
                        // --- Set the current border color
                        tactItem.Phase = ScreenRenderingPhase.Border;
                        if (line >= 64 && line <= 255)
                        {
                            // --- Left or right border area beside the display area
                            if (tactInLine == 64 - 2)
                            {
                                // --- Fetch the first pixel data byte of the current line (2 tacts away)
                                tactItem.Phase = ScreenRenderingPhase.BorderAndFetchPixelByte;
                                tactItem.PixelByteToFetchAddress = CalculatePixelByteAddress(line, tactInLine + 2);
                                tactItem.ContentionDelay = 6;
                            }
                            else if (tactInLine == 64 - 1)
                            {
                                // --- Fetch the first attribute data byte of the current line (1 tact away)
                                tactItem.Phase = ScreenRenderingPhase.BorderAndFetchPixelAttribute;
                                tactItem.AttributeToFetchAddress = CalculateAttributeAddress(line, tactInLine + 1);
                                tactItem.ContentionDelay = 5;
                            }
                        }
                    }
                    else
                    {
                        // --- According to the tact, the ULA does separate actions
                        var pixelTact = tactInLine - 64;
                        switch (pixelTact & 7)
                        {
                            case 0:
                                // --- Display the current tact pixels
                                tactItem.Phase = ScreenRenderingPhase.DisplayByte1;
                                tactItem.ContentionDelay = 4;
                                break;
                            case 1:
                                // --- Display the current tact pixels
                                tactItem.Phase = ScreenRenderingPhase.DisplayByte1;
                                tactItem.ContentionDelay = 3;
                                break;
                            case 2:
                                // --- While displaying the current tact pixels, we need to prefetch the
                                // --- pixel data byte 2 tacts away
                                tactItem.Phase = ScreenRenderingPhase.DisplayByte1AndFetchByte2;
                                tactItem.PixelByteToFetchAddress = CalculatePixelByteAddress(line, tactInLine + 2);
                                tactItem.ContentionDelay = 2;
                                break;
                            case 3:
                                // --- While displaying the current tact pixels, we need to prefetch the
                                // --- attribute data byte 1 tacts away
                                tactItem.Phase = ScreenRenderingPhase.DisplayByte1AndFetchAttribute2;
                                tactItem.AttributeToFetchAddress = CalculateAttributeAddress(line, tactInLine + 1);
                                tactItem.ContentionDelay = 1;
                                break;
                            case 4:
                            case 5:
                                // --- Display the current tact pixels
                                tactItem.Phase = ScreenRenderingPhase.DisplayByte2;
                                break;
                            case 6:
                                if (tactInLine < 64 + 128 - 2)
                                {
                                    // --- There are still more bytes to display in this line.
                                    // --- While displaying the current tact pixels, we need to prefetch the
                                    // --- pixel data byte 2 tacts away
                                    tactItem.Phase = ScreenRenderingPhase.DisplayByte2AndFetchByte1;
                                    tactItem.PixelByteToFetchAddress = CalculatePixelByteAddress(line, tactInLine + 2);
                                    tactItem.ContentionDelay = 6;
                                }
                                else
                                {
                                    // --- Last byte in this line.
                                    // --- Display the current tact pixels
                                    tactItem.Phase = ScreenRenderingPhase.DisplayByte2;
                                }
                                break;
                            case 7:
                                if (tactInLine < 64 + 128 - 1)
                                {
                                    // --- There are still more bytes to display in this line.
                                    // --- While displaying the current tact pixels, we need to prefetch the
                                    // --- attribute data byte 1 tacts away
                                    tactItem.Phase = ScreenRenderingPhase.DisplayByte2AndFetchAttribute1;
                                    tactItem.AttributeToFetchAddress = CalculateAttributeAddress(line, tactInLine + 1);
                                    tactItem.ContentionDelay = 5;
                                }
                                else
                                {
                                    // --- Last byte in this line.
                                    // --- Display the current tact pixels
                                    tactItem.Phase = ScreenRenderingPhase.DisplayByte2;
                                }
                                break;
                        }
                    }
                }

                // --- Calculation is ready, let's store the calculated tact item
                _renderingTStateTable[tact] = tactItem;
            }
        }

        /// <summary>
        /// Calculates the pixel address for the specified line and tact within 
        /// the line
        /// </summary>
        /// <param name="line">Line index</param>
        /// <param name="tactInLine">Tacts index within the line</param>
        /// <returns>ZX spectrum screen memory address</returns>
        /// <remarks>
        /// Memory address bits: 
        /// C0-C2: pixel count within a byte -- not used in address calculation
        /// C3-C7: pixel byte within a line
        /// V0-V7: pixel line address
        /// 
        /// Direct Pixel Address (da)
        /// =================================================================
        /// |A15|A14|A13|A12|A11|A10|A9 |A8 |A7 |A6 |A5 |A4 |A3 |A2 |A1 |A0 |
        /// =================================================================
        /// | 0 | 0 | 0 |V7 |V6 |V5 |V4 |V3 |V2 |V1 |V0 |C7 |C6 |C5 |C4 |C3 |
        /// =================================================================
        /// | 1 | 1 | 1 | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 1 | 1 | 1 | 1 | 1 | 0xF81F
        /// =================================================================
        /// | 0 | 0 | 0 | 0 | 0 | 1 | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0x0700
        /// =================================================================
        /// | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1 | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0x00E0
        /// =================================================================
        /// 
        /// Spectrum Pixel Address
        /// =================================================================
        /// |A15|A14|A13|A12|A11|A10|A9 |A8 |A7 |A6 |A5 |A4 |A3 |A2 |A1 |A0 |
        /// =================================================================
        /// | 0 | 0 | 0 |V7 |V6 |V2 |V1 |V0 |V5 |V4 |V3 |C7 |C6 |C5 |C4 |C3 |
        /// =================================================================
        /// </remarks>
        protected virtual ushort CalculatePixelByteAddress(int line, int tStateInLine)
        {
            var row = line - 64;
            var column = 2 * (tStateInLine - (40 + 24));
            var da = 0x4000 | (column >> 3) | (row << 5);
            return (ushort)((da & 0xF81F) // --- Reset V5, V4, V3, V2, V1
                | ((da & 0x0700) >> 3)    // --- Keep V5, V4, V3 only
                | ((da & 0x00E0) << 3));  // --- Exchange the V2, V1, V0 bit 
                                          // --- group with V5, V4, V3
        }

        /// <summary>
        /// Calculates the pixel attribute address for the specified line and 
        /// tact within the line
        /// </summary>
        /// <param name="line">Line index</param>
        /// <param name="tactInLine">Tacts index within the line</param>
        /// <returns>ZX spectrum screen memory address</returns>
        /// <remarks>
        /// Memory address bits: 
        /// C0-C2: pixel count within a byte -- not used in address calculation
        /// C3-C7: pixel byte within a line
        /// V0-V7: pixel line address
        /// 
        /// Spectrum Attribute Address
        /// =================================================================
        /// |A15|A14|A13|A12|A11|A10|A9 |A8 |A7 |A6 |A5 |A4 |A3 |A2 |A1 |A0 |
        /// =================================================================
        /// | 0 | 1 | 0 | 1 | 1 | 0 |V7 |V6 |V5 |V4 |V3 |C7 |C6 |C5 |C4 |C3 |
        /// =================================================================
        /// </remarks>
        protected virtual ushort CalculateAttributeAddress(int line, int tStateInLine)
        {
            var row = line - 64;
            var column = 2 * (tStateInLine - (40 + 24));
            var da = (column >> 3) | ((row >> 3) << 5);
            return (ushort)(0x5800 + da);
        }


        public virtual void RenderScreen(int fromTact, int toTact)
        {
            // --- Adjust the tact boundaries
            fromTact = fromTact % UlaFrameTStateCount;
            toTact = toTact % UlaFrameTStateCount;

            // --- Carry out each tact action according to the rendering phase
            for (var currentTact = fromTact; currentTact <= toTact; currentTact++)
            {
                var ulaTact = _renderingTStateTable[currentTact];
                _xPos = ulaTact.XPos;
                _yPos = ulaTact.YPos;

                switch (ulaTact.Phase)
                {
                    case ScreenRenderingPhase.None:
                        // --- Invisible screen area, nothing to do
                        break;

                    case ScreenRenderingPhase.Border:
                        // --- Fetch the border color from ULA and set the corresponding border pixels
                        SetPixels(BorderColour, BorderColour);
                        break;

                    case ScreenRenderingPhase.BorderAndFetchPixelByte:
                        // --- Fetch the border color from ULA and set the corresponding border pixels
                        SetPixels(BorderColour, BorderColour);
                        // --- Obtain the future pixel byte
                        _pixelByte1 = _fetchScreenMemory(ulaTact.PixelByteToFetchAddress);
                        break;

                    case ScreenRenderingPhase.BorderAndFetchPixelAttribute:
                        // --- Fetch the border color from ULA and set the corresponding border pixels
                        SetPixels(BorderColour, BorderColour);
                        // --- Obtain the future attribute byte
                        _attrByte1 = _fetchScreenMemory(ulaTact.AttributeToFetchAddress);
                        break;

                    case ScreenRenderingPhase.DisplayByte1:
                        // --- Display bit 7 and 6 according to the corresponding color
                        SetPixels(
                            GetColor(_pixelByte1 & 0x80, _attrByte1),
                            GetColor(_pixelByte1 & 0x40, _attrByte1));
                        // --- Shift in the subsequent bits
                        _pixelByte1 <<= 2;
                        break;

                    case ScreenRenderingPhase.DisplayByte1AndFetchByte2:
                        // --- Display bit 7 and 6 according to the corresponding color
                        SetPixels(
                            GetColor(_pixelByte1 & 0x80, _attrByte1),
                            GetColor(_pixelByte1 & 0x40, _attrByte1));
                        // --- Shift in the subsequent bits
                        _pixelByte1 <<= 2;
                        // --- Obtain the next pixel byte
                        _pixelByte2 = _fetchScreenMemory(ulaTact.PixelByteToFetchAddress);
                        break;

                    case ScreenRenderingPhase.DisplayByte1AndFetchAttribute2:
                        // --- Display bit 7 and 6 according to the corresponding color
                        SetPixels(
                            GetColor(_pixelByte1 & 0x80, _attrByte1),
                            GetColor(_pixelByte1 & 0x40, _attrByte1));
                        // --- Shift in the subsequent bits
                        _pixelByte1 <<= 2;
                        // --- Obtain the next attribute
                        _attrByte2 = _fetchScreenMemory(ulaTact.AttributeToFetchAddress);
                        break;

                    case ScreenRenderingPhase.DisplayByte2:
                        // --- Display bit 7 and 6 according to the corresponding color
                        SetPixels(
                            GetColor(_pixelByte2 & 0x80, _attrByte2),
                            GetColor(_pixelByte2 & 0x40, _attrByte2));
                        // --- Shift in the subsequent bits
                        _pixelByte2 <<= 2;
                        break;

                    case ScreenRenderingPhase.DisplayByte2AndFetchByte1:
                        // --- Display bit 7 and 6 according to the corresponding color
                        SetPixels(
                            GetColor(_pixelByte2 & 0x80, _attrByte2),
                            GetColor(_pixelByte2 & 0x40, _attrByte2));
                        // --- Shift in the subsequent bits
                        _pixelByte2 <<= 2;
                        // --- Obtain the next pixel byte
                        _pixelByte1 = _fetchScreenMemory(ulaTact.PixelByteToFetchAddress);
                        break;

                    case ScreenRenderingPhase.DisplayByte2AndFetchAttribute1:
                        // --- Display bit 7 and 6 according to the corresponding color
                        SetPixels(
                            GetColor(_pixelByte2 & 0x80, _attrByte2),
                            GetColor(_pixelByte2 & 0x40, _attrByte2));
                        // --- Shift in the subsequent bits
                        _pixelByte2 <<= 2;
                        // --- Obtain the next attribute
                        _attrByte1 = _fetchScreenMemory(ulaTact.AttributeToFetchAddress);
                        break;
                }
            }
        }

        protected Func<ushort, byte> _fetchScreenMemory;

        protected int GetColor(int pixelValue, byte attr)
        {
            var offset = (pixelValue == 0 ? 0 : 0x100) + attr;
            return 10;
            //return _flashPhase
            //    ? _flashOnColors[offset]
            //    : _flashOffColors[offset];
        }

        protected void SetPixels(int colorIndex1, int colorIndex2)
        {
            var pos = _yPos * _screenWidth + _xPos;
            _pixelBuffer[pos++] = (byte)colorIndex1;
            _pixelBuffer[pos] = (byte)colorIndex2;
        }

        public void FillScreenBuffer(byte data)
        {
            for (var i = 0; i < _pixelBuffer.Length; i++)
            {
                _pixelBuffer[i] = data;
            }
        }
    }
}
