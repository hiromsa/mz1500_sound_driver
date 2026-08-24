using System;

namespace Mz1500SoundPlayer.Sound.Emulator
{
    public class Beep : IEmulatorDevice
    {
        private bool _signal;
        private int _count;
        private bool _on;
        private bool _mute;
        
        private int _genRate;
        private int _genVol;
        private int _diff;

        public Beep()
        {
            InitializeSound(44100, 1000.0, 8192);
        }

        public void InitializeSound(int rate, double frequency, int volume)
        {
            _genRate = rate;
            _genVol = volume;
            SetFrequency(frequency);
        }

        public void SetFrequency(double frequency)
        {
            if (frequency > 0)
                _diff = (int)(1024.0 * _genRate / frequency / 2.0 + 0.5);
        }

        public void Reset()
        {
            _signal = true;
            _count = 0;
            _on = false;
            _mute = false;
        }

        public void SetOn(bool on)
        {
            _on = on;
        }

        public void SetMute(bool mute)
        {
            _mute = mute;
        }

        // Dummy mix for now - will be expanded later if sound output is needed
        public void Mix(int[] buffer, int cnt, int offset = 0)
        {
            if (_on && !_mute)
            {
                // Simple square wave mix logic can go here
                for (int i = 0; i < cnt; i++)
                {
                    int sample = (_count < 1024) ? (_genVol * (_count - 512)) / 512 : _genVol;
                    int vol = _signal ? sample : -sample;
                    
                    // Left
                    buffer[offset + i * 2] += vol;
                    // Right
                    buffer[offset + i * 2 + 1] += vol;

                    if ((_count -= 1024) < 0)
                    {
                        _count += _diff;
                        _signal = !_signal;
                    }
                }
            }
        }
    }
}
