namespace ZX_sharp.Hardware.Z80_CPU
{
    public interface IPorts
    {
        byte ReadPort(ushort address);
        void WritePort(ushort address, byte value);
        bool NMI { get; }
        bool MI { get; }
        byte Data { get; }
    }
}
