namespace SpectrumPC.Hardware
{
    public class Kempston
    {
        bool[] _buttons = new bool[5];

        public enum JoystikButtons
        {
            Up = 0,
            Down = 1,
            Left = 2,
            Right = 3,
            Fire = 4,
        }

        public void PressButtons(bool[] buttonsState)
        {
            _buttons = buttonsState;
        }
        public int GetJoystikState(int address)
        {


            int returnvalue = 0x0;

            //000FUDLR
            if (_buttons[(int)JoystikButtons.Fire])
                returnvalue |= 16;
            if (_buttons[(int)JoystikButtons.Up])//up
                returnvalue |= 8;
            if (_buttons[(int)JoystikButtons.Down])//down
                returnvalue |= 4;
            if (_buttons[(int)JoystikButtons.Left])//left
                returnvalue |= 2;
            if (_buttons[(int)JoystikButtons.Right])//right
                returnvalue |= 1;

            return returnvalue;
        }
    }

}
