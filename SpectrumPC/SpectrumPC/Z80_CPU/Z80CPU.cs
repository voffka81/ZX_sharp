using Speccy.Filetypes;
using Speccy.Z80_CPU;

namespace SpectrumPC.Z80_CPU
{
    /// <summary>
    /// Class rappresenting a system based on Z80
    /// </summary>
    public class Z80CPU
    {
        public Status DebugInfo => _status;
        public int NextEvent => _next_event;

        private int _next_event = 69888;

        private IBus16Bit _memory;
        private IPorts _io;
        private Status _status = new Status();

        // Memory and IO access
        private int tstates;

        // By default fetching won't be stopped
        private int _statementsToFetch = -1;

        public int TicksCount => tstates;

        public bool IsInterruptBlocked => false;
        /// <summary>
        /// Handler of a fetch event (not a standard M$ event declaration but...)
        /// </summary>
        public delegate void OnFetchHandler();

        /// <summary>
        /// Fetch event (used during debug)
        /// </summary>
        public event OnFetchHandler OnFetch;


        //Whether a half carry occured or not can be determined by looking at
        //the 3rd bit of the two arguments and the result; these are hashed
        //into this table in the form r12, where r is the 3rd bit of the
        //result, 1 is the 3rd bit of the 1st argument and 2 is the
        //third bit of the 2nd argument; the tables differ for add and subtract
        //operations 
        private byte[] LookupTable_halfcarry_add = { 0, FlagRegisterDefinition.H, FlagRegisterDefinition.H, FlagRegisterDefinition.H, 0, 0, 0, FlagRegisterDefinition.H };
        private byte[] LookupTable_halfcarry_sub = { 0, 0, FlagRegisterDefinition.H, 0, FlagRegisterDefinition.H, 0, FlagRegisterDefinition.H, FlagRegisterDefinition.H };

        //Similarly, overflow can be determined by looking at the 7th bits; again
        //the hash into this table is r12
        private byte[] LookupTable_overflow_add = { 0, 0, 0, FlagRegisterDefinition.V, FlagRegisterDefinition.V, 0, 0, 0 };
        private byte[] LookupTable_overflow_sub = { 0, FlagRegisterDefinition.V, 0, 0, 0, 0, FlagRegisterDefinition.V, 0 };


        //Some more tables; initialised in LookupTables_init()
        private byte[] LookupTable_sz53 = new byte[0x100];   // The S, Z, 5 and 3 bits of the lookup value
        private byte[] LookupTable_parity = new byte[0x100]; // The parity of the lookup value
        private byte[] LookupTable_sz53p = new byte[0x100];  // OR the above two tables together



        /// <summary>
        /// Initialise tables used to determine flags
        /// </summary>
        private void LookupTables_Init()
        {
            int i, j, k;
            byte parity;

            for (i = 0; i < 0x100; i++)
            {
                LookupTable_sz53[i] = (byte)(i & (FlagRegisterDefinition._3 | FlagRegisterDefinition._5 | FlagRegisterDefinition.S));

                j = i;
                parity = 0;
                for (k = 0; k < 8; k++)
                {
                    parity ^= (byte)(j & 1);
                    j >>= 1;
                }
                LookupTable_parity[i] = parity != 0 ? (byte)0 : FlagRegisterDefinition.P;
                LookupTable_sz53p[i] = (byte)(LookupTable_sz53[i] | LookupTable_parity[i]);
            }

            LookupTable_sz53[0] |= FlagRegisterDefinition.Z;
            LookupTable_sz53p[0] |= FlagRegisterDefinition.Z;

        }

        /// <summary>
        /// Create new system based on Z80
        /// </summary>
        /// <param name="Memory">System memory</param>
        /// <param name="IO">IO</param>
        public Z80CPU(IBus16Bit memory, IPorts io)
        {
            _memory = memory;
            _io = io;
            LookupTables_Init();
            Reset();
        }

        /// <summary>
        /// Resets the system
        /// </summary>
        public void Reset()
        {
            _status.Reset();
        }

        public void ApplyZ80Snapshot(Z80_Snapshot z80)
        {
            _status.I = z80.I;
            _status.RegisterHL_.w = z80.HL_;
            _status.RegisterDE_.w = z80.DE_;
            _status.RegisterBC_.w = z80.BC_;
            _status.RegisterAF_.w = z80.AF_;

            _status.RegisterHL.w = z80.HL;
            _status.RegisterDE.w = z80.DE;
            _status.RegisterBC.w = z80.BC;
            _status.RegisterIY.w = z80.IY;
            _status.RegisterIX.w = z80.IX;

            _status.IFF1 = z80.IFF1;
            _status.R = z80.R;
            _status.RegisterAF.w = z80.AF;
            _status.RegisterSP.w = z80.SP;
            _status.IM = z80.IM;
            _status.PC = z80.PC;

            for (int f = 0; f < 49152; f++)
            {
                _memory.WriteByte(16384 + f, z80.RAM_BANK[f]);
            }
        }

        /// <summary>
        /// Raise (process) a Z80 maskable interrupt
        /// </summary>
        public void Interrupt()
        {
            // Check if the interrupts are enabled
            if (_status.IFF1)
            {
                // The Z80 is no more halted
                _status.Halted = false;

                // Reset Interrupt Flip Flops
                // When the CPU accepts a maskable interrupt, both IFF1 and IFF2 are automatically reset,
                // inhibiting further interrupts until the programmer issues a new El instruction.
                _status.IFF1 = false;
                _status.IFF2 = false;


                // Push program counter
                Push(_status.PC);

                switch (_status.IM)
                {
                    case 0:
                        _status.PC = 0x0038;
                        IncreaseTStates(12);
                        break;
                    case 1:
                        _status.PC = 0x0038;
                        IncreaseTStates(13);
                        break;
                    case 2:
                        ushort InterruptTableAddress = (ushort)(_status.I << 8 | 0xFF);
                        _status.PC = _memory.ReadWord(InterruptTableAddress);
                        IncreaseTStates(19);
                        break;
                    default:
                        Console.Error.WriteLine("Unknown interrupt mode {0}\n", _status.IM);
                        break;
                }
            }
        }

        #region Misc functions

        /// <summary>
        /// From an _opCode returns a half register following the rules
        /// Reg _opCode
        ///   A xxxxx111
        ///   B xxxxx000
        ///   C xxxxx001
        ///   D xxxxx010
        ///   E xxxxx011
        ///   H xxxxx100
        ///   L xxxxx101
        /// </summary>
        /// <param name="_opCode">_opCode</param>
        /// <returns>The half register or null if _opCode is 110 (it is the ID for (HL))</returns>
        private HalfRegister? GetHalfRegister(byte _opCode)
        {
            switch (_opCode & 0x07)
            {
                case 0x00:
                    return _status.RegisterBC.h;
                case 0x01:
                    return _status.RegisterBC.l;
                case 0x02:
                    return _status.RegisterDE.h;
                case 0x03:
                    return _status.RegisterDE.l;
                case 0x04:
                    return _status.RegisterHL.h;
                case 0x05:
                    return _status.RegisterHL.l;
                case 0x06:
                    return null;
                case 0x07:
                    return _status.RegisterAF.h;
            }
            throw new Exception("Why am I here?");
        }


        /// <summary>
        /// From an _opCode return a register following the rules
        /// Reg         _opCode
        ///  BC         xx00xxxx
        ///  DE         xx01xxxx
        ///  HL         xx10xxxx
        ///  SP or AF   xx11xxxx
        /// </summary>
        /// <param name="_opCode">_opCode</param>
        /// <param name="ReturnSP">Checked if _opCode is 11. If this parameter is true then SP is returned else AF is returned</param>
        /// <returns>The register</returns>
        private Register GetRegister(byte _opCode, bool ReturnSP)
        {
            switch (_opCode & 0x30)
            {
                case 0x00:
                    return _status.RegisterBC;
                case 0x10:
                    return _status.RegisterDE;
                case 0x20:
                    return _status.RegisterHL;
                case 0x30:
                    if (ReturnSP)
                        return _status.RegisterSP;
                    else
                        return _status.RegisterAF;
            }
            throw new Exception("What's happening to me?");
        }


        /// <summary>
        /// Check the flag related to _opCode according with the following table:
        /// Cond _opCode   Flag Description
        /// NZ   xx000xxx  Z   Not Zero
        ///  Z   xx001xxx  Z   Zero
        /// NC   xx010xxx  C   Not Carry
        ///  C   xx011xxx  C   Carry
        /// PO   xx100xxx  P/V Parity odd  (Not parity)
        /// PE   xx101xxx  P/V Parity even (Parity)
        ///  P   xx110xxx  S   Sign positive
        ///  M   xx111xxx  S   Sign negative
        /// </summary>
        /// <param name="_opCode">The _opCode</param>
        /// <returns>True if the condition is satisfied otherwise false</returns>
        private bool CheckFlag(byte _opCode)
        {
            bool Not = false;
            byte Flag;

            // Find the right flag and the condition
            switch (_opCode >> 3 & 0x07)
            {
                case 0:
                    Not = true;
                    Flag = FlagRegisterDefinition.Z;
                    break;
                case 1:
                    Flag = FlagRegisterDefinition.Z;
                    break;
                case 2:
                    Not = true;
                    Flag = FlagRegisterDefinition.C;
                    break;
                case 3:
                    Flag = FlagRegisterDefinition.C;
                    break;
                case 4:
                    Not = true;
                    Flag = FlagRegisterDefinition.P;
                    break;
                case 5:
                    Flag = FlagRegisterDefinition.P;
                    break;
                case 6:
                    Not = true;
                    Flag = FlagRegisterDefinition.S;
                    break;
                case 7:
                    Flag = FlagRegisterDefinition.S;
                    break;
                default:
                    throw new Exception("I'm feeling bad");
            }

            // Check flag and condition
            if (Not)
                return (_status.F & Flag) == 0;
            else
                return (_status.F & Flag) != 0;


        }

        #endregion

        #region Stack access

        /// <summary>
        /// Push a register in stack and decrement SP
        /// </summary>
        /// <param name="Register">Register to push</param>
        private void Push(Register Register)
        {
            Push(Register.w);
        }

        /// <summary>
        /// Push a byte in stack and decrement SP
        /// </summary>
        /// <param name="Byte">Byte to push</param>
        private void Push(byte Byte)
        {
            _status.SP--;
            _memory.WriteByte(_status.SP, Byte);
        }

        /// <summary>
        /// Push a word in stack and decrement SP
        /// </summary>
        /// <param name="Word">Word to push</param>
        private void Push(ushort Word)
        {
            _status.SP -= 2;
            _memory.WriteWord(_status.SP, Word);
        }

        /// <summary>
        /// Pop a register from stack and increment SP
        /// </summary>
        /// <param name="Register">Register to pop</param>
        private void Pop(Register Register)
        {
            ushort MemoryContent;
            Pop(out MemoryContent);
            Register.w = MemoryContent;
        }

        /// <summary>
        /// Pop a byte from stack and increment SP
        /// </summary>
        /// <param name="Byte">Byte to pop</param>
        private void Pop(out byte Byte)
        {
            Byte = _memory.ReadByte(_status.SP);
            _status.SP++;
        }

        /// <summary>
        /// Pop a word from stack and increment SP
        /// </summary>
        /// <param name="Word">Word to pop</param>
        private void Pop(out ushort Word)
        {
            Word = _memory.ReadWord(_status.SP);
            _status.SP += 2;
        }

        #endregion

        #region ALU

        /// <summary>
        /// A logical AND operation is performed between the byte specified by the op
        /// operand and the byte contained in the Accumulator; the result is stored in
        /// the Accumulator.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is set
        /// P/V is reset if overflow; reset otherwise
        /// N is reset
        /// C is reset
        /// </summary>
        /// <param name="op">The operand</param>
        private void AND_A(byte op)
        {
            _status.A &= op;
            _status.F = (byte)
                (FlagRegisterDefinition.H |
                LookupTable_sz53p[_status.A]);
        }

        /// <summary>
        /// The op operand, along with the Carry Flag (C in the F register) is added to the
        /// contents of the Accumulator, and the result is stored in the Accumulator.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is set if carry from bit 3; reset otherwise
        /// P/V is set if overflow; reset otherwise
        /// N is reset
        /// C is set if carry from bit 7: reset otherwise
        /// </summary>
        /// <param name="op">The operand</param>
        private void ADC_A(byte op)
        {
            ushort result = (ushort)(_status.A + op + ((_status.F & FlagRegisterDefinition.C) != 0 ? 1 : 0));
            // Prepare the bits to perform the lookup
            byte lookup = (byte)((_status.A & 0x88) >> 3 | (op & 0x88) >> 2 | (result & 0x88) >> 1);
            _status.A = (byte)result;

            _status.F = (byte)
                (((result & 0x100) != 0 ? FlagRegisterDefinition.C : (byte)0) |
                LookupTable_halfcarry_add[lookup & 0x07] |
                LookupTable_overflow_add[lookup >> 4] |
                LookupTable_sz53[_status.A]);
        }


        /// <summary>
        /// The op operand, along with the Carry Flag (C in the F register) is added to the
        /// contents of HL register, and the result is stored in HL register.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is set if carry from bit 11; reset otherwise
        /// P/V is set if overflow; reset otherwise
        /// N is reset
        /// C is set if carry from bit 15: reset otherwise
        /// </summary>
        /// <param name="op">The operand</param>
        private void ADC_HL(ushort op)
        {
            uint result = (uint)(_status.HL + op + ((_status.F & FlagRegisterDefinition.C) != 0 ? 1 : 0));
            byte lookup = (byte)(
                (byte)((_status.HL & 0x8800) >> 11) |
                (byte)((op & 0x8800) >> 10) |
                (byte)((result & 0x8800) >> 9));
            _status.HL = (ushort)result;
            _status.F = (byte)
                (((result & 0x10000) != 0 ? FlagRegisterDefinition.C : (byte)0) |
                LookupTable_overflow_add[lookup >> 4] |
                _status.H & (FlagRegisterDefinition._3 | FlagRegisterDefinition._5 | FlagRegisterDefinition.S) |
                LookupTable_halfcarry_add[lookup & 0x07] |
                (_status.HL == 0 ? (byte)0 : FlagRegisterDefinition.Z));
        }

        /// <summary>
        /// The op operand is added to the
        /// contents of the Accumulator, and the result is stored in the Accumulator.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is set if carry from bit 3; reset otherwise
        /// P/V is set if overflow; reset otherwise
        /// N is reset
        /// C is set if carry from bit 7: reset otherwise
        /// </summary>
        /// <param name="op">The operand</param>
        private void ADD_A(byte op)
        {
            ushort result = (ushort)(_status.A + op);
            byte lookup = (byte)((_status.A & 0x88) >> 3 | (op & 0x88) >> 2 | (result & 0x88) >> 1);
            _status.A = (byte)result;
            _status.F = (byte)
                (((result & 0x100) != 0 ? FlagRegisterDefinition.C : (byte)0) |
                LookupTable_halfcarry_add[lookup & 0x07] |
                LookupTable_overflow_add[lookup >> 4] |
                LookupTable_sz53[_status.A]);
        }


        /// <summary>
        /// The op1 operand,  is added to the
        /// contents of op2 register, and the result is stored in op1 register.
        /// Condition Bits Affected:
        /// S is not affected
        /// Z is not affected
        /// H is set if carry from bit 11; reset otherwise
        /// P/V is not affected
        /// N is reset
        /// C is set if carry from bit 15: reset otherwise
        /// </summary>
        /// <param name="op1">First operand</param>
        /// <param name="op2">Second operand</param>
        private void ADD_16(Register op1, ushort op2)
        {
            uint result = (uint)(op1.w + op2);
            byte lookup = (byte)(
                (byte)((op1.w & 0x0800) >> 11) |
                (byte)((op2 & 0x0800) >> 10) |
                (byte)((result & 0x0800) >> 9));
            op1.w = (ushort)result;
            _status.F = (byte)(
                _status.F & (FlagRegisterDefinition.V | FlagRegisterDefinition.Z | FlagRegisterDefinition.S) |
                ((result & 0x10000) != 0 ? FlagRegisterDefinition.C : (byte)0) |
                (byte)(result >> 8 & (FlagRegisterDefinition._3 | FlagRegisterDefinition._5)) |
                LookupTable_halfcarry_add[lookup]);
        }

        /// <summary>
        /// This instruction tests bit bit in operand op and sets the Z flag accordingly.
        /// Condition Bits Affected:
        /// S is unknown
        /// Z is set if specified bit is 0; reset otherwise
        /// H is set
        /// P/V is unknown
        /// N is reset
        /// C is not affected
        /// </summary>
        /// <param name="bit">The bit to test</param>
        /// <param name="op">The operand</param>
        private void BIT(byte bit, byte op)
        {
            _status.F = (byte)(
                _status.F & FlagRegisterDefinition.C |
                FlagRegisterDefinition.H |
                op & (FlagRegisterDefinition._3 | FlagRegisterDefinition._5) |
                ((op & 0x01 << bit) != 0 ? 0 : FlagRegisterDefinition.P | FlagRegisterDefinition.Z));
        }


        /// <summary>
        /// This instruction tests bit 7 in operand op and sets the Z flag accordingly.
        /// Condition Bits Affected:
        /// S is affected if bit 7 is set
        /// Z is set if specified bit is 0; reset otherwise
        /// H is set
        /// P/V is unknown
        /// N is reset
        /// C is not affected
        /// </summary>
        /// <param name="op">The operand</param>
        private void BIT7(byte op)
        {
            _status.F = (byte)(
                _status.F & FlagRegisterDefinition.C |
                FlagRegisterDefinition.H |
                op & (FlagRegisterDefinition._3 | FlagRegisterDefinition._5) |
                ((op & 0x80) != 0 ? FlagRegisterDefinition.S : (byte)(FlagRegisterDefinition.P | FlagRegisterDefinition.Z)));
        }


        /// <summary>
        /// Operation: (SP-1) = PCH, (SP-2) = PCL, PC = nn
        /// Description: The current contents of the Program Counter (PC) are pushed onto the top
        /// of the external memory stack. The operands nn are then loaded to the PC to point to the 
        /// address in memory where the first Op Code of a subroutine is to be fetched. At the 
        /// end of the subroutine, a RETurn instruction can be used to return to the original program 
        /// flow by popping the top of the stack back to the PC. The push is accomplished by first 
        /// decrementing the current contents of the Stack Pointer (register pair SP), loading the 
        /// high-order byte of the PC contents to the memory address now pointed to by the SP; then
        /// decrementing SP again, and loading the low order byte of the PC contents to the top of 
        /// stack.
        /// Because this is a 3-byte instruction, the Program Counter was incremented by three before 
        /// the push is executed.
        /// </summary>
        private void CALL()
        {
            ushort address = _memory.ReadWord(_status.PC);
            Push((ushort)(_status.PC + 2));
            _status.PC = address;
        }


        /// <summary>
        /// The Carry flag in the F register is inverted.
        /// Condition Bits Affected:
        /// S is not affected
        /// Z is not affected
        /// H, previous carry is copied
        /// P/V is not affected
        /// N is reset
        /// C is set if CY was 0 before operation; reset otherwise
        /// </summary>
        private void CCF()
        {
            _status.F = (byte)(
                _status.F & (FlagRegisterDefinition.P | FlagRegisterDefinition.Z | FlagRegisterDefinition.S) |
                ((_status.F & FlagRegisterDefinition.C) != 0 ? FlagRegisterDefinition.H : FlagRegisterDefinition.C) |
                _status.A & (FlagRegisterDefinition._3 | FlagRegisterDefinition._5));
        }


        /// <summary>
        /// The contents of the op operand are compared (same of sub) with the contents of the
        /// Accumulator. If there is a true compare, the Z flag is set. The execution of
        /// this instruction does not affect the contents of the Accumulator.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is set if borrow from bit 4; reset otherwise
        /// P/V is set if overflow; reset otherwise
        /// N is set
        /// C is set if borrow; reset otherwise
        /// </summary>
        /// <param name="op">The operand</param>
        private void CP(byte op)
        {
            ushort result = (ushort)(_status.A - op);
            byte lookup = (byte)((_status.A & 0x88) >> 3 | (op & 0x88) >> 2 | (result & 0x88) >> 1);
            _status.F = (byte)(
                ((result & 0x100) != 0 ? FlagRegisterDefinition.C : result != 0 ? (byte)0 : FlagRegisterDefinition.Z) |
                FlagRegisterDefinition.N |
                LookupTable_halfcarry_sub[lookup & 0x07] |
                LookupTable_overflow_sub[lookup >> 4] |
                op & (FlagRegisterDefinition._3 | FlagRegisterDefinition._5) |
                result & FlagRegisterDefinition.S);
        }



        /// <summary>
        /// ???????????????????????????????????????????????????????????
        /// </summary>
        private delegate void Instruction(byte target);
        /* Macro for the {DD,FD} CB dd xx rotate/shift instructions */
        /// <summary>
        /// 
        /// </summary>
        /// <param name="time"></param>
        /// <param name="target"></param>
        /// <param name="instruction"></param>
        /// <param name="tempaddr"></param>
        private void DDFDCB_ROTATESHIFT(byte time, byte target, Instruction instruction, ushort tempaddr)
        {
            IncreaseTStates(time);
            target = _memory.ReadByte(tempaddr);
            instruction(target);
            _memory.WriteByte(tempaddr, target);
        }

        /// <summary>
        /// The contents of the memory location addressed by the HL register pair is
        /// compared with the contents of the Accumulator. In case of a true
        /// compare, a condition bit is set. The HL and Byte Counter (register pair
        /// BC) are decremented.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if A is (HL); reset otherwise
        /// H is set if borrow from bit 4; reset otherwise
        /// P/V is set if BC -1 is not 0; reset otherwise
        /// N is set
        /// C is not affected
        /// </summary>
        private void CPD()
        {
            CPx();
            _status.HL--;
        }

        /// <summary>
        /// The contents of the memory location addressed by the HL register pair is
        /// compared with the contents of the Accumulator. In case of a true compare,
        /// a condition bit is set. The HL and BC (Byte Counter) register pairs are
        /// decremented. If decrementing causes the BC to go to zero or if A = (HL),
        /// the instruction is terminated. If BC is not zero and A = (HL), the program
        /// counter is decremented by two and the instruction is repeated. Interrupts are
        /// recognized and two refresh cycles execute after each data transfer. When
        /// BC is set to zero, prior to instruction execution, the instruction loops
        /// through 64 Kbytes if no match is found.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if A = (HL); reset otherwise
        /// H is set if borrow form bit 4; reset otherwise
        /// P/V is set if BC -1 != 0; reset otherwise
        /// N is set
        /// C is not affected
        /// </summary>
        private void CPDR()
        {
            CPx();
            _status.HL--;
            if ((_status.F & (FlagRegisterDefinition.V | FlagRegisterDefinition.Z)) == FlagRegisterDefinition.V)
            {
                IncreaseTStates(5);
                _status.PC -= 2;
            }
        }



        /// <summary>
        /// The contents of the memory location addressed by the HL register is
        /// compared with the contents of the Accumulator. In case of a true compare,
        /// a condition bit is set. Then HL is incremented and the Byte Counter
        /// (register pair BC) is decremented.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if A is (HL); reset otherwise
        /// H is set if borrow from bit 4; reset otherwise
        /// P/V is set if BC -1 is not 0; reset otherwise
        /// N is set
        /// C is not affected
        /// </summary>
        private void CPI()
        {
            CPx();
            _status.HL++;
        }

        /// <summary>
        /// The contents of the memory location addressed by the HL register pair is
        /// compared with the contents of the Accumulator. In case of a true compare, a
        /// condition bit is set. HL is incremented and the Byte Counter (register pair
        /// BC) is decremented. If decrementing causes BC to go to zero or if A = (HL),
        /// the instruction is terminated. If BC is not zero and A != (HL), the program
        /// counter is decremented by two and the instruction is repeated. Interrupts are
        /// recognized and two refresh cycles are executed after each data transfer.
        /// If BC is set to zero before instruction execution, the instruction loops
        /// through 64 Kbytes if no match is found.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if A equals (HL); reset otherwise
        /// H is set if borrow from bit 4; reset otherwise
        /// P/V is set if BC -1 does not equal 0; reset otherwise
        /// N is set
        /// C is not affected
        /// </summary>
        private void CPIR()
        {
            CPx();
            _status.HL++;
            if ((_status.F & (FlagRegisterDefinition.V | FlagRegisterDefinition.Z)) == FlagRegisterDefinition.V)
            {
                IncreaseTStates(5);
                _status.PC -= 2;
            }
        }


        /// <summary>
        /// Used by CPD, CPI, CPDR, CPIR
        /// The contents of the memory location addressed by the HL register is
        /// compared with the contents of the Accumulator. In case of a true compare,
        /// a condition bit is set. Then the Byte Counter(register pair BC) is 
        /// decremented.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if A is (HL); reset otherwise
        /// H is set if borrow from bit 4; reset otherwise
        /// P/V is set if BC -1 is not 0; reset otherwise
        /// N is set
        /// C is not affected
        /// </summary>
        private void CPx()
        {
            byte _b = _memory.ReadByte(_status.HL);
            byte result = (byte)(_status.A - _b);
            byte lookup = (byte)((_status.A & 0x08) >> 3 | (_b & 0x08) >> 2 | (result & 0x08) >> 1);
            _status.BC--;
            _status.F = (byte)(_status.F & FlagRegisterDefinition.C | (_status.BC != 0 ? (byte)(FlagRegisterDefinition.V | FlagRegisterDefinition.N) : FlagRegisterDefinition.N) | LookupTable_halfcarry_sub[lookup] | (result != 0 ? (byte)0 : FlagRegisterDefinition.Z) | result & FlagRegisterDefinition.S);
            if ((_status.F & FlagRegisterDefinition.H) != 0)
                result--;
            _status.F |= (byte)(result & FlagRegisterDefinition._3 | ((result & 0x02) != 0 ? FlagRegisterDefinition._5 : (byte)0));
        }


        /// <summary>
        /// The contents of the Accumulator (register A) are inverted (one’s
        /// complement).
        /// Condition Bits Affected:
        /// S is not affected
        /// Z is not affected
        /// H is set
        /// P/V is not affected
        /// N is set
        /// C is not affected
        /// </summary>
        private void CPL()
        {
            _status.A ^= 0xFF;
            _status.F = (byte)(_status.F & (FlagRegisterDefinition.C | FlagRegisterDefinition.P | FlagRegisterDefinition.Z | FlagRegisterDefinition.S) | _status.A & (FlagRegisterDefinition._3 | FlagRegisterDefinition._5) | FlagRegisterDefinition.N | FlagRegisterDefinition.H);
        }


        /// <summary>
        /// This instruction conditionally adjusts the Accumulator for BCD addition and
        /// subtraction operations. For addition (ADD, ADC, INC) or subtraction (SUB,
        /// SBC, DEC, NEG)
        /// </summary>
        private void DAA()
        {
            byte add = 0;
            byte carry = (byte)(_status.F & FlagRegisterDefinition.C);
            if ((_status.F & FlagRegisterDefinition.H) != 0 || (_status.A & 0x0F) > 9)
                add = 6;
            if (carry != 0 || _status.A > 0x9F)
                add |= 0x60;
            if (_status.A > 0x99)
                carry = 1;
            if ((_status.F & FlagRegisterDefinition.N) != 0)
            {
                SUB(add);
            }
            else
            {
                if (_status.A > 0x90 && (_status.A & 0x0F) > 9)
                    add |= 0x60;
                ADD_A(add);
            }
            _status.F = (byte)(_status.F & ~(FlagRegisterDefinition.C | FlagRegisterDefinition.P) | carry | LookupTable_parity[_status.A]);
        }

        /// <summary>
        /// The byte specified by the op operand is decremented
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is set if borrow from bit 4, reset otherwise
        /// P/V is set if m was 80H before operation; reset otherwise
        /// N is set
        /// C is not affected
        /// </summary>
        /// <param name="op">The operand</param>
        private void DEC(HalfRegister op)
        {
            _status.F = (byte)(_status.F & FlagRegisterDefinition.C | ((op.Value & 0x0F) != 0 ? (byte)0 : FlagRegisterDefinition.H) | FlagRegisterDefinition.N);
            op.Value--;
            _status.F |= (byte)((op.Value == 0x79 ? FlagRegisterDefinition.V : (byte)0) | LookupTable_sz53[op.Value]);
        }


        /// <summary>
        /// The contents of port are placed on the address bus to select the I/O device at 
        /// one of 256 possible ports.
        /// Usually port is BC (register BC) or An (Accumulator + number)
        /// Then one byte from the selected port is placed on
        /// the data bus and written to register reg in the CPU.
        /// The flags are affected, checking the input data.
        /// Condition Bits Affected:
        /// S is set if input data is negative; reset otherwise
        /// Z is set if input data is zero; reset otherwise
        /// H is reset
        /// P/V is set if parity is even; reset otherwise
        /// N is reset
        /// C is not affected
        /// </summary>
        /// <param name="reg">The register</param>
        /// <param name="port">The port</param>
        private void IN(HalfRegister reg, ushort port)
        {
            reg.Value = _io.ReadByte(port);
            _status.F = (byte)(_status.F & FlagRegisterDefinition.C | LookupTable_sz53p[reg.Value]);
        }


        /// <summary>
        /// The byte contained in op is incremented.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is set if carry from bit 3; reset otherwise
        /// P/V is set if op was 7FH before operation; reset otherwise
        /// N is reset
        /// C is not affected
        /// </summary>
        /// <param name="op">The operand</param>
        private void INC(HalfRegister op)
        {
            op.Value++;
            _status.F = (byte)(
                _status.F & FlagRegisterDefinition.C |
                (op.Value == 0x80 ? FlagRegisterDefinition.V : (byte)0) |
                ((op.Value & 0x0F) != 0 ? (byte)0 : FlagRegisterDefinition.H) |
                (op.Value != 0 ? (byte)0 : FlagRegisterDefinition.Z) |
                LookupTable_sz53[op.Value]);
        }


        /// <summary>
        /// The contents of register C are placed on the bottom half (A0 through A7) of
        /// the address bus to select the I/O device at one of 256 possible ports.
        /// Register B may be used as a byte counter, and its contents are placed on the
        /// top half (A8 through A15) of the address bus at this time. Then one byte
        /// from the selected port is placed on the data bus and written to the CPU. The
        /// contents of the HL register pair are placed on the address bus and the input
        /// byte is written to the corresponding location of memory. Finally, the byte
        /// counter and register pair HL are decremented.
        /// Condition Bits Affected:
        /// S is unknown
        /// Z is set if B–1 = 0; reset otherwise
        /// H is unknown
        /// P/V is unknown
        /// N is set
        /// C is not affected
        /// </summary>
        private void IND()
        {
            INx();
            _status.HL--;
        }


        /// <summary>
        /// The contents of register C are placed on the bottom half (A0 through A7)
        /// of the address bus to select the I/O device at one of 256 possible ports.
        /// Register B is used as a byte counter, and its contents are placed on the top
        /// half (A8 through A15) of the address bus at this time. Then one byte from
        /// the selected port is placed on the data bus and written to the CPU. The
        /// contents of the HL register pair are placed on the address bus and the
        /// input byte is written to the corresponding location of memory. Then HL
        /// and the byte counter are decremented. If decrementing causes B to go to
        /// zero, the instruction is terminated. If B is not zero, the PC is decremented
        /// by two and the instruction repeated. Interrupts are recognized and two
        /// refresh cycles are executed after each data transfer.
        /// When B is set to zero prior to instruction execution, 256 bytes of data are
        /// input.
        /// Condition Bits Affected:
        /// S is unknown
        /// Z is set
        /// H is unknown
        /// P/V is unknown
        /// N is set
        /// </summary>
        private void INDR()
        {
            INx();
            _status.HL--;
            if (_status.B != 0)
            {
                IncreaseTStates(5);
                _status.PC -= 2;
            }
        }


        /// <summary>
        /// The contents of register C are placed on the bottom half (A0 through A7) of
        /// the address bus to select the I/O device at one of 256 possible ports.
        /// Register B may be used as a byte counter, and its contents are placed on the
        /// top half (A8 through A15) of the address bus at this time. Then one byte
        /// from the selected port is placed on the data bus and written to the CPU. The
        /// contents of the HL register pair are then placed on the address bus and the
        /// input byte is written to the corresponding location of memory. Finally, the
        /// byte counter is decremented and register pair HL is incremented.
        /// Condition Bits Affected:
        /// S is unknown
        /// Z is set if B–1 = 0, reset otherwise
        /// H is unknown
        /// P/V is unknown
        /// N is set
        /// C is not affected
        /// </summary>
        private void INI()
        {
            INx();
            _status.HL++;
        }

        /// <summary>
        /// The contents of register C are placed on the bottom half (A0 through A7) of
        /// the address bus to select the I/O device at one of 256 possible ports.
        /// Register B is used as a byte counter, and its contents are placed on the top
        /// half (A8 through A15) of the address bus at this time. Then one byte from
        /// the selected port is placed on the data bus and written to the CPU. The
        /// contents of the HL register pair are placed on the address bus and the input
        /// byte is written to the corresponding location of memory. Then register pair
        /// HL is incremented, the byte counter is decremented. If decrementing causes
        /// B to go to zero, the instruction is terminated. If B is not zero, the PC is
        /// decremented by two and the instruction repeated. Interrupts are recognized
        /// and two refresh cycles execute after each data transfer.
        /// Note: if B is set to zero prior to instruction execution, 256 bytes of data
        /// are input.
        /// Condition Bits Affected:
        /// S is unknown
        /// Z is set
        /// H is unknown
        /// P/V is unknown
        /// N is set
        /// C is not affected
        /// </summary>
        private void INIR()
        {
            INx();
            _status.HL++;
            if (_status.B != 0)
            {
                IncreaseTStates(5);
                _status.PC -= 2;
            }
        }


        /// <summary>
        /// Used by INI, IND, INIR, INDR
        /// The contents of register C are placed on the bottom half (A0 through A7) of
        /// the address bus to select the I/O device at one of 256 possible ports.
        /// Register B may be used as a byte counter, and its contents are placed on the
        /// top half (A8 through A15) of the address bus at this time. Then one byte
        /// from the selected port is placed on the data bus and written to the CPU. The
        /// contents of the HL register pair are then placed on the address bus and the
        /// input byte is written to the corresponding location of memory. Finally, the
        /// byte counter is decremented. Register pair HL must be incremented/decremented 
        /// by callers.
        /// Condition Bits Affected:
        /// S is unknown
        /// Z is set if B–1 = 0, reset otherwise
        /// H is unknown
        /// P/V is unknown
        /// N is set
        /// C is not affected
        /// </summary>
        private void INx()
        {
            byte _b = _io.ReadByte(_status.BC);
            _memory.WriteByte(_status.HL, _b);
            _status.B--;
            _status.F = (byte)(((_b & 0x80) != 0 ? FlagRegisterDefinition.N : (byte)0) | LookupTable_sz53[_status.B]);
        }


        /// <summary>
        /// This 2-byte instruction transfers a byte of data from the memory location
        /// addressed by the contents of the HL register pair to the memory location
        /// addressed by the contents of the DE register pair. Then both of these register
        /// pairs including the BC (Byte Counter) register pair are decremented.
        /// Condition Bits Affected:
        /// S is not affected
        /// Z is not affected
        /// H is reset
        /// P/V is set if BC -1 != 0; reset otherwise
        /// N is reset
        /// C is not affected
        /// </summary>
        private void LDD()
        {
            LDx();
            _status.DE--;
            _status.HL--;
        }


        /// <summary>
        /// This 2-byte instruction transfers a byte of data from the memory
        /// location addressed by the contents of the HL register pair to the memory
        /// location addressed by the contents of the DE register pair. Then both of
        /// these registers, as well as the BC (Byte Counter), are decremented. If
        /// decrementing causes BC to go to zero, the instruction is terminated. If
        /// BC is not zero, the program counter is decremented by two and the
        /// instruction is repeated. Interrupts are recognized and two refresh cycles
        /// execute after each data transfer.
        /// When BC is set to zero, prior to instruction execution, the instruction loops
        /// through 64 Kbytes.
        /// Condition Bits Affected:
        /// S is not affected
        /// Z is not affected
        /// H is reset
        /// P/V is reset
        /// N is reset
        /// </summary>
        private void LDDR()
        {
            LDx();
            _status.HL--;
            _status.DE--;
            if (_status.BC != 0)
            {
                IncreaseTStates(4);
                _status.PC -= 2;
            }

        }


        /// <summary>
        /// A byte of data is transferred from the memory location addressed, by the
        /// contents of the HL register pair to the memory location addressed by the
        /// contents of the DE register pair. Then both these register pairs are
        /// incremented and the BC (Byte Counter) register pair is decremented.
        /// Condition Bits Affected:
        /// S is not affected
        /// Z is not affected
        /// H is reset
        /// P/V is set if BC -1 != 0; reset otherwise
        /// N is reset
        /// C is not affected
        /// </summary>
        private void LDI()
        {
            LDx();
            _status.DE++;
            _status.HL++;
        }



        /// <summary>
        /// This 2-byte instruction transfers a byte of data from the memory location
        /// addressed by the contents of the HL register pair to the memory location
        /// addressed by the DE register pair. Both these register pairs are incremented
        /// and the BC (Byte Counter) register pair is decremented. If decrementing
        /// causes the BC to go to zero, the instruction is terminated. If BC is not zero,
        /// the program counter is decremented by two and the instruction is repeated.
        /// Interrupts are recognized and two refresh cycles are executed after each
        /// data transfer. When BC is set to zero prior to instruction execution, the
        /// instruction loops through 64 Kbytes.
        /// Condition Bits Affected:
        /// S is not affected 
        /// Z is not affected 
        /// H is reset 
        /// P/V is reset 
        /// N is reset 
        /// C is not affected
        /// </summary>
        private void LDIR()
        {
            LDx();
            _status.DE++;
            _status.HL++;
            if (_status.BC != 0)
            {
                IncreaseTStates(5);
                _status.PC -= 2;
            }
        }


        /// <summary>
        /// Used by LDI, LDD, LDIR, LDDR
        /// A byte of data is transferred from the memory location addressed, by the
        /// contents of the HL register pair to the memory location addressed by the
        /// contents of the DE register pair. Then the BC (Byte Counter) register 
        /// pair is decremented. Increase/Decrease of DE/HL registers must be done
        /// by the caller.
        /// Condition Bits Affected:
        /// S is not affected
        /// Z is not affected
        /// H is reset
        /// P/V is set if BC -1 != 0; reset otherwise
        /// N is reset
        /// C is not affected
        /// </summary>
        private void LDx()
        {
            byte _b = _memory.ReadByte(_status.HL);
            _memory.WriteByte(_status.DE, _b);
            _status.BC--;
            _b += _status.A;
            _status.F = (byte)(_status.F & (FlagRegisterDefinition.C | FlagRegisterDefinition.Z | FlagRegisterDefinition.S) | (_status.BC != 0 ? FlagRegisterDefinition.V : (byte)0) | _b & FlagRegisterDefinition._3 | ((_b & 0x02) != 0 ? FlagRegisterDefinition._5 : (byte)0));
        }


        /// <summary>
        /// LD (nn), dd
        /// </summary>
        /// <param name="register">dd register</param>
        private void LD_nndd(Register register)
        {
            // Read write address from PC address
            ushort address = _memory.ReadWord(_status.PC);
            _status.PC += 2;

            _memory.WriteWord(address, register.w);
        }


        /// <summary>
        /// LD dd, (nn)
        /// </summary>
        /// <param name="register"></param>
        private void LD_ddnn(Register register)
        {
            // Read write address from PC address
            ushort address = _memory.ReadWord(_status.PC);
            _status.PC += 2;

            register.w = _memory.ReadWord(address);
        }


        /// <summary>
        /// Jump to absolute address
        /// </summary>
        private void JP()
        {
            _status.PC = _memory.ReadWord(_status.PC);
        }


        /// <summary>
        /// Jump to relative address
        /// </summary>
        private void JR()
        {
            _status.PC = (ushort)(_status.PC + (sbyte)_memory.ReadByte(_status.PC));
        }

        /// <summary>
        /// A logical OR operation is performed between the byte specified by the op
        /// operand and the byte contained in the Accumulator; the result is stored in
        /// the Accumulator.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is reset
        /// P/V is set if overflow; reset otherwise
        /// N is reset
        /// C is reset
        /// </summary>
        /// <param name="op">The operand</param>
        private void OR(byte op)
        {
            _status.A |= op;
            _status.F = LookupTable_sz53p[_status.A];
        }


        /// <summary>
        /// The contents of the HL register pair are placed on the address bus to select a
        /// location in memory. The byte contained in this memory location is
        /// temporarily stored in the CPU. Then, after the byte counter (B) is
        /// decremented, the contents of register C are placed on the bottom half (A0
        /// through A7) of the address bus to select the I/O device at one of 256
        /// possible ports. Register B may be used as a byte counter, and its
        /// decremented value is placed on the top half (A8 through A15) of the
        /// address bus at this time. Next, the byte to be output is placed on the data bus
        /// and written to the selected peripheral device. Finally, the register pair HL is
        /// decremented.
        /// Condition Bits Affected:
        /// S is unknown
        /// Z is set if B–1 = 0; reset otherwise
        /// H is unknown
        /// P/V is unknown
        /// N is set
        /// C is not affected
        /// </summary>
        private void OUTD()
        {
            OUTx();
            _status.HL--;
        }


        /// <summary>
        /// The contents of the HL register pair are placed on the address bus to select a
        /// location in memory. The byte contained in this memory location is tempo-rarily
        /// stored in the CPU. Then, after the byte counter (B) is decremented,
        /// the contents of register C are placed on the bottom half (A0 through A7) of
        /// the address bus to select the I/O device at one of 256 possible ports. Regis-ter
        /// B may be used as a byte counter, and its decremented value is placed on
        /// the top half (A8 through A15) of the address bus at this time. Next, the byte
        /// to be output is placed on the data bus and written to the selected peripheral
        /// device. Then, register pair HL is decremented and if the decremented B
        /// register is not zero, the Program Counter (PC) is decremented by two and
        /// the instruction is repeated. If B has gone to zero, the instruction is termi-nated.
        /// Interrupts are recognized and two refresh cycles are executed after
        /// each data transfer.
        /// Note: When B is set to zero prior to instruction execution, the instruc-tion
        /// outputs 256 bytes of data.
        /// Condition Bits Affected:
        /// S is unknown
        /// Z is set
        /// H is unknown
        /// P/V is unknown
        /// N is set
        /// C is not affected
        /// </summary>
        private void OTDR()
        {
            OUTx();
            _status.HL--;
            if (_status.B != 0)
            {
                IncreaseTStates(5);
                _status.PC -= 2;
            }
        }


        /// <summary>
        /// The contents of the HL register pair are placed on the address bus to select a
        /// location in memory. The byte contained in this memory location is
        /// temporarily stored in the CPU. Then, after the byte counter (B) is
        /// decremented, the contents of register C are placed on the bottom half (A0
        /// through A7) of the address bus to select the I/O device at one of 256
        /// possible ports. Register B may be used as a byte counter, and its
        /// decremented value is placed on the top half (A8 through A15) of the
        /// address bus. The byte to be output is placed on the data bus and written to a
        /// selected peripheral device. Finally, the register pair HL is incremented.
        /// Condition Bits Affected:
        /// S is unknown
        /// Z is set if B–1 = 0; reset otherwise
        /// H is unknown
        /// P/V is unknown
        /// N is set
        /// C is not affected
        /// </summary>
        private void OUTI()
        {
            OUTx();
            _status.HL++;
        }


        /// <summary>
        /// The contents of the HL register pair are placed on the address bus to select
        /// a location in memory. The byte contained in this memory location is tempo-rarily
        /// stored in the CPU. Then, after the byte counter (B) is decremented, the
        /// contents of register C are placed on the bottom half (A0 through A7) of the
        /// address bus to select the I/O device at one of 256 possible ports. Register B
        /// may be used as a byte counter, and its decremented value is placed on the top
        /// half (A8 through A15) of the address bus at this time. Next, the byte to be
        /// output is placed on the data bus and written to the selected peripheral device.
        /// Then register pair HL is incremented. If the decremented B register is not
        /// zero, the Program Counter (PC) is decremented by two and the instruction is
        /// repeated. If B has gone to zero, the instruction is terminated. Interrupts are
        /// recognized and two refresh cycles are executed after each data transfer.
        /// Note: When B is set to zero prior to instruction execution, the instruc-tion
        /// outputs 256 bytes of data.
        /// Condition Bits Affected:
        /// S is unknown
        /// Z is set
        /// H is unknown
        /// P/V is unknown
        /// N is set
        /// C is not affected
        /// </summary>
        private void OTIR()
        {
            OUTx();
            _status.HL++;
            if (_status.B != 0)
            {
                IncreaseTStates(5);
                _status.PC -= 2;
            }
        }

        /// <summary>
        /// Used by OUTI, OUTD, OTIR, OTDR
        /// The contents of the HL register pair are placed on the address bus to select a
        /// location in memory. The byte contained in this memory location is
        /// temporarily stored in the CPU. Then, after the byte counter (B) is
        /// decremented, the contents of register C are placed on the bottom half (A0
        /// through A7) of the address bus to select the I/O device at one of 256
        /// possible ports. Register B may be used as a byte counter, and its
        /// decremented value is placed on the top half (A8 through A15) of the
        /// address bus. The byte to be output is placed on the data bus and written to a
        /// selected peripheral device. The register pair HL must be incremented or decremented
        /// by the callre.
        /// Condition Bits Affected:
        /// S is unknown
        /// Z is set if B–1 = 0; reset otherwise
        /// H is unknown
        /// P/V is unknown
        /// N is set
        /// C is not affected
        /// </summary>
        private void OUTx()
        {
            byte _b = _memory.ReadByte(_status.HL);
            _status.B--;
            _io.WriteByte(_status.BC, _b);
            _status.F = (byte)(((_b & 0x80) != 0 ? FlagRegisterDefinition.N : (byte)0) | LookupTable_sz53[_status.B]);
        }



        /// <summary>
        /// The byte at the memory location specified by the contents of the Stack
        /// Pointer (SP) register pair is moved to the low order eight bits of the
        /// Program Counter (PC). The SP is now incremented and the byte at the
        /// memory location specified by the new contents of this instruction is fetched
        /// from the memory location specified by the PC. This instruction is normally
        /// used to return to the main line program at the completion of a routine
        /// entered by a CALL instruction.
        /// Condition Bits Affected: None
        /// </summary>
        private void RET()
        {
            ushort _PC;
            Pop(out _PC);
            _status.PC = _PC;
        }

        /// <summary>
        /// The contents of the op operand are rotated left 1-bit position. The content of
        /// bit 7 is copied to the Carry flag and the previous content of the Carry flag is
        /// copied to bit 0.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is reset
        /// P/V is set if parity even; reset otherwise
        /// N is reset
        /// C is data from bit 7 of source register
        /// </summary>
        /// <param name="op">The operand</param>
        private void RL(HalfRegister op)
        {
            byte _op = op.Value;
            op.Value = (byte)(op.Value << 1 | _status.F & FlagRegisterDefinition.C);
            _status.F = (byte)(_op >> 7 | LookupTable_sz53p[op.Value]);
        }

        /// <summary>
        /// The contents of the Accumulator (register A) are rotated left 1-bit position
        /// through the Carry flag. The previous content of the Carry flag is copied to
        /// bit 0. Bit 0 is the least-significant bit.
        /// Condition Bits Affected:
        /// S is not affected
        /// Z is not affected
        /// H is reset
        /// P/V is not affected
        /// N is reset
        /// C is data from bit 7 of Accumulator
        /// </summary>
        private void RLA()
        {
            byte _A = _status.A;
            _status.A = (byte)(_status.A << 1 | _status.F & FlagRegisterDefinition.C);
            _status.F = (byte)(
                _status.F & (FlagRegisterDefinition.P | FlagRegisterDefinition.Z | FlagRegisterDefinition.S) |
                _status.A & (FlagRegisterDefinition._3 | FlagRegisterDefinition._5) |
                _A >> 7);
        }

        /// <summary>
        /// The contents of the Accumulator (register A) are rotated left 1-bit position.
        /// The sign bit (bit 7) is copied to the Carry flag and also to bit 0. Bit 0 is the
        /// least-significant bit.
        /// Condition Bits Affected:
        /// S is not affected
        /// Z is not affected
        /// H is reset
        /// P/V is not affected
        /// N is reset
        /// C is data from bit 7 of Accumulator
        /// </summary>
        private void RLCA()
        {
            _status.A = (byte)(_status.A << 1 | _status.A >> 7);
            _status.F = (byte)(
                _status.F & (FlagRegisterDefinition.P | FlagRegisterDefinition.Z | FlagRegisterDefinition.S) |
                _status.A & (FlagRegisterDefinition.C | FlagRegisterDefinition._3 | FlagRegisterDefinition._5));
        }


        /// <summary>
        /// The contents of operand op are rotated left 1-bit position. The content of bit 7
        /// is copied to the Carry flag and also to bit 0.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is reset
        /// P/V is set if parity even; reset otherwise
        /// N is reset
        /// C is data from bit 7 of source register
        /// </summary>
        /// <param name="op"></param>
        private void RLC(HalfRegister op)
        {
            op.Value = (byte)(op.Value << 1 | op.Value >> 7);
            _status.F = (byte)(
                op.Value & FlagRegisterDefinition.C |
                LookupTable_sz53p[op.Value]);
        }


        /// <summary>
        /// The contents of the low order four bits (bits 3, 2, 1, and 0) of the memory
        /// location (HL) are copied to the high order four bits (7, 6, 5, and 4) of that
        /// same memory location; the previous contents of those high order four bits
        /// are copied to the low order four bits of the Accumulator (register A); and
        /// the previous contents of the low order four bits of the Accumulator are
        /// copied to the low order four bits of memory location (HL). The contents of
        /// the high order bits of the Accumulator are unaffected.
        /// Note: (HL) means the memory location specified by the contents of the
        /// HL register pair.
        /// Condition Bits Affected:
        /// S is set if Accumulator is negative after operation; reset otherwise
        /// Z is set if Accumulator is zero after operation; reset otherwise
        /// H is reset
        /// P/V is set if parity of Accumulator is even after operation; reset otherwise
        /// N is reset
        /// C is not affected
        /// </summary>
        private void RLD()
        {
            byte _b = _memory.ReadByte(_status.HL);
            _memory.WriteByte(_status.HL, (byte)(_b << 4 | _status.A & 0x0F));
            _status.A = (byte)(_status.A & 0xF0 | _b >> 4);
            _status.F = (byte)(_status.F & FlagRegisterDefinition.C | LookupTable_sz53p[_status.A]);
        }



        /// <summary>
        /// The contents of operand op are rotated right 1-bit position through the Carry
        /// flag. The content of bit 0 is copied to the Carry flag and the previous
        /// content of the Carry flag is copied to bit 7. Bit 0 is the least-significant bit.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is reset
        /// P/V is set if parity even; reset otherwise,
        /// N is reset
        /// C is data from bit 0 of source register
        /// </summary>
        /// <param name="op">The operand</param>
        private void RR(HalfRegister op)
        {
            byte _op = op.Value;
            op.Value = (byte)(op.Value >> 1 | _status.F << 7);
            _status.F = (byte)(
                _op & FlagRegisterDefinition.C |
                LookupTable_sz53p[op.Value]);
        }

        /// <summary>
        /// The contents of the Accumulator (register A) are rotated right 1-bit position
        /// through the Carry flag. The previous content of the Carry flag is copied to
        /// bit 7. Bit 0 is the least-significant bit.
        /// Condition Bits Affected:
        /// S is not affected
        /// Z is not affected
        /// H is reset
        /// P/V is not affected
        /// N is reset
        /// C is data from bit 0 of Accumulator
        /// </summary>
        private void RRA()
        {
            byte _A = _status.A;
            _status.A = (byte)(_status.A >> 1 | _status.F << 7);
            _status.F = (byte)(
                _status.F & (FlagRegisterDefinition.P | FlagRegisterDefinition.Z | FlagRegisterDefinition.S) |
                _status.A & (FlagRegisterDefinition._3 | FlagRegisterDefinition._5) |
                _A & FlagRegisterDefinition.C);
        }


        /// <summary>
        /// The contents of the op operand are rotated right 1-bit position. The content
        /// of bit 0 is copied to the Carry flag and also to bit 7. Bit 0 is the least-significant
        /// bit.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is reset
        /// P/V is set if parity even; reset otherwise,
        /// N is reset
        /// C is data from bit 0 of source register
        /// </summary>
        /// <param name="op">The operand</param>
        private void RRC(HalfRegister op)
        {
            _status.F = (byte)(op.Value & FlagRegisterDefinition.C);
            op.Value = (byte)(op.Value >> 1 | op.Value << 7);
            _status.F |= LookupTable_sz53p[op.Value];
        }

        /// <summary>
        /// The contents of the Accumulator (register A) are rotated right 1-bit
        /// position. Bit 0 is copied to the Carry flag and also to bit 7. Bit 0 is the leastsignificant
        /// bit.
        /// Condition Bits Affected:
        /// S is not affected
        /// Z is not affected
        /// H is reset
        /// P/V is not affected
        /// N is reset
        /// C is data from bit 0 of Accumulator
        /// </summary>
        private void RRCA()
        {
            _status.F = (byte)(_status.F & (FlagRegisterDefinition.P | FlagRegisterDefinition.Z | FlagRegisterDefinition.S) | _status.A & FlagRegisterDefinition.C);
            _status.A = (byte)(_status.A >> 1 | _status.A << 7);
            _status.F |= (byte)(_status.A & (FlagRegisterDefinition._3 | FlagRegisterDefinition._5));
        }


        /// <summary>
        /// The contents of the low order four bits (bits 3, 2, 1, and 0) of memory
        /// location (HL) are copied to the low order four bits of the Accumulator
        /// (register A). The previous contents of the low order four bits of the
        /// Accumulator are copied to the high order four bits (7, 6, 5, and 4) of
        /// location (HL); and the previous contents of the high order four bits of (HL)
        /// are copied to the low order four bits of (HL). The contents of the high order
        /// bits of the Accumulator are unaffected.
        /// (HL) means the memory location specified by the contents of the HL
        /// register pair.
        /// Condition Bits Affected:
        /// S is set if Accumulator is negative after operation; reset otherwise
        /// Z is set if Accumulator is zero after operation; reset otherwise
        /// H is reset
        /// P/V is set if parity of Accumulator is even after operation; reset otherwise
        /// N is reset
        /// C is not affected
        /// </summary>
        private void RRD()
        {
            byte _b = _memory.ReadByte(_status.HL);
            _memory.WriteByte(_status.HL, (byte)(_status.A << 4 | _b >> 4));
            _status.A = (byte)(_status.A & 0xF0 | _b & 0x0F);
            _status.F = (byte)(_status.F & FlagRegisterDefinition.C | LookupTable_sz53p[_status.A]);
        }


        /// <summary>
        /// The current Program Counter (PC) contents are pushed onto the external
        /// memory stack, and the page zero memory location given by operand op is
        /// loaded to the PC. Program execution then begins with the Op Code in the
        /// address now pointed to by PC. The push is performed by first decrementing
        /// the contents of the Stack Pointer (SP), loading the high-order byte of PC to
        /// the memory address now pointed to by SP, decrementing SP again, and
        /// loading the low order byte of PC to the address now pointed to by SP. The
        /// Restart instruction allows for a jump to one of eight addresses indicated in
        /// the table below. The operand op is assembled to the object code using the
        /// corresponding T state.
        /// Because all addresses are in page zero of memory, the high order byte of
        /// PC is loaded with 00H. The number selected from the p column of the table
        /// is loaded to the low order byte of PC.
        /// p   t
        /// 00H 000
        /// 08H 001
        /// 10H 010
        /// 18H 011
        /// 20H 100
        /// 28H 101
        /// 30H 110
        /// 38H 111
        /// </summary>
        /// <param name="op">The operand</param>
        private void RST(byte op)
        {
            Push(_status.PC);
            _status.PC = op;
        }

        /// <summary>
        /// The s operand, along with the Carry flag (C in the F register) is subtracted
        /// from the contents of the Accumulator, and the result is stored in the
        /// Accumulator.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is set if borrow from bit 4; reset otherwise
        /// P/V is reset if overflow; reset otherwise
        /// N is set
        /// C is set if borrow; reset otherwise
        /// </summary>
        /// <param name="op">The operand</param>
        private void SBC_A(byte op)
        {
            ushort result = (ushort)(_status.A - op - (_status.F & FlagRegisterDefinition.C));
            byte lookup = (byte)((_status.A & 0x88) >> 3 | (op & 0x88) >> 2 | (result & 0x88) >> 1);
            _status.A = (byte)result;
            _status.F = (byte)(((result & 0x100) != 0 ? FlagRegisterDefinition.C : (byte)0) | FlagRegisterDefinition.N | LookupTable_halfcarry_sub[lookup & 0x07] | LookupTable_overflow_sub[lookup >> 4] | LookupTable_sz53[_status.A]);
        }

        /// <summary>
        /// The contents of the operand op and the Carry Flag (C flag in the F register) 
        /// are subtracted from the contents of register pair HL, and the result is 
        /// stored in HL.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is set if a borrow from bit 12; reset otherwise
        /// P/V is set if overflow; reset otherwise
        /// N is set
        /// C is set if borrow; reset otherwise
        /// </summary>
        /// <param name="op">The operand</param>
        private void SBC_HL(ushort op)
        {
            uint result = (uint)(_status.HL - op - ((_status.F & FlagRegisterDefinition.C) != 0 ? 1 : 0));
            byte lookup = (byte)((byte)((_status.HL & 0x8800) >> 11) | (byte)((op & 0x8800) >> 10) | (byte)((result & 0x8800) >> 9));
            _status.HL = (ushort)result;
            _status.F = (byte)(((result & 0x10000) != 0 ? FlagRegisterDefinition.C : (byte)0) | FlagRegisterDefinition.N | LookupTable_overflow_sub[lookup >> 4] | _status.H & (FlagRegisterDefinition._3 | FlagRegisterDefinition._5 | FlagRegisterDefinition.S) | LookupTable_halfcarry_sub[lookup & 0x07] | (_status.HL != 0 ? (byte)0 : FlagRegisterDefinition.Z));
        }

        /// <summary>
        /// An arithmetic shift left 1-bit position is performed on the contents of
        /// operand op. The content of bit 7 is copied to the Carry flag. Bit 0 is the
        /// least-significant bit.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is reset
        /// P/V is set if parity is even; reset otherwise
        /// N is reset
        /// C is data from bit 7
        /// </summary>
        /// <param name="op">The operand</param>
        private void SLA(HalfRegister op)
        {
            _status.F = (byte)(op.Value >> 7);
            op.Value <<= 1;
            _status.F |= LookupTable_sz53p[op.Value];
        }

        /// <summary>
        /// An arithmetic shift left 1-bit position is performed on the contents of
        /// operand op. The content of bit 7 is copied to the Carry flag. Bit 0 is the
        /// least-significant bit.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is reset
        /// P/V is set if parity is even; reset otherwise
        /// N is reset
        /// C is data from bit 7
        /// </summary>
        /// <param name="op">The operand</param>
        private void SLL(HalfRegister op)
        {
            _status.F = (byte)(op.Value >> 7);
            op.Value = (byte)(op.Value << 1 | 0x01);
            _status.F |= LookupTable_sz53p[op.Value];
        }

        /// <summary>
        /// An arithmetic shift right 1-bit position is performed on the contents of
        /// operand m. The content of bit 0 is copied to the Carry flag and the previous
        /// content of bit 7 is unchanged. Bit 0 is the least-significant bit.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is reset
        /// P/V is set if parity is even; reset otherwise
        /// N is reset
        /// C is data from bit 0 of source register
        /// </summary>
        /// <param name="op">The operand</param>
        private void SRA(HalfRegister op)
        {
            _status.F = (byte)(op.Value & FlagRegisterDefinition.C);
            op.Value = (byte)(op.Value & 0x80 | op.Value >> 1);
            _status.F |= LookupTable_sz53p[op.Value];
        }

        /// <summary>
        /// The contents of operand op are shifted right 1-bit position. The content of
        /// bit 0 is copied to the Carry flag, and bit 7 is reset. Bit 0 is the least-significant
        /// bit.
        /// Condition Bits Affected:
        /// S is reset
        /// Z is set if result is zero; reset otherwise
        /// H is reset
        /// P/V is set if parity is even; reset otherwise
        /// N is reset
        /// C is data from bit 0 of source register
        /// </summary>
        /// <param name="op">The operand</param>
        private void SRL(HalfRegister op)
        {
            _status.F = (byte)(op.Value & FlagRegisterDefinition.C);
            op.Value >>= 1;
            _status.F |= LookupTable_sz53p[op.Value];
        }

        /// <summary>
        /// The s operand is subtracted from the contents of the Accumulator, and the
        /// result is stored in the Accumulator.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is set if borrow from bit 4; reset otherwise
        /// P/V is set if overflow; reset otherwise
        /// N is set
        /// C is set if borrow; reset otherwise
        /// </summary>
        /// <param name="op">The operand</param>
        private void SUB(byte op)
        {
            ushort result = (ushort)(_status.A - op);
            byte lookup = (byte)((_status.A & 0x88) >> 3 | (op & 0x88) >> 2 | (result & 0x88) >> 1);
            _status.A = (byte)result;
            _status.F = (byte)(((result & 0x100) != 0 ? FlagRegisterDefinition.C : (byte)0) | FlagRegisterDefinition.N | LookupTable_halfcarry_sub[lookup & 0x07] | LookupTable_overflow_sub[lookup >> 4] | LookupTable_sz53[_status.A]);
        }

        /// <summary>
        /// The logical exclusive-OR operation is performed between the byte
        /// specified by the op operand and the byte contained in the Accumulator; the
        /// result is stored in the Accumulator.
        /// Condition Bits Affected:
        /// S is set if result is negative; reset otherwise
        /// Z is set if result is zero; reset otherwise
        /// H is reset
        /// P/V is set if parity even; reset otherwise
        /// N is reset
        /// C is reset
        /// </summary>
        /// <param name="op">The operand</param>
        private void XOR(byte op)
        {
            _status.A ^= op;
            _status.F = LookupTable_sz53p[_status.A];
        }




        #endregion

        #region Execution unit


        /// <summary>
        /// Number of statements to fetch before returning (-1) if no return at all.
        /// It should be used for debug purpose (set to -1 after each fetch).
        /// </summary>




        /// <summary>
        /// Main execution
        /// </summary>
        public void Cycle()
        {
            ushort Address;

            // Check if Statement to fetch must be handled
            if (_statementsToFetch >= 0)
            {
                if (_statementsToFetch == 0)
                {
                    // Disable next break (just in case the main program forget to do it)
                    _statementsToFetch = -1;
                    return;
                }
                else
                    _statementsToFetch--;
            }

            // Check if someone is registered to receive Fetch event and eventually raise it
            if (OnFetch != null)
                OnFetch();


            // If the z80 is HALTed, execute a NOP-equivalent and loop again
            if (_status.Halted)
            {
                IncreaseTStates(4);
                return;
            }

            // Fetch next instruction
            _status.OpCode = _memory.ReadByte(_status.PC++);

            // Increment refresh register
            _status.R++;
            /*
            #warning Da eliminare
                            System.IO.TextWriter tw = new System.IO.StreamWriter("c:\\speccy\\CSEState.txt",true);
                            _Status.Serialize(tw);
                            tw.Close();
            */


            if (_status.OpCode == 0x76)     // HALT
            {
                // The first check is for HALT otherwise it could be
                // interpreted as LD (HL),(HL)
                IncreaseTStates(4);
                _status.Halted = true;
            }
            else if ((_status.OpCode & 0xC0) == 0x40)   // LD r,r
            {
                HalfRegister? reg1 = GetHalfRegister((byte)(_status.OpCode >> 3));
                HalfRegister? reg2 = GetHalfRegister(_status.OpCode);

                if (reg1 == null)
                {
                    // The target is (HL)
                    IncreaseTStates(7);
                    _memory.WriteByte(_status.HL, reg2.Value);
                }
                else if (reg2 == null)
                {
                    // The source is (HL)
                    IncreaseTStates(7);
                    reg1.Value = _memory.ReadByte(_status.HL);
                }
                else
                {
                    // Source and target are normal registries
                    IncreaseTStates(4);
                    reg1.Value = reg2.Value;
                }
            }
            else if ((_status.OpCode & 0xC0) == 0x80)
            {
                // Operation beetween accumulator and other registers
                // Usually are identified by 10 ooo rrr where ooo is the operation and rrr is the source register
                HalfRegister? reg = GetHalfRegister(_status.OpCode);
                byte _Value;

                if (reg == null)
                {
                    // The source is (HL)
                    IncreaseTStates(7);
                    _Value = _memory.ReadByte(_status.HL);
                }
                else
                {
                    // The source is a normal registry
                    IncreaseTStates(4);
                    _Value = reg.Value;
                }

                switch (_status.OpCode & 0xF8)
                {
                    case 0x80:  // ADD A,r
                        ADD_A(_Value);
                        break;
                    case 0x88:  // ADC A,r
                        ADC_A(_Value);
                        break;
                    case 0x90:  // SUB r
                        SUB(_Value);
                        break;
                    case 0x98:  // SBC A,r
                        SBC_A(_Value);
                        break;
                    case 0xA0:  // AND r
                        AND_A(_Value);
                        break;
                    case 0xA8:  // XOR r
                        XOR(_Value);
                        break;
                    case 0xB0:  // OR r
                        OR(_Value);
                        break;
                    case 0xB8:  // CP r
                        CP(_Value);
                        break;
                    default:
                        throw new Exception("Wrong place in the right time...");
                }

            }
            else if ((_status.OpCode & 0xC7) == 0x04) // INC r
            {
                HalfRegister reg = GetHalfRegister((byte)(_status.OpCode >> 3));

                if (reg == null)
                {
                    // The target is (HL)
                    IncreaseTStates(7);
                    reg = new HalfRegister(_memory.ReadByte(_status.HL));
                    INC(reg);
                    _memory.WriteByte(_status.HL, reg.Value);
                }
                else
                {
                    // The target is a normal registry
                    IncreaseTStates(4);
                    INC(reg);
                }
            }
            else if ((_status.OpCode & 0xC7) == 0x05) // DEC r
            {
                HalfRegister reg = GetHalfRegister((byte)(_status.OpCode >> 3));

                if (reg == null)
                {
                    // The target is (HL)
                    IncreaseTStates(7);
                    reg = new HalfRegister(_memory.ReadByte(_status.HL));
                    DEC(reg);
                    _memory.WriteByte(_status.HL, reg.Value);
                }
                else
                {
                    // The target is a normal registry
                    IncreaseTStates(4);
                    DEC(reg);
                }
            }
            else if ((_status.OpCode & 0xC7) == 0x06) // LD r,nn
            {
                HalfRegister reg = GetHalfRegister((byte)(_status.OpCode >> 3));
                byte Value = _memory.ReadByte(_status.PC++);

                if (reg == null)
                {
                    // The target is (HL)
                    IncreaseTStates(10);
                    _memory.WriteByte(_status.HL, Value);
                }
                else
                {
                    // The target is a normal registry
                    IncreaseTStates(7);
                    reg.Value = Value;
                }
            }
            else if ((_status.OpCode & 0xC7) == 0xC0) // RET cc
            {
                IncreaseTStates(5);
                if (_status.OpCode == 0xC0 && _status.PC == 0x056C)
                {
                    if (tape_load_trap() == 0)
                        return;
                }
                if (CheckFlag(_status.OpCode))
                {
                    IncreaseTStates(6);
                    RET();
                }
            }
            else if ((_status.OpCode & 0xC7) == 0xC2) // JP cc,nn
            {
                IncreaseTStates(10);
                if (CheckFlag(_status.OpCode))
                    JP();
                else
                    _status.PC += 2;
            }
            else if ((_status.OpCode & 0xC7) == 0xC4) // CALL cc,nn
            {
                IncreaseTStates(10);
                if (CheckFlag(_status.OpCode))
                {
                    IncreaseTStates(7);
                    CALL();
                }
                else
                    _status.PC += 2;
            }
            else if ((_status.OpCode & 0xC7) == 0xC7) // RST p
            {
                IncreaseTStates(11);
                RST((byte)(_status.OpCode & 0x38));
            }
            else if ((_status.OpCode & 0xCF) == 0x01) // LD dd,nn
            {
                IncreaseTStates(10);
                Register reg = GetRegister(_status.OpCode, true);
                ushort Value = _memory.ReadWord(_status.PC);
                _status.PC += 2;

                reg.w = Value;
            }
            else if ((_status.OpCode & 0xCF) == 0x03) // INC ss
            {
                IncreaseTStates(6);
                Register reg = GetRegister(_status.OpCode, true);

                // No flags affected
                reg.w++;
            }
            else if ((_status.OpCode & 0xCF) == 0x09) // ADD HL,ss
            {
                IncreaseTStates(11);
                Register reg = GetRegister(_status.OpCode, true);

                ADD_16(_status.RegisterHL, reg.w);
            }
            else if ((_status.OpCode & 0xCF) == 0x0B) // DEC ss
            {
                IncreaseTStates(6);
                Register reg = GetRegister(_status.OpCode, true);

                reg.w--;
            }
            else if ((_status.OpCode & 0xCF) == 0xC5) // PUSH qq
            {
                IncreaseTStates(11);
                Register reg = GetRegister(_status.OpCode, false);

                Push(reg);
            }

            else if ((_status.OpCode & 0xCF) == 0xC1) // POP qq
            {
                IncreaseTStates(10);
                Register reg = GetRegister(_status.OpCode, false);

                Pop(reg);
            }
            else
            {
                switch (_status.OpCode)
                {
                    case 0x00:      // NOP
                        IncreaseTStates(4);
                        break;
                    case 0x02:      // LD (BC),A
                        IncreaseTStates(7);
                        _memory.WriteByte(_status.BC, _status.A);
                        break;
                    case 0x07:      // RLCA
                        IncreaseTStates(4);
                        RLCA();
                        break;
                    case 0x08:      // EX AF,AF'
                        IncreaseTStates(4);
                        // The 2-byte contents of the register pairs AF and AF are exchanged.
                        // Register pair AF consists of registers A' and F'.


                        // Tape saving trap: note this traps the EX AF,AF' at #04d0, not #04d1 as PC has already been incremented 
                        if (_status.PC == 0x04d1)
                        {
                            if (tape_save_trap() == 0)
                                break;
                        }
                        _status.RegisterAF.Swap(_status.RegisterAF_);
                        break;
                    case 0x0A:      // LD A,(BC)
                        IncreaseTStates(7);
                        _status.A = _memory.ReadByte(_status.BC);
                        break;

                    case 0x0F:      // RRCA
                        IncreaseTStates(4);
                        RRCA();
                        break;
                    case 0x10:      // DJNZ offset
                        IncreaseTStates(8);
                        _status.B--;
                        if (_status.B != 0)
                        {
                            IncreaseTStates(5);
                            JR();
                        }
                        _status.PC++;
                        break;
                    case 0x12:      // LD (DE),A
                        IncreaseTStates(7);
                        _memory.WriteByte(_status.DE, _status.A);
                        break;
                    case 0x17:      // RLA
                        IncreaseTStates(4);
                        RLA();
                        break;
                    case 0x18:      // JR offset
                        IncreaseTStates(12);
                        JR();
                        _status.PC++;
                        break;
                    case 0x1A:      // LD A,(DE)
                        IncreaseTStates(7);
                        _status.A = _memory.ReadByte(_status.DE);
                        break;
                    case 0x1F:      // RRA
                        IncreaseTStates(4);
                        RRA();
                        break;
                    case 0x20:      // JR NZ,offset
                        IncreaseTStates(7);
                        if ((_status.F & FlagRegisterDefinition.Z) == 0)
                        {
                            IncreaseTStates(5);
                            JR();
                        }
                        _status.PC++;
                        break;
                    case 0x22:      // LD (nnnn),HL
                        IncreaseTStates(16);
                        LD_nndd(_status.RegisterHL);
                        break;
                    case 0x27:      // DAA
                        IncreaseTStates(4);
                        DAA();
                        break;
                    case 0x28:      // JR Z,offset
                        IncreaseTStates(7);
                        if ((_status.F & FlagRegisterDefinition.Z) != 0)
                        {
                            IncreaseTStates(5);
                            JR();
                        }
                        _status.PC++;
                        break;
                    case 0x2A:      // LD HL,(nnnn)
                        IncreaseTStates(16);
                        LD_ddnn(_status.RegisterHL);
                        break;
                    case 0x2F:      // CPL
                        IncreaseTStates(4);
                        CPL();
                        break;
                    case 0x30:      // JR NC,offset
                        IncreaseTStates(7);
                        if ((_status.F & FlagRegisterDefinition.C) == 0)
                        {
                            IncreaseTStates(5);
                            JR();
                        }
                        _status.PC++;
                        break;
                    case 0x32:      // LD (nnnn),A
                        IncreaseTStates(13);
                        Address = _memory.ReadWord(_status.PC);
                        _status.PC += 2;
                        _memory.WriteByte(Address, _status.A);
                        break;
                    case 0x37:      // SCF
                        IncreaseTStates(4);
                        _status.F |= FlagRegisterDefinition.C;
                        break;
                    case 0x38:      // JR C,offset
                        IncreaseTStates(7);
                        if ((_status.F & FlagRegisterDefinition.C) != 0)
                        {
                            IncreaseTStates(5);
                            JR();
                        }
                        _status.PC++;
                        break;
                    case 0x3A:      // LD A,(nnnn)
                        IncreaseTStates(13);
                        Address = _memory.ReadWord(_status.PC);
                        _status.PC += 2;
                        _status.A = _memory.ReadByte(Address);
                        break;
                    case 0x3F:      // CCF
                        IncreaseTStates(4);
                        CCF();
                        break;
                    case 0xC3:      // JP nnnn
                        IncreaseTStates(10);
                        JP();
                        break;
                    case 0xC6:      // ADD A,nn
                        IncreaseTStates(7);
                        ADD_A(_memory.ReadByte(_status.PC++));
                        break;
                    case 0xC9:      // RET
                        IncreaseTStates(10);
                        RET();
                        break;
                    case 0xCB:      // CBxx _opCodes
                        _status.R++;
                        Execute_CB(_memory.ReadByte(_status.PC++));
                        break;
                    case 0xCD:      // CALL nnnn
                        IncreaseTStates(17);
                        CALL();
                        break;
                    case 0xCE:      // ADC A,nn
                        IncreaseTStates(7);
                        ADC_A(_memory.ReadByte(_status.PC++));
                        break;
                    case 0xD3:      // OUT (nn),A
                        IncreaseTStates(11);
                        // The operand n is placed on the bottom half (A0 through A7) of the address
                        // bus to select the I/O device at one of 256 possible ports. The contents of the
                        // Accumulator (register A) also appear on the top half (A8 through A15) of
                        // the address bus at this time. Then the byte contained in the Accumulator is
                        // placed on the data bus and written to the selected peripheral device.
                        _io.WriteByte((ushort)(_memory.ReadByte(_status.PC++) | _status.A << 8), _status.A);
                        break;
                    case 0xD6:      // SUB nn
                        IncreaseTStates(7);
                        SUB(_memory.ReadByte(_status.PC++));
                        break;
                    case 0xD9:      // EXX
                        IncreaseTStates(4);
                        // Each 2-byte value in register pairs BC, DE, and HL is exchanged with the
                        // 2-byte value in BC', DE', and HL', respectively.

                        _status.RegisterBC.Swap(_status.RegisterBC_);
                        _status.RegisterDE.Swap(_status.RegisterDE_);
                        _status.RegisterHL.Swap(_status.RegisterHL_);
                        break;
                    case 0xDB:      // IN A,(nn)
                        IncreaseTStates(11);
                        // The operand n is placed on the bottom half (A0 through A7) of the address
                        // bus to select the I/O device at one of 256 possible ports. The contents of the
                        // Accumulator also appear on the top half (A8 through A15) of the address
                        // bus at this time. Then one byte from the selected port is placed on the data
                        // bus and written to the Accumulator (register A) in the CPU.
                        _status.A = _io.ReadByte((ushort)(_memory.ReadByte(_status.PC++) | _status.A << 8));
                        break;



                    case 0xDD:      // DDxx _opCodes
                        _status.R++;
                        Execute_DDFD(_status.RegisterIX, _memory.ReadByte(_status.PC++));
                        break;
                    case 0xDE:      // SBC A,nn
                        IncreaseTStates(4);
                        SBC_A(_memory.ReadByte(_status.PC++));
                        break;
                    case 0xE3:      // EX (SP),HL
                        IncreaseTStates(19);
                        {
                            ushort _w = _memory.ReadWord(_status.SP);
                            _memory.WriteWord(_status.SP, _status.HL);
                            _status.HL = _w;
                        }
                        break;
                    case 0xE6:      // AND nn
                        IncreaseTStates(7);
                        AND_A(_memory.ReadByte(_status.PC++));
                        break;
                    case 0xE9:      // JP HL
                        IncreaseTStates(4);
                        _status.PC = _status.HL;
                        break;
                    case 0xEB:      // EX DE,HL
                        IncreaseTStates(4);
                        _status.RegisterDE.Swap(_status.RegisterHL);
                        break;


                    case 0xed:      // EDxx _opCodes
                        _status.R++;
                        Execute_ED(_memory.ReadByte(_status.PC++));
                        break;
                    case 0xEE:      // XOR A,nn
                        IncreaseTStates(7);
                        XOR(_memory.ReadByte(_status.PC++));
                        break;
                    case 0xF3:      // DI
                        IncreaseTStates(4);
                        // DI disables the maskable interrupt by resetting the interrupt enable flip-flops
                        // (IFF1 and IFF2). Note that this instruction disables the maskable
                        // interrupt during its execution.
                        _status.IFF1 = false;
                        _status.IFF2 = false;
                        break;
                    case 0xF6:      // OR nn
                        IncreaseTStates(7);
                        OR(_memory.ReadByte(_status.PC++));
                        break;
                    case 0xF9:      // LD SP,HL
                        IncreaseTStates(6);
                        _status.SP = _status.HL;
                        break;
                    case 0xFB:      // EI
                        IncreaseTStates(4);
                        // The enable interrupt instruction sets both interrupt enable flip flops (IFF1
                        // and IFF2) to a logic 1, allowing recognition of any maskable interrupt. Note
                        // that during the execution of this instruction and the following instruction,
                        // maskable interrupts are disabled.
                        _status.IFF1 = true;
                        _status.IFF2 = true;
                        break;
                    case 0xFD:      // FDxx _opCodes
                        _status.R++;
                        Execute_DDFD(_status.RegisterIY, _memory.ReadByte(_status.PC++));
                        break;
                    case 0xFE:      // CP nn
                        IncreaseTStates(7);
                        CP(_memory.ReadByte(_status.PC++));
                        break;
                    default:
                        throw new Exception(string.Format("Internal execute error. _opCode {0} not implemented.", _status.OpCode));


                }
            }
        }


        /// <summary>
        /// Execution of DD xx codes and FD xx codes.
        /// DD and FD prefix change a multiplexer from HL to IX (if prefix is DD) or IY (if prefix is FD)
        /// </summary>
        /// <param name="RegisterI_">It must be IX if previous _opCode was DD, IY if previous _opCode was FD</param>
        /// <param name="_opCode">_opCode to execute</param>
        private void Execute_DDFD(Register RegisterI_, byte _opCode)
        {

            // _opCodes are the same as base _opCodes but HL is substituted with IX or IY and (HL)
            // is substituted with (IX + d) or (IY + d).
            /*
			
			0x09	 9	00001001	 ADD REGISTER,BC 
			0x19	25	00011001	 ADD REGISTER,DE 
			0x21	33	00100001	 LD REGISTER,nnnn 
			0x22	34	00100010	 LD (nnnn),REGISTER 
			0x23	35	00100011	 INC REGISTER 
			0x24	36	00100100	 INC REGISTERH 
			0x25	37	00100101	 DEC REGISTERH 
			0x26	38	00100110	 LD REGISTERH,nn 
			0x29	41	00101001	 ADD REGISTER,REGISTER 
			0x2a	42	00101010	 LD REGISTER,(nnnn) 
			0x2b	43	00101011	 DEC REGISTER 
			0x2c	44	00101100	 INC REGISTERL 
			0x2d	45	00101101	 DEC REGISTERL 
			0x2e	46	00101110	 LD REGISTERL,nn 
			0x34	52	00110100	 INC (REGISTER + d) 
			0x35	53	00110101	 DEC (REGISTER + d) 
			0x36	54	00110110	 LD (REGISTER + d),nn 
			0x39	57	00111001	 ADD REGISTER,SP 
			0x44	68	01000100	 LD B,REGISTERH 
			0x45	69	01000101	 LD B,REGISTERL 
			0x46	70	01000110	 LD B,(REGISTER + d) 
			0x4c	76	01001100	 LD C,REGISTERH 
			0x4d	77	01001101	 LD C,REGISTERL 
			0x4e	78	01001110	 LD C,(REGISTER + d) 
			0x54	84	01010100	 LD D,REGISTERH 
			0x55	85	01010101	 LD D,REGISTERL 
			0x56	86	01010110	 LD D,(REGISTER + d) 
			0x5c	92	01011100	 LD E,REGISTERH 
			0x5d	93	01011101	 LD E,REGISTERL 
			0x5e	94	01011110	 LD E,(REGISTER + d) 
			0x60	96	01100000	 LD REGISTERH,B 
			0x61	97	01100001	 LD REGISTERH,C 
			0x62	98	01100010	 LD REGISTERH,D 
			0x63	99	01100011	 LD REGISTERH,E 
			0x64	100	01100100	 LD REGISTERH,REGISTERH 
			0x65	101	01100101	 LD REGISTERH,REGISTERL 
			0x66	102	01100110	 LD H,(REGISTER + d) 
			0x67	103	01100111	 LD REGISTERH,A 
			0x68	104	01101000	 LD REGISTERL,B 
			0x69	105	01101001	 LD REGISTERL,C 
			0x6a	106	01101010	 LD REGISTERL,D 
			0x6b	107	01101011	 LD REGISTERL,E 
			0x6c	108	01101100	 LD REGISTERL,REGISTERH 
			0x6d	109	01101101	 LD REGISTERL,REGISTERL 
			0x6e	110	01101110	 LD L,(REGISTER + d) 
			0x6f	111	01101111	 LD REGISTERL,A 
			0x70	112	01110000	 LD (REGISTER + d),B 
			0x71	113	01110001	 LD (REGISTER + d),C 
			0x72	114	01110010	 LD (REGISTER + d),D 
			0x73	115	01110011	 LD (REGISTER + d),E 
			0x74	116	01110100	 LD (REGISTER + d),H 
			0x75	117	01110101	 LD (REGISTER + d),L 
			0x77	119	01110111	 LD (REGISTER + d),A 
			0x7c	124	01111100	 LD A,REGISTERH 
			0x7d	125	01111101	 LD A,REGISTERL 
			0x7e	126	01111110	 LD A,(REGISTER + d) 
			0x84	132	10000100	 ADD A,REGISTERH 
			0x85	133	10000101	 ADD A,REGISTERL 
			0x86	134	10000110	 ADD A,(REGISTER + d) 
			0x8c	140	10001100	 ADC A,REGISTERH 
			0x8d	141	10001101	 ADC A,REGISTERL 
			0x8e	142	10001110	 ADC A,(REGISTER + d) 
			0x94	148	10010100	 SUB A,REGISTERH 
			0x95	149	10010101	 SUB A,REGISTERL 
			0x96	150	10010110	 SUB A,(REGISTER + d) 
			0x9c	156	10011100	 SBC A,REGISTERH 
			0x9d	157	10011101	 SBC A,REGISTERL 
			0x9e	158	10011110	 SBC A,(REGISTER + d) 
			0xa4	164	10100100	 AND A,REGISTERH 
			0xa5	165	10100101	 AND A,REGISTERL 
			0xa6	166	10100110	 AND A,(REGISTER + d) 
			0xac	172	10101100	 XOR A,REGISTERH 
			0xad	173	10101101	 XOR A,REGISTERL 
			0xae	174	10101110	 XOR A,(REGISTER + d) 
			0xb4	180	10110100	 OR A,REGISTERH 
			0xb5	181	10110101	 OR A,REGISTERL 
			0xb6	182	10110110	 OR A,(REGISTER + d) 
			0xbc	188	10111100	 CP A,REGISTERH 
			0xbd	189	10111101	 CP A,REGISTERL 
			0xbe	190	10111110	 CP A,(REGISTER + d) 
			0xcb	203	11001011	 {DD|FD}CBxx _opCodes 
			0xe1	225	11100001	 POP REGISTER 
			0xe3	227	11100011	 EX (SP),REGISTER 
			0xe5	229	11100101	 PUSH REGISTER 
			0xe9	233	11101001	 JP REGISTER 
			0xf9	249	11111001	 LD SP,REGISTER 
			*/






            // I* registers are used for indexed access so (I*) will be used often
            HalfRegister _I__;
            ushort Address;

            if (_opCode == 0x76)     // HALT
            {
                // The first check is for HALT otherwise it could be
                // interpreted as LD (I_ + d),(I_ + d)
                IncreaseTStates(4);
                _status.Halted = true;
            }
            else if ((_opCode & 0xC0) == 0x40)   // LD r,r'
            {
                HalfRegister reg1 = GetHalfRegister((byte)(_opCode >> 3));
                HalfRegister reg2 = GetHalfRegister(_opCode);

                if (reg1 == null)
                {
                    // The target is (I_ + d)
                    IncreaseTStates(19);
                    Address = (ushort)(RegisterI_.w + (sbyte)_memory.ReadByte(_status.PC++));
                    _memory.WriteByte(Address, reg2.Value);
                }
                else if (reg2 == null)
                {
                    // The source is (I_ + d)
                    IncreaseTStates(19);
                    Address = (ushort)(RegisterI_.w + (sbyte)_memory.ReadByte(_status.PC++));
                    reg1.Value = _memory.ReadByte(Address);
                }
                else
                {
                    // Source and target are normal registers but HL is now substituted by I_
                    if (reg1 == _status.RegisterHL.h)
                        reg1 = RegisterI_.h;
                    if (reg1 == _status.RegisterHL.l)
                        reg1 = RegisterI_.l;

                    if (reg2 == _status.RegisterHL.h)
                        reg2 = RegisterI_.h;
                    if (reg2 == _status.RegisterHL.l)
                        reg2 = RegisterI_.l;

                    IncreaseTStates(8);
                    reg1.Value = reg2.Value;
                }
            }
            else if ((_opCode & 0xC0) == 0x80)
            {
                // Operation beetween accumulator and other registers
                // Usually are identified by 10 ooo rrr where ooo is the operation and rrr is the source register
                HalfRegister reg = GetHalfRegister(_opCode);
                byte _Value;

                if (reg == null)
                {
                    // The source is (I_ + d)
                    IncreaseTStates(19);
                    _Value = _memory.ReadByte((ushort)(RegisterI_.w + (sbyte)_memory.ReadByte(_status.PC++)));
                }
                else
                {
                    // The source is a normal registry but HL is substituted by I_
                    IncreaseTStates(8);
                    if (reg == _status.RegisterHL.h)
                        _Value = RegisterI_.h.Value;
                    else if (reg == _status.RegisterHL.l)
                        _Value = RegisterI_.l.Value;
                    else
                        _Value = reg.Value;
                }

                switch (_opCode & 0xF8)
                {
                    case 0x80:  // ADD A,r
                        ADD_A(_Value);
                        break;
                    case 0x88:  // ADC A,r
                        ADC_A(_Value);
                        break;
                    case 0x90:  // SUB r
                        SUB(_Value);
                        break;
                    case 0x98:  // SBC A,r
                        SBC_A(_Value);
                        break;
                    case 0xA0:  // AND r
                        AND_A(_Value);
                        break;
                    case 0xA8:  // XOR r
                        XOR(_Value);
                        break;
                    case 0xB0:  // OR r
                        OR(_Value);
                        break;
                    case 0xB8:  // CP r
                        CP(_Value);
                        break;
                    default:
                        throw new Exception("Wrong place in the right time...");
                }

            }
            else
            {

                switch (_opCode)
                {
                    case 0x09:      // ADD I_,BC
                        IncreaseTStates(15);
                        ADD_16(RegisterI_, _status.BC);
                        break;

                    case 0x19:      // ADD I_,DE
                        IncreaseTStates(15);
                        ADD_16(RegisterI_, _status.DE);
                        break;

                    case 0x21:      // LD I_,nnnn
                        IncreaseTStates(14);
                        RegisterI_.w = _memory.ReadWord(_status.PC);
                        _status.PC += 2;
                        break;

                    case 0x22:      // LD (nnnn),I_
                        IncreaseTStates(20);
                        LD_nndd(RegisterI_);
                        break;

                    case 0x23:      // INC I_
                        IncreaseTStates(10);
                        RegisterI_.w++;
                        break;

                    case 0x24:      // INC I_.h
                        IncreaseTStates(8);
                        INC(RegisterI_.h);
                        break;

                    case 0x25:      // DEC I_.h
                        IncreaseTStates(8);
                        DEC(RegisterI_.h);
                        break;

                    case 0x26:      // LD I_.h,nn
                        IncreaseTStates(11);
                        RegisterI_.h.Value = _memory.ReadByte(_status.PC++);
                        break;

                    case 0x29:      // ADD I_,I_
                        IncreaseTStates(15);
                        ADD_16(RegisterI_, RegisterI_.w);
                        break;

                    case 0x2A:      // LD I_,(nnnn)
                        IncreaseTStates(20);
                        LD_ddnn(RegisterI_);
                        break;

                    case 0x2B:      // DEC I_
                        IncreaseTStates(10);
                        RegisterI_.w--;
                        break;

                    case 0x2C:      // INC I_.l
                        IncreaseTStates(8);
                        INC(RegisterI_.l);
                        break;

                    case 0x2D:      // DEC I_.l
                        IncreaseTStates(8);
                        DEC(RegisterI_.l);
                        break;

                    case 0x2E:      // LD I_.l,nn
                        IncreaseTStates(11);
                        RegisterI_.l.Value = _memory.ReadByte(_status.PC++);
                        break;

                    case 0x34:      // INC (I_ + d)
                        IncreaseTStates(23);
                        Address = (ushort)(RegisterI_.w + (sbyte)_memory.ReadByte(_status.PC++));
                        _I__ = new HalfRegister(_memory.ReadByte(Address));
                        INC(_I__);
                        _memory.WriteByte(Address, _I__.Value);
                        break;

                    case 0x35:      // DEC (I_ + d)
                        IncreaseTStates(23);
                        Address = (ushort)(RegisterI_.w + (sbyte)_memory.ReadByte(_status.PC++));
                        _I__ = new HalfRegister(_memory.ReadByte(Address));
                        INC(_I__);
                        _memory.WriteByte(Address, _I__.Value);
                        break;

                    case 0x36:      // LD (I_ + d),nn
                        IncreaseTStates(19);
                        Address = (ushort)(RegisterI_.w + (sbyte)_memory.ReadByte(_status.PC++));
                        _memory.WriteByte(Address, _memory.ReadByte(_status.PC++));
                        break;

                    case 0x39:      // ADD I_,SP
                        IncreaseTStates(15);
                        ADD_16(RegisterI_, _status.SP);
                        break;




                    case 0xCB:      // {DD|FD}CBxx _opCodes
                        {
                            Address = (ushort)(RegisterI_.w + (sbyte)_memory.ReadByte(_status.PC++));
                            Execute_DDFD_CB(Address, _memory.ReadByte(_status.PC++));
                        }
                        break;
                    case 0xE1:      // POP I_
                        IncreaseTStates(14);
                        Pop(RegisterI_);
                        break;

                    case 0xE3:      // EX (SP),I_
                        IncreaseTStates(23);
                        {
                            ushort _w = _memory.ReadWord(_status.SP);
                            _memory.WriteWord(_status.SP, RegisterI_.w);
                            RegisterI_.w = _w;
                        }
                        break;
                    case 0xE5:      // PUSH I_
                        IncreaseTStates(15);
                        Push(RegisterI_);
                        break;

                    case 0xE9:      // JP I_
                        IncreaseTStates(8);
                        _status.PC = RegisterI_.w;
                        break;

                    // Note EB (EX DE,HL) does not get modified to use either IX or IY;
                    // this is because all EX DE,HL does is switch an internal flip-flop
                    // in the Z80 which says which way round DE and HL are, which can't
                    // be used with IX or IY. (This is also why EX DE,HL is very quick
                    // at only 4 T states).

                    case 0xF9:      // LD SP,I_
                        IncreaseTStates(10);
                        _status.SP = RegisterI_.w;
                        break;

                    default:
                        // Instruction did not involve H or L, so backtrack one instruction and parse again
                        IncreaseTStates(4);
                        _status.PC--;
                        break;

                }
            }
        }


        /// <summary>
        /// Execution of ED xx codes
        /// </summary>
        /// <param name="_opCode">_opCode to execute</param>
        private void Execute_ED(byte _opCode)
        {


            if ((_opCode & 0xC7) == 0x40) // IN r,(C)
            {
                HalfRegister reg = GetHalfRegister((byte)(_opCode >> 3));

                // In this case 110 does not write in (HL) but affects only the flags
                if (reg == null)
                    reg = new HalfRegister();

                IncreaseTStates(12);
                IN(reg, _status.BC);
            }
            else if ((_opCode & 0xC7) == 0x41) // OUT (C),r
            {
                // The contents of register C are placed on the bottom half (A0 through A7) of
                // the address bus to select the I/O device at one of 256 possible ports. The
                // contents of Register B are placed on the top half (A8 through A15) of the
                // address bus at this time. Then the byte contained in register r is placed on
                // the data bus and written to the selected peripheral device.
                HalfRegister reg = GetHalfRegister((byte)(_opCode >> 3));

                // In this case 110 outputs 0 in out port
                if (reg == null)
                    reg = new HalfRegister(0);

                IncreaseTStates(12);
                _io.WriteByte(_status.BC, reg.Value);
            }
            else if ((_opCode & 0xC7) == 0x42) // ALU operations with HL
            {
                IncreaseTStates(15);
                Register reg = GetRegister(_opCode, true);
                switch (_opCode & 0x08)
                {
                    case 0: // SBC HL,ss
                        SBC_HL(reg.w);
                        break;
                    case 8: // ADC HL,ss
                        ADC_HL(reg.w);
                        break;
                    default:
                        throw new Exception("No no no!!!");
                }
            }
            else if ((_opCode & 0xC7) == 0x43) // Load register from to memory address
            {
                IncreaseTStates(20);
                Register reg = GetRegister(_opCode, true);
                switch (_opCode & 0x08)
                {
                    case 0: // LD (nnnn),ss
                        LD_nndd(reg);
                        break;
                    case 8: // LD ss,(nnnn)
                        LD_ddnn(reg);
                        break;
                    default:
                        throw new Exception("No no no!!!");
                }

            }
            else
            {

                switch (_opCode)
                {


                    case 0x44:
                    case 0x4c:
                    case 0x54:
                    case 0x5c:
                    case 0x64:
                    case 0x6c:
                    case 0x74:
                    case 0x7c:  // NEG
                                // The contents of the Accumulator are negated (two’s complement). This is
                                // the same as subtracting the contents of the Accumulator from zero. Note
                                // that 80H is left unchanged.
                                // Condition Bits Affected:
                                // S is set if result is negative; reset otherwise
                                // Z is set if result is 0; reset otherwise
                                // H is set if borrow from bit 4; reset otherwise
                                // P/V is set if Accumulator was 80H before operation; reset otherwise
                                // N is set
                                // C is set if Accumulator was not 00H before operation; reset otherwise
                        IncreaseTStates(8);
                        {
                            byte _b = _status.A;
                            _status.A = 0;
                            SUB(_b);
                        }
                        break;

                    case 0x45:
                    case 0x4d:          // RETI
                                        // This instruction is used at the end of a maskable interrupt service routine to:
                                        // • Restore the contents of the Program Counter (PC) (analogous to the
                                        // RET instruction)
                                        // • Signal an I/O device that the interrupt routine is completed. The RETI
                                        // instruction also facilitates the nesting of interrupts, allowing higher
                                        // priority devices to temporarily suspend service of lower priority
                                        // service routines. However, this instruction does not enable interrupts
                                        // that were disabled when the interrupt routine was entered. Before
                                        // doing the RETI instruction, the enable interrupt instruction (EI)
                                        // should be executed to allow recognition of interrupts after completion
                                        // of the current service routine.

                    // TODO: Reading Z80 specs this instruction should not copy IFF2 to IFF1 but
                    // in real Z80 seems that the operation is done.
                    case 0x55:
                    case 0x5d:
                    case 0x65:
                    case 0x6d:
                    case 0x75:
                    case 0x7d:      // RETN
                                    // This instruction is used at the end of a non-maskable interrupts service
                                    // routine to restore the contents of the Program Counter (PC) (analogous to
                                    // the RET instruction). The state of IFF2 is copied back to IFF1 so that
                                    // maskable interrupts are enabled immediately following the RETN if they
                                    // were enabled before the nonmaskable interrupt.
                        IncreaseTStates(14);
                        _status.IFF1 = _status.IFF2;
                        RET();
                        break;

                    case 0x46:
                    case 0x4e:
                    case 0x66:
                    case 0x6e:  // IM 0
                                // The IM 0 instruction sets interrupt mode 0. In this mode, the interrupting
                                // device can insert any instruction on the data bus for execution by the
                                // CPU. The first byte of a multi-byte instruction is read during the interrupt
                                // acknowledge cycle. Subsequent bytes are read in by a normal memory
                                // read sequence.
                        IncreaseTStates(8);
                        _status.IM = 0;
                        break;

                    case 0x47:  // LD I,A
                        IncreaseTStates(9);
                        _status.I = _status.A;
                        break;

                    case 0x4F:  // LD R,A
                        IncreaseTStates(9);
                        _status.R = _status.R7 = _status.A;
                        break;

                    case 0x56:
                    case 0x76:  // IM 1
                        IncreaseTStates(8);
                        // The IM 1 instruction sets interrupt mode 1. In this mode, the processor
                        // responds to an interrupt by executing a restart to location 0038H.
                        _status.IM = 1;
                        break;
                    case 0x57:  // LD A,I
                        IncreaseTStates(9);
                        // The contents of the Interrupt Vector Register I are loaded to the Accumulator.
                        // Condition Bits Affected:
                        // S is set if I-Register is negative; reset otherwise
                        // Z is set if I-Register is zero; reset otherwise
                        // H is reset
                        // P/V contains contents of IFF2
                        // N is reset
                        // C is not affected
                        // If an interrupt occurs during execution of this instruction, the Parity
                        // flag contains a 0.
                        _status.A = _status.I;
                        _status.F = (byte)(_status.F & FlagRegisterDefinition.C | LookupTable_sz53[_status.A] | (_status.IFF2 ? FlagRegisterDefinition.V : (byte)0));
                        break;
                    case 0x5E:
                    case 0x7E:  // IM 2
                                // The IM 2 instruction sets the vectored interrupt mode 2. This mode allows
                                // an indirect call to any memory location by an 8-bit vector supplied from the
                                // peripheral device. This vector then becomes the least-significant eight bits
                                // of the indirect pointer, while the I register in the CPU provides the most-significant
                                // eight bits. This address points to an address in a vector table that
                                // is the starting address for the interrupt service routine.
                        IncreaseTStates(8);
                        _status.IM = 2;
                        break;

                    case 0x5F:  // LD A,R
                        IncreaseTStates(9);
                        _status.A = (byte)(_status.R & 0x7F | _status.R7 & 0x80);
                        _status.F = (byte)(_status.F & FlagRegisterDefinition.C | LookupTable_sz53[_status.A] | (_status.IFF2 ? FlagRegisterDefinition.V : (byte)0));
                        break;

                    case 0x67:  // RRD
                        IncreaseTStates(18);
                        RRD();
                        break;
                    case 0x6F:  // RLD
                        IncreaseTStates(18);
                        RLD();
                        break;
                    case 0xA0:  // LDI
                        IncreaseTStates(16);
                        LDI();
                        break;
                    case 0xA1:  // CPI
                        IncreaseTStates(16);
                        CPI();
                        break;

                    case 0xA2:  // INI
                        IncreaseTStates(16);
                        INI();
                        break;

                    case 0xA3:  // OUTI
                        IncreaseTStates(16);
                        OUTI();
                        break;

                    case 0xA8:  // LDD
                        IncreaseTStates(16);
                        LDD();
                        break;

                    case 0xA9:  // CPD
                        IncreaseTStates(16);
                        CPD();
                        break;
                    case 0xAA:  // IND
                        IncreaseTStates(16);
                        IND();
                        break;

                    case 0xAB:  // OUTD
                        IncreaseTStates(16);
                        OUTD();
                        break;

                    case 0xB0:  // LDIR
                        IncreaseTStates(16);
                        LDIR();
                        break;

                    case 0xB1:  // CPIR
                        IncreaseTStates(16);
                        CPIR();
                        break;

                    case 0xB2:  // INIR
                        IncreaseTStates(16);
                        INIR();
                        break;

                    case 0xB3:  // OTIR
                        IncreaseTStates(16);
                        OTIR();
                        break;

                    case 0xB8:  // LDDR
                        IncreaseTStates(17);
                        LDDR();
                        break;

                    case 0xB9:  // CPDR
                        IncreaseTStates(16);
                        CPDR();
                        break;

                    case 0xba:  // INDR
                        IncreaseTStates(16);
                        INDR();
                        break;

                    case 0xbb:  // OTDR
                        IncreaseTStates(16);
                        OTDR();
                        break;

                    default:    // All other _opCodes are NOPD
                        IncreaseTStates(8);
                        break;

                }
            }
        }



        /// <summary>
        /// Execution of CB xx codes
        /// </summary>
        /// <param name="_opCode">_opCode to execute</param>
        private void Execute_CB(byte _opCode)
        {


            // Operations with single byte register
            // The format is 00 ooo rrr where ooo is the operation and rrr is the register
            HalfRegister reg = GetHalfRegister(_opCode);
            HalfRegister? _HL_ = null;

            // Check if the source/target is (HL)
            if (reg == null)
                reg = _HL_ = new HalfRegister(_memory.ReadByte(_status.HL));

            Execute_CB_on_reg(_opCode, reg);

            if (reg == _HL_)
            {
                // The target is (HL)
                IncreaseTStates(15);
                _memory.WriteByte(_status.HL, _HL_.Value); //We should not do this when we check bits (BIT n,r)!
            }
            else
            {
                // The source is a normal registry
                IncreaseTStates(8);
            }


        }


        /// <summary>
        /// Execution of DD CB xx codes or FD CB xx codes
        /// </summary>
        /// <param name="Address">Address to act on - Address = I_ + d</param>
        /// <param name="_opCode">_opCode</param>
        private void Execute_DDFD_CB(ushort Address, byte _opCode)
        {
            // This is a mix of DD/FD _opCodes (Normal operation but access to 
            // I_ register instead of HL register) and CB op codes.
            // Behaviour is a little different:
            // if (_opCodes use B, C, D, E, H, L)  -  _opCodes with rrr different from 110
            //   r = (I_ + d)
            //   execute_op r
            //   (I_ + d) = r
            // if (_opCodes use (HL))              -  _opCodes with rrr = 110
            //   execute_op (I_ + d)
            //
            // if execute_op is a bit checking operation BIT n,r no assignement are done



            HalfRegister reg;

            // Check if the operation is a bit checking operation
            // The format is 01 bbb rrr
            if (_opCode >> 6 == 0x01)
            {
                reg = new HalfRegister();
                IncreaseTStates(20);
            }
            else
            {
                // Retrieve the register from _opCode xxxxx rrr
                reg = GetHalfRegister(_opCode);

                // Check if the source is (I_ + d) so the op will not act on any register
                // but only on memory
                if (reg == null)
                {
                    IncreaseTStates(23);
                    reg = new HalfRegister();
                }
                else
                    IncreaseTStates(23);
                // In case reg is not null the timings are not documented. I think the operation 
                // take at least 23 tstates (the same of the operation without storing the 
                // result in register too).
            }

            // Assign (I_ + d) value to reg
            reg.Value = _memory.ReadByte(Address);


            Execute_CB_on_reg(_opCode, reg);

            _memory.WriteByte(Address, reg.Value);

        }


        /// <summary>
        /// This is the low level function called within a CB _opCode fetch
        /// (single byte or DD CB or FD CB)
        /// It must be called after the execution unit has determined on
        /// wich register act
        /// </summary>
        /// <param name="_opCode">_opCode</param>
        /// <param name="reg">Register to act on</param>
        private void Execute_CB_on_reg(byte _opCode, HalfRegister reg)
        {
            switch (_opCode >> 3)
            {
                case 0: // RLC r
                    RLC(reg);
                    break;
                case 1: // RRC r
                    RRC(reg);
                    break;
                case 2: // RL r
                    RL(reg);
                    break;
                case 3: // RR r
                    RR(reg);
                    break;
                case 4: // SLA r
                    SLA(reg);
                    break;
                case 5: // SRA r
                    SRA(reg);
                    break;
                case 6: // SLL r
                    SLL(reg);
                    break;
                case 7: // SRL r
                    SRL(reg);
                    break;
                default:
                    // Work on bits

                    // The format is oo bbb rrr
                    // oo is the operation (01 BIT, 10 RES, 11 SET)
                    // bbb is the bit number
                    // rrr is the register
                    byte bit = (byte)(_opCode >> 3 & 0x07);

                    switch (_opCode >> 6)
                    {
                        case 1: // BIT n,r
                            if (bit == 7)
                                BIT7(reg.Value);
                            else
                                BIT(bit, reg.Value);
                            break;
                        case 2: // RES n,r
                            reg.Value &= (byte)~(1 << bit);
                            break;
                        case 3: // SET n,r
                            reg.Value |= (byte)(1 << bit);
                            break;
                        default:
                            throw new Exception("What am I doing here?!?");
                    }

                    break;
            }
        }


        int tape_load_trap() { return 0; }
        int tape_save_trap() { return 0; }
        public int TStateValue { get; private set; }

        #endregion

        public void ResetTStates()
        {
            tstates -= _next_event;
        }

        private void IncreaseTStates(int value)
        {
            tstates += value;
            TStateValue = value;
        }
    }
}
