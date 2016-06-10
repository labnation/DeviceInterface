using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LabNation.DeviceInterface.Hardware
{
#if DEBUG
    public
#else
    internal
#endif
    static class SmartScopeInterfaceHelpers
    {
        public enum PIC_COMMANDS
        {
            PIC_VERSION = 1,
            PIC_WRITE = 2,
            PIC_READ = 3,
            PIC_RESET = 4,
            PIC_BOOTLOADER = 5,
            EEPROM_READ = 6,
            EEPROM_WRITE = 7,
            FLASH_ROM_READ = 8,
            FLASH_ROM_WRITE = 9,
            I2C_WRITE = 10,
            I2C_READ = 11,
            PROGRAM_FPGA_START = 12,
            PROGRAM_FPGA_END = 13,
            I2C_WRITE_START = 14,
            I2C_WRITE_BULK = 15,
            I2C_WRITE_STOP = 16,
        }

        public const byte HEADER_CMD_BYTE = 0xC0; //C0 as in Command
        public const byte HEADER_RESPONSE_BYTE = 0xAD; //AD as in Answer Dude
        public const int FLASH_USER_ADDRESS_MASK = 0x0FFF;
        public const byte FPGA_I2C_ADDRESS_SETTINGS = 0x0C;
        public const byte FPGA_I2C_ADDRESS_ROM = 0x0D;
        public const byte FPGA_I2C_ADDRESS_AWG = 0x0E;
        public const int I2C_MAX_WRITE_LENGTH = 27;
        public const int I2C_MAX_WRITE_LENGTH_BULK = 29;

        public enum Operation { READ, WRITE, WRITE_BEGIN, WRITE_BODY, WRITE_END };

        public static void GetControllerRegister(this ISmartScopeInterface i, ScopeController ctrl, uint address, uint length, out byte[] data)
        {
            //In case of FPGA (I2C), first write address we're gonna read from to FPGA
            //FIXME: this should be handled by the PIC firmware
            if (ctrl == ScopeController.FPGA || ctrl == ScopeController.FPGA_ROM)
                i.SetControllerRegister(ctrl, address, null);

            if (ctrl == ScopeController.FLASH && (address + length) > (FLASH_USER_ADDRESS_MASK + 1))
            {
                throw new ScopeIOException(String.Format("Can't read flash rom beyond 0x{0:X8}", FLASH_USER_ADDRESS_MASK));
            }

            byte[] header = UsbCommandHeader(ctrl, Operation.READ, address, length);
            i.WriteControlBytes(header, false);

            //EP3 always contains 16 bytes xxx should be linked to constant
            //FIXME: use endpoint length or so, or don't pass the argument to the function
            byte[] readback = i.ReadControlBytes(16);
            if(readback == null)
            {
                data = null;
                LabNation.Common.Logger.Error("Failde to read back bytes");
                return;
            }


            int readHeaderLength;
            if (ctrl == ScopeController.FLASH)
                readHeaderLength = 5;
            else
                readHeaderLength = 4;

            //strip away first 4 bytes as these are not data
            data = new byte[length];
            Array.Copy(readback, readHeaderLength, data, 0, length);
        }

        public static void SetControllerRegister(this ISmartScopeInterface i, ScopeController ctrl, uint address, byte[] data)
        {
            if (data != null && data.Length > I2C_MAX_WRITE_LENGTH)
            {
                if (ctrl != ScopeController.AWG)
                    throw new Exception(String.Format("Can't do writes of this length ({0}) to controller {1:G}", data.Length, ctrl));

                int offset = 0;
                byte[] toSend = new byte[32];

                //Begin I2C - send start condition
                i.WriteControlBytes(UsbCommandHeader(ctrl, Operation.WRITE_BEGIN, address, 0), false);

                while (offset < data.Length)
                {
                    int length = Math.Min(data.Length - offset, I2C_MAX_WRITE_LENGTH_BULK);
                    byte[] header = UsbCommandHeader(ctrl, Operation.WRITE_BODY, address, (uint)length);
                    Array.Copy(header, toSend, header.Length);
                    Array.Copy(data, offset, toSend, header.Length, length);
                    i.WriteControlBytes(toSend, false);
                    offset += length;
                }
                i.WriteControlBytes(UsbCommandHeader(ctrl, Operation.WRITE_END, address, 0), false);
            }
            else
            {
                uint length = data != null ? (uint)data.Length : 0;
                byte[] header = UsbCommandHeader(ctrl, Operation.WRITE, address, length);

                //Paste header and data together and send it
                byte[] toSend = new byte[header.Length + length];
                Array.Copy(header, toSend, header.Length);
                if (length > 0)
                    Array.Copy(data, 0, toSend, header.Length, data.Length);
                i.WriteControlBytes(toSend, false);
            }
        }

        public static void SendCommand(this ISmartScopeInterface i, PIC_COMMANDS cmd, bool async = false)
        {
            byte[] toSend = new byte[2] { HEADER_CMD_BYTE, (byte)cmd };
            i.WriteControlBytes(toSend, async);
        }

        public static void LoadBootLoader(this ISmartScopeInterface i)
        {
            SendCommand(i, PIC_COMMANDS.PIC_BOOTLOADER, true);
        }

        public static void Reset(this ISmartScopeInterface i)
        {
            SendCommand(i, PIC_COMMANDS.PIC_RESET, true);
			#if IOS
			Common.Logger.Debug("Destroying interface after reset for ios");
            i.Destroy();
            #endif
        }

        public static byte[] UsbCommandHeader(ScopeController ctrl, Operation op, uint address, uint length)
        {
            byte[] header = null;

            if (ctrl == ScopeController.PIC)
            {
                if (op == Operation.WRITE)
                {
                    header = new byte[4] {
                               HEADER_CMD_BYTE,
               (byte)PIC_COMMANDS.PIC_WRITE, 
                            (byte)(address),
                             (byte)(length)  //first I2C byte: FPGA i2c address (5) + '0' as LSB, indicating write operation
                        };
                }
                else if (op == Operation.READ)
                {
                    header = new byte[4] {
                               HEADER_CMD_BYTE,
                (byte)PIC_COMMANDS.PIC_READ, 
                            (byte)(address),
                             (byte)(length)  //first I2C byte: FPGA i2c address (5) + '0' as LSB, indicating write operation
                        };
                }
            }
            else if (ctrl == ScopeController.ROM)
            {
                if (op == Operation.WRITE)
                {
                    header = new byte[4] {
                               HEADER_CMD_BYTE,
            (byte)PIC_COMMANDS.EEPROM_WRITE, 
                            (byte)(address),
                             (byte)(length)
                        };
                }
                else if (op == Operation.READ)
                {
                    header = new byte[4] {
                               HEADER_CMD_BYTE,
             (byte)PIC_COMMANDS.EEPROM_READ, 
                            (byte)(address),
                             (byte)(length)
                        };
                }
            }
            else if (ctrl == ScopeController.FLASH)
            {
                if (op == Operation.WRITE)
                {
                    header = new byte[5] {
                               HEADER_CMD_BYTE,
         (byte)PIC_COMMANDS.FLASH_ROM_WRITE, 
                            (byte)(address),
                             (byte)(length),
                       (byte)(address >> 8),
                        };
                }
                else if (op == Operation.READ)
                {
                    header = new byte[5] {
                               HEADER_CMD_BYTE,
          (byte)PIC_COMMANDS.FLASH_ROM_READ, 
                            (byte)(address),
                             (byte)(length),
                       (byte)(address >> 8),
                        };
                }
            }
            else if (ctrl == ScopeController.FPGA)
            {
                if (op == Operation.WRITE)
                {
                    header = new byte[5] {
                               HEADER_CMD_BYTE,
               (byte)PIC_COMMANDS.I2C_WRITE,
                         (byte)(length + 2), //data and 2 more bytes: the FPGA I2C address, and the register address inside the FPGA
     (byte)(FPGA_I2C_ADDRESS_SETTINGS << 1), //first I2C byte: FPGA i2c address bit shifted and LSB 0 indicating write
                              (byte)address  //second I2C byte: address of the register inside the FPGA
                    };
                }
                else if (op == Operation.READ)
                {
                    header = new byte[4] {
                               HEADER_CMD_BYTE,
                (byte)PIC_COMMANDS.I2C_READ,
          (byte)(FPGA_I2C_ADDRESS_SETTINGS), //first I2C byte: FPGA i2c address bit shifted and LSB 1 indicating read
                             (byte)(length) 
                    };
                }
            }
            else if (ctrl == ScopeController.FPGA_ROM)
            {
                if (op == Operation.WRITE)
                {
                    header = new byte[5] {
                            HEADER_CMD_BYTE,
               (byte)PIC_COMMANDS.I2C_WRITE,
                         (byte)(length + 2), 
    (byte)((FPGA_I2C_ADDRESS_ROM << 1) + 0), //first I2C byte: FPGA i2c address bit shifted and LSB 1 indicating read
                              (byte)address,
                    };
                }
                else if (op == Operation.READ)
                {
                    header = new byte[4] {
                               HEADER_CMD_BYTE,
                (byte)PIC_COMMANDS.I2C_READ,
                (byte)(FPGA_I2C_ADDRESS_ROM), //first I2C byte: FPGA i2c address, not bitshifted
                             (byte)(length) 
                    };
                }
            }
            else if (ctrl == ScopeController.AWG)
            {
                if (op == Operation.WRITE)
                {
                    header = new byte[5] {
                               HEADER_CMD_BYTE,
               (byte)PIC_COMMANDS.I2C_WRITE,
                         (byte)(length + 2), //data and 2 more bytes: the FPGA I2C address, and the register address inside the FPGA
     (byte)(FPGA_I2C_ADDRESS_AWG << 1), //first I2C byte: FPGA i2c address bit shifted and LSB 0 indicating write
                              (byte)address  //second I2C byte: address of the register inside the FPGA
                    };
                }
                if (op == Operation.WRITE_BEGIN)
                {
                    header = new byte[5] {
                            HEADER_CMD_BYTE,
         (byte)PIC_COMMANDS.I2C_WRITE_START,
                         (byte)(length + 2), //data and 2 more bytes: the FPGA I2C address, and the register address inside the FPGA
          (byte)(FPGA_I2C_ADDRESS_AWG << 1), //first I2C byte: FPGA i2c address bit shifted and LSB 0 indicating write
                              (byte)address  //second I2C byte: address of the register inside the FPGA
                    };
                }
                if (op == Operation.WRITE_BODY)
                {
                    header = new byte[3] {
                               HEADER_CMD_BYTE,
             (byte)PIC_COMMANDS.I2C_WRITE_BULK,
                                (byte)(length), //data and 2 more bytes: the FPGA I2C address, and the register address inside the FPGA
                    };
                }
                if (op == Operation.WRITE_END)
                {
                    header = new byte[3] {
                               HEADER_CMD_BYTE,
             (byte)PIC_COMMANDS.I2C_WRITE_STOP,
                             (byte)(length)
                    };
                }
                else if (op == Operation.READ)
                {
                    throw new Exception("Can't read out AWG");
                }
            }
            return header;
        }
    }
}
