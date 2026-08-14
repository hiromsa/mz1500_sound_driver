using System;
using System.Collections.Generic;

namespace Mz1500SoundPlayer.Sound;

public static class Ym2151Helper
{
    private static readonly byte[] NoteToKc = new byte[] { 0x0E, 0x00, 0x01, 0x02, 0x04, 0x05, 0x06, 0x08, 0x09, 0x0A, 0x0C, 0x0D };

    public static void GetKcKf(double freq, out byte kc, out byte kf)
    {
        if (freq <= 0) { kc = 0; kf = 0; return; }
        
        // YM2151カードは4.0MHzで駆動する予定です。
        // しかし、YM2151の内部仕様（MAMEのシミュレーション）では、3.579545MHzの時に標準ピッチになります。
        // 4.0MHzで駆動すると、3.579545MHzの時と比べてピッチが 4.0 / 3.579545 ＝ 約1.117倍（約+1.93半音）高くなります。
        // このため「2半音引く」だけでは約7セント（細かい単位）のズレが残ってしまっていました。
        // これを完全に補正するため、目標周波数を 3.579545 / 4.0 で逆算スケーリングします。
        double targetFreq = freq * (3579545.0 / 4000000.0);
        double midiNote = Math.Log(targetFreq / 440.0, 2.0) * 12.0 + 69.0;

        int noteInt = (int)Math.Floor(midiNote);
        double cents = (midiNote - noteInt) * 100.0;
        
        int octave = (noteInt / 12) - 1;
        int noteInOctave = noteInt % 12;

        // YM2151 octaves start at C# (0x00) and end at C (0x0E).
        // Therefore, C belongs to the YM2151's previous octave.
        if (noteInOctave == 0)
        {
            octave -= 1;
        }

        if (octave < 0) octave = 0;
        if (octave > 7) octave = 7;
        
        kc = (byte)((octave << 4) | NoteToKc[noteInOctave]);
        
        // KF is 6 bits (0-63), representing 100 cents. 64 / 100 = 0.64
        kf = (byte)Math.Clamp(Math.Round(cents * 0.64), 0, 63);
    }
}