using System;

namespace Mz1500SoundPlayer.Sound.Emulator
{
    public class Intel8253 : IIoDevice
    {
        private class Channel
        {
            public ushort CountReg;
            public ushort CurrentCount;
            public ushort Latch;
            public byte Mode;
            public byte AccessMode; // 1=L, 2=M, 3=L/M
            
            public bool Bcd;
            public bool RlState; // false=Lower, true=Upper (for mode 3)
            public bool Latched;

            public Action OnInterrupt; // Triggered when timer reaches 0 (Mode 0) or repeats (Mode 2/3)

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
                    CountReg = (ushort)((CountReg & 0xFF00) | data);
                    CurrentCount = CountReg;
                }
                else if (AccessMode == 2) // MSB only
                {
                    CountReg = (ushort)((CountReg & 0x00FF) | (data << 8));
                    CurrentCount = CountReg;
                }
                else if (AccessMode == 3) // LSB then MSB
                {
                    if (!RlState)
                    {
                        CountReg = (ushort)((CountReg & 0xFF00) | data);
                        RlState = true;
                    }
                    else
                    {
                        CountReg = (ushort)((CountReg & 0x00FF) | (data << 8));
                        CurrentCount = CountReg;
                        RlState = false;
                    }
                }
            }

            public byte ReadCount()
            {
                if (Latched)
                {
                    byte res = 0;
                    if (AccessMode == 1) res = (byte)(Latch & 0xFF);
                    else if (AccessMode == 2) res = (byte)(Latch >> 8);
                    else
                    {
                        if (!RlState)
                        {
                            res = (byte)(Latch & 0xFF);
                            RlState = true;
                        }
                        else
                        {
                            res = (byte)(Latch >> 8);
                            RlState = false;
                            Latched = false; // unlatch
                        }
                    }
                    return res;
                }
                else
                {
                    // If not latched, read on-the-fly
                    byte res = 0;
                    if (AccessMode == 1) res = (byte)(CurrentCount & 0xFF);
                    else if (AccessMode == 2) res = (byte)(CurrentCount >> 8);
                    else
                    {
                        if (!RlState)
                        {
                            res = (byte)(CurrentCount & 0xFF);
                            RlState = true;
                        }
                        else
                        {
                            res = (byte)(CurrentCount >> 8);
                            RlState = false;
                        }
                    }
                    return res;
                }
            }

            public void LatchCommand()
            {
                Latch = CurrentCount;
                Latched = true;
                if (AccessMode == 3) RlState = false;
            }

            public void Tick()
            {
                if (CurrentCount > 0)
                {
                    CurrentCount--;
                    if (CurrentCount == 0)
                    {
                        OnInterrupt?.Invoke();
                        
                        if (Mode == 2 || Mode == 3)
                        {
                            // Auto reload
                            CurrentCount = CountReg;
                        }
                    }
                }
            }
        }

        private Channel[] _channels = new Channel[3];

        public Intel8253()
        {
            for (int i = 0; i < 3; i++) _channels[i] = new Channel();
        }

        public void Reset()
        {
            for (int i = 0; i < 3; i++) _channels[i] = new Channel();
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

        // Must be called from the main emulator loop
        public void Tick()
        {
            _channels[0].Tick();
            _channels[1].Tick();
            _channels[2].Tick();
        }
    }
}
