using System;
using Mz1500SoundPlayer.Sound;
class Program {
    static void Main() {
        var parser = new MultiTrackMmlParser();
        var mmlData = parser.Parse(""@FM1={ 4,3 31,10,0,0,0,40,0,1,0,0,0 31,12,0,0,0,30,0,2,0,0,0 31,12,0,0,0,30,0,2,0,0,0 31,12,0,0,0,30,0,2,0,0,0 } \n F1 t140v15@1o4cdefg"");
        var expander = new TrackEventExpander();
        var events = expander.Expand(mmlData.Tracks[""F1""]);
        var compiler = new MmlToZ80Compiler();
        var bin = compiler.CompileFmTrack(events, 0, mmlData);
        Console.WriteLine(""Size: "" + bin.Length);
    }
}