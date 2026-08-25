using System;
using NAudio.Wave;

namespace Mz1500SoundPlayer.Sound.Emulator
{
    public class EmulatorAudioProvider : IWaveProvider
    {
        private readonly Mz1500Machine _machine;
        public WaveFormat WaveFormat { get; } = new WaveFormat(44100, 16, 2);

        private readonly float[] _leftBuf = new float[2048];
        private readonly float[] _rightBuf = new float[2048];
        private readonly int[][] _opmBuf = new int[2][] { new int[2048], new int[2048] };

        public EmulatorAudioProvider(Mz1500Machine machine)
        {
            _machine = machine;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            // 16-bit stereo: 4 bytes per frame (2 bytes L, 2 bytes R)
            int requestedFrames = count / 4;
            int framesProcessed = 0;

            while (framesProcessed < requestedFrames)
            {
                int chunk = Math.Min(requestedFrames - framesProcessed, _leftBuf.Length);

                Array.Clear(_leftBuf, 0, chunk);
                Array.Clear(_rightBuf, 0, chunk);

                lock (_machine.SoundLock)
                {
                    // Render PSG Left & Right
                    _machine.PsgL.Render(_leftBuf, 0, chunk, 44100);
                    _machine.PsgR.Render(_rightBuf, 0, chunk, 44100);

                    // Render OPM (YM2151)
                    _machine.Opm.Update(0, _opmBuf, chunk);
                }

                for (int i = 0; i < chunk; i++)
                {
                    float opmL = (_opmBuf[0][i] / 32768f) * 0.5f;
                    float opmR = (_opmBuf[1][i] / 32768f) * 0.5f;

                    float mixL = Math.Clamp(_leftBuf[i] * 0.5f + opmL, -1.0f, 1.0f);
                    float mixR = Math.Clamp(_rightBuf[i] * 0.5f + opmR, -1.0f, 1.0f);

                    short sampleL = (short)(mixL * 32767f);
                    short sampleR = (short)(mixR * 32767f);

                    int byteIdx = offset + (framesProcessed + i) * 4;
                    buffer[byteIdx] = (byte)(sampleL & 0xFF);
                    buffer[byteIdx + 1] = (byte)((sampleL >> 8) & 0xFF);
                    buffer[byteIdx + 2] = (byte)(sampleR & 0xFF);
                    buffer[byteIdx + 3] = (byte)((sampleR >> 8) & 0xFF);
                }

                framesProcessed += chunk;
            }

            return requestedFrames * 4;
        }
    }
}
