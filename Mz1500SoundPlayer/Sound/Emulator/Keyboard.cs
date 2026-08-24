using System;

namespace Mz1500SoundPlayer.Sound.Emulator
{
    public class Keyboard
    {
        private byte _matrixStrobe;

        public void Reset()
        {
            _matrixStrobe = 0;
        }

        public void SetStrobe(byte strobe)
        {
            _matrixStrobe = strobe;
        }

        public byte ReadMatrix()
        {
            // For headless sound emulation, just return 0xFF (no keys pressed)
            // or 0x00 depending on active low/high. MZ-1500 uses active low for key matrix.
            // If the sound driver or Monitor ROM hangs waiting for a key, we might need
            // to implement specific key presses here.
            return 0xFF; 
        }
    }
}
