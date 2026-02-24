using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace Mz1500SoundPlayer.Sound;

public class MmlSequenceProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; }

    private readonly byte[] _bytecode;
    private int _pc; // Program Counter
    
    // SN76489 VM State
    private int _hwVolume = 15; // 0=Max, 15=Silent (Hardware logic)
    private ushort _hwFreqRaw = 0; // 10-bit value
    private double _phase = 0;
    private double _phaseIncrement = 0;
    
    // Engine State
    private int _waitFrames = 0;
    private bool _isEnd = false;
    private bool _isRest = false;
    
    // Envelope State
    private bool _envActive = false;
    private int _envId = -1;
    private int _envPosOffset = 0;
    private readonly Dictionary<int, EnvelopeData> _envelopes;

    // Pitch Envelope State
    private bool _pEnvActive = false;
    private int _pEnvId = -1;
    private int _pEnvPosOffset = 0;
    private readonly List<MmlToZ80Compiler.HwPitchEnvData> _hwPitchEnvelopes;

    // Constants
    private const double BaseClockFreq = 111860.0;
    private readonly double _samplesPerFrame;
    private double _samplesCurrentFrameCount = 0;

    public MmlSequenceProvider(byte[] bytecode, Dictionary<int, EnvelopeData> envelopes, List<MmlToZ80Compiler.HwPitchEnvData> hwPitchEnvelopes, int sampleRate = 44100)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _bytecode = bytecode;
        _envelopes = envelopes ?? new Dictionary<int, EnvelopeData>();
        _hwPitchEnvelopes = hwPitchEnvelopes ?? new List<MmlToZ80Compiler.HwPitchEnvData>();
        _samplesPerFrame = WaveFormat.SampleRate / 60.0;
        
        Reset();
    }

    public void Reset()
    {
        _pc = 0;
        _hwVolume = 15;
        _hwFreqRaw = 0;
        _phase = 0;
        _phaseIncrement = 0;
        _waitFrames = 0;
        _isEnd = false;
        _isRest = false;
        
        _envActive = false;
        _envId = -1;
        _envPosOffset = 0;

        _pEnvActive = false;
        _pEnvId = -1;
        _pEnvPosOffset = 0;

        _samplesCurrentFrameCount = 0;

        // Boot VM for the very first frame
        ProcessVM();
    }

    private void ProcessVM()
    {
        bool fetchNext = true;
        while (fetchNext && !_isEnd && _pc < _bytecode.Length)
        {
            byte cmd = _bytecode[_pc++];
            switch (cmd)
            {
                case MmlToZ80Compiler.CMD_TONE:
                    byte t1 = _bytecode[_pc++];
                    byte t2 = _bytecode[_pc++];
                    byte lenL = _bytecode[_pc++];
                    byte lenH = _bytecode[_pc++];
                    
                    _hwFreqRaw = (ushort)((t1 & 0x0F) | ((t2 & 0x3F) << 4));
                    _waitFrames = lenL | (lenH << 8);
                    
                    // Reset envelope
                    _envPosOffset = 0;
                    _pEnvPosOffset = 0;
                    _isRest = false;
                    
                    // Update frequency
                    if (_hwFreqRaw > 0)
                    {
                        // SN76489 Formula: freq_hz = (MasterClock/32) / reg_value
                        // Here BaseClockFreq is already MasterClock/32 (111860)
                        double freqHz = BaseClockFreq / _hwFreqRaw;
                        _phaseIncrement = freqHz / WaveFormat.SampleRate;
                    }
                    else
                    {
                        _phaseIncrement = 0;
                    }
                    
                    fetchNext = false; // Yield VM processing until next tick
                    break;
                    
                case MmlToZ80Compiler.CMD_REST:
                    byte rlenL = _bytecode[_pc++];
                    byte rlenH = _bytecode[_pc++];
                    _waitFrames = rlenL | (rlenH << 8);
                    _isRest = true;
                    _hwVolume = 15;
                    
                    fetchNext = false; // Yield VM processing until next tick
                    break;
                    
                case MmlToZ80Compiler.CMD_VOL:
                    byte volData = _bytecode[_pc++];
                    _hwVolume = volData & 0x0F;
                    break;
                    
                case MmlToZ80Compiler.CMD_ENV:
                    byte id = _bytecode[_pc++];
                    if (id == 0xFF)
                    {
                        _envActive = false;
                    }
                    else
                    {
                        _envActive = true;
                        _envId = id;
                        _envPosOffset = 0;
                    }
                    break;
                    
                case MmlToZ80Compiler.CMD_PENV:
                    byte pid = _bytecode[_pc++];
                    if (pid == 0xFF)
                    {
                        _pEnvActive = false;
                    }
                    else
                    {
                        _pEnvActive = true;
                        _pEnvId = pid;
                        _pEnvPosOffset = 0;
                    }
                    break;
                    
                case MmlToZ80Compiler.CMD_END:
                    _isEnd = true;
                    fetchNext = false;
                    break;
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesWritten = 0;

        while (samplesWritten < count)
        {
            if (_isEnd)
            {
                buffer[offset + samplesWritten] = 0f;
                samplesWritten++;
                continue;
            }

            // Tick progress
            if (_samplesCurrentFrameCount >= _samplesPerFrame)
            {
                _samplesCurrentFrameCount -= _samplesPerFrame;

                // Process envelope for this frame (applies to current hardware sound)
                if (!_isRest && _envActive && _envelopes.TryGetValue(_envId, out var envData) && envData.Values.Count > 0)
                {
                    int maxLen = envData.Values.Count;
                    if (_envPosOffset >= maxLen)
                    {
                        if (envData.LoopIndex >= 0 && envData.LoopIndex < maxLen)
                        {
                            _envPosOffset = envData.LoopIndex;
                        }
                        else
                        {
                            _envPosOffset = maxLen - 1;
                        }
                    }
                    
                    int envVal = envData.Values[_envPosOffset];
                    _hwVolume = 15 - envVal; 
                    if (_hwVolume < 0) _hwVolume = 0;
                    if (_hwVolume > 15) _hwVolume = 15;
                    
                    _envPosOffset++;
                }
                
                // Process pitch envelope for this frame
                if (!_isRest && _pEnvActive && _pEnvId >= 0 && _pEnvId < _hwPitchEnvelopes.Count)
                {
                    var pEnvData = _hwPitchEnvelopes[_pEnvId];
                    if (pEnvData.AbsoluteRegisters.Count > 0)
                    {
                        int maxLen = pEnvData.AbsoluteRegisters.Count;
                        if (_pEnvPosOffset >= maxLen)
                        {
                            if (pEnvData.LoopIndex >= 0 && pEnvData.LoopIndex < maxLen)
                            {
                                _pEnvPosOffset = pEnvData.LoopIndex;
                            }
                            else
                            {
                                _pEnvPosOffset = maxLen - 1;
                            }
                        }
                        
                        ushort hwCmd = pEnvData.AbsoluteRegisters[_pEnvPosOffset];
                        byte cmd1 = (byte)(hwCmd & 0xFF);
                        byte cmd2 = (byte)(hwCmd >> 8);
                        
                        ushort freqReg = (ushort)((cmd1 & 0x0F) | ((cmd2 & 0x3F) << 4));
                        
                        if (freqReg > 0)
                        {
                            double freqHz = BaseClockFreq / freqReg;
                            _phaseIncrement = freqHz / WaveFormat.SampleRate;
                        }
                        else
                        {
                            _phaseIncrement = 0;
                        }
                        
                        _pEnvPosOffset++;
                    }
                }
                
                // End of frame logic for wait counter
                if (_waitFrames > 0)
                {
                    _waitFrames--;
                }
                else
                {
                    // Fetch new commands if wait is over
                    ProcessVM();
                }
            }

            // Render current sample
            float activeVol = (15 - _hwVolume) / 15.0f * 0.15f; // Scale 0.0 ~ 0.15
            float sampleValue = 0f;

            if (_phaseIncrement > 0 && activeVol > 0)
            {
                sampleValue = (float)((_phase < 0.5) ? activeVol : -activeVol);
                
                _phase += _phaseIncrement;
                if (_phase >= 1.0) _phase -= 1.0;
            }

            buffer[offset + samplesWritten] = sampleValue;
            samplesWritten++;
            _samplesCurrentFrameCount++;
        }

        return count;
    }
}
