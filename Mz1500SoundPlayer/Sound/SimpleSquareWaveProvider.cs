using System;
using NAudio.Wave;

namespace Mz1500SoundPlayer.Sound;

public class SimpleSquareWaveProvider : ISampleProvider
{
    private double phase;
    private double phaseIncrement;
    public double Frequency { get; set; } = 440.0;
    public double Volume { get; set; } = 0.2;
    public WaveFormat WaveFormat { get; }

    public SimpleSquareWaveProvider(int sampleRate = 44100)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        phaseIncrement = Frequency / WaveFormat.SampleRate;

        for (int i = 0; i < count; i++)
        {
            // 矩形波の生成 (50% duty cycle)
            double sampleValue = (phase < 0.5) ? Volume : -Volume;
            buffer[offset + i] = (float)sampleValue;

            phase += phaseIncrement;
            if (phase >= 1.0)
            {
                phase -= 1.0;
            }
        }
        return count;
    }
}
