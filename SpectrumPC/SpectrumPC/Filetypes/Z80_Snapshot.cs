namespace Speccy.Filetypes
{
    public class Z80_Snapshot
    {
        public int TYPE;
        public int TSTATES;
        public bool AY_FOR_48K;
        public bool ISSUE2;
        public bool IFF2;
        public bool IFF1;
        public byte[] AY_REGS;
        public byte PORT_1FFD;
        public byte PORT_FFFD;
        public byte PORT_7FFD;
        public ushort PC;
        public byte BORDER;
        public byte IM;
        public ushort SP;
        public ushort AF;
        public byte R;
        public ushort IY;
        public ushort IX;
        public ushort BC;
        public ushort DE;
        public ushort HL;
        public ushort AF_;
        public ushort BC_;
        public ushort DE_;
        public ushort HL_;
        public byte I;
        public byte[]RAM_BANK=new byte[49152];
    }
}
