using System;
using ZX_sharp.Hardware.CPU;
using ZX_sharp.Hardware.Z80_CPU;

namespace ZX_sharp.Hardware
{
    class SamplePorts : IPorts
    {
        public byte ReadPort(ushort port)
        {
            Console.WriteLine($"IN 0x{port:X4}");
            return 0;
        }
        public void WritePort(ushort port, byte value)
        {
            Console.WriteLine($"OUT 0x{port:X4}, 0x{value:X2}");
        }
        public bool NMI => false;
        public bool MI => false;
        public byte Data => 0x00;
    }
    public class Speccy
    {
        protected bool _frameCompleted;

        private const int PixelRamStart = 0x4000;
        private const int PixelRamEnd = 0x5800;
        private const int AttributeRamEnd = 0x5B00;

        private Screen _screenDevice;
        RAM _ram = new RAM();
        Z80 _z80;

        public Speccy(Screen screenDevice)
        {
            _z80 = new Z80(_ram,new SamplePorts());
            _screenDevice = screenDevice;

            //Fill memory with random stuff to simulate hard reset
            TestVideoBuffer();
        }

        private void TestVideoBuffer()
        {
            var rand = new Random();
            for (var index = PixelRamStart; index < PixelRamEnd; ++index)
                _ram.Write(index, (byte)rand.Next(255));
            for (var index = PixelRamEnd; index < AttributeRamEnd; ++index)
                _ram.Write(index, (byte)rand.Next(255));
        }

        long cycleStartTime = DateTime.Now.Ticks;
        int cycleFrameCount = 0;

        public void ExecuteCycle()
        {
          
            //while (true)
            //{
                if (cycleFrameCount > 1500)
                    cycleFrameCount = 0;
                if (cycleFrameCount == 0)
                {
                    // TestVideoBuffer();
                    CopyScreenBuffer();
                }
                cycleFrameCount++;
                _z80.Parse();
            //}
        }

        private void CopyScreenBuffer()
        {
            int stolbec;
            int stroka;

            int xpoz;
            int ypoz;

            byte atribut;
            int tone;
            int fone;

            byte bitcount;

            for (var borderY = 0; borderY < 24; borderY++)
            {
                for (var borderX = 0; borderX < 352; borderX++)
                {
                    _screenDevice.pixelBuffer[borderX, borderY] =_screenDevice.ULAColours[_ram.Border];
                }
            }

            for (var videoAdress = PixelRamStart; videoAdress < PixelRamEnd; videoAdress++)
            {
                var data = _ram.Read(videoAdress);
                var zeroVideoAdress = videoAdress - 0x4000;
                stolbec = (byte)(zeroVideoAdress & 0x1F);
                xpoz = (stolbec * 8) + 0x20;
                ypoz = (_screenDevice.scr_ypoz[zeroVideoAdress / 32]);
                stroka = ypoz / 8;
                ypoz = ypoz + 0x18;

                atribut = _ram.Read(((stroka * 32) + stolbec) + PixelRamEnd);
                tone = _screenDevice.ULAColours[((atribut & 64) >> 3) | (atribut & 7)];
                fone = _screenDevice.ULAColours[(atribut & 120) >> 3];

                bitcount = 8;
                do
                {
                    bitcount--;
                    var x = (xpoz + (8 - bitcount));
                    if ((data & (1 << bitcount)) != 0)
                    {
                        _screenDevice.pixelBuffer[x, ypoz] = tone;
                    }
                    else
                    {
                        _screenDevice.pixelBuffer[x, ypoz] = fone;
                    }
                } while (bitcount != 0);
            }

        }
    }
}
