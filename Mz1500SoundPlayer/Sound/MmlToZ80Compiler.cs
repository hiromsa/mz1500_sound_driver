using System;
using System.Collections.Generic;

namespace Mz1500SoundPlayer.Sound;

/// <summary>
/// MML（ASTのNoteEventリスト）を、Z80サウンドドライバ向けの簡易シーケンスバイナリへ変換するコンパイラ
/// </summary>
public class MmlToZ80Compiler
{
    // SN76489 の計算式: freq = 111860 / register
    // -> register = 111860 / freq (Hz)
    public const double BaseClockFreq = 111860.0;
    public const double BeepClockFreq = 894886.0; // Intel 8253 Timer0 Base Clock
    
    // Command Types (VB版互換に近い形で定義)
    public const byte CMD_TONE = 0x01;
    public const byte CMD_REST = 0x02;
    public const byte CMD_VOL  = 0x03;
    public const byte CMD_ENV  = 0x04; // ソフトウェア音量エンベロープのセット
    public const byte CMD_PENV = 0x05; // ピッチエンベロープ(HwPitchEnv)の切り替え
    public const byte CMD_NOISE= 0x06; // ノイズジェネレータ専用出力
    public const byte CMD_SYNC_NOISE = 0x07; // Tone 3 連携モード専用出力
    public const byte CMD_LOOP_MARKER = 0x08; // Lコマンドによる無限ループマーカー
    public const byte CMD_END  = 0xFF; // 曲の終わり

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

        foreach (var ev in events)
        {
            if (ev.IsLoopPoint)
            {
                output.Add(CMD_LOOP_MARKER);
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
                output.Add(CMD_ENV);
                output.Add((byte)ev.EnvelopeId);
                currentEnvId = ev.EnvelopeId;
            }
            else if (ev.EnvelopeId < 0 && currentEnvId >= 0)
            {
                // リリースがある場合はサステイン終了直後にCMD_ENVをOFFにすると音が切れる可能性があるため、
                // リリースを持たない場合のみ即座にOFFにする。
                // (リリースがある場合は、NoteOff時の展開ループに任せる)
                if (!VolumeEnvelopes.TryGetValue(currentEnvId, out var envDataOff) || envDataOff.ReleaseValues.Count == 0)
                {
                    output.Add(CMD_ENV);
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
                    output.Add(CMD_VOL);
                    output.Add(muteVolCmd);
                    currentVol = 15;
                    currentReleaseEnvPos = -1;
                }

                // 休符 (Kyufu) / Release Phase Expansion
                ushort durationUnits = (ushort)(totalFrames - 1);
                
                if (currentReleaseEnvPos >= 0 && currentEnvId >= 0 && VolumeEnvelopes.TryGetValue(currentEnvId, out var envDataR) && envDataR.ReleaseValues.Count > 0)
                {
                    // 休符開始時にハードウェアエンベロープをOFFにしてリリース展開を許可する
                    output.Add(CMD_ENV);
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
                                output.Add(CMD_VOL);
                                output.Add((byte)(0x90 | ((psgChannel & 0x03) << 5) | (hwVol & 0x0F)));
                                currentVol = hwVol;
                            }
                        }
                        else
                        {
                            if (currentVol != 15)
                            {
                                output.Add(CMD_VOL);
                                output.Add((byte)(0x90 | ((psgChannel & 0x03) << 5) | (0x0F)));
                                currentVol = 15;
                            }
                            currentReleaseEnvPos = -1;
                        }
                        
                        // Emit 1 frame rest
                        output.Add(CMD_REST);
                        output.Add(0);
                        output.Add(0);
                    }
                }
                else
                {
                    output.Add(CMD_REST);
                    output.Add((byte)(durationUnits & 0xFF));
                    output.Add((byte)((durationUnits >> 8) & 0xFF));
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
                    output.Add(CMD_VOL);
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
                    output.Add((byte)(durationUnitsBp & 0xFF));
                    output.Add((byte)((durationUnitsBp >> 8) & 0xFF));
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
                    output.Add((byte)(durationUnitsNoise & 0xFF));
                    output.Add((byte)((durationUnitsNoise >> 8) & 0xFF));
                }
                else
                {
                    // ---------- トーンチャンネル (A/B/C/E/F/G) ----------
                    // ベースレジスタ値を計算 (Hz→レジスタ)
                    while (freq > 0 && BaseClockFreq / freq > 1023) freq *= 2.0; // octave up if too low
                    int baseReg = (int)Math.Round(BaseClockFreq / freq);
                    baseReg = Math.Clamp(baseReg, 0, 1023);

                    bool hasSweep = (ev.Sweep != 0);
                    bool hasPEnv = (ev.PitchEnvelopeId >= 0);

                    if (psgChannel == 2 && ev.IntegrateNoiseMode > 0)
                    {
                        // Tone3 連動ノイズモード: ベースレジスタにDetuneだけ適用
                        int syncReg = Math.Clamp(baseReg - ev.Detune, 0, 1023);
                        ushort syncRegU = (ushort)syncReg;
                        byte freqCmd1 = (byte)(0x80 | ((psgChannel & 0x03) << 5) | (syncRegU & 0x0F));
                        byte freqCmd2 = (byte)((syncRegU >> 4) & 0x3F);
                        byte muteVolCmd = (byte)(0x90 | ((psgChannel & 0x03) << 5) | 0x0F);
                        byte feedback2 = (ev.IntegrateNoiseMode == 2) ? (byte)1 : (byte)0;
                        byte linkedNoiseCmd = (byte)(0xE0 | (feedback2 << 2) | 3);
                        byte noiseVolCmd = (byte)(0x90 | (((psgChannel + 1) & 0x03) << 5) | ((byte)(currentVol >= 0 ? currentVol : 15) & 0x0F));
                        output.Add(CMD_SYNC_NOISE);
                        output.Add(freqCmd1);
                        output.Add(freqCmd2);
                        output.Add(muteVolCmd);
                        output.Add(linkedNoiseCmd);
                        output.Add(noiseVolCmd);
                        // 長さ出力
                        ushort syncDur = (ushort)(gateFrames - 1);
                        output.Add((byte)(syncDur & 0xFF));
                        output.Add((byte)((syncDur >> 8) & 0xFF));
                    }
                    else if (!hasSweep)
                    {
                        // スイープなし: @EPがあればHwPitchEnvに任せ、なければデッド始まりスタット出力
                        int startReg = Math.Clamp(baseReg - ev.Detune, 0, 1023);
                        ushort startRegU = (ushort)startReg;
                        byte toneCmd1 = (byte)(0x80 | ((psgChannel & 0x03) << 5) | (startRegU & 0x0F));
                        byte toneCmd2 = (byte)((startRegU >> 4) & 0x3F);
                        output.Add(CMD_TONE);
                        output.Add(toneCmd1);
                        output.Add(toneCmd2);
                        // 長さ出力
                        ushort durationUnits = (ushort)(gateFrames - 1);
                        output.Add((byte)(durationUnits & 0xFF));
                        output.Add((byte)((durationUnits >> 8) & 0xFF));
                    }
                    else
                    {
                        // スイープあり: gateFrames分をス1フレームずつ展開し、tickごとにレジスタ値を変化させる
                        for (int tick = 0; tick < gateFrames; tick++)
                        {
                            // 出力レジスタ = ベース - D値 - Sweep*tick
                            int sweepReg = Math.Clamp(baseReg - ev.Detune - (ev.Sweep * tick), 0, 1023);
                            ushort sweepRegU = (ushort)sweepReg;
                            byte sweepCmd1 = (byte)(0x80 | ((psgChannel & 0x03) << 5) | (sweepRegU & 0x0F));
                            byte sweepCmd2 = (byte)((sweepRegU >> 4) & 0x3F);
                            output.Add(CMD_TONE);
                            output.Add(sweepCmd1);
                            output.Add(sweepCmd2);
                            output.Add(0); // duration = 1 frame
                            output.Add(0);
                        }
                    }
                }

                // Rest 処理
                int restFrames = totalFrames - gateFrames;
                if (restFrames > 0)
                {
                    if (currentReleaseEnvPos >= 0 && currentEnvId >= 0 && VolumeEnvelopes.TryGetValue(currentEnvId, out var envDataR) && envDataR.ReleaseValues.Count > 0)
                    {
                        // リリース開始時にハードウェアエンベロープをOFFにする (ソフトウェアでの音量制御に切り替えるため)
                        output.Add(CMD_ENV);
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
                                    output.Add(CMD_VOL);
                                    output.Add((byte)(0x90 | ((psgChannel & 0x03) << 5) | (relHwVol & 0x0F)));
                                    currentVol = relHwVol;
                                }
                            }
                            else
                            {
                                if (currentVol != 15)
                                {
                                    output.Add(CMD_VOL);
                                    output.Add((byte)(0x90 | ((psgChannel & 0x03) << 5) | 0x0F));
                                    currentVol = 15;
                                }
                                currentReleaseEnvPos = -1;
                            }
                            
                            output.Add(CMD_REST);
                            output.Add(0);
                            output.Add(0);
                        }
                    }
                    else
                    {
                        byte muteVolCmd = (byte)(0x90 | ((psgChannel & 0x03) << 5) | 0x0F);
                        output.Add(CMD_VOL);
                        output.Add(muteVolCmd);
                        currentVol = 15;
                        
                        ushort restUnits = (ushort)(restFrames - 1);
                        output.Add(CMD_REST);
                        output.Add((byte)(restUnits & 0xFF));
                        output.Add((byte)((restUnits >> 8) & 0xFF));
                    }
                }
            }

            currentTimeMs = nextTimeMs;
            currentFrame = nextFrame;
        }
        
        // 曲端 (Terminator)
        output.Add(CMD_END);

        return output.ToArray();
    }
}
