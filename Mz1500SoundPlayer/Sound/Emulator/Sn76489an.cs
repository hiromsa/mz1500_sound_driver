using System;

namespace Mz1500SoundPlayer.Sound.Emulator
{
    public class Sn76489an : IIoDevice
    {
        private readonly int[] _registers = new int[8];
        private int _latch;
        private readonly double[] _phase = new double[3];
        private double _noisePhase = 0;
        private ushort _lfsr = 0x4000;
        private const double BaseClock = 4000000.0 / 32.0; // 125,000 Hz

        private static readonly float[] VolumeTable = new float[16]
        {
            1.0000f, 0.7943f, 0.6310f, 0.5012f,
            0.3981f, 0.3162f, 0.2512f, 0.1995f,
            0.1585f, 0.1259f, 0.1000f, 0.0794f,
            0.0631f, 0.0501f, 0.0398f, 0.0000f
        };

        public Sn76489an()
        {
            Reset();
        }

        public void Reset()
        {
            for (int i = 0; i < 8; i++)
            {
                _registers[i] = (i & 1) != 0 ? 15 : 0; // default volume = silent (15)
            }
            _latch = 0;
            _phase[0] = 0;
            _phase[1] = 0;
            _phase[2] = 0;
            _noisePhase = 0;
            _lfsr = 0x4000;
        }

        public byte ReadIo(byte port) => 0xFF;

        public void WriteIo(byte port, byte data)
        {
            if ((data & 0x80) != 0)
            {
                _latch = (data >> 4) & 0x07;
                if ((_latch & 1) == 0 && _latch != 6)
                {
                    _registers[_latch] = (_registers[_latch] & 0x3F0) | (data & 0x0F);
                }
                else
                {
                    _registers[_latch] = data & 0x0F;
                }
            }
            else
            {
                if ((_latch & 1) == 0 && _latch != 6)
                {
                    _registers[_latch] = (_registers[_latch] & 0x0F) | ((data & 0x3F) << 4);
                }
                else
                {
                    _registers[_latch] = data & 0x0F;
                }
            }
        }

        public void Render(float[] buffer, int offset, int count, int sampleRate = 44100)
        {
            for (int s = 0; s < count; s++)
            {
                float sample = 0;

                // 3 Tone Channels
                for (int ch = 0; ch < 3; ch++)
                {
                    int freqReg = _registers[ch * 2];
                    int volReg = _registers[ch * 2 + 1] & 0x0F;
                    float vol = VolumeTable[volReg];

                    if (vol > 0 && freqReg > 1)
                    {
                        double freq = BaseClock / freqReg;
                        _phase[ch] += freq / sampleRate;
                        if (_phase[ch] >= 1.0) _phase[ch] -= (int)_phase[ch];
                        sample += (_phase[ch] < 0.5 ? 0.25f : -0.25f) * vol;
                    }
                }

                // Noise Channel
                int noiseCtrl = _registers[6];
                int noiseVol = _registers[7] & 0x0F;
                float nvol = VolumeTable[noiseVol];

                if (nvol > 0)
                {
                    int rateSelect = noiseCtrl & 3;
                    double noiseFreq = rateSelect switch
                    {
                        0 => BaseClock / 16.0,
                        1 => BaseClock / 32.0,
                        2 => BaseClock / 64.0,
                        _ => (_registers[4] > 1) ? (BaseClock / _registers[4]) : (BaseClock / 16.0)
                    };

                    _noisePhase += noiseFreq / sampleRate;
                    while (_noisePhase >= 1.0)
                    {
                        _noisePhase -= 1.0;
                        int fb = (noiseCtrl & 4) != 0
                            ? ((_lfsr & 1) ^ ((_lfsr >> 3) & 1)) // White noise feedback
                            : (_lfsr & 1);                      // Periodic noise feedback
                        _lfsr = (ushort)((_lfsr >> 1) | (fb << 14));
                    }

                    sample += ((_lfsr & 1) != 0 ? 0.25f : -0.25f) * nvol;
                }

                buffer[offset + s] += sample;
            }
        }
    }
}
