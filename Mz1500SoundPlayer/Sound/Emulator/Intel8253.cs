using System;

namespace Mz1500SoundPlayer.Sound.Emulator
{
    public class Intel8253 : IIoDevice
    {
        public class Channel
        {
            public int CountReg;
            public int CurrentCount = 0x10000;
            public int Latch;
            public byte Mode = 3;
            public byte AccessMode = 3; // 1=L, 2=M, 3=L/M
            
            public bool Bcd;
            public bool RlState; // false=Lower, true=Upper (for mode 3)
            public bool Latched;

            public Action? OnInterrupt;

            public void Reset()
            {
                CountReg = 0;
                CurrentCount = 0x10000;
                Latch = 0;
                Mode = 3;
                AccessMode = 3;
                Bcd = false;
                RlState = false;
                Latched = false;
            }

            public void WriteCtrl(byte data)
            {
                Mode = (byte)((data >> 1) & 0x07);
                Bcd = (data & 0x01) != 0;
                AccessMode = (byte)((data >> 4) & 0x03);
                RlState = false;
                Latched = false;
            }

            public void WriteCount(byte data)
            {
                if (AccessMode == 1) // LSB only
                {
                    CountReg = data;
                    CurrentCount = (CountReg == 0) ? 0x10000 : CountReg;
                }
                else if (AccessMode == 2) // MSB only
                {
                    CountReg = (data << 8);
                    CurrentCount = (CountReg == 0) ? 0x10000 : CountReg;
                }
                else if (AccessMode == 3) // LSB then MSB
                {
                    if (!RlState)
                    {
                        CountReg = data;
                        RlState = true;
                    }
                    else
                    {
                        CountReg = (CountReg & 0xFF) | (data << 8);
                        CurrentCount = (CountReg == 0) ? 0x10000 : CountReg;
                        RlState = false;
                    }
                }
            }

            public byte ReadCount()
            {
                int val = Latched ? Latch : CurrentCount;
                if (val == 0x10000) val = 0; // 65536 is read as 0x0000

                byte res = 0;
                if (AccessMode == 1)
                {
                    res = (byte)(val & 0xFF);
                }
                else if (AccessMode == 2)
                {
                    res = (byte)((val >> 8) & 0xFF);
                }
                else
                {
                    if (!RlState)
                    {
                        res = (byte)(val & 0xFF);
                        RlState = true;
                    }
                    else
                    {
                        res = (byte)((val >> 8) & 0xFF);
                        RlState = false;
                        if (Latched) Latched = false; // Unlatch after both bytes read
                    }
                }
                return res;
            }

            public void LatchCommand()
            {
                Latch = CurrentCount;
                Latched = true;
                RlState = false;
            }

            public bool Tick()
            {
                if (CurrentCount <= 1)
                {
                    OnInterrupt?.Invoke();
                    int reload = (CountReg == 0) ? 0x10000 : CountReg;
                    CurrentCount = reload;
                    return true;
                }
                else
                {
                    CurrentCount--;
                    return false;
                }
            }
        }

        private readonly Channel[] _channels = new Channel[3];

        public Intel8253()
        {
            for (int i = 0; i < 3; i++) _channels[i] = new Channel();
        }

        public void Reset()
        {
            for (int i = 0; i < 3; i++) _channels[i].Reset();
        }

        public void RegisterInterruptHandler(int channelIndex, Action handler)
        {
            _channels[channelIndex].OnInterrupt = handler;
        }

        public byte ReadIo(byte port)
        {
            int ch = port & 0x03;
            if (ch < 3)
            {
                return _channels[ch].ReadCount();
            }
            return 0xFF;
        }

        public void WriteIo(byte port, byte data)
        {
            int ch = port & 0x03;
            if (ch < 3)
            {
                _channels[ch].WriteCount(data);
            }
            else
            {
                // Control word
                int target = (data >> 6) & 0x03;
                if (target < 3)
                {
                    if (((data >> 4) & 0x03) == 0)
                    {
                        _channels[target].LatchCommand();
                    }
                    else
                    {
                        _channels[target].WriteCtrl(data);
                    }
                }
            }
        }

        public void TickChannel0()
        {
            _channels[0].Tick();
        }

        public void TickChannel1()
        {
            // In MZ-1500, Channel 1 OUT clocks Channel 2
            if (_channels[1].Tick())
            {
                _channels[2].Tick();
            }
        }

        public void TickChannel2()
        {
            _channels[2].Tick();
        }
    }
}
