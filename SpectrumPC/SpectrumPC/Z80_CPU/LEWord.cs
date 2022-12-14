using System;

namespace Speccy.Z80_CPU
{
    public struct LEWord
    {
        public byte Low, High;

        public LEWord(byte low, byte high)
        {
            Low = low;
            High = high;
        }

        public LEWord(ushort word)
        {
            High = (byte)(word / 256);
            Low = (byte)(word % 256);
        }

        public void IncLow()
        {
            if (Low == 255)
                Low = 0;
            else
                Low++;
        }

        public void IncHigh()
        {
            if (High == 255)
                High = 0;
            else
            {
                High++;
            }
        }

        public void Inc()
        {
            if (Low != 255)
                Low++;
            else
            {
                Low = 0;
                if (High != 255)
                    High++;
                else
                    High = 0;
            }
        }

        public void DecLow()
        {
            if (Low == 0)
                Low = 255;
            else
                Low--;
        }

        public void DecHigh()
        {
            if (High == 0)
                High = 255;
            else
                High--;
        }

        public void Dec()
        {
            if (Low != 0)
                Low--;
            else
            {
                Low = 255;
                if (High != 0)
                    High--;
                else
                    High = 255;
            }
        }

        public ushort ToUInt16()
        {
            return (ushort)((High * 256) + Low);
        }

        public override string ToString()
        {
            return $"{ToUInt16():X4}";
        }

        public static implicit operator ushort(LEWord value)
        {
            return value.ToUInt16();
        }

        public static implicit operator LEWord(ushort value)
        {
            return new LEWord(value);
        }
    }
}
