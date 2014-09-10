using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECore.HardwareInterfaces
{
#if INTERNAL
    public
#else
    internal
#endif
    static class SmartScopeUsbInterfaceHelpers
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
