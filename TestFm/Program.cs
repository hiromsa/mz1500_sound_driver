using System;
using System.IO;
using System.Collections.Generic;
using Mz1500SoundPlayer.Sound;

class Program
{
    static void Main()
    {
        string mml = @"
@FM1 = {
    4, 3
    31, 10, 0, 15, 0, 40, 0, 1, 0, 0, 0
    31, 12, 0, 15, 0, 30, 0, 2, 0, 0, 0
    31, 12, 0, 15, 0, 30, 0, 2, 0, 0, 0
    31, 12, 0, 15, 0, 30, 0, 2, 0, 0, 0
}
F1 t120 v15 @1 o4
F1 c d e f g
F1 c r d r e r f r g r
";
        var parser = new MultiTrackMmlParser();
        var mmlData = parser.Parse(mml);
        var expander = new TrackEventExpander();
        var trackEventsMap = new Dictionary<string, List<NoteEvent>>();
        foreach (var kvp in mmlData.Tracks)
        {
            trackEventsMap[kvp.Key] = expander.Expand(kvp.Value);
        }
        var compiler = new MmlToZ80Compiler();
        compiler.VolumeEnvelopes = mmlData.VolumeEnvelopes;
        compiler.PitchEnvelopes = mmlData.PitchEnvelopes;
        
        var seqBin = compiler.CompileFmTrack(trackEventsMap["F1"], (byte)0, mmlData);
        Console.WriteLine($"Compiled size: {seqBin.Length}");
        
        int pc = 0;
        int frames = 0;
        while(pc < seqBin.Length) {
            byte cmd = seqBin[pc++];
            if(cmd == 0x20) { // CMD_WAIT
                byte l = seqBin[pc++];
                byte h = seqBin[pc++];
                int w = (l | (h << 8)) + 1;
                frames += w;
                Console.WriteLine($"WAIT {w} frames (Total frames: {frames})");
            } else if(cmd == 0x21) { // CMD_YM2151_REG_WRITE
                byte r = seqBin[pc++];
                byte v = seqBin[pc++];
                Console.WriteLine($"REG 0x{r:X2} = 0x{v:X2}");
            } else if (cmd == 0xFF) { // CMD_END
                Console.WriteLine("END");
                break;
            } else {
                Console.WriteLine($"CMD {cmd:X2}");
            }
        }
    }
}
