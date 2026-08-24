using System;

namespace Mz1500SoundPlayer.Sound.Emulator
{
    public class Keyboard
    {
        private byte _matrixStrobe;
        private readonly byte[] _keyMatrix = new byte[16];

        public Keyboard()
        {
            Reset();
        }

        public void Reset()
        {
            _matrixStrobe = 0;
            // 0xFF means no keys are pressed (MZ-1500 key matrix is active low)
            for (int i = 0; i < 16; i++)
            {
                _keyMatrix[i] = 0xFF;
            }
        }

        public void SetStrobe(byte strobe)
        {
            _matrixStrobe = (byte)(strobe & 0x0F);
        }

        public byte ReadMatrix()
        {
            if (_matrixStrobe < 16)
            {
                return _keyMatrix[_matrixStrobe];
            }
            return 0xFF; 
        }

        public void SetKeyDown(int row, int col)
        {
            if (row >= 0 && row < 16 && col >= 0 && col < 8)
            {
                _keyMatrix[row] &= (byte)~(1 << col);
            }
        }

        public void SetKeyUp(int row, int col)
        {
            if (row >= 0 && row < 16 && col >= 0 && col < 8)
            {
                _keyMatrix[row] |= (byte)(1 << col);
            }
        }
    }
}
