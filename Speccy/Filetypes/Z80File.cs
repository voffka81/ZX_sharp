using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Speccy.Filetypes
{
    public class Z80File
    {
        private static void GetPage(byte[] buffer, int counter, byte[] bank, int dataLength)
        {
            if (dataLength == 0xffff)
            {
                Array.Copy(buffer, counter, bank, 0, 16384);
            }
            else //Compressed data (needs testing!)
            {
                int dataBlockOffset = counter;
                int memStart = 0;
                while ((counter - dataBlockOffset) < dataLength)
                {
                    byte bite = buffer[counter++];

                    if (bite == 0xED)
                    {
                        int bite2 = buffer[counter];
                        if (bite2 == 0xED)
                        {
                            counter++;
                            int dataSize = buffer[counter++];
                            byte data = buffer[counter++];

                            //compressed data
                            for (int f = 0; f < dataSize; f++)
                            {
                                bank[memStart++] = data;
                            }
                            continue;
                        }
                        bank[memStart++] = bite;
                        continue;
                    }
                    else
                        bank[memStart++] = bite;
                    //   dataCounter = counter - dataBlockOffset;
                }
            }
        }

        public static Z80_Snapshot LoadZ80(System.IO.Stream fs)
        {
            Z80_Snapshot snapshot = new Z80_Snapshot();
            using (System.IO.BinaryReader r = new System.IO.BinaryReader(fs))
            {
                int bytesToRead = (int)fs.Length;

                byte[] buffer = new byte[bytesToRead];
                int bytesRead = r.Read(buffer, 0, bytesToRead);

                if (bytesRead == 0)
                    return null; //something bad happened!

                snapshot.AF = (ushort)(buffer[0] << 8);
                snapshot.AF |= buffer[1];
                snapshot.BC = (ushort)(buffer[2] | (buffer[3] << 8));
                snapshot.HL = (ushort)(buffer[4] | (buffer[5] << 8));
                snapshot.PC = (ushort)(buffer[6] | (buffer[7] << 8));
                snapshot.SP = (ushort)(buffer[8] | (buffer[9] << 8));
                snapshot.I = buffer[10];
                snapshot.R = buffer[11];

                byte byte12 = buffer[12];
                if (byte12 == 255)
                    byte12 = 1;

                snapshot.R |= (byte)((byte12 & 0x01) << 7);
                snapshot.BORDER = (byte)((byte12 >> 1) & 0x07);
                bool isCompressed = ((byte12 & 0x20) != 0);

                snapshot.DE = (ushort)(buffer[13] | (buffer[14] << 8));
                snapshot.BC_ = (ushort)(buffer[15] | (buffer[16] << 8));
                snapshot.DE_ = (ushort)(buffer[17] | (buffer[18] << 8));
                snapshot.HL_ = (ushort)(buffer[19] | (buffer[20] << 8));
                snapshot.AF_ = (ushort)((buffer[21] << 8) | buffer[22]);

                snapshot.IY = (ushort)(buffer[23] | (buffer[24] << 8));
                snapshot.IX = (ushort)(buffer[25] | (buffer[26] << 8));

                snapshot.IFF1 = (buffer[27] != 0);
                snapshot.IFF2 = (buffer[28] != 0);

                byte byte29 = buffer[29];

                snapshot.IM = (byte)(byte29 & 0x3);
                snapshot.ISSUE2 = ((byte29 & 0x08) != 0);


                //Version 2 or 3
                if (snapshot.PC == 0)
                {
                    int headerLength = buffer[30];
                    snapshot.PC = (ushort)(buffer[32] | (buffer[33] << 8));
                    switch (buffer[34])
                    {
                        case 0:
                            snapshot.TYPE = 0;
                            break;

                        case 1:
                            snapshot.TYPE = 0;
                            break;

                        case 3:
                            if (headerLength == 23)
                                snapshot.TYPE = 1;
                            else
                                snapshot.TYPE = 0;
                            break;

                        case 4:
                            snapshot.TYPE = 1;
                            break;

                        case 5:
                            snapshot.TYPE = 1;
                            break;

                        case 6:
                            snapshot.TYPE = 1;
                            break;

                        case 7:
                            snapshot.TYPE = 2;
                            break;

                        case 8:
                            snapshot.TYPE = 2;
                            break;

                        case 9:
                            snapshot.TYPE = 3;
                            break;
                    }
                    int counter = 32 + headerLength;

                    //128K or Pentagon?
                    // if ((snapshot.TYPE == 1) || (snapshot.TYPE == 3))
                    {
                        snapshot.PORT_7FFD = buffer[35];
                        snapshot.AY_FOR_48K = (buffer[37] & 0x4) != 0;
                        snapshot.PORT_FFFD = buffer[38];
                        snapshot.AY_REGS = new byte[16];
                        for (int f = 0; f < 16; f++)
                            snapshot.AY_REGS[f] = buffer[39 + f];
                    }

                    snapshot.TSTATES = 0;
                    if (headerLength != 23)
                    {
                        snapshot.TSTATES = (buffer[55] | (buffer[56] << 8)) * buffer[57];
                        if (headerLength == 55)
                            snapshot.PORT_1FFD = buffer[86];
                    }

                    byte[] _bank = new byte[16384];

                    //Load rest of the data
                    while (counter < buffer.Length)
                    {
                        //Get length of data block
                        int dataLength = buffer[counter] | (buffer[counter + 1] << 8);
                        counter += 2;
                        if (counter >= buffer.Length) break;  //Some 128K .z80 files have a trailing zero or two (DamienG)
                        int page = buffer[counter++];

                        //copies page data to temporary RAM array
                        GetPage(buffer, counter, _bank, dataLength);
                        counter += (dataLength == 0xffff ? 16384 : dataLength);

                        switch (page)
                        {
                            //Ignore any ROM pages.
                            //For 128k we can deduce from 0x7ffd, which ROM to use.
                            case 0:
                                break;

                            case 1:
                                break;

                            case 2:
                                break;

                            case 3:
                                break;

                            case 4:
                                if (snapshot.TYPE > 0)
                                {
                                }
                                else //48k
                                {
                                    for(int i=0;i<16384;i++)
                                    {
                                        snapshot.RAM_BANK[16384 + i] = _bank[i];
                                    }
                                    //Array.Copy(_bank, 16384, snapshot.RAM_BANK, 0, 16384);
                                }
                                break;

                            case 5:
                                if (snapshot.TYPE > 0)
                                {
                                }
                                else //48k
                                {
                                    for (int i = 0; i < 16384; i++)
                                    {
                                        snapshot.RAM_BANK[32768 + i] = _bank[i];
                                    }
                                }
                                break;

                            case 6:
                                break;

                            case 7:
                                break;

                            case 8:

                                //4000 - 7fff//for both 48k and 128k
                                for (int i = 0; i < 16384; i++)
                                {
                                    snapshot.RAM_BANK[ i] = _bank[i];
                                }
                                break;

                            case 9:
                               
                                break;

                            case 10:
                                break;

                            default:
                                break;
                        }
                    }
                }
                else //Version 1
                {
                    snapshot.TYPE = 0;
                    //int screenAddr = GetPageAddress(10);
                    byte[] RAM_48K = new byte[49152];

                    if (!isCompressed)
                    {
                        //copy ram bank 5
                        Array.Copy(buffer, 30, RAM_48K, 0, 49152);
                    }
                    else
                    {
                        bool done = false;
                        int byteCounter = 30;
                        int memCounter = 0;

                        while (!done)
                        {
                            byte bite = buffer[byteCounter++];
                            if (bite == 0)
                            {
                                //check if this is the end marker
                                byte bite2 = buffer[byteCounter];
                                if (bite2 == 0xED)
                                {
                                    byte bite3 = buffer[byteCounter + 1];
                                    if (bite3 == 0xED)
                                    {
                                        byte bite4 = buffer[byteCounter + 2];
                                        if (bite4 == 0)
                                        {
                                            done = true;
                                            break;
                                        }
                                    }
                                }
                                RAM_48K[memCounter++] = bite;
                            }
                            else
                                if (bite == 0xED)
                            {
                                byte bite2 = buffer[byteCounter];
                                if (bite2 == 0xED)
                                {
                                    byteCounter++;
                                    int dataLength = buffer[byteCounter++];
                                    byte data = buffer[byteCounter++];

                                    //compressed data
                                    for (int f = 0; f < dataLength; f++)
                                    {
                                        RAM_48K[memCounter++] = data;
                                    }
                                    continue;
                                }
                                RAM_48K[memCounter++] = bite;
                                continue;
                            }
                            else
                                RAM_48K[memCounter++] = bite;
                        } //while
                    } //compressed

                    snapshot.RAM_BANK = RAM_48K;
                }
            } //binary reader

            return snapshot;
        }

        public static Z80_Snapshot LoadZ80(string filename)
        {
            Z80_Snapshot snapshot;
            using (System.IO.FileStream fs = new System.IO.FileStream(filename, System.IO.FileMode.Open))
            {
                snapshot = LoadZ80(fs);
            }
            return snapshot;
        } //LoadZ80
    }
}
