using System;
using Konamiman.Z80dotNet;
using System.Collections.Generic;

namespace Mz1500SoundPlayer.Sound.Emulator
{
    public class Mz1500Memory : IMemory
    {
        private byte[] _ram = new byte[65536];
        private byte[] _rom = new byte[16384]; // 0000-3FFF (MONITOR ROM)
        private byte[] _pcg = new byte[8192];  // 1000-2FFF (PCG RAM, mapped at various banks)
        private byte[] _vram = new byte[8192]; // D000-EFFF (VRAM)
        private byte[] _sram = new byte[8192]; // E000-FFFF (SRAM) - Sound registers mapped in this area D800-DFFF etc.

        private bool _romEnabled = true;

        public int Size => 65536;

        public void SetRomEnabled(bool enabled)
        {
            _romEnabled = enabled;
        }

        public byte[] GetContents(int startAddress, int length)
        {
            var result = new byte[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = GetByte(startAddress + i);
            }
            return result;
        }

        public void SetContents(int startAddress, byte[] contents, int startIndex = 0, int? length = null)
        {
            int len = length ?? (contents.Length - startIndex);
            for (int i = 0; i < len; i++)
            {
                SetByte(startAddress + i, contents[startIndex + i]);
            }
        }

        public byte GetByte(int address)
        {
            address &= 0xFFFF;
            
            if (address >= 0xE000 && address <= 0xE00F)
            {
                if (_deviceReadMapper != null)
                {
                    return _deviceReadMapper((ushort)address);
                }
            }

            // Basic memory map for MZ-1500
            // 0000 - 3FFF: ROM (if enabled), else RAM
            if (address < 0x4000)
            {
                if (_romEnabled) return _rom[address];
                return _ram[address];
            }
            // Add PCG / VRAM mappings later if necessary, but RAM is default
            return _ram[address];
        }

        public void SetByte(int address, byte value)
        {
            address &= 0xFFFF;
            
            if (address >= 0xE000 && address <= 0xE00F)
            {
                if (_deviceWriteMapper != null)
                {
                    _deviceWriteMapper((ushort)address, value);
                    return;
                }
            }

            if (address < 0x4000)
            {
                _ram[address] = value; // Always write to RAM even if ROM is enabled (MZ-1500 specs)
                return;
            }
            
            _ram[address] = value;
        }

        public short GetShort(int address)
        {
            return (short)(GetByte(address) | (GetByte(address + 1) << 8));
        }

        private Action<ushort, byte> _deviceWriteMapper;
        private Func<ushort, byte> _deviceReadMapper;

        public void SetDeviceMapper(Action<ushort, byte> writeMapper, Func<ushort, byte> readMapper)
        {
            _deviceWriteMapper = writeMapper;
            _deviceReadMapper = readMapper;
        }

        public void SetShort(int address, short value)
        {
            SetByte(address, (byte)(value & 0xFF));
            SetByte(address + 1, (byte)((value >> 8) & 0xFF));
        }
        
        public byte this[int address]
        {
            get => GetByte(address);
            set => SetByte(address, value);
        }
        
        // Debug method to directly inject binary
        public void LoadBinary(int address, byte[] data)
        {
            Array.Copy(data, 0, _ram, address, data.Length);
        }
    }
}
