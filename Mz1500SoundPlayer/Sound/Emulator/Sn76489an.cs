using System;

namespace Mz1500SoundPlayer.Sound.Emulator
{
    public class Sn76489an : IIoDevice
    {
        private int[] _registers = new int[8];
        private int _latch;

        public Sn76489an()
        {
            Reset();
        }

        public void Reset()
        {
            for (int i = 0; i < 8; i++)
            {
                _registers[i] = 0;
            }
            _latch = 0;
        }

        public byte ReadIo(byte port)
        {
            // The SN76489AN is generally write-only
            return 0xFF;
        }

        public void WriteIo(byte port, byte data)
        {
            if ((data & 0x80) != 0)
            {
                // LATCH byte
                _latch = (data >> 4) & 0x07;
                _registers[_latch] = (_registers[_latch] & 0x3F0) | (data & 0x0F);
                
                // If it's a volume register (bit 0 of latch is 1), or noise (latch 6)
                if ((_latch & 1) != 0 || _latch == 6)
                {
                    _registers[_latch] = data & 0x0F;
                }
            }
            else
            {
                // DATA byte
                if ((_latch & 1) == 0 && _latch != 6)
                {
                    // Tone frequency (10 bits)
                    _registers[_latch] = (_registers[_latch] & 0x0F) | ((data & 0x3F) << 4);
                }
                else
                {
                    // Volume or noise
                    _registers[_latch] = data & 0x0F;
                }
            }
            
            // Console.WriteLine($"PSG Write: Latch={_latch}, RegValue={_registers[_latch]} (Data={data:X2})");
        }
        
        // Dummy mix for now - will be expanded later if sound output is needed
        public void Mix(int[] buffer, int cnt, int offset = 0)
        {
        }
    }
}
