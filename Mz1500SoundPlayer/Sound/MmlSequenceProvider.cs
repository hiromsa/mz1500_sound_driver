using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace Mz1500SoundPlayer.Sound;

public class MmlSequenceProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; }

    public bool IsMuted { get; set; } = false;

    private readonly byte[] _bytecode;
    private int _pc; // Program Counter
    
    // SN76489 VM State
    private int _hwVolume = 15; // 0=Max, 15=Silent (Hardware logic)
    private ushort _hwFreqRaw = 0; // 10-bit value
    private double _phase = 0;
    private double _phaseIncrement = 0;
    
    // Noise LFSR variables
    private bool _isNoiseMode = false;
    private int _noiseFeedback = 0; // 0=Periodic, 1=White
    private ushort _lfsr = 0x4000;
    
    // Engine State
    private int _waitFrames = 0;
    private bool _isEnd = false;
    private bool _isRest = false;
    private int _loopOffsetPc = -1;
    
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
    private readonly bool _isBeep;
    
    // SN76489 Volume translation table (2dB per step)
    private static readonly float[] VolumeTable = new float[16]
    {
        1.0000f, 0.7943f, 0.6310f, 0.5012f,
        0.3981f, 0.3162f, 0.2512f, 0.1995f,
        0.1585f, 0.1259f, 0.1000f, 0.0794f,
        0.0631f, 0.0501f, 0.0398f, 0.0000f
    };

    public MmlSequenceProvider(byte[] bytecode, Dictionary<int, EnvelopeData> envelopes, List<MmlToZ80Compiler.HwPitchEnvData> hwPitchEnvelopes, int sampleRate = 44100, bool isBeep = false)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _bytecode = bytecode;
        _envelopes = envelopes ?? new Dictionary<int, EnvelopeData>();
        _hwPitchEnvelopes = hwPitchEnvelopes ?? new List<MmlToZ80Compiler.HwPitchEnvData>();
        _samplesPerFrame = WaveFormat.SampleRate / 60.0;
        _isBeep = isBeep;
        
        Reset();
    }

    public void Reset()
    {
        _pc = 0;
        _hwVolume = 15;
        _hwFreqRaw = 0;
        _phase = 0;
        _phaseIncrement = 0;
        
        _isNoiseMode = false;
        _noiseFeedback = 0;
        _lfsr = 0x4000;
        
        _waitFrames = 0;
        _isEnd = false;
        _isRest = false;
        _loopOffsetPc = -1;
        
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
                    
                    if (_isBeep)
                    {
                        _hwFreqRaw = (ushort)(t1 | (t2 << 8));
                    }
                    else
                    {
                        _hwFreqRaw = (ushort)((t1 & 0x0F) | ((t2 & 0x3F) << 4));
                    }
                    
                    _waitFrames = lenL | (lenH << 8);
                    
                    // Reset envelope
                    _envPosOffset = 0;
                    _pEnvPosOffset = 0;
                    _isRest = false;
                    _isNoiseMode = false;
                    
                    // Immediately initialize envelope for the new tone so no delay occurs on first frame
                    if (_envActive && _envelopes.TryGetValue(_envId, out var envInitData) && envInitData.Values.Count > 0)
                    {
                        int envVal = envInitData.Values[0];
                        _hwVolume = 15 - envVal; 
                        if (_hwVolume < 0) _hwVolume = 0;
                        if (_hwVolume > 15) _hwVolume = 15;
                        _envPosOffset = 1; // Already consumed first step
                    }
                    
                    // Immediately initialize pitch envelope
                    if (_pEnvActive && _pEnvId >= 0 && _pEnvId < _hwPitchEnvelopes.Count)
                    {
                        var pEnvInitData = _hwPitchEnvelopes[_pEnvId];
                        if (pEnvInitData.AbsoluteRegisters.Count > 0)
                        {
                             ushort hwCmd = pEnvInitData.AbsoluteRegisters[0];
                             byte initCmd1 = (byte)(hwCmd & 0xFF);
                             byte initCmd2 = (byte)(hwCmd >> 8);
                             ushort freqReg = _isBeep ? (ushort)(initCmd1 | (initCmd2 << 8)) : (ushort)((initCmd1 & 0x0F) | ((initCmd2 & 0x3F) << 4));
                             if (freqReg > 0)
                             {
                                 double pFreqHz = _isBeep ? MmlToZ80Compiler.BeepClockFreq / freqReg : BaseClockFreq / freqReg;
                                 _phaseIncrement = pFreqHz / WaveFormat.SampleRate;
                             }
                             else
                             {
                                 _phaseIncrement = 0;
                             }
                             _pEnvPosOffset = 1;
                        }
                    }
                    
                    // Update frequency
                    if (_pEnvPosOffset == 0 && _hwFreqRaw > 0)
                    {
                        // SN76489 Formula: freq_hz = (MasterClock/32) / reg_value
                        // Intel 8253 Formula: freq_hz = 894886 / reg_value
                        double freqHz = _isBeep ? MmlToZ80Compiler.BeepClockFreq / _hwFreqRaw : BaseClockFreq / _hwFreqRaw;
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
                    
                case MmlToZ80Compiler.CMD_NOISE:
                    byte noiseCmd = _bytecode[_pc++];
                    byte nlenL = _bytecode[_pc++];
                    byte nlenH = _bytecode[_pc++];
                    _waitFrames = nlenL | (nlenH << 8);

                    _noiseFeedback = (noiseCmd >> 2) & 0x01;
                    int shiftRate = noiseCmd & 0x03;
                    
                    double nFreqHz = 0;
                    if (shiftRate == 0) nFreqHz = BaseClockFreq / 16.0;
                    else if (shiftRate == 1) nFreqHz = BaseClockFreq / 32.0;
                    else if (shiftRate == 2) nFreqHz = BaseClockFreq / 64.0;

                    _phaseIncrement = nFreqHz / WaveFormat.SampleRate;
                    _isNoiseMode = true;
                    _lfsr = 0x4000;
                    
                    _envPosOffset = 0;
                    _pEnvPosOffset = 0;
                    _isRest = false;
                    fetchNext = false;
                    break;

                case MmlToZ80Compiler.CMD_SYNC_NOISE:
                    byte fCmd1 = _bytecode[_pc++];
                    byte fCmd2 = _bytecode[_pc++];
                    byte muteVol = _bytecode[_pc++];
                    byte linkedNoiseCmd = _bytecode[_pc++];
                    byte noiseVol = _bytecode[_pc++];
                    
                    byte synclenL = _bytecode[_pc++];
                    byte synclenH = _bytecode[_pc++];
                    _waitFrames = synclenL | (synclenH << 8);
                    
                    _noiseFeedback = (linkedNoiseCmd >> 2) & 0x01;
                    // Extract frequency (Sync Noise implies Tone3 linked)
                    ushort syncFreqRaw = (ushort)((fCmd1 & 0x0F) | ((fCmd2 & 0x3F) << 4));
                    double syncFreqHz = (syncFreqRaw > 0) ? BaseClockFreq / syncFreqRaw : 0;
                    
                    _phaseIncrement = syncFreqHz / WaveFormat.SampleRate;
                    _hwVolume = noiseVol & 0x0F;
                    _isNoiseMode = true;
                    _lfsr = 0x4000;

                    _envPosOffset = 0;
                    _pEnvPosOffset = 0;
                    _isRest = false;
                    fetchNext = false;
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
                    
                case MmlToZ80Compiler.CMD_LOOP_MARKER:
                    _loopOffsetPc = _pc;
                    fetchNext = true;
                    break;
                    
                case MmlToZ80Compiler.CMD_END:
                    if (_loopOffsetPc >= 0)
                    {
                        _pc = _loopOffsetPc;
                        fetchNext = true;
                    }
                    else
                    {
                        _isEnd = true;
                        fetchNext = false;
                    }
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
                        
                        ushort freqReg = 0;
                        if (_isBeep)
                        {
                            freqReg = (ushort)(cmd1 | (cmd2 << 8));
                        }
                        else
                        {
                            freqReg = (ushort)((cmd1 & 0x0F) | ((cmd2 & 0x3F) << 4));
                        }
                        
                        if (freqReg > 0)
                        {
                            double freqHz = _isBeep ? MmlToZ80Compiler.BeepClockFreq / freqReg : BaseClockFreq / freqReg;
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
            float activeVol = VolumeTable[_hwVolume] * 0.15f; // Scale 0.0 ~ 0.15
            float sampleValue = 0f;

            if (_phaseIncrement > 0 && activeVol > 0 && !IsMuted)
            {
                if (_isNoiseMode)
                {
                    // LFSR Output
                    int outputBit = _lfsr & 1;
                    sampleValue = (outputBit == 1) ? activeVol : -activeVol;
                    
                    _phase += _phaseIncrement;
                    while (_phase >= 1.0)
                    {
                        _phase -= 1.0;
                        int tappedBit = (_noiseFeedback == 1)
                                        ? ((_lfsr & 1) ^ ((_lfsr >> 1) & 1))
                                        : (_lfsr & 1);
                        _lfsr = (ushort)((_lfsr >> 1) | (tappedBit << 14));
                    }
                }
                else
                {
                    // Square Wave Output
                    sampleValue = (float)((_phase < 0.5) ? activeVol : -activeVol);
                    
                    _phase += _phaseIncrement;
                    if (_phase >= 1.0) _phase -= 1.0;
                }
            }

            buffer[offset + samplesWritten] = sampleValue;
            samplesWritten++;
            _samplesCurrentFrameCount++;
        }

        return count;
    }
}
