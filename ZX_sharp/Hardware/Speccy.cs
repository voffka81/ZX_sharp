using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZX_sharp.Hardware
{
    public class Speccy
    {
        protected bool _frameCompleted;

        private const int DisplayStart = 0x4000;
        private const int DisplayLength = 0x1B00;
        protected byte[][] PageReadPointer = new byte[8][];
        protected byte[][] PageWritePointer = new byte[8][];
        Screen _screenDevice = new Screen();
        RAM _ram = new RAM();

        public Speccy()
        {
            _screenDevice.InitializeUlaTStateTable();
            _ram.ClearRam();

            Random rand = new Random();
            //Fill memory with random stuff to simulate hard reset
            for (int index = DisplayStart; index < DisplayStart + DisplayLength; ++index)
                PokeByteNoContend(index, rand.Next(255));


        }

        public void PokeByteNoContend(int addr, int b)
        {
            addr &= 0xffff;
            b &= 0xff;

            int page = (addr) >> 13;
            int offset = (addr) & 0x1FFF;

            _ram.RamBanks[page][offset] = (byte)b;
        }

        long cpuTacts = 0;
        long LastFrameCPUTick;
        int CurrentFrameTState => (int)(cpuTacts - LastFrameCPUTick);
        int LastRenderedULATState;

        public void ExecuteCycle()
        {
            var cycleStartTime = DateTime.Now.Ticks;
            var cycleFrameCount = 0;

            if (_frameCompleted)
            {
                //// frame has been completed - get last frame CPU tick
                LastFrameCPUTick = cpuTacts - 0;

                // notify all devices to start a new frame
                //OnNewFrame();

                // set the last rendered ULA T-State
                //LastRenderedULATState = OverFlowTStates;

                //_frameCompleted = false;
            }

            while (!_frameCompleted)
            {
                // --- Check for leaving maskable interrupt mode
                //if (RunsInMaskableInterrupt && Cpu.Registers.PC == 0x0053)
                //{
                //    // --- We leave the maskable interrupt mode when the
                //    // --- current instruction completes
                //    RunsInMaskableInterrupt = false;
                //}

                // check for interrupt signal generation
                //InterruptDevice.CheckForInterrupt(CurrentFrameTState);

                // cycle the Z80 cpu once (for one instruction)
                cpuTacts += 4;//Cpu.ExecuteCpuCycle();
                              // run a rendering cycle based on the current CPU tact count
                var lastTState = CurrentFrameTState;
                _screenDevice.RenderScreen(LastRenderedULATState + 1, lastTState);
                LastRenderedULATState = lastTState;
            }
        }
    }
}
