using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZX_sharp.Hardware
{
    public class Screen
    {
        public int[,] pixelBuffer = new int[352,303]; //Left border * right border = 48 , 256 working area //top=55,bottom=56,working area =192

        public byte[] scr_ypoz = new byte[192]{//Mассив порядка строк на экране
            0,8,16,24,32,40,48,56,
            1,9,17,25,33,41,49,57,
            2,10,18,26,34,42,50,58,
            3,11,19,27,35,43,51,59,
            4,12,20,28,36,44,52,60,
            5,13,21,29,37,45,53,61,
            6,14,22,30,38,46,54,62,
            7,15,23,31,39,47,55,63,

            64,72,80,88,96,104,112,120,
            65,73,81,89,97,105,113,121,
            66,74,82,90,98,106,114,122,
            67,75,83,91,99,107,115,123,
            68,76,84,92,100,108,116,124,
            69,77,85,93,101,109,117,125,
            70,78,86,94,102,110,118,126,
            71,79,87,95,103,111,119,127,

            128,136,144,152,160,168,176,184,
            129,137,145,153,161,169,177,185,
            130,138,146,154,162,170,178,186,
            131,139,147,155,163,171,179,187,
            132,140,148,156,164,172,180,188,
            133,141,149,157,165,173,181,189,
            134,142,150,158,166,174,182,190,
            135,143,151,159,167,175,183,191
        };

        public int[] scr_atr_ypoz = new int[192] {//Массив начала строк в атрибуте
            0,256,512,768,1024,1280,1536,1792,
            32,288,544,800,1056,1312,1568,1824,
            64,320,576,832,1088,1344,1600,1856,
            96,352,608,864,1120,1376,1632,1888,
            128,384,640,896,1152,1408,1664,1920,
            160,416,672,928,1184,1440,1696,1952,
            192,448,704,960,1216,1472,1728,1984,
            224,480,736,992,1248,1504,1760,2016,

            2048,2304,2560,2816,3072,3328,3584,3840,
            2080,2336,2592,2848,3104,3360,3616,3872,
            2112,2368,2624,2880,3136,3392,3648,3904,
            2144,2400,2656,2912,3168,3424,3680,3936,
            2176,2432,2688,2944,3200,3456,3712,3968,
            2208,2464,2720,2976,3232,3488,3744,4000,
            2240,2496,2752,3008,3264,3520,3776,4032,
            2272,2528,2784,3040,3296,3552,3808,4064,

            4096,4352,4608,4864,5120,5376,5632,5888,
            4128,4384,4640,4896,5152,5408,5664,5920,
            4160,4416,4672,4928,5184,5440,5696,5952,
            4192,4448,4704,4960,5216,5472,5728,5984,
            4224,4480,4736,4992,5248,5504,5760,6016,
            4256,4512,4768,5024,5280,5536,5792,6048,
            4288,4544,4800,5056,5312,5568,5824,6080,
            4320,4576,4832,5088,5344,5600,5856,6112
        };

        public int[] ULAColours = 
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
        int BorderColour = 0xFF0000;


        //public void Write_Picsel_RAM(int baitRAM, byte picsel)
        //{
        //    int stolbec;
        //    int stroka;

        //    int xpoz;
        //    int ypoz;

        //    byte atribut;
        //    int tone;
        //    int fone;

        //    byte bitcount;

        //    baitRAM = baitRAM - 0x4000;
        //    scr_picsel_RAM[baitRAM] = picsel;
        //    stolbec = (byte)(baitRAM & 0x1F);
        //    xpoz = (stolbec * 8) + 0x20;
        //    ypoz = (scr_ypoz[baitRAM / 32]);
        //    stroka = ypoz / 8;
        //    ypoz = ypoz + 0x18;

        //    atribut = scr_atr_RAM[(stroka * 32) + stolbec];
        //    tone = scr_Color[((atribut & 64) >> 3) | (atribut & 7)];
        //    fone = scr_Color[(atribut & 120) >> 3];

        //    //LCD_SetCursor(xpoz, ypoz);
        //    //LCD_WriteRAM_Prepare();

        //    bitcount = 8;
        //    do
        //    {
        //        bitcount--;
        //        if (((scr_picsel_RAM[baitRAM]) & (1 << bitcount)) != 0)
        //        {
        //            // DrawPixel(xpoz, ypoz, tone);
        //        }
        //        else
        //        {
        //            //DrawPixel(xpoz, ypoz, fone);//LCD_WriteRAM(fone);
        //        }
        //    } while (bitcount != 0);
        //}
    }
}
