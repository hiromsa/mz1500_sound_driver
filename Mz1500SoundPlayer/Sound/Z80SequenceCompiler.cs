using System;
using System.Collections.Generic;

namespace Mz1500SoundPlayer.Sound;

/// <summary>
/// MML・・ST縺ｮNoteEvent繝ｪ繧ｹ繝茨ｼ峨ｒ縲〇80繧ｵ繧ｦ繝ｳ繝峨ラ繝ｩ繧､繝仙髄縺代・邁｡譏薙す繝ｼ繧ｱ繝ｳ繧ｹ繝舌う繝翫Μ縺ｸ螟画鋤縺吶ｋ繧ｳ繝ｳ繝代う繝ｩ
/// </summary>
public class Z80SequenceCompiler
{
    // SN76489 縺ｮ險育ｮ怜ｼ・ freq = 111860 / register
    // -> register = 111860 / freq (Hz)
    public const double BaseClockFreq = 111860.0;
    public const double BeepClockFreq = 894886.0; // Intel 8253 Timer0 Base Clock
    
    // Command Types (VB迚井ｺ呈鋤縺ｫ霑代＞蠖｢縺ｧ螳夂ｾｩ)
    public const byte CMD_TONE = 0x01;
    // CMD_REST removed
    // CMD_VOL removed
    // CMD_ENV removed // 繧ｽ繝輔ヨ繧ｦ繧ｧ繧｢髻ｳ驥上お繝ｳ繝吶Ο繝ｼ繝励・繧ｻ繝・ヨ
    public const byte CMD_PENV = 0xA2; // 繝斐ャ繝√お繝ｳ繝吶Ο繝ｼ繝・HwPitchEnv)縺ｮ蛻・ｊ譖ｿ縺・
    public const byte CMD_NOISE = 0xA6; // 繝弱う繧ｺ繧ｸ繧ｧ繝阪Ξ繝ｼ繧ｿ蟆ら畑蜃ｺ蜉・
    public const byte CMD_SYNC_NOISE = 0xA7; // Tone 3 騾｣謳ｺ繝｢繝ｼ繝牙ｰら畑蜃ｺ蜉・
    // CMD_LOOP_MARKER removed // L繧ｳ繝槭Φ繝峨↓繧医ｋ辟｡髯舌Ν繝ｼ繝励・繝ｼ繧ｫ繝ｼ
    // CMD_END removed
    public const byte CMD_WAIT = 0xA3;
    public const byte CMD_YM2151_REG_WRITE = 0x21; // 譖ｲ縺ｮ邨ゅｏ繧・

    public Dictionary<int, EnvelopeData> VolumeEnvelopes { get; set; } = new();
    public Dictionary<int, EnvelopeData> PitchEnvelopes { get; set; } = new();
    public List<HwPitchEnvData> HwPitchEnvelopes { get; } = new();
    private Dictionary<string, int> _hwPitchEnvCache = new();

    public class HwPitchEnvData
    {
        public int Id { get; set; }
        public List<ushort> AbsoluteRegisters { get; set; } = new();
        public int LoopIndex { get; set; } = -1;
    }

    public byte[] CompileTrack(List<NoteEvent> events, byte psgChannel = 0, bool isBeep = false)
    {
        var output = new List<byte>();
        
        int currentVol = -1; // -1 means uninitialized
        int currentEnvId = -1;
        int currentPEnvId = -1;
        double currentTimeMs = 0;
        int currentFrame = 0;
        int currentReleaseEnvPos = -1; // -1 means release is off or finished
        
        int currentLength = -1;
        Action<int> emitLength = (len) => {
            if (len == currentLength) return;
            if (len >= 1 && len <= 16) {
                output.Add((byte)((int)Z80SequenceCommand.ShortLengthBase + (len - 1)));
            } else {
                output.Add((byte)Z80SequenceCommand.LongLength);
                output.Add((byte)(len & 0xFF));
                output.Add((byte)((len >> 8) & 0xFF));
            }
            currentLength = len;
        };

        foreach (var ev in events)
        {
            if (ev.IsLoopPoint)
            {
                output.Add((byte)Z80SequenceCommand.LoopMarker);
            }

            double nextTimeMs = currentTimeMs + ev.DurationMs;
            int nextFrame = (int)Math.Round(nextTimeMs * 60.0 / 1000.0);
            int totalFrames = nextFrame - currentFrame;
            
            if (totalFrames < 1) totalFrames = 1;

            double gateEndTimeMs = currentTimeMs + ev.GateTimeMs;
            int gateEndFrame = (int)Math.Round(gateEndTimeMs * 60.0 / 1000.0);
            int gateFrames = gateEndFrame - currentFrame;
            
            // Allow gateFrames to equal totalFrames for full legato (q8 or @q0)
            if (gateFrames > totalFrames) gateFrames = totalFrames;
            if (gateFrames < 1 && ev.Frequency > 0) gateFrames = 1; // 蟆代↑縺上→繧・繝輔Ξ繝ｼ繝縺ｯ魑ｴ繧峨☆・磯撼蟶ｸ縺ｫ遏ｭ縺・浹縺ｮ蝣ｴ蜷茨ｼ・

            // 繧ｨ繝ｳ繝吶Ο繝ｼ繝励・迥ｶ諷句､牙喧縺後≠繧後・縺ｾ縺壼・蜉帙☆繧・
            if (ev.EnvelopeId >= 0 && ev.EnvelopeId != currentEnvId)
            {
                output.Add((byte)Z80SequenceCommand.SetVoice);
                output.Add((byte)ev.EnvelopeId);
                currentEnvId = ev.EnvelopeId;
            }
            else if (ev.EnvelopeId < 0 && currentEnvId >= 0)
            {
                // 繝ｪ繝ｪ繝ｼ繧ｹ縺後≠繧句ｴ蜷医・繧ｵ繧ｹ繝・う繝ｳ邨ゆｺ・峩蠕後↓(byte)Z80SequenceCommand.SetVoice繧丹FF縺ｫ縺吶ｋ縺ｨ髻ｳ縺悟・繧後ｋ蜿ｯ閭ｽ諤ｧ縺後≠繧九◆繧√・
                // 繝ｪ繝ｪ繝ｼ繧ｹ繧呈戟縺溘↑縺・ｴ蜷医・縺ｿ蜊ｳ蠎ｧ縺ｫOFF縺ｫ縺吶ｋ縲・
                // (繝ｪ繝ｪ繝ｼ繧ｹ縺後≠繧句ｴ蜷医・縲¨oteOff譎ゅ・螻暮幕繝ｫ繝ｼ繝励↓莉ｻ縺帙ｋ)
                if (!VolumeEnvelopes.TryGetValue(currentEnvId, out var envDataOff) || envDataOff.ReleaseValues.Count == 0)
                {
                    output.Add((byte)Z80SequenceCommand.SetVoice);
                    output.Add(0xFF); // 0xFF means off
                    currentEnvId = -1;
                    currentVol = -1;
                }
            }

            if (ev.Frequency == 0 || ev.Volume == 0 || gateFrames <= 0)
            {
                // Mute before rest unless release is active
                if (currentEnvId >= 0 && VolumeEnvelopes.TryGetValue(currentEnvId, out var relEnvData) && relEnvData.ReleaseValues.Count > 0 && currentReleaseEnvPos >= 0)
                {
                    // Fall back to Rest 蜃ｦ逅・below to continue release phase
                }
                else
                {
                    byte muteVolCmd = (byte)(0x90 | ((psgChannel & 0x03) << 5) | 0x0F);
                    output.Add((byte)Z80SequenceCommand.SetVolume);
                    output.Add(muteVolCmd);
                    currentVol = 15;
                    currentReleaseEnvPos = -1;
                }

                // 莨醍ｬｦ (Kyufu) / Release Phase Expansion
                ushort durationUnits = (ushort)(totalFrames - 1);
                
                if (currentReleaseEnvPos >= 0 && currentEnvId >= 0 && VolumeEnvelopes.TryGetValue(currentEnvId, out var envDataR) && envDataR.ReleaseValues.Count > 0)
                {
                    // 莨醍ｬｦ髢句ｧ区凾縺ｫ繝上・繝峨え繧ｧ繧｢繧ｨ繝ｳ繝吶Ο繝ｼ繝励ｒOFF縺ｫ縺励※繝ｪ繝ｪ繝ｼ繧ｹ螻暮幕繧定ｨｱ蜿ｯ縺吶ｋ
                    output.Add((byte)Z80SequenceCommand.SetVoice);
                    output.Add(0xFF);
                    currentEnvId = -1;

                    // 1 frame step expansion for release phase during explicit rest
                    for (int frm = 0; frm < totalFrames; frm++)
                    {
                        if (currentReleaseEnvPos >= 0 && currentReleaseEnvPos < envDataR.ReleaseValues.Count)
                        {
                            int relVal = envDataR.ReleaseValues[currentReleaseEnvPos++];
                            int relVol15 = relVal;
                            if (relVol15 < 0) relVol15 = 0;
                            if (relVol15 > 15) relVol15 = 15;
                            byte hwVol = (byte)(15 - relVol15);

                            if (currentVol != hwVol)
                            {
                                output.Add((byte)Z80SequenceCommand.SetVolume);
                                output.Add((byte)(0x90 | ((psgChannel & 0x03) << 5) | (hwVol & 0x0F)));
                                currentVol = hwVol;
                            }
                        }
                        else
                        {
                            if (currentVol != 15)
                            {
                                output.Add((byte)Z80SequenceCommand.SetVolume);
                                output.Add((byte)(0x90 | ((psgChannel & 0x03) << 5) | (0x0F)));
                                currentVol = 15;
                            }
                            currentReleaseEnvPos = -1;
                        }
                        
                        // Emit 1 frame rest
                        emitLength(1);
                        output.Add((byte)Z80SequenceCommand.Rest);
                    }
                }
                else
                {
                    emitLength(durationUnits + 1);
                    output.Add((byte)Z80SequenceCommand.Rest);
                }
            }
            else
            {
                // Note ON, reset release envelope position
                currentReleaseEnvPos = 0; 

                // 髻ｳ驥上メ繧ｧ繝ｳ繧ｸ縺後≠繧後・蜈医↓蜷舌￥ (繧ｨ繝ｳ繝吶Ο繝ｼ繝励′蜉ｹ縺・※縺・ｌ縺ｰZ80蛛ｴ縺ｧ荳頑嶌縺阪＆繧後ｋ縺溘ａ蛻晄悄蛟､縺ｨ縺励※讖溯・縺吶ｋ)
                int vol15 = (int)Math.Round((ev.Volume / 0.15) * 15.0);
                if (vol15 < 0) vol15 = 0;
                if (vol15 > 15) vol15 = 15;
                byte hwVol = (byte)(15 - vol15);

                if (currentVol != hwVol)
                {
                    output.Add((byte)Z80SequenceCommand.SetVolume);
                    // SN76489 Volume Command
                    byte volCmd = (byte)(0x90 | ((psgChannel & 0x03) << 5) | (hwVol & 0x0F));
                    output.Add(volCmd);
                    currentVol = hwVol;
                }

                // ---------- @EP (繝斐ャ繝√お繝ｳ繝吶Ο繝ｼ繝・ 蜃ｦ逅・----------
                // @EP縺ｮ蛟､縺ｯ縲後Ξ繧ｸ繧ｹ繧ｿ蟾ｮ蛻・阪→縺励※謇ｱ縺・ｼ医・繝ｩ繧ｹ=髻ｳ遞倶ｸ頑・・・
                // baseReg - ep蛟､ 縺ｮ謨ｴ謨ｰ繧ｯ繝ｩ繝ｳ繝励〒逕滓・縺吶ｋ
                if (ev.PitchEnvelopeId >= 0 && PitchEnvelopes.ContainsKey(ev.PitchEnvelopeId))
                {
                    // 繝吶・繧ｹ蜻ｨ豕｢謨ｰ縺九ｉ繝吶・繧ｹ繝ｬ繧ｸ繧ｹ繧ｿ蛟､繧呈ｱゅａ繧・
                    double baseFreqForEp = ev.Frequency;
                    double baseClockForEp = isBeep ? BeepClockFreq : BaseClockFreq;
                    double baseRegRaw = (baseFreqForEp > 0) ? (baseClockForEp / baseFreqForEp) : 0;
                    int baseRegInt = (int)Math.Round(baseRegRaw);

                    string cacheKey = $"Reg_{baseRegInt}_EP_{ev.PitchEnvelopeId}_Ch_{psgChannel}_D_{ev.Detune}";
                    if (!_hwPitchEnvCache.TryGetValue(cacheKey, out int hwId))
                    {
                        var pEnvData = PitchEnvelopes[ev.PitchEnvelopeId];
                        var registers = new List<ushort>();

                        foreach (var epDelta in pEnvData.Values)
                        {
                            // 蜃ｺ蜉帙Ξ繧ｸ繧ｹ繧ｿ = 繝吶・繧ｹ繝ｬ繧ｸ繧ｹ繧ｿ - D蛟､ - EP蛟､縺ｮ蠑輔″邂・
                            int reg = Math.Clamp(baseRegInt - ev.Detune - epDelta, 0, isBeep ? 65535 : 1023);
                            ushort regU = (ushort)reg;

                            if (isBeep)
                            {
                                registers.Add((ushort)(regU & 0xFF | ((regU >> 8) << 8)));
                            }
                            else
                            {
                                byte c1 = (byte)(0x80 | ((psgChannel & 0x03) << 5) | (regU & 0x0F));
                                byte c2 = (byte)((regU >> 4) & 0x3F);
                                registers.Add((ushort)(c1 | (c2 << 8)));
                            }
                        }

                        hwId = HwPitchEnvelopes.Count;
                        HwPitchEnvelopes.Add(new HwPitchEnvData
                        {
                            Id = hwId,
                            AbsoluteRegisters = registers,
                            LoopIndex = pEnvData.LoopIndex
                        });
                        _hwPitchEnvCache[cacheKey] = hwId;
                    }

                    if (hwId != currentPEnvId)
                    {
                        output.Add(CMD_PENV);
                        output.Add((byte)hwId);
                        currentPEnvId = hwId;
                    }
                }
                else if (ev.PitchEnvelopeId < 0 && currentPEnvId >= 0)
                {
                    output.Add(CMD_PENV);
                    output.Add(0xFF); // OFF
                    currentPEnvId = -1;
                }

                // ---------- 繝医・繝ｳ蜃ｺ蜉・----------
                // 繝吶・繧ｹ蜻ｨ豕｢謨ｰ縺九ｉPSG繝ｬ繧ｸ繧ｹ繧ｿ蛟､繧定ｨ育ｮ・
                double freq = ev.Frequency;

                if (isBeep)
                {
                    // Beep繝√Ε繝ｳ繝阪Ν
                    int noteNum = ev.NoteNumber;
                    if (noteNum < 0) noteNum = 0;
                    if (noteNum > 95) noteNum = 95;
                    
                    // Beep縺ｯ髟ｷ縺募・蜉・
                    ushort durationUnitsBp = (ushort)(gateFrames - 1);
                    emitLength(durationUnitsBp + 1);
                    output.Add((byte)noteNum);
                }
                else if (psgChannel == 3)
                {
                    // 繝弱う繧ｺ繝医Λ繝・け
                    byte shiftRate = (freq < 300) ? (byte)2 : (freq < 350) ? (byte)1 : (byte)0;
                    byte feedback = (byte)(ev.NoiseWaveMode & 0x01);
                    byte noiseCmd = (byte)(0xE0 | (feedback << 2) | shiftRate);
                    output.Add(CMD_NOISE);
                    // 髟ｷ縺募・蜉・
                    ushort durationUnitsNoise = (ushort)(gateFrames - 1);
                    emitLength(durationUnitsNoise + 1);
                    output.Add(noiseCmd);
                }
                else
                {
                    // ---------- 繝医・繝ｳ繝√Ε繝ｳ繝阪Ν (A/B/C/E/F/G) ----------
                    // Tone ON: 0x00 - 0x5F (NoteNumber)
                    int noteNum = ev.NoteNumber;
                    if (noteNum < 0) noteNum = 0;
                    if (noteNum > 95) noteNum = 95;
                    
                    // 髟ｷ縺募・蜉・
                    ushort durationUnits = (ushort)(gateFrames - 1);
                    emitLength(durationUnits + 1);
                    output.Add((byte)noteNum);
                }

                // Rest 蜃ｦ逅・
                int restFrames = totalFrames - gateFrames;
                if (restFrames > 0)
                {
                    if (currentReleaseEnvPos >= 0 && currentEnvId >= 0 && VolumeEnvelopes.TryGetValue(currentEnvId, out var envDataR) && envDataR.ReleaseValues.Count > 0)
                    {
                        // 繝ｪ繝ｪ繝ｼ繧ｹ髢句ｧ区凾縺ｫ繝上・繝峨え繧ｧ繧｢繧ｨ繝ｳ繝吶Ο繝ｼ繝励ｒOFF縺ｫ縺吶ｋ (繧ｽ繝輔ヨ繧ｦ繧ｧ繧｢縺ｧ縺ｮ髻ｳ驥丞宛蠕｡縺ｫ蛻・ｊ譖ｿ縺医ｋ縺溘ａ)
                        output.Add((byte)Z80SequenceCommand.SetVoice);
                        output.Add(0xFF);
                        int activeEnvId = currentEnvId;
                        currentEnvId = -1;

                        for (int frm = 0; frm < restFrames; frm++)
                        {
                            if (currentReleaseEnvPos >= 0 && currentReleaseEnvPos < envDataR.ReleaseValues.Count)
                            {
                                int relVal = envDataR.ReleaseValues[currentReleaseEnvPos++];
                                int relVol15 = relVal;
                                if (relVol15 < 0) relVol15 = 0;
                                if (relVol15 > 15) relVol15 = 15;
                                byte relHwVol = (byte)(15 - relVol15);

                                if (currentVol != relHwVol)
                                {
                                    output.Add((byte)Z80SequenceCommand.SetVolume);
                                    output.Add((byte)(0x90 | ((psgChannel & 0x03) << 5) | (relHwVol & 0x0F)));
                                    currentVol = relHwVol;
                                }
                            }
                            else
                            {
                                if (currentVol != 15)
                                {
                                    output.Add((byte)Z80SequenceCommand.SetVolume);
                                    output.Add((byte)(0x90 | ((psgChannel & 0x03) << 5) | 0x0F));
                                    currentVol = 15;
                                }
                                currentReleaseEnvPos = -1;
                            }
                            
                            emitLength(1);
                            output.Add((byte)Z80SequenceCommand.Rest);
                        }
                    }
                    else
                    {
                        byte muteVolCmd = (byte)(0x90 | ((psgChannel & 0x03) << 5) | 0x0F);
                        output.Add((byte)Z80SequenceCommand.SetVolume);
                        output.Add(muteVolCmd);
                        currentVol = 15;
                        
                        ushort restUnits = (ushort)(restFrames - 1);
                        emitLength(restUnits + 1);
                        output.Add((byte)Z80SequenceCommand.Rest);
                    }
                }
            }

            currentTimeMs = nextTimeMs;
            currentFrame = nextFrame;
        }
        
        // 譖ｲ遶ｯ (Terminator)
        output.Add((byte)Z80SequenceCommand.TrackEnd);

        return output.ToArray();
    }

    public byte[] CompileFmTrack(List<NoteEvent> events, byte fmChannel, MmlData mmlData)
    {
        var output = new List<byte>();
        
        double currentTimeMs = 0;
        int currentFrame = 0;
        int currentVoiceId = -1;
        int currentPan = -1;
        
        Action<byte, byte> emitReg = (reg, val) => 
        {
            output.Add(CMD_YM2151_REG_WRITE);
            output.Add(reg);
            output.Add(val);
        };
        
        Action<int> emitWait = (frames) =>
        {
            while (frames > 0)
            {
                int waitFrames = Math.Min(frames, 65535);
                ushort fUnits = (ushort)(waitFrames - 1);
                output.Add(CMD_WAIT);
                output.Add((byte)(fUnits & 0xFF));
                output.Add((byte)((fUnits >> 8) & 0xFF));
                frames -= waitFrames;
            }
        };
        
        foreach (var ev in events)
        {
            if (ev.IsLoopPoint)
            {
                output.Add((byte)Z80SequenceCommand.LoopMarker);
            }
            
            if (ev.RegisterWrites != null)
            {
                foreach (var rw in ev.RegisterWrites)
                {
                    emitReg((byte)rw.Register, (byte)rw.Value);
                }
            }
            
            if (ev.VoiceId >= 0 && ev.VoiceId != currentVoiceId && mmlData.FmVoiceEnvelopes.TryGetValue(ev.VoiceId, out var toneData))
            {
                currentVoiceId = ev.VoiceId;
                int[] p = toneData.Parameters;
                if (p.Length >= 46)
                {
                    byte panFlCon = (byte)(((ev.Pan & 3) << 6) | ((p[1] & 7) << 3) | (p[0] & 7));
                    emitReg((byte)(0x20 + fmChannel), panFlCon);
                    
                    for (int op = 0; op < 4; op++)
                    {
                        int slotNum = op;
                        if (op == 1) slotNum = 2; // OP2 -> Slot 3 (C1)
                        else if (op == 2) slotNum = 1; // OP3 -> Slot 2 (M2)

                        int opOffset = slotNum * 8;
                        int pd = 2 + (op * 11); 
                        emitReg((byte)(0x40 + opOffset + fmChannel), (byte)(((p[pd+8]&7)<<4) | (p[pd+7]&15)));
                        emitReg((byte)(0x60 + opOffset + fmChannel), (byte)(p[pd+5] & 127));
                        emitReg((byte)(0x80 + opOffset + fmChannel), (byte)(((p[pd+6]&3)<<6) | (p[pd+0]&31)));
                        emitReg((byte)(0xA0 + opOffset + fmChannel), (byte)(((p[pd+10]&1)<<7) | (p[pd+1]&31)));
                        emitReg((byte)(0xC0 + opOffset + fmChannel), (byte)(((p[pd+9]&3)<<6) | (p[pd+2]&31)));
                        emitReg((byte)(0xE0 + opOffset + fmChannel), (byte)(((p[pd+4]&15)<<4) | (p[pd+3]&15)));
                    }
                }
            }
            
            if (ev.Pan != currentPan)
            {
                currentPan = ev.Pan;
                // Simplified pan update without caching AL/FB
                // emitReg((byte)(0x20 + fmChannel), ...);
            }

            double nextTimeMs = currentTimeMs + ev.DurationMs;
            int nextFrame = (int)Math.Round(nextTimeMs * 60.0 / 1000.0);
            int totalFrames = nextFrame - currentFrame;
            if (totalFrames < 1) totalFrames = 1;

            double gateEndTimeMs = currentTimeMs + ev.GateTimeMs;
            int gateEndFrame = (int)Math.Round(gateEndTimeMs * 60.0 / 1000.0);
            int gateFrames = gateEndFrame - currentFrame;
            if (gateFrames > totalFrames) gateFrames = totalFrames;
            if (gateFrames < 1 && ev.Frequency > 0) gateFrames = 1;
            
            if (ev.Frequency > 0 && gateFrames > 0)
            {
                Ym2151Helper.GetKcKf(ev.Frequency, out byte kc, out byte kf);
                emitReg((byte)(0x28 + fmChannel), kc);
                emitReg((byte)(0x30 + fmChannel), kf);
                
                emitReg(0x08, (byte)(0x78 | fmChannel));
                
                int actualGate = gateFrames;
                int restWait = totalFrames - gateFrames;
                
                // Hardware envelope needs at least 1 frame of KEYOFF to re-trigger properly
                if (restWait == 0)
                {
                    actualGate = Math.Max(1, gateFrames - 1);
                    restWait = totalFrames - actualGate;
                }

                emitWait(actualGate);
                emitReg(0x08, (byte)(0x00 | fmChannel));
                emitWait(restWait);
            }
            else
            {
                // 莨醍ｬｦ(r)縺ｮ譎ゅ・譏守｢ｺ縺ｫKEY OFF繧帝√▲縺ｦ縺九ｉWAIT縺吶ｋ
                emitReg(0x08, (byte)(0x00 | fmChannel));
                emitWait(totalFrames);
            }

            currentTimeMs = nextTimeMs;
            currentFrame = nextFrame;
        }
        
        output.Add((byte)Z80SequenceCommand.TrackEnd);
        return output.ToArray();
    }

}
