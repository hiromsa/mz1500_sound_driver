using NAudio.Wave;
using System;

namespace Mz1500SoundPlayer.Sound;

public class Ym2151SequenceProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; }
    
    private readonly byte[] _bytecode;
    private readonly YM2151Manager _ym2151Manager;
    private int _pc = 0;
    private int _waitFrames = 0;
    private bool _isEnd = false;
    public bool IsMuted { get; set; } = false;

    private readonly double _samplesPerFrame;
    private double _samplesCurrentFrameCount = 0;

    public Ym2151SequenceProvider(byte[] bytecode, YM2151Manager ym2151Manager, int sampleRate = 44100)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _bytecode = bytecode;
        _ym2151Manager = ym2151Manager;
        _samplesPerFrame = WaveFormat.SampleRate / 60.0;
        
        Reset();
    }

    public void Reset()
    {
        _pc = 0;
        _waitFrames = 0;
        _isEnd = false;
        _samplesCurrentFrameCount = 0;

        ProcessVM();
    }

    private long _totalSamplesProcessed = 0;

    private void ProcessVM()
    {
        bool fetchNext = true;
        while (fetchNext && !_isEnd && _pc < _bytecode.Length)
        {
            if (_waitFrames > 0)
            {
                fetchNext = false;
                continue;
            }

            byte cmd = _bytecode[_pc++];
            switch (cmd)
            {
                case MmlToZ80Compiler.CMD_WAIT:
                    byte wL = _bytecode[_pc++];
                    byte wH = _bytecode[_pc++];
                    _waitFrames = (wL | (wH << 8)) + 1; // CMD_WAIT stores frames - 1
                    Console.WriteLine($"  [FM-VM] CMD_WAIT: {_waitFrames} frames (sample pos: {_totalSamplesProcessed})");
                    break;
                case MmlToZ80Compiler.CMD_YM2151_REG_WRITE:
                    byte reg = _bytecode[_pc++];
                    byte val = _bytecode[_pc++];
                    if (!IsMuted)
                    {
                        _ym2151Manager.OutPort(0x0708, reg);
                        _ym2151Manager.OutPort(0x0709, val);
                    }
                    // KEY ON/OFFとKCの書き込みを目立つようにログ出力
                    if (reg == 0x08)
                        Console.WriteLine($"  [FM-VM] ** KEY {(val > 0 ? "ON " : "OFF")} ** reg=0x{reg:X2} val=0x{val:X2} (sample pos: {_totalSamplesProcessed})");
                    else if (reg >= 0x28 && reg <= 0x2F)
                        Console.WriteLine($"  [FM-VM] KC  reg=0x{reg:X2} val=0x{val:X2} (sample pos: {_totalSamplesProcessed})");
                    else if (reg >= 0x30 && reg <= 0x37)
                        Console.WriteLine($"  [FM-VM] KF  reg=0x{reg:X2} val=0x{val:X2} (sample pos: {_totalSamplesProcessed})");
                    break;
                case MmlToZ80Compiler.CMD_LOOP_MARKER:
                    // Loops not fully implemented in simple version, ignore marker
                    break;
                case MmlToZ80Compiler.CMD_END:
                    Console.WriteLine($"  [FM-VM] CMD_END (sample pos: {_totalSamplesProcessed})");
                    _isEnd = true;
                    fetchNext = false;
                    break;
                default:
                    Console.WriteLine($"  [FM-VM] UNKNOWN CMD 0x{cmd:X2}, treating as END (sample pos: {_totalSamplesProcessed})");
                    _isEnd = true;
                    fetchNext = false;
                    break;
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            buffer[offset + i] = 0f;
            
            _samplesCurrentFrameCount += 1.0;
            if (_samplesCurrentFrameCount >= _samplesPerFrame)
            {
                _samplesCurrentFrameCount -= _samplesPerFrame;
                if (_waitFrames > 0)
                {
                    _waitFrames--;
                }
                if (_waitFrames == 0 && !_isEnd)
                {
                    Console.WriteLine($"[FM-VM] Frame boundary at sample {_totalSamplesProcessed + i}, calling ProcessVM (waitFrames was just decremented to 0)");
                    ProcessVM();
                }
            }
        }
        _totalSamplesProcessed += count;
        return count;
    }
}