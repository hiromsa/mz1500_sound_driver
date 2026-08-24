using System;
using System.Collections.Generic;
using Mz1500SoundPlayer.Sound;

class Program {
    static void Main() {
        var parser = new MultiTrackMmlParser();
        var data = parser.Parse("P1P2P3 t144\nP1 l8 c c\nP2 l4 e\nP3 l2 g");
        var expander = new TrackEventExpander();
        var evs1 = expander.Expand(data.Tracks["P1"]);
        var evs2 = expander.Expand(data.Tracks["P2"]);
        var evs3 = expander.Expand(data.Tracks["P3"]);

        void PrintFrames(string name, List<NoteEvent> events) {
            Console.WriteLine($"--- {name} ---");
            double currentTimeMs = 0;
            foreach (var ev in events) {
                int currentFrame = (int)Math.Round(currentTimeMs * 60.0 / 1000.0);
                int nextFrame = (int)Math.Round((currentTimeMs + ev.DurationMs) * 60.0 / 1000.0);
                int totalFrames = nextFrame - currentFrame;
                Console.WriteLine($"Note: {ev.DurationMs:F2} ms -> Frames: {totalFrames}");
                currentTimeMs += ev.DurationMs;
            }
            Console.WriteLine($"Total Frames: {(int)Math.Round(currentTimeMs * 60.0 / 1000.0)}");
        }
        
        PrintFrames("P1 (2x l8)", evs1);
        PrintFrames("P2 (1x l4)", evs2);
        PrintFrames("P3 (1x l2)", evs3);
    }
}
