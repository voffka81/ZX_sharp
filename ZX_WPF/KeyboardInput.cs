using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ZX_sharp
{

    public abstract class KeyboardInput
    {
        [Flags]
        private enum KeyStates
        {
            None = 0,
            Down = 1,
            Toggled = 2
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern short GetKeyState(int keyCode);

        private static KeyStates GetKeyState(Keys key)
        {
            KeyStates state = KeyStates.None;

            short retVal = GetKeyState((int)key);

            //If the high-order bit is 1, the key is down
            //otherwise, it is up.
            if ((retVal & 0x8000) == 0x8000)
                state |= KeyStates.Down;

            //If the low-order bit is 1, the key is toggled.
            if ((retVal & 1) == 1)
                state |= KeyStates.Toggled;

            return state;
        }

        public static bool IsKeyDown(Keys key)
        {
            return KeyStates.Down == (GetKeyState(key) & KeyStates.Down);
        }

        public static bool IsKeyToggled(Keys key)
        {
            return KeyStates.Toggled == (GetKeyState(key) & KeyStates.Toggled);
        }

        public SpectrumKeyCode Map(Keys key)
        {
            switch (key)
            {
                case Keys.D1:
                    return SpectrumKeyCode.N1;
                case Keys.D2:
                    return SpectrumKeyCode.N2;
                case Keys.D3:
                    return SpectrumKeyCode.N3;
                case Keys.D4:
                    return SpectrumKeyCode.N4;
                case Keys.D5:
                    return SpectrumKeyCode.N5;
                case Keys.D6:
                    return SpectrumKeyCode.N6;
                case Keys.D7:
                    return SpectrumKeyCode.N7;
                case Keys.D8:
                    return SpectrumKeyCode.N8;
                case Keys.D9:
                    return SpectrumKeyCode.N9;
                case Keys.D0:
                    return SpectrumKeyCode.N0;

                case Keys.Q:
                    return SpectrumKeyCode.Q;
                case Keys.W:
                    return SpectrumKeyCode.W;
                case Keys.E:
                    return SpectrumKeyCode.E;
                case Keys.R:
                    return SpectrumKeyCode.R;
                case Keys.T:
                    return SpectrumKeyCode.T;
                case Keys.Y:
                    return SpectrumKeyCode.Y;
                case Keys.U:
                    return SpectrumKeyCode.U;
                case Keys.I:
                    return SpectrumKeyCode.I;
                case Keys.O:
                    return SpectrumKeyCode.O;
                case Keys.P:
                    return SpectrumKeyCode.P;

                case Keys.A:
                    return SpectrumKeyCode.A;
                case Keys.S:
                    return SpectrumKeyCode.S;
                case Keys.D:
                    return SpectrumKeyCode.D;
                case Keys.F:
                    return SpectrumKeyCode.F;
                case Keys.G:
                    return SpectrumKeyCode.G;
                case Keys.H:
                    return SpectrumKeyCode.H;
                case Keys.J:
                    return SpectrumKeyCode.J;
                case Keys.K:
                    return SpectrumKeyCode.K;
                case Keys.L:
                    return SpectrumKeyCode.L;

                case Keys.Z:
                    return SpectrumKeyCode.Z;
                case Keys.X:
                    return SpectrumKeyCode.X;
                case Keys.C:
                    return SpectrumKeyCode.C;
                case Keys.V:
                    return SpectrumKeyCode.V;
                case Keys.B:
                    return SpectrumKeyCode.B;
                case Keys.N:
                    return SpectrumKeyCode.N;
                case Keys.M:
                    return SpectrumKeyCode.M;

                case Keys.LShiftKey:
                    return SpectrumKeyCode.SShift;
                case Keys.RShiftKey:
                    return SpectrumKeyCode.CShift;
                case Keys.Space:
                    return SpectrumKeyCode.Space;

                case Keys.Enter:
                    return SpectrumKeyCode.Enter;

                default:
                    return SpectrumKeyCode.Invalid;
            }
        }

        public Keys Map(SpectrumKeyCode key)
        {
            return key switch
            {
                SpectrumKeyCode.N1 => Keys.D1,
                SpectrumKeyCode.N2 => Keys.D2,
                SpectrumKeyCode.N3 => Keys.D3,
                SpectrumKeyCode.N4 => Keys.D4,
                SpectrumKeyCode.N5 => Keys.D5,
                SpectrumKeyCode.N6 => Keys.D6,
                SpectrumKeyCode.N7 => Keys.D7,
                SpectrumKeyCode.N8 => Keys.D8,
                SpectrumKeyCode.N9 => Keys.D9,
                SpectrumKeyCode.N0 => Keys.D0,

                SpectrumKeyCode.Q => Keys.Q,
                SpectrumKeyCode.W => Keys.W,
                SpectrumKeyCode.E => Keys.E,
                SpectrumKeyCode.R => Keys.R,
                SpectrumKeyCode.T => Keys.T,
                SpectrumKeyCode.Y => Keys.Y,
                SpectrumKeyCode.U => Keys.U,
                SpectrumKeyCode.I => Keys.I,
                SpectrumKeyCode.O => Keys.O,
                SpectrumKeyCode.P => Keys.P,

                SpectrumKeyCode.A => Keys.A,
                SpectrumKeyCode.S => Keys.S,
                SpectrumKeyCode.D => Keys.D,
                SpectrumKeyCode.F => Keys.F,
                SpectrumKeyCode.G => Keys.G,
                SpectrumKeyCode.H => Keys.H,
                SpectrumKeyCode.J => Keys.J,
                SpectrumKeyCode.K => Keys.K,
                SpectrumKeyCode.L => Keys.L,


                SpectrumKeyCode.Z => Keys.Z,
                SpectrumKeyCode.X => Keys.X,
                SpectrumKeyCode.C => Keys.C,
                SpectrumKeyCode.V => Keys.V,
                SpectrumKeyCode.B => Keys.B,
                SpectrumKeyCode.N => Keys.N,
                SpectrumKeyCode.M => Keys.M,

                SpectrumKeyCode.SShift => Keys.LShiftKey,
                SpectrumKeyCode.CShift => Keys.RShiftKey,
                SpectrumKeyCode.Space => Keys.Space,

                SpectrumKeyCode.Enter => Keys.Enter,

                _ => SpectrumKeyCode.Invalid
            };

        }

    }
}
