using System;
using System.Collections.Generic;
using Mz1500SoundPlayer.Sound;

class Program {
    static void Main() {
        var parser = new MultiTrackMmlParser();
        var mmlData = parser.Parse(""@FM1={ 4,3 31,10,0,0,0,40,0,1,0,0,0 31,12,0,0,0,30,0,2,0,0,0 31,12,0,0,0,30,0,2,0,0,0 31,12,0,0,0,30,0,2,0,0,0 } \n F1 t140v15@1o4cdefg"");
        var expander = new TrackEventExpander();
        var events = expander.Expand(mmlData.Tracks[""F1""]);
        Console.WriteLine(""Events count: "" + events.Count);
        foreach(var ev in events) {
            Console.WriteLine($""Note: {ev.Frequency:F1}Hz, Dur: {ev.DurationMs:F1}ms"");
        }
    }
}