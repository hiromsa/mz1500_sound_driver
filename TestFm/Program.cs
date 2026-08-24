using System;
using System.Collections.Generic;
using Mz1500SoundPlayer.Sound;

class Program {
    static void Main() {
        var parser = new MultiTrackMmlParser();
        var data = parser.Parse("P1P2P3 t144\nP1 l8 c r c r c r c r c r c r c r c r\nP2 l4 e e e e e e e e");
        var expander = new TrackEventExpander();
        var evs1 = expander.Expand(data.Tracks["P1"]);
        var evs2 = expander.Expand(data.Tracks["P2"]);

        Console.WriteLine("P1 events:");
        double t1 = 0;
        foreach (var ev in evs1) {
            Console.WriteLine($"  {ev.Frequency} Hz, {ev.DurationMs} ms (Gate: {ev.GateTimeMs} ms)");
            t1 += ev.DurationMs;
        }
        Console.WriteLine($"P1 Total ms: {t1}");
        
        Console.WriteLine("P2 events:");
        double t2 = 0;
        foreach (var ev in evs2) {
            Console.WriteLine($"  {ev.Frequency} Hz, {ev.DurationMs} ms (Gate: {ev.GateTimeMs} ms)");
            t2 += ev.DurationMs;
        }
        Console.WriteLine($"P2 Total ms: {t2}");
    }
}
