namespace ZX_sharp.Hardware
{
    public class RAM
    {

        private const byte RAM_PAGES_COUNT = 16;
        private const int RAM_PAGE_SIZE = 8192;

        public byte[][] RamBanks;

        public RAM()
        {
            RamBanks = new byte[RAM_PAGES_COUNT][];
        }

        public void ClearRam()
        {
            for (byte page = 0; page < RAM_PAGES_COUNT; page++)
            {
                RamBanks[page] = new byte[RAM_PAGE_SIZE];
            }
        }
    }
}
