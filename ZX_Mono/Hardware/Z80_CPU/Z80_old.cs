using System;
namespace ZX_sharp.Hardware.CPU
{
    public class Z80_old
    {
        public byte[] scr_atr_RAM = new byte[768];
        public byte[] scr_picsel_RAM = new byte[6144];
        //public byte[] ram_ = new byte[65536];

        // Flag register , 6 flip flops (Carry, Sign, Parity, Overflow, Zero, HALT)
        const byte FLAG_C = 0x01;
        const byte FLAG_N = 0x02;
        const byte FLAG_PV = 0x04;
        const byte FLAG_H = 0x08;
        const byte FLAG_Z = 0x40;
        const byte FLAG_S = 0x80;

        //Accumulator & Alternate Accumlator
        byte ra_ = 0;
        byte ra2_ = 0;

        // General purpose registers (BC, DE, HL)
        byte rb_ = 0;
        byte rd_ = 0;
        byte rh_ = 0;
        byte rf_ = 0;
        byte rc_ = 0;
        byte re_ = 0;
        byte rl_ = 0;

        // Alternate General purpose registers (BC' , DE' , HL' )

        byte rb2_ = 0;
        byte rd2_ = 0;
        byte rh2_ = 0;
        byte rf2_ = 0;
        byte rc2_ = 0;
        byte re2_ = 0;
        byte rl2_ = 0;

        //Special Registers (I , R, IX , IY , PC , SP)
        byte ri_ = 0;
        byte rr_ = 0;
        int rix_ = 0;
        int riy_ = 0;
        int rsp_ = 0;
        int rpc_ = 0;

        private RAM _ram;
        public Z80_old(RAM ram)
        {
            _ram=ram;
        }


        //Rotate bits
        byte rhl()
        {
            return (byte)(rh_ + 8 & rl_);
        }
        byte rbc()
        {
            return (byte)(rb_ + 8 & rc_);
        }
        byte rde()
        {
            return (byte)(rd_ + 8 & re_);
        }

        //Assign Flags
        bool fc()
        {
            return (rf_ & FLAG_C) > 0;
        }
        bool fn()
        {
            return (rf_ & FLAG_N) > 0;
        }
        bool fpv()
        {
            return (rf_ & FLAG_PV) > 0;
        }
        bool fh()
        {
            return (rf_ & FLAG_H) > 0;
        }
        bool fz()
        {
            return (rf_ & FLAG_Z) > 0;
        }
        bool fs()
        {
            return (rf_ & FLAG_S) > 0;
        }
        ////////////////////// ALU Functions /////////////////////////////

        byte op_add(byte a, byte b)
        {
            return w_calc_flags(a + b, false);
        }
        byte op_adc(byte a, byte b)
        {
            return w_calc_flags(a + b + (rf_ & 0x01), false);
        }
        byte op_sub(byte a, byte b)
        {
            return w_calc_flags(a - b, true);
        }
        byte op_sbc(byte a, byte b)
        {
            return w_calc_flags(a - b - (rf_ & 0x01), true);
        }
        byte op_and(byte a, byte b)
        {
            return w_logic_flags((byte)(a & b));
        }
        byte op_xor(byte a, byte b)
        {
            return w_logic_flags((byte)(a ^ b));
        }
        byte op_or(byte a, byte b)
        {
            return w_logic_flags((byte)(a | b));
        }
        void op_cp(byte a)
        {
            w_calc_flags(ra_ - a, true);
        }
        byte op_inc(byte a)
        {
            return w_calc_flags(a + 1, false);
        }
        byte op_dec(byte a)
        {
            return w_calc_flags(a - 1, false);
        }

        //////////////////////Program Counter & Program Memory /////////////////////////////

        byte next()
        {
            var cmd = _ram.Read(rpc_++);
            return cmd;
        }
        int next16()
        {
            return ((int)next() + 8) | next();
        }
       
       public void step()
        {
            int tmp;
            int tmp16;
            String cerr = "Error :\n";

            byte code;
            switch (code = next())
            {
                case (byte)Op.LD_A_A: break;
                case (byte)Op.LD_A_B: ra_ = rb_; break;
                case (byte)Op.LD_A_C: ra_ = rc_; break;
                case (byte)Op.LD_A_D: ra_ = rd_; break;
                case (byte)Op.LD_A_E: ra_ = re_; break;
                case (byte)Op.LD_A_F: ra_ = rf_; break;
                case (byte)Op.LD_A_L: ra_ = rl_; break;
                case (byte)Op.LD_B_A: rb_ = ra_; break;
                case (byte)Op.LD_B_B: break;
                case (byte)Op.LD_B_C: rb_ = rc_; break;
                case (byte)Op.LD_B_D: rb_ = rd_; break;
                case (byte)Op.LD_B_E: rb_ = re_; break;
                case (byte)Op.LD_B_F: rb_ = rf_; break;
                case (byte)Op.LD_B_L: rb_ = rl_; break;
                case (byte)Op.LD_C_A: rc_ = ra_; break;
                case (byte)Op.LD_C_B: rc_ = rb_; break;
                case (byte)Op.LD_C_C: break;
                case (byte)Op.LD_C_D: rc_ = rd_; break;
                case (byte)Op.LD_C_E: rc_ = re_; break;
                case (byte)Op.LD_C_F: rc_ = rf_; break;
                case (byte)Op.LD_C_L: rc_ = rl_; break;
                case (byte)Op.LD_D_A: rd_ = ra_; break;
                case (byte)Op.LD_D_B: rd_ = rb_; break;
                case (byte)Op.LD_D_C: rd_ = rc_; break;
                case (byte)Op.LD_D_D: break;
                case (byte)Op.LD_D_E: rd_ = re_; break;
                case (byte)Op.LD_D_F: rd_ = rf_; break;
                case (byte)Op.LD_D_L: rd_ = rl_; break;
                case (byte)Op.LD_E_A: re_ = ra_; break;
                case (byte)Op.LD_E_B: re_ = rb_; break;
                case (byte)Op.LD_E_C: re_ = rc_; break;
                case (byte)Op.LD_E_D: re_ = rd_; break;
                case (byte)Op.LD_E_E: break;
                case (byte)Op.LD_E_F: re_ = rf_; break;
                case (byte)Op.LD_E_L: re_ = rl_; break;
                case (byte)Op.LD_H_A: rh_ = ra_; break;
                case (byte)Op.LD_H_B: rh_ = rb_; break;
                case (byte)Op.LD_H_C: rh_ = rc_; break;
                case (byte)Op.LD_H_D: rh_ = rd_; break;
                case (byte)Op.LD_H_E: rh_ = re_; break;
                case (byte)Op.LD_H_F: rh_ = rf_; break;
                case (byte)Op.LD_H_L: rf_ = rl_; break;
                case (byte)Op.LD_L_A: rl_ = ra_; break;
                case (byte)Op.LD_L_B: rl_ = rb_; break;
                case (byte)Op.LD_L_C: rl_ = rc_; break;
                case (byte)Op.LD_L_D: rl_ = rd_; break;
                case (byte)Op.LD_L_E: rl_ = re_; break;
                case (byte)Op.LD_L_F: rl_ = rf_; break;
                case (byte)Op.LD_L_L: break;
                case (byte)Op.LD_A_ind_HL: ra_ = _ram.Read(rhl()); break;
                case (byte)Op.LD_A_ind_BC: ra_ = _ram.Read(rbc()); break;
                case (byte)Op.LD_A_ind_DE: ra_ = _ram.Read(rde()); break;
                case (byte)Op.LD_B_ind_HL: rb_ = _ram.Read(rhl()); break;
                case (byte)Op.LD_C_ind_HL: rc_ = _ram.Read(rhl()); break;
                case (byte)Op.LD_D_ind_HL: rd_ = _ram.Read(rhl()); break;
                case (byte)Op.LD_E_ind_HL: re_ = _ram.Read(rhl()); break;
                case (byte)Op.LD_H_ind_HL: rh_ = _ram.Read(rhl()); break;
                case (byte)Op.LD_L_ind_HL: rl_ = _ram.Read(rhl()); break;
                case (byte)Op.LD_ind_HL_A: _ram.Write(rhl(), ra_); break;
                case (byte)Op.LD_ind_HL_B: _ram.Write(rhl(), rb_); break;
                case (byte)Op.LD_ind_HL_C: _ram.Write(rhl(), rc_); break;
                case (byte)Op.LD_ind_HL_D: _ram.Write(rhl(), rd_); break;
                case (byte)Op.LD_ind_HL_E: _ram.Write(rhl(), re_); break;
                case (byte)Op.LD_ind_HL_F: _ram.Write(rhl(), rf_); break;
                case (byte)Op.LD_ind_HL_L: _ram.Write(rhl(), rl_); break;
                case (byte)Op.LD_ind_BC_A: _ram.Write(rbc(), ra_); break;
                case (byte)Op.LD_ind_DE_A: _ram.Write(rde(), ra_); break;
                case (byte)Op.LD_ext_A: _ram.Write(next16(), ra_); break;
                case (byte)Op.ADD_A_A: ra_ = op_add(ra_, ra_); break;
                case (byte)Op.ADD_A_B: ra_ = op_add(ra_, rb_); break;
                case (byte)Op.ADD_A_C: ra_ = op_add(ra_, rc_); break;
                case (byte)Op.ADD_A_D: ra_ = op_add(ra_, rd_); break;
                case (byte)Op.ADD_A_E: ra_ = op_add(ra_, re_); break;
                case (byte)Op.ADD_A_F: ra_ = op_add(ra_, rf_); break;
                case (byte)Op.ADD_A_L: ra_ = op_add(ra_, rl_); break;
                case (byte)Op.ADD_A_ind_HL: ra_ = op_add(ra_, _ram.Read(rhl())); break;
                case (byte)Op.ADD_A_imm: ra_ = op_add(ra_, next()); break;
                case (byte)Op.ADC_A_A: ra_ = op_adc(ra_, ra_); break;
                case (byte)Op.ADC_A_B: ra_ = op_adc(ra_, rb_); break;
                case (byte)Op.ADC_A_C: ra_ = op_adc(ra_, rc_); break;
                case (byte)Op.ADC_A_D: ra_ = op_adc(ra_, rd_); break;
                case (byte)Op.ADC_A_E: ra_ = op_adc(ra_, re_); break;
                case (byte)Op.ADC_A_F: ra_ = op_adc(ra_, rf_); break;
                case (byte)Op.ADC_A_L: ra_ = op_adc(ra_, rl_); break;
                case (byte)Op.ADC_A_ind_HL: ra_ = op_adc(ra_, _ram.Read(rhl())); break;
                case (byte)Op.ADC_A_imm: ra_ = op_adc(ra_, next()); break;
                case (byte)Op.SUB_A_A: ra_ = op_sub(ra_, ra_); break;
                case (byte)Op.SUB_A_B: ra_ = op_sub(ra_, rb_); break;
                case (byte)Op.SUB_A_C: ra_ = op_sub(ra_, rc_); break;
                case (byte)Op.SUB_A_D: ra_ = op_sub(ra_, rd_); break;
                case (byte)Op.SUB_A_E: ra_ = op_sub(ra_, re_); break;
                case (byte)Op.SUB_A_F: ra_ = op_sub(ra_, rf_); break;
                case (byte)Op.SUB_A_L: ra_ = op_sub(ra_, rl_); break;
                case (byte)Op.SUB_A_ind_HL: ra_ = op_sub(ra_, _ram.Read(rhl())); break;
                case (byte)Op.SUB_A_imm: ra_ = op_sub(ra_, next()); break;
                case (byte)Op.SBC_A_A: ra_ = op_sbc(ra_, ra_); break;
                case (byte)Op.SBC_A_B: ra_ = op_sbc(ra_, rb_); break;
                case (byte)Op.SBC_A_C: ra_ = op_sbc(ra_, rc_); break;
                case (byte)Op.SBC_A_D: ra_ = op_sbc(ra_, rd_); break;
                case (byte)Op.SBC_A_E: ra_ = op_sbc(ra_, re_); break;
                case (byte)Op.SBC_A_F: ra_ = op_sbc(ra_, rf_); break;
                case (byte)Op.SBC_A_L: ra_ = op_sbc(ra_, rl_); break;
                case (byte)Op.SBC_A_ind_HL: ra_ = op_sbc(ra_, _ram.Read(rhl())); break;
                case (byte)Op.SBC_A_imm: ra_ = op_sbc(ra_, next()); break;
                case (byte)Op.AND_A_A: ra_ = op_and(ra_, ra_); break;
                case (byte)Op.AND_A_B: ra_ = op_and(ra_, rb_); break;
                case (byte)Op.AND_A_C: ra_ = op_and(ra_, rc_); break;
                case (byte)Op.AND_A_D: ra_ = op_and(ra_, rd_); break;
                case (byte)Op.AND_A_E: ra_ = op_and(ra_, re_); break;
                case (byte)Op.AND_A_F: ra_ = op_and(ra_, rf_); break;
                case (byte)Op.AND_A_L: ra_ = op_and(ra_, rl_); break;
                case (byte)Op.AND_A_ind_HL: ra_ = op_and(ra_, _ram.Read(rhl())); break;
                case (byte)Op.AND_A_imm: ra_ = op_and(ra_, next()); break;
                case (byte)Op.XOR_A_A: ra_ = op_xor(ra_, ra_); break;
                case (byte)Op.XOR_A_B: ra_ = op_xor(ra_, rb_); break;
                case (byte)Op.XOR_A_C: ra_ = op_xor(ra_, rc_); break;
                case (byte)Op.XOR_A_D: ra_ = op_xor(ra_, rd_); break;
                case (byte)Op.XOR_A_E: ra_ = op_xor(ra_, re_); break;
                case (byte)Op.XOR_A_F: ra_ = op_xor(ra_, rf_); break;
                case (byte)Op.XOR_A_L: ra_ = op_xor(ra_, rl_); break;
                case (byte)Op.XOR_A_ind_HL: ra_ = op_xor(ra_, _ram.Read(rhl())); break;
                case (byte)Op.XOR_A_imm: ra_ = op_xor(ra_, next()); break;
                case (byte)Op.OR_A_A: ra_ = op_or(ra_, ra_); break;
                case (byte)Op.OR_A_B: ra_ = op_or(ra_, rb_); break;
                case (byte)Op.OR_A_C: ra_ = op_or(ra_, rc_); break;
                case (byte)Op.OR_A_D: ra_ = op_or(ra_, rd_); break;
                case (byte)Op.OR_A_E: ra_ = op_or(ra_, re_); break;
                case (byte)Op.OR_A_F: ra_ = op_or(ra_, rf_); break;
                case (byte)Op.OR_A_L: ra_ = op_or(ra_, rl_); break;
                case (byte)Op.OR_A_ind_HL: ra_ = op_or(ra_, _ram.Read(rhl())); break;
                case (byte)Op.OR_A_imm: ra_ = op_or(ra_, next()); break;
                case (byte)Op.CP_A: op_cp(ra_); break;
                case (byte)Op.CP_B: op_cp(rb_); break;
                case (byte)Op.CP_C: op_cp(rc_); break;
                case (byte)Op.CP_D: op_cp(rd_); break;
                case (byte)Op.CP_E: op_cp(re_); break;
                case (byte)Op.CP_F: op_cp(rf_); break;
                case (byte)Op.CP_L: op_cp(rl_); break;
                case (byte)Op.CP_ind_HL: op_cp(_ram.Read(rhl())); break;
                case (byte)Op.CP_imm: op_cp(next()); break;
                case (byte)Op.INC_A: ra_ = op_inc(ra_); break;
                case (byte)Op.INC_B: rb_ = op_inc(rb_); break;
                case (byte)Op.INC_C: rc_ = op_inc(rc_); break;
                case (byte)Op.INC_D: rd_ = op_inc(rd_); break;
                case (byte)Op.INC_E: re_ = op_inc(re_); break;
                case (byte)Op.INC_F: rf_ = op_inc(rf_); break;
                case (byte)Op.INC_L: rl_ = op_inc(rl_); break;
                case (byte)Op.INC_ind_HL: tmp = _ram.Read(rhl()) + next(); _ram.Write(tmp, op_inc(_ram.Read(tmp))); break;
                case (byte)Op.DEC_A: ra_ = op_dec(ra_); break;
                case (byte)Op.DEC_B: rb_ = op_dec(rb_); break;
                case (byte)Op.DEC_C: rc_ = op_dec(rc_); break;
                case (byte)Op.DEC_D: rd_ = op_dec(rd_); break;
                case (byte)Op.DEC_E: re_ = op_dec(re_); break;
                case (byte)Op.DEC_F: rf_ = op_dec(rf_); break;
                case (byte)Op.DEC_L: rl_ = op_dec(rl_); break;
                case (byte)Op.DEC_ind_HL: tmp = _ram.Read(rhl()) + next(); _ram.Write(tmp, op_dec(_ram.Read(tmp))); break;
                case (byte)Op.JP: rpc_ = next16(); break;
                case (byte)Op.JP_C: tmp16 = next16(); if (fc()) rpc_ = tmp16; break;
                case (byte)Op.JP_NC: tmp16 = next16(); if (!fc()) rpc_ = tmp16; break;
                case (byte)Op.JP_Z: tmp16 = next16(); if (fz()) rpc_ = tmp16; break;
                case (byte)Op.JP_NZ: tmp16 = next16(); if (!fz()) rpc_ = tmp16; break;
                case (byte)Op.JP_PO: tmp16 = next16(); if (!fpv()) rpc_ = tmp16; break;
                case (byte)Op.JP_PE: tmp16 = next16(); if (fpv()) rpc_ = tmp16; break;
                case (byte)Op.JP_M: tmp16 = next16(); if (fs()) rpc_ = tmp16; break;
                case (byte)Op.JP_P: tmp16 = next16(); if (!fs()) rpc_ = tmp16; break;
                case (byte)Op.JP_ind_HL: rpc_ = _ram.Read(rhl()); break;
                case (byte)Op.JR: tmp = next(); rpc_ = tmp; break;
                case (byte)Op.JR_C: tmp = next(); if (fc()) rpc_ = tmp; break;
                case (byte)Op.JR_NC: tmp = next(); if (!fc()) rpc_ = tmp; break;
                case (byte)Op.JR_Z: tmp = next(); if (fz()) rpc_ = tmp; break;
                case (byte)Op.JR_NZ: tmp = next(); if (!fz()) rpc_ = tmp; break;
                case (byte)Op.DJNZ: tmp = next(); if (--rb_ != 0) rpc_ = tmp; break;
                case (byte)Op.OUT_nn_A: write_io(next(), ra_); break;
                case (byte)Op.IN_A_nn: ra_ = read_io(next()); break;

                case (byte)Op.EXT_DD:
                    switch (code = next())
                    {
                        case (byte)DDOp.DD_LD_B_imm: rb_ = next(); break;
                        case (byte)DDOp.DD_LD_C_imm: rc_ = next(); break;
                        case (byte)DDOp.DD_LD_D_imm: rd_ = next(); break;
                        case (byte)DDOp.DD_LD_E_imm: re_ = next(); break;
                        case (byte)DDOp.DD_LD_H_imm: rh_ = next(); break;
                        case (byte)DDOp.DD_LD_A_idx_IY: ra_ = _ram.Read(riy_ + next()); break;
                        case (byte)DDOp.DD_LD_B_idx_IX: rb_ = _ram.Read(rix_ + next()); break;
                        case (byte)DDOp.DD_LD_C_idx_IX: rc_ = _ram.Read(rix_ + next()); break;
                        case (byte)DDOp.DD_LD_D_idx_IX: rd_ = _ram.Read(rix_ + next()); break;
                        case (byte)DDOp.DD_LD_E_idx_IX: re_ = _ram.Read(rix_ + next()); break;
                        case (byte)DDOp.DD_LD_H_idx_IX: rh_ = _ram.Read(rix_ + next()); break;
                        case (byte)DDOp.DD_LD_L_idx_IX: rl_ = _ram.Read(rix_ + next()); break;
                        case (byte)DDOp.DD_LD_idx_IX_A: _ram.Write(rix_ + next(), ra_); break;
                        case (byte)DDOp.DD_LD_idx_IX_B: _ram.Write(rix_ + next(), rb_); break;
                        case (byte)DDOp.DD_LD_idx_IX_C: _ram.Write(rix_ + next(), rc_); break;
                        case (byte)DDOp.DD_LD_idx_IX_D: _ram.Write(rix_ + next(), rd_); break;
                        case (byte)DDOp.DD_LD_idx_IX_E: _ram.Write(rix_ + next(), re_); break;
                        case (byte)DDOp.DD_LD_idx_IX_F: _ram.Write(rix_ + next(), rf_); break;
                        case (byte)DDOp.DD_LD_idx_IX_L: _ram.Write(rix_ + next(), rl_); break;
                        case (byte)DDOp.DD_LD_idx_IX_imm: _ram.Write(rix_ + next(), next()); break;
                        case (byte)DDOp.DD_LD_ind_HL_imm: _ram.Write(rhl(), next()); break;
                        case (byte)DDOp.DD_ADD_A_idx_IX: ra_ = op_add(ra_, _ram.Read(rix_ + next())); break;
                        case (byte)DDOp.DD_ADC_A_idx_IX: ra_ = op_adc(ra_, _ram.Read(rix_ + next())); break;
                        case (byte)DDOp.DD_SUB_A_idx_IX: ra_ = op_sub(ra_, _ram.Read(rix_ + next())); break;
                        case (byte)DDOp.DD_SBC_A_idx_IX: ra_ = op_sbc(ra_, _ram.Read(rix_ + next())); break;
                        case (byte)DDOp.DD_AND_A_idx_IX: ra_ = op_and(ra_, _ram.Read(rix_ + next())); break;
                        case (byte)DDOp.DD_XOR_A_idx_IX: ra_ = op_xor(ra_, _ram.Read(rix_ + next())); break;
                        case (byte)DDOp.DD_OR_A_idx_IX: ra_ = op_or(ra_, _ram.Read(rix_ + next())); break;
                        case (byte)DDOp.DD_CP_idx_IX: op_cp(_ram.Read(rix_ + next())); break;
                        case (byte)DDOp.DD_JP_ind_IX: rpc_ = _ram.Read(rix_); break;

                        default:

                            break;
                    }
                    break;

                case (byte)Op.EXT_ED:
                    switch (next())
                    {
                        case (byte)EDOp.ED_LD_imp_I_A: ri_ = ra_; break;
                        case (byte)EDOp.ED_LD_imp_R_A: rr_ = ra_; break;

                        default:

                            break;
                    }
                    break;

                case (byte)Op.EXT_FD:
                    switch (code = next())
                    {
                        case (byte)FDOp.FD_LD_A_imm: ra_ = next(); break;
                        case (byte)FDOp.FD_LD_A_ext: ra_ = _ram.Read(next16()); break;
                        case (byte)FDOp.FD_LD_A_idx_IX: ra_ = _ram.Read(rix_ + next()); break;
                        case (byte)FDOp.FD_LD_B_idx_IY: rb_ = _ram.Read(riy_ + next()); break;
                        case (byte)FDOp.FD_LD_C_idx_IY: rc_ = _ram.Read(riy_ + next()); break;
                        case (byte)FDOp.FD_LD_D_idx_IY: rd_ = _ram.Read(riy_ + next()); break;
                        case (byte)FDOp.FD_LD_E_idx_IY: re_ = _ram.Read(riy_ + next()); break;
                        case (byte)FDOp.FD_LD_H_idx_IY: rh_ = _ram.Read(riy_ + next()); break;
                        case (byte)FDOp.FD_LD_idx_IY_A: _ram.Write(riy_ + next(), ra_); break;
                        case (byte)FDOp.FD_LD_idx_IY_B: _ram.Write(riy_ + next(), rb_); break;
                        case (byte)FDOp.FD_LD_idx_IY_C: _ram.Write(riy_ + next(), rc_); break;
                        case (byte)FDOp.FD_LD_idx_IY_D: _ram.Write(riy_ + next(), rd_); break;
                        case (byte)FDOp.FD_LD_idx_IY_E: _ram.Write(riy_ + next(), re_); break;
                        case (byte)FDOp.FD_LD_idx_IY_F: _ram.Write(riy_ + next(), rf_); break;
                        case (byte)FDOp.FD_LD_idx_IY_L: _ram.Write(riy_ + next(), rl_); break;
                        case (byte)FDOp.FD_LD_idx_IY_imm: _ram.Write(riy_ + next(), next()); break;
                        case (byte)FDOp.FD_ADD_A_idx_IY: ra_ = op_add(ra_, _ram.Read(riy_ + next())); break;
                        case (byte)FDOp.FD_ADC_A_idx_IY: ra_ = op_adc(ra_, _ram.Read(riy_ + next())); break;
                        case (byte)FDOp.FD_SUB_A_idx_IY: ra_ = op_sub(ra_, _ram.Read(riy_ + next())); break;
                        case (byte)FDOp.FD_SBC_A_idx_IY: ra_ = op_sbc(ra_, _ram.Read(riy_ + next())); break;
                        case (byte)FDOp.FD_AND_A_idx_IY: ra_ = op_and(ra_, _ram.Read(riy_ + next())); break;
                        case (byte)FDOp.FD_XOR_A_idx_IY: ra_ = op_xor(ra_, _ram.Read(riy_ + next())); break;
                        case (byte)FDOp.FD_OR_A_idx_IY: ra_ = op_or(ra_, _ram.Read(riy_ + next())); break;
                        case (byte)FDOp.FD_CP_idx_IY: op_cp(_ram.Read(riy_ + next())); break;
                        case (byte)FDOp.FD_JP_ind_IY: rpc_ = _ram.Read(riy_); break;

                        default:
                            break;
                    }

                    break;

                default:

                    break;
            }
        }

        //public void run_to_nop()
        //{

        //    while (read(rpc_) != -1)
        //    {
        //        step();
        //    }
        //}


        byte w_calc_flags(int result, bool is_sub)
        {
            rf_ = 0;

            if ((result & 0x0100) != 0) rf_ |= FLAG_C;
            if (is_sub) rf_ |= FLAG_N;
            if (result > 0xFF) rf_ |= FLAG_PV;
            if ((result & 0x08) != 0) rf_ |= FLAG_H;
            if (result == 0) rf_ |= FLAG_Z;
            if ((result & 0x80) != 0) rf_ |= FLAG_S;

            return (byte)result;

        }

        byte w_logic_flags(byte result)
        {
            rf_ = 0;

            int x = result;

            x ^= x >> 4;
            x ^= x >> 2;
            x ^= x >> 1;

            bool parity = ((~x) & 0x01) == 0;

            if (parity) rf_ |= FLAG_PV;
            if (result == 0) rf_ |= FLAG_Z;
            if ((result & 0x80) != 0) rf_ |= FLAG_S;

            return result;

        }


        public void write_io(int addr, byte val)
        {
            if ((addr & 0xFF) == 0x00FE) //перехват порта 0xFE
            {
                _ram.Border = (val & 0x07);
            }
        }

        public byte read_io(int addr)
        {
            return 0;// read(addr);
        }      
    }
}