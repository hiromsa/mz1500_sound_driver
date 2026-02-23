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
    private const double BaseClockFreq = 111860.0;
    
    // Command Types (VB版互換に近い形で定義)
    public const byte CMD_TONE = 0x01;
    public const byte CMD_REST = 0x02;
    public const byte CMD_VOL  = 0x03;
    public const byte CMD_ENV  = 0x04; // ソフトウェア音量エンベロープのセット
    public const byte CMD_END  = 0xFF; // 曲の終わり

    public byte[] CompileTrack(List<NoteEvent> events, byte psgChannel = 0)
    {
        var output = new List<byte>();
        
        int currentVol = -1; // -1 means uninitialized
        int currentEnvId = -1;
        double currentTimeMs = 0;
        int currentFrame = 0;

        foreach (var ev in events)
        {
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

                // トーン出力 (Tone)
                double freq = ev.Frequency;
                // SN76489は10bitレジスタのため、BaseClockFreq / 1023 = 約109Hz より低い音は出せない。
                // 1023でクリップすると全部A2辺りに張り付いて音痴になるため、収まるまでオクターブを上げる
                while (freq > 0 && BaseClockFreq / freq > 1023)
                {
                    freq *= 2.0;
                }

                double regVal = BaseClockFreq / freq;
                if (regVal > 1023) regVal = 1023; // Safety (Should not hit normally)
                ushort regUshort = (ushort)regVal;

                output.Add(CMD_TONE);
                
                // 周波数レジスタ: Base 10bit
                byte toneCmd1 = (byte)(0x80 | ((psgChannel & 0x03) << 5) | (regUshort & 0x0F));
                byte toneCmd2 = (byte)((regUshort >> 4) & 0x3F);

                output.Add(toneCmd1);
                output.Add(toneCmd2);

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
