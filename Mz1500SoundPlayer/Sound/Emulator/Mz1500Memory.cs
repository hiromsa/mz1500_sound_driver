using System;
using Konamiman.Z80dotNet;

namespace Mz1500SoundPlayer.Sound.Emulator
{
    public class Mz1500Memory : IMemory, IIoDevice
    {
        private readonly byte[] _ram = new byte[65536];
        private readonly byte[] _iplRom = new byte[4096]; // 0000-0FFF (4KB)
        private readonly byte[] _extRom = new byte[8192]; // E800-FFFF (6KB or 8KB)
        private readonly byte[] _vram = new byte[4096];   // D000-DFFF (4KB: D000 text, D800 attr)
        private readonly byte[] _pcg = new byte[0x6000];  // 24KB PCG RAM (3 banks of 8KB)
        
        public byte[] Palette { get; } = new byte[8] { 0, 1, 2, 3, 4, 5, 6, 7 };
        public byte Priority { get; private set; } = 0;

        private bool _monLow = true;
        private bool _monHigh = true;
        private bool _pcgEnabled = false;
        private byte _pcgBank = 0;

        private bool _hblank = false;
        private bool _tempo = false;

        public byte[]? CgRom { get; set; }

        public int Size => 65536;

        public Mz1500Memory()
        {
            Reset();
        }

        public void Reset()
        {
            Array.Clear(_ram, 0, _ram.Length);
            Array.Clear(_vram, 0, _vram.Length);
            // Default attribute VRAM in MZ-1500/700 is 0x71 (White on Blue)
            for (int i = 0x800; i < 0xC00; i++)
            {
                _vram[i] = 0x71;
            }
            Array.Clear(_pcg, 0, _pcg.Length);

            for (int i = 0; i < 8; i++) Palette[i] = (byte)i;
            Priority = 0;

            _monLow = true;
            _monHigh = true;
            _pcgEnabled = false;
            _pcgBank = 0;
            _hblank = false;
            _tempo = false;
        }

        public void SetHBlank(bool hblank) => _hblank = hblank;
        public void ToggleTempo() => _tempo = !_tempo;

        public byte[] GetVram() => _vram;
        public byte[] GetPcg() => _pcg;
        public bool IsPcgEnabled => _pcgEnabled;
        public byte PcgBank => _pcgBank;

        public void LoadRom(int startAddress, byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                int addr = startAddress + i;
                if (addr < 0x1000)
                {
                    _iplRom[addr] = data[i];
                }
                else if (addr >= 0xE800 && addr <= 0xFFFF)
                {
                    _extRom[addr - 0xE800] = data[i];
                }
            }
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

            // 0000 - 0FFF: Monitor ROM Low / RAM
            if (address < 0x1000)
            {
                return _monLow ? _iplRom[address] : _ram[address];
            }

            // 1000 - CFFF: Main RAM
            if (address < 0xD000)
            {
                return _ram[address];
            }

            // D000 - DFFF: VRAM / PCG / CGROM / RAM
            if (address < 0xE000)
            {
                if (_pcgEnabled)
                {
                    if ((_pcgBank & 3) != 0)
                    {
                        int offset = ((_pcgBank & 3) - 1) * 0x2000 + (address - 0xD000);
                        if (offset < _pcg.Length) return _pcg[offset];
                        return 0xFF;
                    }
                    else
                    {
                        // CGROM font read
                        if (CgRom != null && (address - 0xD000) < CgRom.Length)
                        {
                            return CgRom[address - 0xD000];
                        }
                        return 0xFF;
                    }
                }
                else if (_monHigh)
                {
                    return _vram[address - 0xD000];
                }
                else
                {
                    return _ram[address];
                }
            }

            // E000 - E7FF: Memory Mapped I/O / RAM
            if (address < 0xE800)
            {
                if (_monHigh)
                {
                    if (address <= 0xE003)
                    {
                        return _pio != null ? _pio.ReadIo((byte)(address & 3)) : (byte)0xFF;
                    }
                    if (address <= 0xE007)
                    {
                        return _pit != null ? _pit.ReadIo((byte)(address & 3)) : (byte)0xFF;
                    }
                    if (address == 0xE008)
                    {
                        // Bit 7: !HBLANK, Bit 0: TEMPO (32kHz)
                        return (byte)((_hblank ? 0x00 : 0x80) | (_tempo ? 0x01 : 0x00) | 0x7E);
                    }
                    return 0xFF;
                }
                else
                {
                    return _ram[address];
                }
            }

            // E800 - FFFF: EXT ROM / RAM
            if (_monHigh)
            {
                int offset = address - 0xE800;
                return offset < _extRom.Length ? _extRom[offset] : (byte)0xFF;
            }
            else
            {
                return _ram[address];
            }
        }

        public void SetByte(int address, byte value)
        {
            address &= 0xFFFF;

            // 0000 - 0FFF: Always write to RAM under ROM on MZ
            if (address < 0x1000)
            {
                _ram[address] = value;
                return;
            }

            // 1000 - CFFF: Main RAM
            if (address < 0xD000)
            {
                _ram[address] = value;
                return;
            }

            // D000 - DFFF: VRAM / PCG / RAM
            if (address < 0xE000)
            {
                if (_pcgEnabled)
                {
                    if ((_pcgBank & 3) != 0)
                    {
                        int offset = ((_pcgBank & 3) - 1) * 0x2000 + (address - 0xD000);
                        if (offset < _pcg.Length) _pcg[offset] = value;
                    }
                    // CGROM is read-only when _pcgBank & 3 == 0
                }
                else if (_monHigh)
                {
                    _vram[address - 0xD000] = value;
                }
                else
                {
                    _ram[address] = value;
                }
                return;
            }

            // E000 - E7FF: Memory Mapped I/O / RAM
            if (address < 0xE800)
            {
                if (_monHigh)
                {
                    if (address <= 0xE003)
                    {
                        _pio?.WriteIo((byte)(address & 3), value);
                    }
                    else if (address <= 0xE007)
                    {
                        _pit?.WriteIo((byte)(address & 3), value);
                    }
                    else if (address == 0xE008)
                    {
                        // 8253 Gate 0
                    }
                }
                else
                {
                    _ram[address] = value;
                }
                return;
            }

            // E800 - FFFF: RAM write
            _ram[address] = value;
        }

        public short GetShort(int address)
        {
            return (short)(GetByte(address) | (GetByte(address + 1) << 8));
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

        public void LoadBinary(int address, byte[] data)
        {
            Array.Copy(data, 0, _ram, address, data.Length);
        }

        // Connect Devices for Memory-Mapped I/O
        private Intel8255? _pio;
        private Intel8253? _pit;

        public void SetDevices(Intel8255 pio, Intel8253 pit)
        {
            _pio = pio;
            _pit = pit;
        }

        // IIoDevice implementation for MZ-1500 memory banking & palette ports
        public byte ReadIo(byte port)
        {
            if (port == 0xE8)
            {
                return 0xEF; // Voice board missing
            }
            return 0xFF;
        }

        public void WriteIo(byte port, byte data)
        {
            switch (port)
            {
                case 0xE0: // Disable Mon Low
                    _monLow = false;
                    break;
                case 0xE1: // Disable Mon High
                    _monHigh = false;
                    break;
                case 0xE2: // Enable Mon Low
                    _monLow = true;
                    break;
                case 0xE3: // Enable Mon High
                    _monHigh = true;
                    break;
                case 0xE4: // Enable Mon Low + High, Disable PCG
                    _monLow = true;
                    _monHigh = true;
                    _pcgEnabled = false;
                    break;
                case 0xE5: // Enable PCG
                    _pcgEnabled = true;
                    _pcgBank = data;
                    break;
                case 0xE6: // Disable PCG
                    _pcgEnabled = false;
                    break;
                case 0xF0: // Priority
                    Priority = data;
                    break;
                case 0xF1: // Palette: upper nibble = index (0-7), lower nibble = color (0-7)
                    Palette[(data >> 4) & 7] = (byte)(data & 7);
                    break;
            }
        }
    }
}
