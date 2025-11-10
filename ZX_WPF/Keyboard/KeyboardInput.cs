using Speccy;
using System;
using System.Runtime.InteropServices;
using SpectrumPC.Hardware;

namespace ZX_WPF.Keyboard
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

        private static bool[] _buttons = new bool[5];

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

        public static bool[] IsArrowKeysDown()
        {
            _buttons[(int)Kempston.JoystikButtons.Up] = KeyStates.Down == (GetKeyState(Keys.Up) & KeyStates.Down);
            _buttons[(int)Kempston.JoystikButtons.Down] = KeyStates.Down == (GetKeyState(Keys.Down) & KeyStates.Down);
            _buttons[(int)Kempston.JoystikButtons.Left] = KeyStates.Down == (GetKeyState(Keys.Left) & KeyStates.Down);
            _buttons[(int)Kempston.JoystikButtons.Right] = KeyStates.Down == (GetKeyState(Keys.Right) & KeyStates.Down);
            _buttons[(int)Kempston.JoystikButtons.Fire] = KeyStates.Down == (GetKeyState(Keys.LControlKey) & KeyStates.Down);

            return _buttons;
        }

        public static bool IsKeyDown(SpectrumKeyCode spectrumKey)
        {   if(spectrumKey==SpectrumKeyCode.Invalid)
                return false;
            return KeyStates.Down == (GetKeyState(Map(spectrumKey)) & KeyStates.Down);
        }

        public static bool IsKeyToggled(SpectrumKeyCode spectrumKey)
        {
            return KeyStates.Toggled == (GetKeyState(Map(spectrumKey)) & KeyStates.Toggled);
        }

        private static Keys Map(SpectrumKeyCode key)
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

                _ => Keys.None
            };

        }

    }
}
