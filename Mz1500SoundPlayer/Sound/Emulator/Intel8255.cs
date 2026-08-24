using System;

namespace Mz1500SoundPlayer.Sound.Emulator
{
    public class Intel8255 : IIoDevice
    {
        private byte _porta;
        private byte _portb;
        private byte _portc;
        private byte _ctrl;

        // Mode and Direction flags
        private int _modeA;
        private int _modeB;
        private bool _dirA_In;
        private bool _dirB_In;
        private bool _dirCUpper_In;
        private bool _dirCLower_In;

        // Callbacks for when ports are written to (e.g. to PSG or Beep)
        public Action<byte> OnPortAWrite;
        public Action<byte> OnPortBWrite;
        public Action<byte> OnPortCWrite;

        // Delegates for when ports are read from (e.g. Keyboard)
        public Func<byte> OnPortARead;
        public Func<byte> OnPortBRead;
        public Func<byte> OnPortCRead;

        public void Reset()
        {
            _porta = 0;
            _portb = 0;
            _portc = 0;
            WriteControl(0x9B); // Default: all input, mode 0
        }

        public byte ReadIo(byte port)
        {
            switch (port & 0x03)
            {
                case 0: return _dirA_In && OnPortARead != null ? OnPortARead() : _porta;
                case 1: return _dirB_In && OnPortBRead != null ? OnPortBRead() : _portb;
                case 2: return OnPortCRead != null ? OnPortCRead() : _portc; // simplified C read
                case 3: return _ctrl;
            }
            return 0xFF;
        }

        public void WriteIo(byte port, byte data)
        {
            switch (port & 0x03)
            {
                case 0:
                    if (!_dirA_In)
                    {
                        _porta = data;
                        OnPortAWrite?.Invoke(data);
                    }
                    break;
                case 1:
                    if (!_dirB_In)
                    {
                        _portb = data;
                        OnPortBWrite?.Invoke(data);
                    }
                    break;
                case 2:
                    // Simplified Port C write
                    _portc = data;
                    OnPortCWrite?.Invoke(data);
                    break;
                case 3:
                    WriteControl(data);
                    break;
            }
        }

        private void WriteControl(byte data)
        {
            if ((data & 0x80) != 0)
            {
                // Mode set flag active
                _ctrl = data;
                _modeA = (data >> 5) & 0x03;
                _dirA_In = (data & 0x10) != 0;
                _dirCUpper_In = (data & 0x08) != 0;
                _modeB = (data & 0x04) != 0 ? 1 : 0;
                _dirB_In = (data & 0x02) != 0;
                _dirCLower_In = (data & 0x01) != 0;

                // Reset ports on mode change
                _porta = 0;
                _portb = 0;
                _portc = 0;
            }
            else
            {
                // Bit set/reset on Port C
                int bit = (data >> 1) & 0x07;
                bool set = (data & 0x01) != 0;
                if (set)
                    _portc |= (byte)(1 << bit);
                else
                    _portc &= (byte)~(1 << bit);
                
                OnPortCWrite?.Invoke(_portc);
            }
        }
    }
}
