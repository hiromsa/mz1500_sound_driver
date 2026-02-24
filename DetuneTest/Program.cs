using System;
using Mz1500SoundPlayer.Sound;

class Program
{
    static void Main()
    {
        var parser = new MultiTrackMmlParser();
        var data = parser.Parse("A o4 l4 c D10 c D-10 c");
        var expander = new TrackEventExpander();
        var events = expander.Expand(data.Tracks["A"]);
        
        foreach (var ev in events)
        {
            if (ev.Frequency > 0)
                Console.WriteLine($"Freq: {ev.Frequency:F2}");
        }
    }
}
