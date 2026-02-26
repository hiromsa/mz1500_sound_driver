using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace Mz1500SoundPlayer.Sound;

public class MetronomeSampleProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; }
    
    private readonly List<double> _beatTimingsMs;
    private int _beatIndex = 0;
    
    // Playback state
    private double _currentTimeMs = 0;
    private double _sampleDurationMs;
    
    // Click generator state
    private bool _isClicking = false;
    private double _clickRemainingMs = 0;
    private double _phaseAngle = 0;
    private const double ClickFrequency = 1000.0;
    private const double ClickDurationBaseMs = 30.0;
    private const float ClickVolume = 0.5f;

    public bool IsActive { get; set; } = false;

    public MetronomeSampleProvider(List<double> beatTimingsMs, int sampleRate = 44100)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _beatTimingsMs = beatTimingsMs;
        _sampleDurationMs = 1000.0 / sampleRate;
        _beatIndex = 0;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        // Even if inactive, we must advance time so it stays in sync if toggled on later
        for (int i = 0; i < count; i++)
        {
            // Check if we hit the next beat
            if (_beatIndex < _beatTimingsMs.Count && _currentTimeMs >= _beatTimingsMs[_beatIndex])
            {
                _isClicking = true;
                
                // Emphasize the first beat (downbeat) slightly longer/higher if we wanted to
                // For now, pure consistent click
                _clickRemainingMs = ClickDurationBaseMs;
                _phaseAngle = 0;
                
                _beatIndex++;
            }

            float sample = 0;

            if (_isClicking)
            {
                // Simple Sine wave oscillator
                sample = (float)(Math.Sin(_phaseAngle) * ClickVolume);
                
                // Fast decay to avoid popping
                float envelope = (float)(_clickRemainingMs / ClickDurationBaseMs);
                sample *= envelope * envelope; // exponential decay

                _phaseAngle += 2 * Math.PI * ClickFrequency / WaveFormat.SampleRate;
                if (_phaseAngle > 2 * Math.PI) _phaseAngle -= 2 * Math.PI;

                _clickRemainingMs -= _sampleDurationMs;
                if (_clickRemainingMs <= 0)
                {
                    _isClicking = false;
                    _clickRemainingMs = 0;
                }
            }

            buffer[offset + i] = IsActive ? sample : 0f;
            _currentTimeMs += _sampleDurationMs;
        }

        return count;
    }
}
