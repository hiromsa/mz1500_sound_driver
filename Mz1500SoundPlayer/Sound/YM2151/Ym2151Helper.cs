using System;
using System.Collections.Generic;

namespace Mz1500SoundPlayer.Sound;

public static class Ym2151Helper
{
    private static readonly byte[] NoteToKc = new byte[] { 0x00, 0x01, 0x02, 0x04, 0x05, 0x06, 0x08, 0x09, 0x0A, 0x0C, 0x0D, 0x0E };

    public static void GetKcKf(double freq, out byte kc, out byte kf)
    {
        if (freq <= 0) { kc = 0; kf = 0; return; }
        double midiNote = Math.Log(freq / 440.0, 2.0) * 12.0 + 69.0;
        int noteInt = (int)Math.Floor(midiNote);
        double cents = (midiNote - noteInt) * 100.0;
        
        int octave = (noteInt / 12) - 1;
        if (octave < 0) octave = 0;
        if (octave > 7) octave = 7;
        
        int noteInOctave = noteInt % 12;
        kc = (byte)((octave << 4) | NoteToKc[noteInOctave]);
        
        // KF is 6 bits (0-63), representing 100 cents. 64 / 100 = 0.64
        kf = (byte)Math.Clamp(Math.Round(cents * 0.64), 0, 63);
    }
}