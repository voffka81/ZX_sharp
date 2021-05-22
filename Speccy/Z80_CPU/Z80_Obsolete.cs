using System;
using System.Threading;

// ReSharper disable InconsistentNaming

namespace Speccy.Z80_CPU
{
    public class Z80_Obsolete : IZ80
    {
        private const byte B = 0;
        private const byte C = 1;
        private const byte D = 2;
        private const byte E = 3;
        private const byte H = 4;
        private const byte L = 5;
        private const byte F = 6;
        private const byte A = 7;
        private const byte Bp = 8;
        private const byte Cp = 9;
        private const byte Dp = 10;
        private const byte Ep = 11;
        private const byte Hp = 12;
        private const byte Lp = 13;
        private const byte Fp = 14;
        private const byte Ap = 15;
        private const byte I = 16;
        private const byte R = 17;
        private const byte IX = 18;
        private const byte IY = 20;
        private const byte SP = 22;
        private const byte PC = 24;
        private readonly Memory mem;
        private readonly byte[] registers = new byte[26];
        private DateTime _clock = DateTime.UtcNow;
        private bool IFF1;
        private bool IFF2;
        private int interruptMode;

        private readonly IPorts ports;

        public Z80_Obsolete(Memory memory, IPorts ports)
        {
            mem = memory ?? throw new ArgumentNullException(nameof(memory));
            this.ports = ports ?? throw new ArgumentNullException(nameof(ports));
            Reset();
        }

        private ushort Hl => (ushort)(registers[L] + (registers[H] << 8));
        private ushort Sp => (ushort)(registers[SP + 1] + (registers[SP] << 8));
        private ushort Ix => (ushort)(registers[IX + 1] + (registers[IX] << 8));
        private ushort Iy => (ushort)(registers[IY + 1] + (registers[IY] << 8));
        private ushort Bc => (ushort)((registers[B] << 8) + registers[C]);
        private ushort De => (ushort)((registers[D] << 8) + registers[E]);
        private ushort Pc => (ushort)(registers[PC + 1] + (registers[PC] << 8));
        public bool Halt { get; private set; }

        public ushort TicksCount => throw new NotImplementedException();

        public void Cycle()
        {
            if (ports.NMI)
            {
                var stack = Sp;
                mem.WriteByte(--stack, (byte)(Pc >> 8));
                mem.WriteByte(--stack, (byte)(Pc));
                registers[SP] = (byte)(stack >> 8);
                registers[SP + 1] = (byte)(stack);
                registers[PC] = 0x00;
                registers[PC + 1] = 0x66;
                IFF1 = IFF2;
                IFF1 = false;

                Wait(17);
                Halt = false;
                return;
            }
            if (IFF1 && ports.MI)
            {
                IFF1 = false;
                IFF2 = false;
                switch (interruptMode)
                {
                    case 0:
                        {
                            // This is not quite correct, as it only runs a RST xx
                            // Instead, it should also support any other instruction
                            var instruction = ports.Data;
                            var stack = Sp;
                            mem.WriteByte(--stack, (byte)(Pc >> 8));
                            mem.WriteByte(--stack, (byte)(Pc));
                            registers[SP] = (byte)(stack >> 8);
                            registers[SP + 1] = (byte)(stack);
                            registers[PC] = 0x00;
                            registers[PC + 1] = (byte)(instruction & 0x38);
                            Wait(17);

                            Halt = false;
                            return;
                        }
                    case 1:
                        {
                            var stack = Sp;
                            mem.WriteByte(--stack, (byte)(Pc >> 8));
                            mem.WriteByte(--stack, (byte)(Pc));
                            registers[SP] = (byte)(stack >> 8);
                            registers[SP + 1] = (byte)(stack);
                            registers[PC] = 0x00;
                            registers[PC + 1] = 0x38;

                            Wait(17);
                            Halt = false;
                            return;
                        }
                    case 2:
                        {
                            var vector = ports.Data;
                            var stack = Sp;
                            mem.WriteByte(--stack, (byte)(Pc >> 8));
                            mem.WriteByte(--stack, (byte)(Pc));
                            registers[SP] = (byte)(stack >> 8);
                            registers[SP + 1] = (byte)(stack);
                            var address = (ushort)((registers[I] << 8) + vector);
                            registers[PC] = mem.ReadByte(address++);
                            registers[PC + 1] = mem.ReadByte(address);

                            Wait(17);
                            Halt = false;
                            return;
                        }
                }
                return;
            }
            if (Halt) return;
            var mc = Fetch();
            var hi = (byte)(mc >> 6);
            var lo = (byte)(mc & 0x07);
            var r = (byte)((mc >> 3) & 0x07);
            if (hi == 1)
            {
                var useHL1 = r == 6;
                var useHL2 = lo == 6;
                if (useHL2 && useHL1)
                {

                    Halt = true;
                    return;
                }
                var reg = useHL2 ? mem.ReadByte(Hl) : registers[lo];

                if (useHL1)
                    mem.WriteByte(Hl, reg);
                else
                    registers[r] = reg;
                Wait(useHL1 || useHL2 ? 7 : 4);

                return;
            }
            switch (mc)
            {
                case 0xCB:
                    ParseCB();
                    return;
                case 0xDD:
                    ParseDD();
                    return;
                case 0xED:
                    ParseED();
                    return;
                case 0xFD:
                    ParseFD();
                    return;
                case 0x00:
                    // NOP

                    Wait(4);
                    return;
                case 0x01:
                case 0x11:
                case 0x21:
                    {
                        // LD dd, nn
                        registers[r + 1] = Fetch();
                        registers[r] = Fetch();

                        Wait(10);
                        return;
                    }
                case 0x31:
                    {
                        // LD SP, nn
                        registers[SP + 1] = Fetch();
                        registers[SP] = Fetch();

                        Wait(10);
                        return;
                    }
                case 0x06:
                case 0x0e:
                case 0x16:
                case 0x1e:
                case 0x26:
                case 0x2e:
                case 0x3e:
                    {
                        // LD r,n
                        var n = Fetch();
                        registers[r] = n;

                        Wait(7);
                        return;
                    }
                case 0x36:
                    {
                        // LD (HL), n
                        var n = Fetch();
                        mem.WriteByte(Hl, n);

                        Wait(10);
                        return;
                    }
                case 0x0A:
                    {
                        // LD A, (BC)
                        registers[A] = mem.ReadByte(Bc);

                        Wait(7);
                        return;
                    }
                case 0x1A:
                    {
                        // LD A, (DE)
                        registers[A] = mem.ReadByte(De);

                        Wait(7);
                        return;
                    }
                case 0x3A:
                    {
                        // LD A, (nn)
                        var addr = Fetch16();
                        registers[A] = mem.ReadByte(addr);

                        Wait(13);
                        return;
                    }
                case 0x02:
                    {
                        // LD (BC), A
                        mem.WriteByte(Bc, registers[A]);
                        Wait(7);
                        return;
                    }
                case 0x12:
                    {
                        // LD (DE), A
                        mem.WriteByte(De, registers[A]);
                        Wait(7);
                        return;
                    }
                case 0x32:
                    {
                        // LD (nn), A 
                        var addr = Fetch16();
                        mem.WriteByte(addr, registers[A]);
                        Wait(13);
                        return;
                    }
                case 0x2A:
                    {
                        // LD HL, (nn) 
                        var addr = Fetch16();
                        registers[L] = mem.ReadByte(addr++);
                        registers[H] = mem.ReadByte(addr);
                        Wait(16);
                        return;
                    }
                case 0x22:
                    {
                        // LD (nn), HL
                        var addr = Fetch16();
                        mem.WriteByte(addr++, registers[L]);
                        mem.WriteByte(addr, registers[H]);
                        Wait(16);
                        return;
                    }
                case 0xF9:
                    {
                        // LD SP, HL
                        registers[SP + 1] = registers[L];
                        registers[SP] = registers[H];
                        Wait(6);
                        return;
                    }

                case 0xC5:
                    {
                        // PUSH BC
                        var addr = Sp;
                        mem.WriteByte(--addr, registers[B]);
                        mem.WriteByte(--addr, registers[C]);
                        registers[SP + 1] = (byte)(addr & 0xFF);
                        registers[SP] = (byte)(addr >> 8);
                        Wait(11);
                        return;
                    }
                case 0xD5:
                    {
                        // PUSH DE
                        var addr = Sp;
                        mem.WriteByte(--addr, registers[D]);
                        mem.WriteByte(--addr, registers[E]);
                        registers[SP + 1] = (byte)(addr & 0xFF);
                        registers[SP] = (byte)(addr >> 8);
                        Wait(11);
                        return;
                    }
                case 0xE5:
                    {
                        // PUSH HL
                        var addr = Sp;
                        mem.WriteByte(--addr, registers[H]);
                        mem.WriteByte(--addr, registers[L]);
                        registers[SP + 1] = (byte)(addr & 0xFF);
                        registers[SP] = (byte)(addr >> 8);
                        Wait(11);
                        return;
                    }
                case 0xF5:
                    {
                        // PUSH AF
                        var addr = Sp;
                        mem.WriteByte(--addr, registers[A]);
                        mem.WriteByte(--addr, registers[F]);
                        registers[SP + 1] = (byte)(addr & 0xFF);
                        registers[SP] = (byte)(addr >> 8);
                        Wait(11);
                        return;
                    }
                case 0xC1:
                    {
                        // POP BC
                        var addr = Sp;
                        registers[C] = mem.ReadByte(addr++);
                        registers[B] = mem.ReadByte(addr++);
                        registers[SP + 1] = (byte)(addr & 0xFF);
                        registers[SP] = (byte)(addr >> 8);
                        Wait(10);
                        return;
                    }
                case 0xD1:
                    {
                        // POP DE
                        var addr = Sp;
                        registers[E] = mem.ReadByte(addr++);
                        registers[D] = mem.ReadByte(addr++);
                        registers[SP + 1] = (byte)(addr & 0xFF);
                        registers[SP] = (byte)(addr >> 8);
                        Wait(10);
                        return;
                    }
                case 0xE1:
                    {
                        // POP HL
                        var addr = Sp;
                        registers[L] = mem.ReadByte(addr++);
                        registers[H] = mem.ReadByte(addr++);
                        registers[SP + 1] = (byte)(addr & 0xFF);
                        registers[SP] = (byte)(addr >> 8);
                        Wait(10);
                        return;
                    }
                case 0xF1:
                    {
                        // POP AF
                        var addr = Sp;
                        registers[F] = mem.ReadByte(addr++);
                        registers[A] = mem.ReadByte(addr++);
                        registers[SP + 1] = (byte)(addr & 0xFF);
                        registers[SP] = (byte)(addr >> 8);
                        Wait(10);
                        return;
                    }
                case 0xEB:
                    {
                        // EX DE, HL
                        SwapReg8(D, H);
                        SwapReg8(E, L);
                        Wait(4);
                        return;
                    }
                case 0x08:
                    {
                        // EX AF, AF'
                        SwapReg8(Ap, A);
                        SwapReg8(Fp, F);
                        Wait(4);
                        return;
                    }
                case 0xD9:
                    {
                        // EXX
                        SwapReg8(B, Bp);
                        SwapReg8(C, Cp);
                        SwapReg8(D, Dp);
                        SwapReg8(E, Ep);
                        SwapReg8(H, Hp);
                        SwapReg8(L, Lp);
                        Wait(4);
                        return;
                    }
                case 0xE3:
                    {
                        // EX (SP), HL
                        var addr = Sp;

                        var tmp = registers[L];
                        registers[L] = mem.ReadByte(addr);
                        mem.WriteByte(addr++, tmp);

                        tmp = registers[H];
                        registers[H] = mem.ReadByte(addr);
                        mem.WriteByte(addr, tmp);

                        Wait(19);
                        return;
                    }
                case 0x80:
                case 0x81:
                case 0x82:
                case 0x83:
                case 0x84:
                case 0x85:
                case 0x87:
                    {
                        // ADD A, r
                        Add(registers[lo]);
                        Wait(4);
                        return;
                    }
                case 0xC6:
                    {
                        // ADD A, n
                        var b = Fetch();
                        Add(b);
                        Wait(4);
                        return;
                    }
                case 0x86:
                    {
                        // ADD A, (HL)
                        Add(mem.ReadByte(Hl));
                        Wait(7);
                        return;
                    }
                case 0x88:
                case 0x89:
                case 0x8A:
                case 0x8B:
                case 0x8C:
                case 0x8D:
                case 0x8F:
                    {
                        // ADC A, r
                        Adc(registers[lo]);
                        Wait(4);
                        return;
                    }
                case 0xCE:
                    {
                        // ADC A, n
                        var b = Fetch();
                        Adc(b);
                        Wait(4);
                        return;
                    }
                case 0x8E:
                    {
                        // ADC A, (HL)
                        Adc(mem.ReadByte(Hl));
                        Wait(7);
                        return;
                    }
                case 0x90:
                case 0x91:
                case 0x92:
                case 0x93:
                case 0x94:
                case 0x95:
                case 0x97:
                    {
                        // SUB A, r
                        Sub(registers[lo]);
                        Wait(4);
                        return;
                    }
                case 0xD6:
                    {
                        // SUB A, n
                        var b = Fetch();
                        Sub(b);
                        Wait(4);
                        return;
                    }
                case 0x96:
                    {
                        // SUB A, (HL)
                        Sub(mem.ReadByte(Hl));
                        Wait(7);
                        return;
                    }
                case 0x98:
                case 0x99:
                case 0x9A:
                case 0x9B:
                case 0x9C:
                case 0x9D:
                case 0x9F:
                    {
                        // SBC A, r
                        Sbc(registers[lo]);
                        Wait(4);
                        return;
                    }
                case 0xDE:
                    {
                        // SBC A, n
                        var b = Fetch();
                        Sbc(b);
                        Wait(4);
                        return;
                    }
                case 0x9E:
                    {
                        // SBC A, (HL)
                        Sbc(mem.ReadByte(Hl));
                        Wait(7);
                        return;
                    }

                case 0xA0:
                case 0xA1:
                case 0xA2:
                case 0xA3:
                case 0xA4:
                case 0xA5:
                case 0xA7:
                    {
                        // AND A, r
                        And(registers[lo]);
                        Wait(4);
                        return;
                    }
                case 0xE6:
                    {
                        // AND A, n
                        var b = Fetch();

                        And(b);
                        Wait(4);
                        return;
                    }
                case 0xA6:
                    {
                        // AND A, (HL)
                        And(mem.ReadByte(Hl));
                        Wait(7);
                        return;
                    }
                case 0xB0:
                case 0xB1:
                case 0xB2:
                case 0xB3:
                case 0xB4:
                case 0xB5:
                case 0xB7:
                    {
                        // OR A, r
                        Or(registers[lo]);
                        Wait(4);
                        return;
                    }
                case 0xF6:
                    {
                        // OR A, n
                        var b = Fetch();
                        Or(b);
                        Wait(4);
                        return;
                    }
                case 0xB6:
                    {
                        // OR A, (HL)
                        Or(mem.ReadByte(Hl));
                        Wait(7);
                        return;
                    }
                case 0xA8:
                case 0xA9:
                case 0xAA:
                case 0xAB:
                case 0xAC:
                case 0xAD:
                case 0xAF:
                    {
                        // XOR A, r
                        Xor(registers[lo]);
                        Wait(4);
                        return;
                    }
                case 0xEE:
                    {
                        // XOR A, n
                        var b = Fetch();
                        Xor(b);
                        Wait(4);
                        return;
                    }
                case 0xAE:
                    {
                        // XOR A, (HL)
                        Xor(mem.ReadByte(Hl));
                        Wait(7);
                        return;
                    }

                case 0xF3:
                    {
                        // DI
                        IFF1 = false;
                        IFF2 = false;
                        Wait(4);
                        return;
                    }
                case 0xFB:
                    {
                        // EI
                        IFF1 = true;
                        IFF2 = true;
                        Wait(4);
                        return;
                    }
                case 0xB8:
                case 0xB9:
                case 0xBA:
                case 0xBB:
                case 0xBC:
                case 0xBD:
                case 0xBF:
                    {
                        // CP A, r
                        Cmp(registers[lo]);
                        Wait(4);
                        return;
                    }
                case 0xFE:
                    {
                        // CP A, n
                        var b = Fetch();
                        Cmp(b);
                        Wait(4);
                        return;
                    }
                case 0xBE:
                    {
                        // CP A, (HL)
                        Cmp(mem.ReadByte(Hl));
                        Wait(7);
                        return;
                    }
                case 0x04:
                case 0x0C:
                case 0x14:
                case 0x1C:
                case 0x24:
                case 0x2C:
                case 0x3C:
                    {
                        // INC r
                        registers[r] = Inc(registers[r]);
                        Wait(4);
                        return;
                    }
                case 0x34:
                    {
                        // INC (HL)
                        mem.WriteByte(Hl, Inc(mem.ReadByte(Hl)));
                        Wait(7);
                        return;
                    }

                case 0x05:
                case 0x0D:
                case 0x15:
                case 0x1D:
                case 0x25:
                case 0x2D:
                case 0x3D:
                    {
                        // DEC r
                        registers[r] = Dec(registers[r]);
                        Wait(7);
                        return;
                    }
                case 0x35:
                    {
                        // DEC (HL)
                        mem.WriteByte(Hl, Dec(mem.ReadByte(Hl)));
                        Wait(7);
                        return;
                    }
                case 0x27:
                    {
                        // DAA
                        var a = registers[A];
                        var f = registers[F];
                        if ((a & 0x0F) > 0x09 || (f & (byte)Fl.H) > 0)
                        {
                            Add(0x06);
                            a = registers[A];
                        }
                        if ((a & 0xF0) > 0x90 || (f & (byte)Fl.C) > 0)
                        {
                            Add(0x60);
                        }
                        Wait(4);
                        return;
                    }
                case 0x2F:
                    {
                        // CPL
                        registers[A] ^= 0xFF;
                        registers[F] |= (byte)(Fl.H | Fl.N);
                        Wait(4);
                        return;
                    }
                case 0x3F:
                    {
                        // CCF
                        registers[F] &= (byte)~(Fl.N);
                        registers[F] ^= (byte)(Fl.C);
                        Wait(4);
                        return;
                    }
                case 0x37:
                    {
                        // SCF
                        registers[F] &= (byte)~(Fl.N);
                        registers[F] |= (byte)(Fl.C);
                        Wait(4);
                        return;
                    }
                case 0x09:
                    {
                        AddHl(Bc);

                        Wait(4);
                        return;
                    }
                case 0x19:
                    {
                        AddHl(De);
                        Wait(4);
                        return;
                    }
                case 0x29:
                    {
                        AddHl(Hl);
                        Wait(4);
                        return;
                    }
                case 0x39:
                    {
                        AddHl(Sp);
                        Wait(4);
                        return;
                    }
                case 0x03:
                    {
                        var val = Bc + 1;
                        registers[B] = (byte)(val >> 8);
                        registers[C] = (byte)(val & 0xFF);
                        Wait(4);
                        return;
                    }
                case 0x13:
                    {
                        var val = De + 1;
                        registers[D] = (byte)(val >> 8);
                        registers[E] = (byte)(val & 0xFF);
                        Wait(4);
                        return;
                    }
                case 0x23:
                    {
                        var val = Hl + 1;
                        registers[H] = (byte)(val >> 8);
                        registers[L] = (byte)(val & 0xFF);
                        Wait(4);
                        return;
                    }
                case 0x33:
                    {
                        var val = Sp + 1;
                        registers[SP] = (byte)(val >> 8);
                        registers[SP + 1] = (byte)(val & 0xFF);
                        Wait(4);
                        return;
                    }
                case 0x0B:
                    {
                        var val = Bc - 1;
                        registers[B] = (byte)(val >> 8);
                        registers[C] = (byte)(val & 0xFF);
                        Wait(4);
                        return;
                    }
                case 0x1B:
                    {
                        var val = De - 1;
                        registers[D] = (byte)(val >> 8);
                        registers[E] = (byte)(val & 0xFF);
                        Wait(4);
                        return;
                    }
                case 0x2B:
                    {
                        var val = Hl - 1;
                        registers[H] = (byte)(val >> 8);
                        registers[L] = (byte)(val & 0xFF);
                        Wait(4);
                        return;
                    }
                case 0x3B:
                    {
                        var val = Sp - 1;
                        registers[SP] = (byte)(val >> 8);
                        registers[SP + 1] = (byte)(val & 0xFF);
                        Wait(4);
                        return;
                    }
                case 0x07:
                    {
                        var a = registers[A];
                        var c = (byte)((a & 0x80) >> 7);
                        a <<= 1;
                        registers[A] = a;
                        registers[F] &= (byte)~(Fl.H | Fl.N | Fl.C);
                        registers[F] |= c;
                        Wait(4);
                        return;
                    }
                case 0x17:
                    {
                        var a = registers[A];
                        var c = (byte)((a & 0x80) >> 7);
                        a <<= 1;
                        var f = registers[F];
                        a |= (byte)(f & (byte)Fl.C);
                        registers[A] = a;
                        f &= (byte)~(Fl.H | Fl.N | Fl.C);
                        f |= c;
                        registers[F] = f;
                        Wait(4);
                        return;
                    }
                case 0x0F:
                    {
                        var a = registers[A];
                        var c = (byte)(a & 0x01);
                        a >>= 1;
                        registers[A] = a;
                        registers[F] &= (byte)~(Fl.H | Fl.N | Fl.C);
                        registers[F] |= c;
                        Wait(4);
                        return;
                    }
                case 0x1F:
                    {
                        var a = registers[A];
                        var c = (byte)(a & 0x01);
                        a >>= 1;
                        var f = registers[F];
                        a |= (byte)((f & (byte)Fl.C) << 7);
                        registers[A] = a;
                        f &= (byte)~(Fl.H | Fl.N | Fl.C);
                        f |= c;
                        registers[F] = f;
                        Wait(4);
                        return;
                    }
                case 0xC3:
                    {
                        var addr = Fetch16();
                        registers[PC] = (byte)(addr >> 8);
                        registers[PC + 1] = (byte)(addr);
                        Wait(10);
                        return;
                    }
                case 0xC2:
                case 0xCA:
                case 0xD2:
                case 0xDA:
                case 0xE2:
                case 0xEA:
                case 0xF2:
                case 0xFA:
                    {
                        var addr = Fetch16();
                        if (JumpCondition(r))
                        {
                            registers[PC] = (byte)(addr >> 8);
                            registers[PC + 1] = (byte)(addr);
                        }
                        Wait(10);
                        return;

                    }
                case 0x18:
                    {
                        // order is important here
                        var d = (sbyte)Fetch();
                        var addr = Pc + d;
                        registers[PC] = (byte)(addr >> 8);
                        registers[PC + 1] = (byte)(addr);
                        Wait(12);
                        return;
                    }
                case 0x20:
                case 0x28:
                case 0x30:
                case 0x38:
                    {
                        // order is important here
                        var d = (sbyte)Fetch();
                        var addr = Pc + d;
                        if (JumpCondition((byte)(r & 3)))
                        {
                            registers[PC] = (byte)(addr >> 8);
                            registers[PC + 1] = (byte)(addr);
                            Wait(12);
                        }
                        else
                        {
                            Wait(7);
                        }
                        return;

                    }
                case 0xE9:
                    {
                        var addr = Hl;
                        registers[PC] = (byte)(addr >> 8);
                        registers[PC + 1] = (byte)(addr);
                        Wait(4);
                        return;
                    }
                case 0x10:
                    {
                        // order is important here
                        var d = (sbyte)Fetch();
                        var addr = Pc + d;
                        var b = registers[B];
                        registers[B] = --b;
                        if (b != 0)
                        {
                            registers[PC] = (byte)(addr >> 8);
                            registers[PC + 1] = (byte)(addr);
                            Wait(13);
                        }
                        else
                        {
                            Wait(8);
                        }
                        return;
                    }
                case 0xCD:
                    {
                        var addr = Fetch16();
                        var stack = Sp;
                        mem.WriteByte(--stack, (byte)(Pc >> 8));
                        mem.WriteByte(--stack, (byte)(Pc));
                        registers[SP] = (byte)(stack >> 8);
                        registers[SP + 1] = (byte)(stack);
                        registers[PC] = (byte)(addr >> 8);
                        registers[PC + 1] = (byte)(addr);
                        Wait(17);
                        return;
                    }
                case 0xC4:
                case 0xCC:
                case 0xD4:
                case 0xDC:
                case 0xE4:
                case 0xEC:
                case 0xF4:
                case 0xFC:
                    {
                        var addr = Fetch16();
                        if (JumpCondition(r))
                        {
                            var stack = Sp;
                            mem.WriteByte(--stack, (byte)(Pc >> 8));
                            mem.WriteByte(--stack, (byte)(Pc));
                            registers[SP] = (byte)(stack >> 8);
                            registers[SP + 1] = (byte)(stack);
                            registers[PC] = (byte)(addr >> 8);
                            registers[PC + 1] = (byte)(addr);
                            Wait(17);
                        }
                        else
                        {
                            Wait(10);
                        }
                        return;

                    }
                case 0xC9:
                    {
                        var stack = Sp;
                        registers[PC + 1] = mem.ReadByte(stack++);
                        registers[PC] = mem.ReadByte(stack++);
                        registers[SP] = (byte)(stack >> 8);
                        registers[SP + 1] = (byte)(stack);
                        Wait(10);
                        return;
                    }
                case 0xC0:
                case 0xC8:
                case 0xD0:
                case 0xD8:
                case 0xE0:
                case 0xE8:
                case 0xF0:
                case 0xF8:
                    {
                        if (JumpCondition(r))
                        {
                            var stack = Sp;
                            registers[PC + 1] = mem.ReadByte(stack++);
                            registers[PC] = mem.ReadByte(stack++);
                            registers[SP] = (byte)(stack >> 8);
                            registers[SP + 1] = (byte)(stack);
                            Wait(11);
                        }
                        else
                        {
                            Wait(5);
                        }
                        return;

                    }
                case 0xC7:
                case 0xCF:
                case 0xD7:
                case 0xDF:
                case 0xE7:
                case 0xEF:
                case 0xF7:
                case 0xFF:
                    {
                        var stack = Sp;
                        mem.WriteByte(--stack, (byte)(Pc >> 8));
                        mem.WriteByte(--stack, (byte)(Pc));
                        registers[SP] = (byte)(stack >> 8);
                        registers[SP + 1] = (byte)(stack);
                        registers[PC] = 0;
                        registers[PC + 1] = (byte)(mc & 0x38);
                        Wait(17);
                        return;
                    }
                case 0xDB:
                    {
                        var port = Fetch() + (registers[A] << 8);
                        //registers[A] = ports.ReadPort((ushort)port);
                        Wait(11);
                        return;
                    }
                case 0xD3:
                    {
                        var port = Fetch() + (registers[A] << 8);
                        //ports.WritePort((ushort)port, registers[A]);
                        Wait(11);
                        return;
                    }
            }

            Halt = true;
        }

        private void ParseCB(byte mode = 0)
        {
            sbyte d = 0;
            if (mode != 0)
            {
                d = (sbyte)Fetch();
            }
            if (Halt) return;
            var mc = Fetch();
            var hi = (byte)(mc >> 6);
            var lo = (byte)(mc & 0x07);
            var r = (byte)((mc >> 3) & 0x07);
            var useHL = lo == 6;
            var useIX = mode == 0xDD;
            var useIY = mode == 0XFD;
            var reg = useHL ? useIX ? mem.ReadByte(Ix + d) : useIY ? mem.ReadByte(Iy + d) : mem.ReadByte(Hl) : registers[lo];
            switch (hi)
            {
                case 0:
                    byte c;
                    if ((r & 1) == 1)
                    {
                        c = (byte)(reg & 0x01);
                        reg >>= 1;
                    }
                    else
                    {
                        c = (byte)((reg & 0x80) >> 7);
                        reg <<= 1;
                    }
                    var f = registers[F];
                    switch (r)
                    {
                        case 0:
                            {
                                reg |= c;
                                break;
                            }
                        case 1:
                            {
                                reg |= (byte)(c << 7);
                                break;
                            }
                        case 2:
                            {
                                reg |= (byte)(f & (byte)Fl.C);
                                break;
                            }
                        case 3:
                            {
                                reg |= (byte)((f & (byte)Fl.C) << 7);
                                break;
                            }
                        case 4:
                            {
                                break;
                            }
                        case 5:
                            {
                                reg |= (byte)((reg & 0x40) << 1);
                                break;
                            }
                        case 6:
                            {
                                reg |= 1;

                                break;
                            }
                        case 7:
                            {

                                break;
                            }
                    }
                    f &= (byte)~(Fl.H | Fl.N | Fl.C | Fl.PV | Fl.S | Fl.Z);
                    f |= (byte)(reg & (byte)Fl.S);
                    if (reg == 0) f |= (byte)Fl.Z;
                    if (Parity(reg)) f |= (byte)Fl.PV;
                    f |= c;
                    registers[F] = f;

                    break;
                case 1:
                    {
                        Bit(r, reg);
                        Wait(useHL ? 12 : 8);
                        return;
                    }
                case 2:
                    reg &= (byte)~(0x01 << r);
                    Wait(useHL ? 12 : 8);
                    break;
                case 3:
                    reg |= (byte)(0x01 << r);
                    Wait(useHL ? 12 : 8);
                    break;
            }
            if (useHL)
            {
                if (useIX)
                {
                    mem.WriteByte(Ix + d, reg);
                    Wait(23);
                }
                else if (useIY)
                {
                    mem.WriteByte(Iy + d, reg);
                    Wait(23);
                }
                else
                {
                    mem.WriteByte(Hl, reg);
                    Wait(15);
                }
            }
            else
            {
                if (useIX)
                {
                    mem.WriteByte(Ix + d, reg);
                    Wait(23);
                }
                else if (useIY)
                {
                    mem.WriteByte(Iy + d, reg);
                    Wait(23);
                }
                registers[lo] = reg;
                Wait(8);
            }
        }

        private void Bit(byte bit, byte value)
        {
            var f = (byte)(registers[F] & (byte)~(Fl.Z | Fl.H | Fl.N));
            if ((value & (0x01 << bit)) == 0) f |= (byte)Fl.Z;
            f |= (byte)Fl.H;
            registers[F] = f;
        }

        private void AddHl(ushort value)
        {
            var sum = Add(Hl, value);
            registers[H] = (byte)(sum >> 8);
            registers[L] = (byte)(sum & 0xFF);
        }

        private void AddIx(ushort value)
        {
            var sum = Add(Ix, value);
            registers[IX] = (byte)(sum >> 8);
            registers[IX + 1] = (byte)(sum & 0xFF);
        }

        private void AddIy(ushort value)
        {
            var sum = Add(Iy, value);
            registers[IY] = (byte)(sum >> 8);
            registers[IY + 1] = (byte)(sum & 0xFF);
        }

        private ushort Add(ushort value1, ushort value2)
        {
            var sum = value1 + value2;
            var f = (byte)(registers[F] & (byte)~(Fl.H | Fl.N | Fl.C));
            if ((value1 & 0x0FFF) + (value2 & 0x0FFF) > 0x0FFF)
                f |= (byte)Fl.H;
            if (sum > 0xFFFF)
                f |= (byte)Fl.C;
            registers[F] = f;
            return (ushort)sum;
        }

        private void AdcHl(ushort value)
        {
            var sum = Adc(Hl, value);
            registers[H] = (byte)(sum >> 8);
            registers[L] = (byte)(sum & 0xFF);
        }

        private ushort Adc(ushort value1, ushort value2)
        {
            var sum = value1 + value2 + (registers[F] & (byte)Fl.C);
            var f = (byte)(registers[F] & (byte)~(Fl.S | Fl.Z | Fl.H | Fl.PV | Fl.N | Fl.C));
            if ((short)sum < 0)
                f |= (byte)Fl.S;
            if (sum == 0)
                f |= (byte)Fl.Z;
            if ((value1 & 0x0FFF) + (value2 & 0x0FFF) + (byte)Fl.C > 0x0FFF)
                f |= (byte)Fl.H;
            if (sum > 0x7FFF)
                f |= (byte)Fl.PV;
            if (sum > 0xFFFF)
                f |= (byte)Fl.C;
            registers[F] = f;
            return (ushort)sum;
        }

        private void SbcHl(ushort value)
        {
            var sum = Sbc(Hl, value);
            registers[H] = (byte)(sum >> 8);
            registers[L] = (byte)(sum & 0xFF);
        }


        private ushort Sbc(ushort value1, ushort value2)
        {
            var diff = value1 - value2 - (registers[F] & (byte)Fl.C);
            var f = (byte)(registers[F] & (byte)~(Fl.S | Fl.Z | Fl.H | Fl.PV | Fl.N | Fl.C));
            if ((short)diff < 0)
                f |= (byte)Fl.S;
            if (diff == 0)
                f |= (byte)Fl.Z;
            if ((value1 & 0xFFF) < (value2 & 0xFFF) + (registers[F] & (byte)Fl.C))
                f |= (byte)Fl.H;
            if (diff > short.MaxValue || diff < short.MinValue)
                f |= (byte)Fl.PV;
            if ((ushort)diff > value1)
                f |= (byte)Fl.C;
            registers[F] = f;
            return (ushort)diff;
        }

        private void ParseED()
        {
            if (Halt) return;
            var mc = Fetch();
            var r = (byte)((mc >> 3) & 0x07);

            switch (mc)
            {
                case 0x47:
                    {
                        // LD I, A
                        registers[I] = registers[A];
                        Wait(9);
                        return;
                    }
                case 0x4F:
                    {
                        // LD R, A
                        registers[R] = registers[A];
                        Wait(9);
                        return;
                    }
                case 0x57:
                    {
                        // LD A, I

                        /*
                                     * Condition Bits Affected
                                     * S is set if the I Register is negative; otherwise, it is reset.
                                     * Z is set if the I Register is 0; otherwise, it is reset.
                                     * H is reset.
                                     * P/V contains contents of IFF2.
                                     * N is reset.
                                     * C is not affected.
                                     * If an interrupt occurs during execution of this instruction, the Parity flag contains a 0.
                                     */
                        var i = registers[I];
                        registers[A] = i;
                        var f = (byte)(registers[F] & (~(byte)(Fl.H | Fl.PV | Fl.N | Fl.S | Fl.Z | Fl.PV)));
                        if (i >= 0x80)
                        {
                            f |= (byte)Fl.S;
                        }
                        else if (i == 0x00)
                        {
                            f |= (byte)Fl.Z;
                        }
                        if (IFF2)
                        {
                            f |= (byte)Fl.PV;
                        }
                        registers[F] = f;
                        Wait(9);
                        return;
                    }
                case 0x5F:
                    {
                        // LD A, R

                        /*
                                     * Condition Bits Affected
                                     * S is set if, R-Register is negative; otherwise, it is reset.
                                     * Z is set if the R Register is 0; otherwise, it is reset.
                                     * H is reset.
                                     * P/V contains contents of IFF2.
                                     * N is reset.
                                     * C is not affected.
                                     * If an interrupt occurs during execution of this instruction, the parity flag contains a 0. 
                                     */
                        var reg = registers[R];
                        registers[A] = reg;
                        var f = (byte)(registers[F] & (~(byte)(Fl.H | Fl.PV | Fl.N | Fl.S | Fl.Z | Fl.PV)));
                        if (reg >= 0x80)
                        {
                            f |= (byte)Fl.S;
                        }
                        else if (reg == 0x00)
                        {
                            f |= (byte)Fl.Z;
                        }
                        if (IFF2)
                        {
                            f |= (byte)Fl.PV;
                        }
                        registers[F] = f;
                        Wait(9);
                        return;
                    }
                case 0x4B:
                    {
                        // LD BC, (nn)
                        var addr = Fetch16();
                        registers[C] = mem.ReadByte(addr++);
                        registers[B] = mem.ReadByte(addr);
                        Wait(20);
                        return;
                    }
                case 0x5B:
                    {
                        // LD DE, (nn)
                        var addr = Fetch16();
                        registers[E] = mem.ReadByte(addr++);
                        registers[D] = mem.ReadByte(addr);
                        Wait(20);
                        return;
                    }
                case 0x6B:
                    {
                        // LD HL, (nn)
                        var addr = Fetch16();
                        registers[L] = mem.ReadByte(addr++);
                        registers[H] = mem.ReadByte(addr);
                        Wait(20);
                        return;
                    }
                case 0x7B:
                    {
                        // LD SP, (nn)
                        var addr = Fetch16();
                        registers[SP + 1] = mem.ReadByte(addr++);
                        registers[SP] = mem.ReadByte(addr);
                        Wait(20);
                        return;
                    }
                case 0x43:
                    {
                        // LD (nn), BC
                        var addr = Fetch16();
                        mem.WriteByte(addr++, registers[C]);
                        mem.WriteByte(addr, registers[B]);
                        Wait(20);
                        return;
                    }
                case 0x53:
                    {
                        // LD (nn), DE
                        var addr = Fetch16();
                        mem.WriteByte(addr++, registers[E]);
                        mem.WriteByte(addr, registers[D]);
                        Wait(20);
                        return;
                    }
                case 0x63:
                    {
                        // LD (nn), HL
                        var addr = Fetch16();
                        mem.WriteByte(addr++, registers[L]);
                        mem.WriteByte(addr, registers[H]);
                        Wait(20);
                        return;
                    }
                case 0x73:
                    {
                        // LD (nn), SP
                        var addr = Fetch16();
                        mem.WriteByte(addr++, registers[SP + 1]);
                        mem.WriteByte(addr, registers[SP]);
                        Wait(20);
                        return;
                    }
                case 0xA0:
                    {
                        // LDI
                        var bc = Bc;
                        var de = De;
                        var hl = Hl;

                        mem.WriteByte(de, mem.ReadByte(hl));
                        de++;
                        hl++;
                        bc--;

                        registers[B] = (byte)(bc >> 8);
                        registers[C] = (byte)(bc & 0xFF);
                        registers[D] = (byte)(de >> 8);
                        registers[E] = (byte)(de & 0xFF);
                        registers[H] = (byte)(hl >> 8);
                        registers[L] = (byte)(hl & 0xFF);

                        var f = (byte)(registers[F] & 0xE9);
                        if (bc != 0) f = (byte)(f | 0x04);
                        registers[F] = f;
                        Wait(16);
                        return;
                    }
                case 0xB0:
                    {
                        // LDIR
                        var bc = Bc;
                        var de = De;
                        var hl = Hl;

                        mem.WriteByte(de, mem.ReadByte(hl));
                        de++;
                        hl++;
                        bc--;

                        registers[B] = (byte)(bc >> 8);
                        registers[C] = (byte)(bc & 0xFF);
                        registers[D] = (byte)(de >> 8);
                        registers[E] = (byte)(de & 0xFF);
                        registers[H] = (byte)(hl >> 8);
                        registers[L] = (byte)(hl & 0xFF);

                        registers[F] = (byte)(registers[F] & 0xE9);
                        if (bc != 0)
                        {
                            var pc = (ushort)((registers[PC] << 8) + registers[PC + 1]);
                            // jumps back to itself
                            pc -= 2;
                            registers[PC] = (byte)(pc >> 8);
                            registers[PC + 1] = (byte)(pc & 0xFF);
                            Wait(21);
                            return;
                        }
                        Wait(16);
                        return;
                    }
                case 0xA8:
                    {
                        // LDD
                        var bc = Bc;
                        var de = De;
                        var hl = Hl;

                        mem.WriteByte(de, mem.ReadByte(hl));
                        de--;
                        hl--;
                        bc--;

                        registers[B] = (byte)(bc >> 8);
                        registers[C] = (byte)(bc & 0xFF);
                        registers[D] = (byte)(de >> 8);
                        registers[E] = (byte)(de & 0xFF);
                        registers[H] = (byte)(hl >> 8);
                        registers[L] = (byte)(hl & 0xFF);

                        var f = (byte)(registers[F] & 0xE9);
                        if (bc != 0) f = (byte)(f | 0x04);
                        registers[F] = f;
                        Wait(16);
                        return;
                    }
                case 0xB8:
                    {
                        // LDDR
                        var bc = Bc;
                        var de = De;
                        var hl = Hl;

                        mem.WriteByte(de, mem.ReadByte(hl));
                        de--;
                        hl--;
                        bc--;

                        registers[B] = (byte)(bc >> 8);
                        registers[C] = (byte)(bc & 0xFF);
                        registers[D] = (byte)(de >> 8);
                        registers[E] = (byte)(de & 0xFF);
                        registers[H] = (byte)(hl >> 8);
                        registers[L] = (byte)(hl & 0xFF);

                        registers[F] = (byte)(registers[F] & 0xE9);
                        if (bc != 0)
                        {
                            var pc = (ushort)((registers[PC] << 8) + registers[PC + 1]);
                            // jumps back to itself
                            pc -= 2;
                            registers[PC] = (byte)(pc >> 8);
                            registers[PC + 1] = (byte)(pc & 0xFF);
                            Wait(21);
                            return;
                        }
                        Wait(16);
                        return;
                    }

                case 0xA1:
                    {
                        // CPI
                        var bc = Bc;
                        var hl = Hl;

                        var a = registers[A];
                        var b = mem.ReadByte(hl);
                        hl++;
                        bc--;

                        registers[B] = (byte)(bc >> 8);
                        registers[C] = (byte)(bc & 0xFF);
                        registers[H] = (byte)(hl >> 8);
                        registers[L] = (byte)(hl & 0xFF);

                        var f = (byte)(registers[F] & 0x2A);
                        if (a < b) f = (byte)(f | 0x80);
                        if (a == b) f = (byte)(f | 0x40);
                        if ((a & 8) < (b & 8)) f = (byte)(f | 0x10);
                        if (bc != 0) f = (byte)(f | 0x04);
                        registers[F] = (byte)(f | 0x02);
                        Wait(16);
                        return;
                    }

                case 0xB1:
                    {
                        // CPIR
                        var bc = Bc;
                        var hl = Hl;

                        var a = registers[A];
                        var b = mem.ReadByte(hl);
                        hl++;
                        bc--;

                        registers[B] = (byte)(bc >> 8);
                        registers[C] = (byte)(bc & 0xFF);
                        registers[H] = (byte)(hl >> 8);
                        registers[L] = (byte)(hl & 0xFF);

                        if (a == b || bc == 0)
                        {
                            var f = (byte)(registers[F] & 0x2A);
                            if (a < b) f = (byte)(f | 0x80);
                            if (a == b) f = (byte)(f | 0x40);
                            if ((a & 8) < (b & 8)) f = (byte)(f | 0x10);
                            if (bc != 0) f = (byte)(f | 0x04);
                            registers[F] = (byte)(f | 0x02);
                            Wait(16);
                            return;
                        }

                        var pc = (ushort)((registers[PC] << 8) + registers[PC + 1]);
                        // jumps back to itself
                        pc -= 2;
                        registers[PC] = (byte)(pc >> 8);
                        registers[PC + 1] = (byte)(pc & 0xFF);
                        Wait(21);
                        return;
                    }

                case 0xA9:
                    {
                        // CPD
                        var bc = Bc;
                        var hl = Hl;

                        var a = registers[A];
                        var b = mem.ReadByte(hl);
                        hl--;
                        bc--;

                        registers[B] = (byte)(bc >> 8);
                        registers[C] = (byte)(bc & 0xFF);
                        registers[H] = (byte)(hl >> 8);
                        registers[L] = (byte)(hl & 0xFF);

                        var f = (byte)(registers[F] & 0x2A);
                        if (a < b) f = (byte)(f | 0x80);
                        if (a == b) f = (byte)(f | 0x40);
                        if ((a & 8) < (b & 8)) f = (byte)(f | 0x10);
                        if (bc != 0) f = (byte)(f | 0x04);
                        registers[F] = (byte)(f | 0x02);
                        Wait(16);
                        return;
                    }

                case 0xB9:
                    {
                        // CPDR
                        var bc = Bc;
                        var hl = Hl;

                        var a = registers[A];
                        var b = mem.ReadByte(hl);
                        hl--;
                        bc--;

                        registers[B] = (byte)(bc >> 8);
                        registers[C] = (byte)(bc & 0xFF);
                        registers[H] = (byte)(hl >> 8);
                        registers[L] = (byte)(hl & 0xFF);

                        if (a == b || bc == 0)
                        {
                            var f = (byte)(registers[F] & 0x2A);
                            if (a < b) f = (byte)(f | 0x80);
                            if (a == b) f = (byte)(f | 0x40);
                            if ((a & 8) < (b & 8)) f = (byte)(f | 0x10);
                            if (bc != 0) f = (byte)(f | 0x04);
                            registers[F] = (byte)(f | 0x02);
                            Wait(21);
                            return;
                        }

                        var pc = (ushort)((registers[PC] << 8) + registers[PC + 1]);
                        // jumps back to itself
                        pc -= 2;
                        registers[PC] = (byte)(pc >> 8);
                        registers[PC + 1] = (byte)(pc & 0xFF);
                        Wait(21);
                        return;
                    }
                case 0x44:
                case 0x54:
                case 0x64:
                case 0x74:
                case 0x4C:
                case 0x5C:
                case 0x6C:
                case 0x7C:
                    {
                        // NEG
                        var a = registers[A];
                        var diff = -a;
                        registers[A] = (byte)diff;

                        var f = (byte)(registers[F] & 0x28);
                        if ((diff & 0x80) > 0) f |= (byte)Fl.S;
                        if (diff == 0) f |= (byte)Fl.Z;
                        if ((a & 0xF) != 0) f |= (byte)Fl.H;
                        if (a == 0x80) f |= (byte)Fl.PV;
                        f |= (byte)Fl.N;
                        if (diff != 0) f |= (byte)Fl.C;
                        registers[F] = f;

                        Wait(8);
                        return;
                    }
                case 0x46:
                case 0x66:
                    {
                        // IM 0
                        interruptMode = 0;
                        Wait(8);
                        return;
                    }
                case 0x56:
                case 0x76:
                    {
                        // IM 1
                        interruptMode = 1;
                        Wait(8);
                        return;
                    }
                case 0x5E:
                case 0x7E:
                    {
                        // IM 2
                        interruptMode = 2;
                        Wait(8);
                        return;
                    }
                case 0x4A:
                    {
                        AdcHl(Bc);

                        Wait(15);
                        return;
                    }
                case 0x5A:
                    {
                        AdcHl(De);
                        Wait(15);
                        return;
                    }
                case 0x6A:
                    {
                        AdcHl(Hl);
                        Wait(15);
                        return;
                    }
                case 0x7A:
                    {
                        AdcHl(Sp);
                        Wait(15);
                        return;
                    }
                case 0x42:
                    {
                        SbcHl(Bc);

                        Wait(15);
                        return;
                    }
                case 0x52:
                    {
                        SbcHl(De);
                        Wait(15);
                        return;
                    }
                case 0x62:
                    {
                        SbcHl(Hl);
                        Wait(15);
                        return;
                    }
                case 0x72:
                    {
                        SbcHl(Sp);
                        Wait(15);
                        return;
                    }

                case 0x6F:
                    {
                        var a = registers[A];
                        var b = mem.ReadByte(Hl);
                        mem.WriteByte(Hl, (byte)((b << 4) | (a & 0x0F)));
                        a = (byte)((a & 0xF0) | (b >> 4));
                        registers[A] = a;
                        var f = (byte)(registers[F] & 0x29);
                        if ((a & 0x80) > 0) f |= (byte)Fl.S;
                        if (a == 0) f |= (byte)Fl.Z;
                        if (Parity(a)) f |= (byte)Fl.PV;
                        registers[F] = f;
                        Wait(18);
                        return;
                    }
                case 0x67:
                    {
                        var a = registers[A];
                        var b = mem.ReadByte(Hl);
                        mem.WriteByte(Hl, (byte)((b >> 4) | (a << 4)));
                        a = (byte)((a & 0xF0) | (b & 0x0F));
                        registers[A] = a;
                        var f = (byte)(registers[F] & 0x29);
                        if ((a & 0x80) > 0) f |= (byte)Fl.S;
                        if (a == 0) f |= (byte)Fl.Z;
                        if (Parity(a)) f |= (byte)Fl.PV;
                        registers[F] = f;
                        Wait(18);
                        return;
                    }
                case 0x45:
                case 0x4D:
                case 0x55:
                case 0x5D:
                case 0x65:
                case 0x6D:
                case 0x75:
                case 0x7D:
                    {
                        var stack = Sp;
                        registers[PC + 1] = mem.ReadByte(stack++);
                        registers[PC] = mem.ReadByte(stack++);
                        registers[SP] = (byte)(stack >> 8);
                        registers[SP + 1] = (byte)(stack);
                        IFF1 = IFF2;
                        Wait(10);
                        return;
                    }

                case 0x77:
                case 0x7F:
                    {
                        Wait(8);
                        return;
                    }
                case 0x40:
                case 0x48:
                case 0x50:
                case 0x58:
                case 0x60:
                case 0x68:
                case 0x78:
                    {
                        byte a = 0;// (byte)ports.ReadPort(Bc);
                        registers[r] = a;
                        var f = (byte)(registers[F] & 0x29);
                        if ((a & 0x80) > 0) f |= (byte)Fl.S;
                        if (a == 0) f |= (byte)Fl.Z;
                        if (Parity(a)) f |= (byte)Fl.PV;
                        registers[F] = f;
                        Wait(8);
                        return;
                    }
                case 0xA2:
                    {
                        byte a = 0;// (byte)ports.ReadPort(Bc);
                        var hl = Hl;
                        mem.WriteByte(hl++, a);
                        registers[H] = (byte)(hl >> 8);
                        registers[L] = (byte)hl;
                        var b = (byte)(registers[B] - 1);
                        registers[B] = b;
                        var f = (byte)(registers[F] & (byte)~(Fl.N | Fl.Z));
                        if (b == 0) f |= (byte)Fl.Z;
                        f |= (byte)Fl.N;
                        registers[F] = f;

                        Wait(16);
                        return;
                    }
                case 0xB2:
                    {
                        var a = (byte)0;// ports.ReadPort(Bc);
                        var hl = Hl;
                        mem.WriteByte(hl++, a);
                        registers[H] = (byte)(hl >> 8);
                        registers[L] = (byte)hl;
                        var b = (byte)(registers[B] - 1);
                        registers[B] = b;
                        if (b != 0)
                        {
                            var pc = Pc - 2;
                            registers[PC] = (byte)(pc >> 8);
                            registers[PC + 1] = (byte)pc;
                            Wait(21);
                        }
                        else
                        {
                            registers[F] = (byte)(registers[F] | (byte)(Fl.N | Fl.Z));
                            Wait(16);
                        }
                        return;
                    }
                case 0xAA:
                    {
                        var a = (byte)0;// ports.ReadPort(Bc);
                        var hl = Hl;
                        mem.WriteByte(hl--, a);
                        registers[H] = (byte)(hl >> 8);
                        registers[L] = (byte)hl;
                        var b = (byte)(registers[B] - 1);
                        registers[B] = b;
                        var f = (byte)(registers[F] & (byte)~(Fl.N | Fl.Z));
                        if (b == 0) f |= (byte)Fl.Z;
                        f |= (byte)Fl.N;
                        registers[F] = f;
                        Wait(16);
                        return;
                    }
                case 0xBA:
                    {
                        var a = (byte)0;// ports.ReadPort(Bc);
                        var hl = Hl;
                        mem.WriteByte(hl--, a);
                        registers[H] = (byte)(hl >> 8);
                        registers[L] = (byte)hl;
                        var b = (byte)(registers[B] - 1);
                        registers[B] = b;
                        if (b != 0)
                        {
                            var pc = Pc - 2;
                            registers[PC] = (byte)(pc >> 8);
                            registers[PC + 1] = (byte)pc;
                            Wait(21);
                        }
                        else
                        {
                            registers[F] = (byte)(registers[F] | (byte)(Fl.N | Fl.Z));
                            Wait(16);
                        }
                        return;
                    }
                case 0x41:
                case 0x49:
                case 0x51:
                case 0x59:
                case 0x61:
                case 0x69:
                case 0x79:
                    {
                        var a = registers[r];
                        //ports.WritePort(Bc, a);
                        var f = (byte)(registers[F] & 0x29);
                        if ((a & 0x80) > 0) f |= (byte)Fl.S;
                        if (a == 0) f |= (byte)Fl.Z;
                        if (Parity(a)) f |= (byte)Fl.PV;
                        registers[F] = f;
                        Wait(8);
                        return;
                    }
                case 0xA3:
                    {
                        var hl = Hl;
                        var a = mem.ReadByte(hl++);
                        //ports.WritePort(Bc, a);
                        registers[H] = (byte)(hl >> 8);
                        registers[L] = (byte)hl;
                        var b = (byte)(registers[B] - 1);
                        registers[B] = b;
                        var f = (byte)(registers[F] & (byte)~(Fl.N | Fl.Z));
                        if (b == 0) f |= (byte)Fl.Z;
                        f |= (byte)Fl.N;
                        registers[F] = f;
                        Wait(16);
                        return;
                    }
                case 0xB3:
                    {
                        var hl = Hl;
                        var a = mem.ReadByte(hl++);
                        //ports.WritePort(Bc, a);
                        registers[H] = (byte)(hl >> 8);
                        registers[L] = (byte)hl;
                        var b = (byte)(registers[B] - 1);
                        registers[B] = b;
                        if (b != 0)
                        {
                            var pc = Pc - 2;
                            registers[PC] = (byte)(pc >> 8);
                            registers[PC + 1] = (byte)pc;
                            Wait(21);
                        }
                        else
                        {
                            registers[F] = (byte)(registers[F] | (byte)(Fl.N | Fl.Z));
                            Wait(16);
                        }
                        return;
                    }
                case 0xAB:
                    {
                        var hl = Hl;
                        var a = mem.ReadByte(hl--);
                        //ports.WritePort(Bc, a);
                        registers[H] = (byte)(hl >> 8);
                        registers[L] = (byte)hl;
                        var b = (byte)(registers[B] - 1);
                        registers[B] = b;
                        var f = (byte)(registers[F] & (byte)~(Fl.N | Fl.Z));
                        if (b == 0) f |= (byte)Fl.Z;
                        f |= (byte)Fl.N;
                        registers[F] = f;
                        Wait(16);
                        return;
                    }
                case 0xBB:
                    {
                        var hl = Hl;
                        var a = mem.ReadByte(hl--);
                        //ports.WritePort(Bc, a);
                        registers[H] = (byte)(hl >> 8);
                        registers[L] = (byte)hl;
                        var b = (byte)(registers[B] - 1);
                        registers[B] = b;
                        if (b != 0)
                        {
                            var pc = Pc - 2;
                            registers[PC] = (byte)(pc >> 8);
                            registers[PC + 1] = (byte)pc;
                            Wait(21);
                        }
                        else
                        {
                            registers[F] = (byte)(registers[F] | (byte)(Fl.N | Fl.Z));
                            Wait(16);
                        }
                        return;
                    }
            }
            Halt = true;
        }

        private void ParseDD()
        {
            if (Halt) return;
            var mc = Fetch();
            var hi = (byte)(mc >> 6);
            var lo = (byte)(mc & 0x07);
            var mid = (byte)((mc >> 3) & 0x07);

            switch (mc)
            {
                case 0xCB:
                    {
                        ParseCB(0xDD);
                        return;
                    }
                case 0x21:
                    {
                        // LD IX, nn
                        registers[IX + 1] = Fetch();
                        registers[IX] = Fetch();
                        Wait(14);
                        return;
                    }
                case 0x46:
                case 0x4e:
                case 0x56:
                case 0x5e:
                case 0x66:
                case 0x6e:
                case 0x7e:
                    {
                        // LD r, (IX+d)
                        var d = (sbyte)Fetch();
                        registers[mid] = mem.ReadByte(Ix + d);
                        Wait(19);
                        return;
                    }
                case 0x70:
                case 0x71:
                case 0x72:
                case 0x73:
                case 0x74:
                case 0x75:
                case 0x77:
                    {
                        // LD (IX+d), r
                        var d = (sbyte)Fetch();
                        mem.WriteByte((ushort)(Ix + d), registers[lo]);
                        Wait(19);
                        return;
                    }
                case 0x36:
                    {
                        // LD (IX+d), n
                        var d = (sbyte)Fetch();
                        var n = Fetch();
                        mem.WriteByte((ushort)(Ix + d), n);
                        Wait(19);
                        return;
                    }
                case 0x2A:
                    {
                        // LD IX, (nn)
                        var addr = Fetch16();
                        registers[IX + 1] = mem.ReadByte(addr++);
                        registers[IX] = mem.ReadByte(addr);
                        Wait(20);
                        return;
                    }
                case 0x22:
                    {
                        // LD (nn), IX
                        var addr = Fetch16();
                        mem.WriteByte(addr++, registers[IX + 1]);
                        mem.WriteByte(addr, registers[IX]);
                        Wait(20);
                        return;
                    }

                case 0xF9:
                    {
                        // LD SP, IX
                        registers[SP] = registers[IX];
                        registers[SP + 1] = registers[IX + 1];
                        Wait(10);
                        return;
                    }
                case 0xE5:
                    {
                        // PUSH IX
                        var addr = Sp;
                        addr--;
                        mem.WriteByte(addr, registers[IX]);
                        addr--;
                        mem.WriteByte(addr, registers[IX + 1]);
                        registers[SP + 1] = (byte)(addr & 0xFF);
                        registers[SP] = (byte)(addr >> 8);
                        Wait(15);
                        return;
                    }
                case 0xE1:
                    {
                        // POP IX
                        var addr = Sp;
                        registers[IX + 1] = mem.ReadByte(addr++);
                        registers[IX] = mem.ReadByte(addr++);
                        registers[SP + 1] = (byte)(addr & 0xFF);
                        registers[SP] = (byte)(addr >> 8);
                        Wait(14);
                        return;
                    }
                case 0xE3:
                    {
                        // EX (SP), IX
                        var h = registers[IX];
                        var l = registers[IX + 1];
                        var addr = Sp;
                        registers[IX + 1] = mem.ReadByte(addr++);
                        registers[IX] = mem.ReadByte(addr);
                        mem.WriteByte(addr--, h);
                        mem.WriteByte(addr, l);

                        Wait(24);
                        return;
                    }

                case 0x86:
                    {
                        // ADD A, (IX+d)
                        var d = (sbyte)Fetch();

                        Add(mem.ReadByte(Ix + d));
                        Wait(19);
                        return;
                    }
                case 0x8E:
                    {
                        // ADC A, (IX+d)
                        var d = (sbyte)Fetch();
                        var a = registers[A];
                        Adc(mem.ReadByte(Ix + d));
                        Wait(19);
                        return;
                    }
                case 0x96:
                    {
                        // SUB A, (IX+d)
                        var d = (sbyte)Fetch();
                        var b = mem.ReadByte(Ix + d);

                        Sub(b);
                        Wait(19);
                        return;
                    }
                case 0x9E:
                    {
                        // SBC A, (IX+d)
                        var d = (sbyte)Fetch();

                        Sbc(mem.ReadByte(Ix + d));
                        Wait(19);
                        return;
                    }
                case 0xA6:
                    {
                        // AND A, (IX+d)
                        var d = (sbyte)Fetch();
                        var b = mem.ReadByte(Ix + d);

                        And(b);
                        Wait(19);
                        return;
                    }
                case 0xB6:
                    {
                        // OR A, (IX+d)
                        var d = (sbyte)Fetch();
                        var b = mem.ReadByte(Ix + d);

                        Or(b);
                        Wait(19);
                        return;
                    }
                case 0xAE:
                    {
                        // OR A, (IX+d)
                        var d = (sbyte)Fetch();
                        var b = mem.ReadByte(Ix + d);

                        Xor(b);
                        Wait(19);
                        return;
                    }
                case 0xBE:
                    {
                        // CP A, (IX+d)
                        var d = (sbyte)Fetch();
                        var b = mem.ReadByte(Ix + d);

                        Cmp(b);
                        Wait(19);
                        return;
                    }
                case 0x34:
                    {
                        // INC (IX+d)
                        var d = (sbyte)Fetch();
                        mem.WriteByte(Ix + d, Inc(mem.ReadByte(Ix + d)));
                        Wait(7);
                        return;
                    }
                case 0x35:
                    {
                        // DEC (IX+d)
                        var d = (sbyte)Fetch();
                        mem.WriteByte(Ix + d, Dec(mem.ReadByte(Ix + d)));
                        Wait(7);
                        return;
                    }
                case 0x09:
                    {
                        AddIx(Bc);
                        Wait(4);
                        return;
                    }
                case 0x19:
                    {
                        AddIx(De);
                        Wait(4);
                        return;
                    }
                case 0x29:
                    {
                        AddIx(Ix);
                        Wait(4);
                        return;
                    }
                case 0x39:
                    {
                        AddIx(Sp);
                        Wait(4);
                        return;
                    }
                case 0x23:
                    {
                        var val = Ix + 1;
                        registers[IX] = (byte)(val >> 8);
                        registers[IX + 1] = (byte)(val & 0xFF);
                        Wait(4);
                        return;
                    }
                case 0x2B:
                    {
                        var val = Ix - 1;
                        registers[IX] = (byte)(val >> 8);
                        registers[IX + 1] = (byte)(val & 0xFF);
                        Wait(4);
                        return;
                    }
                case 0xE9:
                    {
                        var addr = Ix;
                        registers[PC] = (byte)(addr >> 8);
                        registers[PC + 1] = (byte)(addr);
                        Wait(8);
                        return;
                    }

            }
            Halt = true;
        }

        private void ParseFD()
        {
            if (Halt) return;
            var mc = Fetch();
            var hi = (byte)(mc >> 6);
            var lo = (byte)(mc & 0x07);
            var r = (byte)((mc >> 3) & 0x07);

            switch (mc)
            {
                case 0xCB:
                    {
                        ParseCB(0xFD);
                        return;
                    }
                case 0x21:
                    {
                        // LD IY, nn
                        registers[IY + 1] = Fetch();
                        registers[IY] = Fetch();
                        Wait(14);
                        return;
                    }

                case 0x46:
                case 0x4e:
                case 0x56:
                case 0x5e:
                case 0x66:
                case 0x6e:
                case 0x7e:
                    {
                        // LD r, (IY+d)
                        var d = (sbyte)Fetch();
                        registers[r] = mem.ReadByte(Iy + d);
                        Wait(19);
                        return;
                    }
                case 0x70:
                case 0x71:
                case 0x72:
                case 0x73:
                case 0x74:
                case 0x75:
                case 0x77:
                    {
                        // LD (IY+d), r
                        var d = (sbyte)Fetch();
                        mem.WriteByte(Iy + d, registers[lo]);
                        Wait(19);
                        return;
                    }
                case 0x36:
                    {
                        // LD (IY+d), n
                        var d = (sbyte)Fetch();
                        var n = Fetch();
                        mem.WriteByte(Iy + d, n);
                        Wait(19);
                        return;
                    }
                case 0x2A:
                    {
                        // LD IY, (nn)
                        var addr = Fetch16();
                        registers[IY + 1] = mem.ReadByte(addr++);
                        registers[IY] = mem.ReadByte(addr);
                        Wait(20);
                        return;
                    }

                case 0x22:
                    {
                        // LD (nn), IY
                        var addr = Fetch16();
                        mem.WriteByte(addr++, registers[IY + 1]);
                        mem.WriteByte(addr, registers[IY]);
                        Wait(20);
                        return;
                    }
                case 0xF9:
                    {
                        // LD SP, IY
                        registers[SP] = registers[IY];
                        registers[SP + 1] = registers[IY + 1];
                        Wait(10);
                        return;
                    }
                case 0xE5:
                    {
                        // PUSH IY
                        var addr = Sp;
                        mem.WriteByte(--addr, registers[IY]);
                        mem.WriteByte(--addr, registers[IY + 1]);
                        registers[SP + 1] = (byte)(addr & 0xFF);
                        registers[SP] = (byte)(addr >> 8);
                        Wait(15);
                        return;
                    }
                case 0xE1:
                    {
                        // POP IY
                        var addr = Sp;
                        registers[IY + 1] = mem.ReadByte(addr++);
                        registers[IY] = mem.ReadByte(addr++);
                        registers[SP + 1] = (byte)(addr & 0xFF);
                        registers[SP] = (byte)(addr >> 8);
                        Wait(14);
                        return;
                    }
                case 0xE3:
                    {
                        // EX (SP), IY
                        var h = registers[IY];
                        var l = registers[IY + 1];
                        var addr = Sp;
                        registers[IY + 1] = mem.ReadByte(addr);
                        mem.WriteByte(addr++, l);
                        registers[IY] = mem.ReadByte(addr);
                        mem.WriteByte(addr, h);

                        Wait(24);
                        return;
                    }
                case 0x86:
                    {
                        // ADD A, (IY+d)
                        var d = (sbyte)Fetch();

                        Add(mem.ReadByte(Iy + d));
                        Wait(19);
                        return;
                    }
                case 0x8E:
                    {
                        // ADC A, (IY+d)
                        var d = (sbyte)Fetch();
                        var a = registers[A];
                        Adc(mem.ReadByte(Iy + d));

                        Wait(19);
                        return;
                    }
                case 0x96:
                    {
                        // SUB A, (IY+d)
                        var d = (sbyte)Fetch();

                        Sub(mem.ReadByte(Iy + d));
                        Wait(19);
                        return;
                    }
                case 0x9E:
                    {
                        // SBC A, (IY+d)
                        var d = (sbyte)Fetch();

                        Sbc(mem.ReadByte(Iy + d));
                        Wait(19);
                        return;
                    }
                case 0xA6:
                    {
                        // AND A, (IY+d)
                        var d = (sbyte)Fetch();
                        var b = mem.ReadByte(Iy + d);

                        And(b);
                        Wait(19);
                        return;
                    }
                case 0xB6:
                    {
                        // OR A, (IY+d)
                        var d = (sbyte)Fetch();
                        var b = mem.ReadByte(Iy + d);

                        Or(b);
                        Wait(19);
                        return;
                    }
                case 0xAE:
                    {
                        // XOR A, (IY+d)
                        var d = (sbyte)Fetch();
                        var b = mem.ReadByte(Iy + d);

                        Xor(b);
                        Wait(19);
                        return;
                    }
                case 0xBE:
                    {
                        // CP A, (IY+d)
                        var d = (sbyte)Fetch();

                        Cmp(mem.ReadByte(Iy + d));
                        Wait(19);
                        return;
                    }
                case 0x34:
                    {
                        // INC (IY+d)
                        var d = (sbyte)Fetch();
                        mem.WriteByte(Iy + d, Inc(mem.ReadByte(Iy + d)));
                        Wait(7);
                        return;
                    }
                case 0x35:
                    {
                        // DEC (IY+d)
                        var d = (sbyte)Fetch();
                        mem.WriteByte(Iy + d, Dec(mem.ReadByte(Iy + d)));
                        Wait(7);
                        return;
                    }
                case 0x09:
                    {
                        AddIy(Bc);
                        Wait(4);
                        return;
                    }
                case 0x19:
                    {
                        AddIy(De);
                        Wait(4);
                        return;
                    }
                case 0x29:
                    {
                        AddIy(Iy);
                        Wait(4);
                        return;
                    }
                case 0x39:
                    {
                        AddIy(Sp);
                        Wait(4);
                        return;
                    }
                case 0x23:
                    {
                        var val = Iy + 1;
                        registers[IY] = (byte)(val >> 8);
                        registers[IY + 1] = (byte)(val & 0xFF);
                        Wait(4);
                        return;
                    }
                case 0x2B:
                    {
                        var val = Iy - 1;
                        registers[IY] = (byte)(val >> 8);
                        registers[IY + 1] = (byte)(val & 0xFF);
                        Wait(4);
                        return;
                    }
                case 0xE9:
                    {
                        var addr = Iy;
                        registers[PC] = (byte)(addr >> 8);
                        registers[PC + 1] = (byte)(addr);
                        Wait(8);
                        return;
                    }

            }
            Halt = true;
        }

        private void Add(byte b)
        {
            var a = registers[A];
            var sum = a + b;
            registers[A] = (byte)sum;
            var f = (byte)(registers[F] & 0x28);
            if ((sum & 0x80) > 0)
                f |= (byte)Fl.S;
            if ((byte)sum == 0)
                f |= (byte)Fl.Z;
            if ((a & 0xF + b & 0xF) > 0xF)
                f |= (byte)Fl.H;
            if ((a >= 0x80 && b >= 0x80 && (sbyte)sum > 0) || (a < 0x80 && b < 0x80 && (sbyte)sum < 0))
                f |= (byte)Fl.PV;
            if (sum > 0xFF)
                f |= (byte)Fl.C;
            registers[F] = f;
        }

        private void Adc(byte b)
        {
            var a = registers[A];
            var c = (byte)(registers[F] & (byte)Fl.C);
            var sum = a + b + c;
            registers[A] = (byte)sum;
            var f = (byte)(registers[F] & 0x28);
            if ((sum & 0x80) > 0)
                f |= (byte)Fl.S;
            if ((byte)sum == 0)
                f |= (byte)Fl.Z;
            if ((a & 0xF + b & 0xF) > 0xF)
                f |= (byte)Fl.H;
            if ((a >= 0x80 && b >= 0x80 && (sbyte)sum > 0) || (a < 0x80 && b < 0x80 && (sbyte)sum < 0))
                f |= (byte)Fl.PV;
            f = (byte)(f & ~(byte)Fl.N);
            if (sum > 0xFF) f |= (byte)Fl.C;
            registers[F] = f;
        }

        private void Sub(byte b)
        {
            var a = registers[A];
            var diff = a - b;
            registers[A] = (byte)diff;
            var f = (byte)(registers[F] & 0x28);
            if ((diff & 0x80) > 0)
                f |= (byte)Fl.S;
            if (diff == 0)
                f |= (byte)Fl.Z;
            if ((a & 0xF) < (b & 0xF))
                f |= (byte)Fl.H;
            if ((a >= 0x80 && b >= 0x80 && (sbyte)diff > 0) || (a < 0x80 && b < 0x80 && (sbyte)diff < 0))
                f |= (byte)Fl.PV;
            f |= (byte)Fl.N;
            if (diff < 0)
                f |= (byte)Fl.C;
            registers[F] = f;
        }

        private void Sbc(byte b)
        {
            var a = registers[A];
            var c = (byte)(registers[F] & 0x01);
            var diff = a - b - c;
            registers[A] = (byte)diff;
            var f = (byte)(registers[F] & 0x28);
            if ((diff & 0x80) > 0) f |= (byte)Fl.S;
            if (diff == 0) f |= (byte)Fl.Z;
            if ((a & 0xF) < (b & 0xF) + c) f |= (byte)Fl.H;
            if ((a >= 0x80 && b >= 0x80 && (sbyte)diff > 0) || (a < 0x80 && b < 0x80 && (sbyte)diff < 0))
                f |= (byte)Fl.PV;
            f |= (byte)Fl.N;
            if (diff > 0xFF) f |= (byte)Fl.C;
            registers[F] = f;
        }

        private void And(byte b)
        {
            var a = registers[A];
            var res = (byte)(a & b);
            registers[A] = res;
            var f = (byte)(registers[F] & 0x28);
            if ((res & 0x80) > 0) f |= (byte)Fl.S;
            if (res == 0) f |= (byte)Fl.Z;
            f |= (byte)Fl.H;
            if (Parity(res)) f |= (byte)Fl.PV;
            registers[F] = f;
        }

        private void Or(byte b)
        {
            var a = registers[A];
            var res = (byte)(a | b);
            registers[A] = res;
            var f = (byte)(registers[F] & 0x28);
            if ((res & 0x80) > 0)
                f |= (byte)Fl.S;
            if (res == 0)
                f |= (byte)Fl.Z;
            if (Parity(res))
                f |= (byte)Fl.PV;
            registers[F] = f;
        }

        private void Xor(byte b)
        {
            var a = registers[A];
            var res = (byte)(a ^ b);
            registers[A] = res;
            var f = (byte)(registers[F] & 0x28);
            if ((res & 0x80) > 0)
                f |= (byte)Fl.S;
            if (res == 0)
                f |= (byte)Fl.Z;
            if (Parity(res))
                f |= (byte)Fl.PV;
            registers[F] = f;
        }

        private void Cmp(byte b)
        {
            var a = registers[A];
            var diff = a - b;
            var f = (byte)(registers[F] & 0x28);
            if ((diff & 0x80) > 0)
                f = (byte)(f | 0x80);
            if (diff == 0)
                f = (byte)(f | 0x40);
            if ((a & 0xF) < (b & 0xF))
                f = (byte)(f | 0x10);
            if ((a > 0x80 && b > 0x80 && (sbyte)diff > 0) || (a < 0x80 && b < 0x80 && (sbyte)diff < 0))
                f = (byte)(f | 0x04);
            f = (byte)(f | 0x02);
            if (diff > 0xFF)
                f = (byte)(f | 0x01);
            registers[F] = f;
        }

        private byte Inc(byte b)
        {
            var sum = b + 1;
            var f = (byte)(registers[F] & 0x28);
            if ((sum & 0x80) > 0)
                f = (byte)(f | 0x80);
            if (sum == 0)
                f = (byte)(f | 0x40);
            if ((b & 0xF) == 0xF)
                f = (byte)(f | 0x10);
            if ((b < 0x80 && (sbyte)sum < 0))
                f = (byte)(f | 0x04);
            f = (byte)(f | 0x02);
            if (sum > 0xFF) f = (byte)(f | 0x01);
            registers[F] = f;

            return (byte)sum;
        }

        private byte Dec(byte b)
        {
            var sum = b - 1;
            var f = (byte)(registers[F] & 0x28);
            if ((sum & 0x80) > 0)
                f = (byte)(f | 0x80);
            if (sum == 0)
                f = (byte)(f | 0x40);
            if ((b & 0x0F) == 0)
                f = (byte)(f | 0x10);
            if (b == 0x80)
                f = (byte)(f | 0x04);
            f = (byte)(f | 0x02);
            registers[F] = f;

            return (byte)sum;
        }

        private static bool Parity(ushort value)
        {
            var parity = true;
            while (value > 0)
            {
                if ((value & 1) == 1) parity = !parity;
                value = (byte)(value >> 1);
            }
            return parity;
        }

        private bool JumpCondition(byte condition)
        {
            Fl mask;
            switch (condition & 0xFE)
            {
                case 0:
                    mask = Fl.Z;
                    break;
                case 2:
                    mask = Fl.C;
                    break;
                case 4:
                    mask = Fl.PV;
                    break;
                case 6:
                    mask = Fl.S;
                    break;
                default:
                    return false;
            }
            return ((registers[F] & (byte)mask) > 0) == ((condition & 1) == 1);

        }

        /// <summary>
        ///     Fetches from [PC] and increments PC
        /// </summary>
        /// <returns></returns>
        private byte Fetch()
        {
            var pc = Pc;
            var ret = mem.ReadByte(pc);
            pc++;
            registers[PC] = (byte)(pc >> 8);
            registers[PC + 1] = (byte)(pc & 0xFF);
            return ret;
        }

        private ushort Fetch16()
        {
            return (ushort)(Fetch() + (Fetch() << 8));
        }

        public void Reset()
        {
            Array.Clear(registers, 0, registers.Length);

            registers[A] = 0xFF;
            registers[F] = 0xFF;
            registers[SP] = 0xFF;
            registers[SP + 1] = 0xFF;

            //A CPU reset forces both the IFF1 and IFF2 to the reset state, which disables interrupts
            IFF1 = false;
            IFF2 = false;

            _clock = DateTime.UtcNow;
        }

        public byte[] GetState()
        {
            var length = registers.Length;
            var ret = new byte[length + 2];
            Array.Copy(registers, ret, length);
            ret[length] = (byte)(IFF1 ? 1 : 0);
            ret[length + 1] = (byte)(IFF2 ? 1 : 0);
            return ret;
        }

        private void Wait(int t)
        {
            registers[R] += (byte)((t + 3) / 4);
            const int realTicksPerTick = 250; // 4MHz
            var ticks = t * realTicksPerTick;
            var elapsed = (DateTime.UtcNow - _clock).Ticks;
            var sleep = ticks - elapsed;
            if (sleep > 0)
            {
                for (int count = 0; count < 100; count++) ;
                //Thread.Sleep(1);
                _clock = _clock + new TimeSpan(ticks);
            }
            else
            {
                _clock = DateTime.UtcNow;
            }
        }

        private void SwapReg8(byte r1, byte r2)
        {
            var t = registers[r1];
            registers[r1] = registers[r2];
            registers[r2] = t;
        }

        public void Interrupt()
        {
            throw new NotImplementedException();
        }

        [Flags]
        private enum Fl : byte
        {
            C = 0x01,
            N = 0x02,
            PV = 0x04,
            H = 0x10,
            Z = 0x40,
            S = 0x80,

            None = 0x00,
            All = 0xD7
        }
    }
}