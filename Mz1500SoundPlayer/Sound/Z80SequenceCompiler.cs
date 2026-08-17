using System;
using System.Collections.Generic;

namespace Mz1500SoundPlayer.Sound;

/// <summary>
/// MML（ASTのNoteEventリスト）を、Z80サウンドドライバ向けの簡易シーケンスバイナリへ変換するコンパイラ
/// </summary>
public class Z80SequenceCompiler
{
    // SN76489 の計算式: freq = 111860 / register
    // -> register = 111860 / freq (Hz)
    public const double BaseClockFreq = 111860.0;
    public const double BeepClockFreq = 894886.0; // Intel 8253 Timer0 Base Clock
    
    // Command Types (VB版互換に近い形で定義)
    public const byte CMD_TONE = 0x01;
    // CMD_REST removed
    // CMD_VOL removed
    // CMD_ENV removed // ソフトウェア音量エンベロープのセット
    public const byte CMD_PENV = 0xA2; // ピッチエンベロープ(HwPitchEnv)の切り替え
    public const byte CMD_NOISE= 0x06; // ノイズジェネレータ専用出力
    public const byte CMD_SYNC_NOISE = 0x07; // Tone 3 連携モード専用出力
    // CMD_LOOP_MARKER removed // Lコマンドによる無限ループマーカー
    // CMD_END removed
    public const byte CMD_WAIT = 0x20;
    public const byte CMD_YM2151_REG_WRITE = 0x21; // 曲の終わり

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
            if (gateFrames < 1 && ev.Frequency > 0) gateFrames = 1; // 少なくとも1フレームは鳴らす（非常に短い音の場合）

            // エンベロープの状態変化があればまず出力する
            if (ev.EnvelopeId >= 0 && ev.EnvelopeId != currentEnvId)
            {
                output.Add((byte)Z80SequenceCommand.SetVoice);
                output.Add((byte)ev.EnvelopeId);
                currentEnvId = ev.EnvelopeId;
            }
            else if (ev.EnvelopeId < 0 && currentEnvId >= 0)
            {
                // リリースがある場合はサステイン終了直後に(byte)Z80SequenceCommand.SetVoiceをOFFにすると音が切れる可能性があるため、
                // リリースを持たない場合のみ即座にOFFにする。
                // (リリースがある場合は、NoteOff時の展開ループに任せる)
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
                    // Fall back to Rest 処理 below to continue release phase
                }
                else
                {
                    byte muteVolCmd = (byte)(0x90 | ((psgChannel & 0x03) << 5) | 0x0F);
                    output.Add((byte)Z80SequenceCommand.SetVolume);
                    output.Add(muteVolCmd);
                    currentVol = 15;
                    currentReleaseEnvPos = -1;
                }

                // 休符 (Kyufu) / Release Phase Expansion
                ushort durationUnits = (ushort)(totalFrames - 1);
                
                if (currentReleaseEnvPos >= 0 && currentEnvId >= 0 && VolumeEnvelopes.TryGetValue(currentEnvId, out var envDataR) && envDataR.ReleaseValues.Count > 0)
                {
                    // 休符開始時にハードウェアエンベロープをOFFにしてリリース展開を許可する
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
                    output.Add((byte)Z80SequenceCommand.Rest);
                    emitLength(durationUnits + 1);
                }
            }
            else
            {
                // Note ON, reset release envelope position
                currentReleaseEnvPos = 0; 

                // 音量チェンジがあれば先に吐く (エンベロープが効いていればZ80側で上書きされるため初期値として機能する)
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

                // ---------- @EP (ピッチエンベロープ) 処理 ----------
                // @EPの値は「レジスタ差分」として扱う（プラス=音程上昇）
                // baseReg - ep値 の整数クランプで生成する
                if (ev.PitchEnvelopeId >= 0 && PitchEnvelopes.ContainsKey(ev.PitchEnvelopeId))
                {
                    // ベース周波数からベースレジスタ値を求める
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
                            // 出力レジスタ = ベースレジスタ - D値 - EP値の引き算
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

                // ---------- トーン出力 ----------
                // ベース周波数からPSGレジスタ値を計算
                double freq = ev.Frequency;

                if (isBeep)
                {
                    // Beepチャンネル: ユーザー向けにHzベースのままここは変更なし
                    double regVal = BeepClockFreq / freq;
                    if (regVal > 65535) regVal = 65535;
                    if (regVal < 1) regVal = 1;
                    ushort regUshort = (ushort)regVal;
                    byte toneCmd1 = (byte)(regUshort & 0xFF);
                    byte toneCmd2 = (byte)((regUshort >> 8) & 0xFF);
                    output.Add(CMD_TONE);
                    output.Add(toneCmd1);
                    output.Add(toneCmd2);
                    // Beepは長さ出力
                    ushort durationUnitsBp = (ushort)(gateFrames - 1);
                    emitLength(durationUnitsBp + 1);
                }
                else if (psgChannel == 3)
                {
                    // ノイズトラック
                    byte shiftRate = (freq < 300) ? (byte)2 : (freq < 350) ? (byte)1 : (byte)0;
                    byte feedback = (byte)(ev.NoiseWaveMode & 0x01);
                    byte noiseCmd = (byte)(0xE0 | (feedback << 2) | shiftRate);
                    output.Add(CMD_NOISE);
                    output.Add(noiseCmd);
                    // 長さ出力
                    ushort durationUnitsNoise = (ushort)(gateFrames - 1);
                    emitLength(durationUnitsNoise + 1);
                }
                else
                {
                    // ---------- トーンチャンネル (A/B/C/E/F/G) ----------
                    // Tone ON: 0x00 - 0x5F (NoteNumber)
                    int noteNum = ev.NoteNumber;
                    if (noteNum < 0) noteNum = 0;
                    if (noteNum > 95) noteNum = 95;
                    
                    output.Add((byte)noteNum);
                    
                    // 長さ出力
                    ushort durationUnits = (ushort)(gateFrames - 1);
                    emitLength(durationUnits + 1);
                }

                // Rest 処理
                int restFrames = totalFrames - gateFrames;
                if (restFrames > 0)
                {
                    if (currentReleaseEnvPos >= 0 && currentEnvId >= 0 && VolumeEnvelopes.TryGetValue(currentEnvId, out var envDataR) && envDataR.ReleaseValues.Count > 0)
                    {
                        // リリース開始時にハードウェアエンベロープをOFFにする (ソフトウェアでの音量制御に切り替えるため)
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
                        output.Add((byte)Z80SequenceCommand.Rest);
                        emitLength(restUnits + 1);
                    }
                }
            }

            currentTimeMs = nextTimeMs;
            currentFrame = nextFrame;
        }
        
        // 曲端 (Terminator)
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
                // 休符(r)の時は明確にKEY OFFを送ってからWAITする
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
