using System;

namespace Mz1500SoundPlayer.Sound.Emulator
{
    public class Pcg : IMemoryDevice
    {
        private byte[] _ram = new byte[8192];
        
        public void Reset()
        {
            Array.Clear(_ram, 0, _ram.Length);
        }

        public byte ReadMemory(ushort address)
        {
            // Usually PCG is mapped to a specific memory window, e.g., 0xE000-0xE7FF
            // address here would be offset.
            if (address < _ram.Length)
                return _ram[address];
            return 0xFF;
        }

        public void WriteMemory(ushort address, byte data)
        {
            if (address < _ram.Length)
            {
                _ram[address] = data;
            }
        }
    }
}
