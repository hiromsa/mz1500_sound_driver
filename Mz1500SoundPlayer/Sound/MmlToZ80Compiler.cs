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
            
            // 連続音を分離するため、Gateは必ずTotalより最低でも1フレーム短くする
            if (gateFrames >= totalFrames) gateFrames = totalFrames - 1;
            if (gateFrames < 1 && ev.Frequency > 0) gateFrames = 1; // 少なくとも1フレームは鳴らす（非常に短い音の場合）

            // 万が一 totalFrames が1フレームしかなく、音が鳴る場合、gateFrames=1、restFrames=0になる可能性がある。
            // テンポが極端に早い場合を除き、休符が優先か発音が優先かのトレードオフ。

            // エンベロープの状態変化があればまず出力する
            if (ev.EnvelopeId >= 0 && ev.EnvelopeId != currentEnvId)
            {
                output.Add(CMD_ENV);
                output.Add((byte)ev.EnvelopeId);
                currentEnvId = ev.EnvelopeId;
            }
            else if (ev.EnvelopeId < 0 && currentEnvId >= 0)
            {
                output.Add(CMD_ENV);
                output.Add(0xFF); // 0xFF means off
                currentEnvId = -1;
                currentVol = -1; 
            }

            if (ev.Frequency == 0 || ev.Volume == 0 || gateFrames <= 0)
            {
                // Mute before rest
                byte muteVolCmd = (byte)(0x90 | ((psgChannel & 0x03) << 5) | 0x0F);
                output.Add(CMD_VOL);
                output.Add(muteVolCmd);
                currentVol = 15;

                // 休符 (Kyufu)
                ushort durationUnits = (ushort)(totalFrames - 1);
                output.Add(CMD_REST);
                output.Add((byte)(durationUnits & 0xFF));
                output.Add((byte)((durationUnits >> 8) & 0xFF));
            }
            else
            {

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

                // ピッチエンベロープの処理 (周波数に依存するためTone出力前に動的生成)
                if (ev.PitchEnvelopeId >= 0 && PitchEnvelopes.ContainsKey(ev.PitchEnvelopeId))
                {
                    string cacheKey = $"Freq_{ev.Frequency}_EP_{ev.PitchEnvelopeId}_Ch_{psgChannel}";
                    if (!_hwPitchEnvCache.TryGetValue(cacheKey, out int hwId))
                    {
                        var pEnvData = PitchEnvelopes[ev.PitchEnvelopeId];
                        var registers = new List<ushort>();
                        double baseFreq = ev.Frequency;
                        
                        double baseClock = isBeep ? BeepClockFreq : BaseClockFreq;
                        foreach (var cent in pEnvData.Values)
                        {
                            double freqCent = baseFreq * Math.Pow(2.0, cent / 1200.0);
                            
                            if (isBeep)
                            {
                                double regCent = baseClock / freqCent;
                                if (regCent > 65535) regCent = 65535;
                                if (regCent < 1) regCent = 1;
                                ushort regUshortCent = (ushort)regCent;
                                
                                byte cmd1 = (byte)(regUshortCent & 0xFF);
                                byte cmd2 = (byte)((regUshortCent >> 8) & 0xFF);
                                registers.Add((ushort)(cmd1 | (cmd2 << 8)));
                            }
                            else
                            {
                                while (freqCent > 0 && baseClock / freqCent > 1023) freqCent *= 2.0;
                                double regCent = baseClock / freqCent;
                                if (regCent > 1023) regCent = 1023;
                                if (regCent < 0) regCent = 0;
                                ushort regUshortCent = (ushort)regCent;
                                
                                byte cmd1 = (byte)(0x80 | ((psgChannel & 0x03) << 5) | (regUshortCent & 0x0F));
                                byte cmd2 = (byte)((regUshortCent >> 4) & 0x3F);
                                
                                registers.Add((ushort)(cmd1 | (cmd2 << 8)));
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

                // トーン出力 (Tone)
                double freq = ev.Frequency;
                byte toneCmd1 = 0;
                byte toneCmd2 = 0;

                if (isBeep)
                {
                    double regVal = BeepClockFreq / freq;
                    if (regVal > 65535) regVal = 65535;
                    if (regVal < 1) regVal = 1;
                    ushort regUshort = (ushort)regVal;
                    toneCmd1 = (byte)(regUshort & 0xFF);
                    toneCmd2 = (byte)((regUshort >> 8) & 0xFF);

                    output.Add(CMD_TONE);
                    output.Add(toneCmd1);
                    output.Add(toneCmd2);
                }
                else if (psgChannel == 3)
                {
                    // ノイズトラック判定 (D, H)
                    // 仕様: freq値をShift Rateへマッピング (c < 300Hz -> Low(2), e < 350Hz -> Mid(1), g -> High(0))
                    byte shiftRate = (freq < 300) ? (byte)2 : (freq < 350) ? (byte)1 : (byte)0;
                    byte feedback = (byte)(ev.NoiseWaveMode & 0x01); // 0=Periodic, 1=White Noise
                    
                    // ノイズ制御レジスタ形式: 1110_0FBW
                    byte noiseCmd = (byte)(0xE0 | (feedback << 2) | shiftRate);

                    // TODO 骨格: Z80ドライバとVMに CMD_NOISE(0x06) を実装するまでの間、
                    // 仮のコマンドとしてバイト列を出力するステートメント
                    output.Add(CMD_NOISE);
                    output.Add(noiseCmd);
                }
                else
                {
                    // トーンチャネル判定 (A, B, C, E, F, G)
                    if (psgChannel == 2 && ev.IntegrateNoiseMode > 0)
                    {
                        // Tone 3 連動ノイズモード (@in)
                        // Tone 3として周波数計算
                        while (freq > 0 && BaseClockFreq / freq > 1023) freq *= 2.0;
                        double regVal = BaseClockFreq / freq;
                        if (regVal > 1023) regVal = 1023;
                        ushort regUshort = (ushort)regVal;

                        byte freqCmd1 = (byte)(0x80 | ((psgChannel & 0x03) << 5) | (regUshort & 0x0F));
                        byte freqCmd2 = (byte)((regUshort >> 4) & 0x3F);

                        byte muteVolCmd = (byte)(0x90 | ((psgChannel & 0x03) << 5) | 0x0F);
                        
                        byte feedback = (ev.IntegrateNoiseMode == 2) ? (byte)1 : (byte)0; // 2=White, 1=Periodic
                        byte linkedNoiseCmd = (byte)(0xE0 | (feedback << 2) | 3); // W=3 (Tone 3 Linked)
                        
                        // Noise Channel = psgChannel + 1
                        byte noiseVolCmd = (byte)(0x90 | (((psgChannel + 1) & 0x03) << 5) | ((byte)(currentVol >= 0 ? currentVol : 15) & 0x0F));

                        // TODO 骨格: Z80ドライバとVMに CMD_SYNC_NOISE(0x07) を実装し、以下のパラメータを解釈させる
                        output.Add(CMD_SYNC_NOISE);
                        output.Add(freqCmd1);
                        output.Add(freqCmd2);
                        output.Add(muteVolCmd);
                        output.Add(linkedNoiseCmd);
                        output.Add(noiseVolCmd);
                    }
                    else
                    {
                        // 通常トーンモード
                        // SN76489は10bitレジスタのため、BaseClockFreq / 1023 = 約109Hz より低い音は出せない。
                        while (freq > 0 && BaseClockFreq / freq > 1023) freq *= 2.0;

                        double regVal = BaseClockFreq / freq;
                        if (regVal > 1023) regVal = 1023; // Safety
                        ushort regUshort = (ushort)regVal;

                        toneCmd1 = (byte)(0x80 | ((psgChannel & 0x03) << 5) | (regUshort & 0x0F));
                        toneCmd2 = (byte)((regUshort >> 4) & 0x3F);

                        output.Add(CMD_TONE);
                        output.Add(toneCmd1);
                        output.Add(toneCmd2);
                    }
                }

                // 長さ (2バイト)
                ushort durationUnits = (ushort)(gateFrames - 1);
                output.Add((byte)(durationUnits & 0xFF));
                output.Add((byte)((durationUnits >> 8) & 0xFF));

                // Rest 処理
                int restFrames = totalFrames - gateFrames;
                if (restFrames > 0)
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

            currentTimeMs = nextTimeMs;
            currentFrame = nextFrame;
        }
        
        // 曲端 (Terminator)
        output.Add(CMD_END);

        return output.ToArray();
    }
}
