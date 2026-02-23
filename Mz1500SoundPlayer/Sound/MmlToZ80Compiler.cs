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
    public const byte CMD_END  = 0xFF; // 曲の終わり

    public byte[] CompileTrack(List<NoteEvent> events, byte psgChannel = 0)
    {
        var output = new List<byte>();
        
        int currentVol = -1; // -1 means uninitialized

        foreach (var ev in events)
        {
            // NoteEvent は msec 単位。Z80ドライバのウェイト基準単位に変換する
            // 簡易的に 1 unit = 1ms とする (要Z80ドライバ実装次第調整)
            ushort durationUnits = (ushort)ev.DurationMs;
            ushort gateUnits = (ushort)ev.GateTimeMs;

            if (ev.Frequency == 0 || ev.Volume == 0)
            {
                // 休符 (Kyufu)
                output.Add(CMD_REST);
                output.Add((byte)(durationUnits & 0xFF));
                output.Add((byte)((durationUnits >> 8) & 0xFF));
            }
            else
            {
                // 音量チェンジがあれば先に吐く
                // ev.Volumeは0.0~0.2くらいにスケーリングされている。元の 0-15 に戻して15を引く(反転)などの処理が要る
                // ここでは仮に(ev.Volume / 0.15) * 15 でMAX=15に戻す計算とする
                int vol15 = (int)Math.Round((ev.Volume / 0.15) * 15.0);
                if (vol15 < 0) vol15 = 0;
                if (vol15 > 15) vol15 = 15;

                // ドライバ用のボリュームは HW依存(0x00=大, 0x0F=無音)なので反転
                byte hwVol = (byte)(15 - vol15);

                if (currentVol != hwVol)
                {
                    output.Add(CMD_VOL);
                    // SN76489 Volume Command: 1 c c 1 v v v v (c=channel, v=volume)
                    byte volCmd = (byte)(0x90 | ((psgChannel & 0x03) << 5) | (hwVol & 0x0F));
                    output.Add(volCmd);
                    currentVol = hwVol;
                }

                // トーン出力 (Tone)
                double regVal = BaseClockFreq / ev.Frequency;
                if (regVal > 1023) regVal = 1023; // SN76489は10bitレジスタ
                ushort regUshort = (ushort)regVal;

                output.Add(CMD_TONE);
                
                // 周波数レジスタ: Base 10bit
                // Byte 1: 1 c c 0 d d d d (c=channel, d=lower 4 bits)
                byte toneCmd1 = (byte)(0x80 | ((psgChannel & 0x03) << 5) | (regUshort & 0x0F));
                // Byte 2: 0 - d d d d d d (d=upper 6 bits)
                byte toneCmd2 = (byte)((regUshort >> 4) & 0x3F);

                output.Add(toneCmd1);
                output.Add(toneCmd2);

                // 長さ (2バイト)
                output.Add((byte)(durationUnits & 0xFF));
                output.Add((byte)((durationUnits >> 8) & 0xFF));

                // 発音長(Gate) (2バイト)
                output.Add((byte)(gateUnits & 0xFF));
                output.Add((byte)((gateUnits >> 8) & 0xFF));
            }
        }
        
        // 曲端 (Terminator)
        output.Add(CMD_END);

        return output.ToArray();
    }
}
