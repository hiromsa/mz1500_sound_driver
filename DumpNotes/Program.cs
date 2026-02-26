using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using FamiStudio;

class Program
{
    static string GenerateMml(string nstr, int rows, Song song, Project project)
    {
        if (rows <= 0) return "";
        
        List<string> parts = new List<string>();
        int remainingRows = rows;
        
        int quarterNoteRows = project.UsesFamiStudioTempo ? song.BeatLength : 4;
        int wholeNoteRows = quarterNoteRows * 4;
        
        while (remainingRows > 0)
        {
            if (remainingRows >= wholeNoteRows)            { parts.Add("1"); remainingRows -= wholeNoteRows; }
            else if (remainingRows >= quarterNoteRows * 2) { parts.Add("2"); remainingRows -= quarterNoteRows * 2; }
            else if (remainingRows >= quarterNoteRows)     { parts.Add("4"); remainingRows -= quarterNoteRows; }
            else if (remainingRows >= quarterNoteRows / 2) { parts.Add("8"); remainingRows -= quarterNoteRows / 2; }
            else if (remainingRows >= quarterNoteRows / 4) { parts.Add("16"); remainingRows -= quarterNoteRows / 4; }
            else if (remainingRows >= quarterNoteRows / 8) { parts.Add("32"); remainingRows -= quarterNoteRows / 8; }
            else if (remainingRows >= quarterNoteRows / 16){ parts.Add("64"); remainingRows -= quarterNoteRows / 16; }
            else { parts.Add("64"); remainingRows -= 1; }
        }
        
        if (nstr == "r") {
            var res = new StringBuilder();
            foreach(var p in parts) res.Append("r" + p + " ");
            return res.ToString();
        } else {
            var res = new StringBuilder(nstr + parts[0]);
            for (int i = 1; i < parts.Count; i++) res.Append("^" + parts[i]);
            res.Append(" ");
            return res.ToString();
        }
    }

    static void Main(string[] args)
    {
        var project = new ProjectFile().Load("c:/tools/mz1500_sound_driver/fmsSample/PenguinAdventure_Forest/PenguinAdventure_Forest.fms");
        var song = project.Songs[0];
        
        Console.WriteLine("TempoMode: " + (project.UsesFamiStudioTempo ? "FamiStudio" : "FamiTracker"));
        Console.WriteLine("Song tempo=" + song.FamitrackerTempo + " speed=" + song.FamitrackerSpeed + " bpm=" + song.BPM + " beatLength=" + song.BeatLength);
        
        Console.WriteLine("GenerateMml(c, 7) = " + GenerateMml("c", 7, song, project));
        Console.WriteLine("GenerateMml(c, 14) = " + GenerateMml("c", 14, song, project));
        Console.WriteLine("GenerateMml(c, 21) = " + GenerateMml("c", 21, song, project));
    }
}
