using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Mz1500SoundPlayer.Sound;

public class MmlEditorOptions
{
    // Length Edit
    public enum LengthEditMode { None, RemoveAll, Unify, Increment, Optimize, Expand }
    public LengthEditMode LengthMode { get; set; } = LengthEditMode.None;
    public int UnifyLength { get; set; } = 8;
    public bool UseLCommandForUnify { get; set; } = false;
    public int IncrementLengthValue { get; set; } = 0; // Negative means shorter note (e.g., -16)

    // Pitch Edit
    public enum OctaveEditMode { None, ToRelative, ToAbsolute }
    public OctaveEditMode OctaveMode { get; set; } = OctaveEditMode.None;
    public int TransposeAmount { get; set; } = 0;
    
    // Note Replacement
    public Dictionary<string, string> NoteReplacements { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class MmlTextTransformer
{
    private static readonly string[] NoteNames = { "c", "c+", "d", "d+", "e", "f", "f+", "g", "g+", "a", "a+", "b" };
    private static readonly string[] NoteNamesDim = { "c", "d-", "d", "e-", "e", "f", "g-", "g", "a-", "a", "b-", "b" };

    public static string Transform(string selectedText, string prefixContext, MmlEditorOptions options)
    {
        // 0. Quick Bypass if strictly nothing to be done
        if (options.LengthMode == MmlEditorOptions.LengthEditMode.None && 
            options.OctaveMode == MmlEditorOptions.OctaveEditMode.None && 
            options.TransposeAmount == 0 &&
            options.NoteReplacements.Count == 0)
        {
            return selectedText;
        }

        // 1. Determine Context (Current Octave and Default Length) at the start of selection
        int contextOctave = 4;
        int contextLength = 4;
        string currentLengthText = "4";
        
        try
        {
            var parser = new MultiTrackMmlParser();
            var dummyData = parser.Parse("A " + prefixContext); // Force track A to gather context
            if (dummyData.Tracks.ContainsKey("A"))
            {
                var cmds = dummyData.Tracks["A"];
                foreach (var cmd in cmds.Commands)
                {
                    if (cmd is OctaveCommand oc) contextOctave = oc.Octave;
                    else if (cmd is RelativeOctaveCommand rc) contextOctave += rc.Offset;
                    else if (cmd is DefaultLengthCommand lc)
                    {
                        contextLength = lc.Length;
                        currentLengthText = lc.Length.ToString() + new string('.', lc.Dots);
                    }
                }
            }
        }
        catch { /* Fallback to defaults */ }

        int outputOctave = contextOctave; // Tracks the *output* string's octave context to compute correct boundaries
        
        var regex = new Regex(@"([a-grA-GR][\+\-\#]?)([0-9]*\.*)|(o[0-9]+)|([<>]+)|(l[0-9]+\.*)|(\{)|(\})|(\^[0-9]*\.*)|(K\-?[0-9]+)", RegexOptions.Compiled);

        var output = new StringBuilder();
        bool lCommandInserted = false;
        string activeLCmdLength = currentLengthText;

        string[] lines = Regex.Split(selectedText, @"(?<=\n)");
        bool firstLine = true;

        foreach (string line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;

            bool checkHeader = true;
            if (firstLine)
            {
                if (prefixContext.Length > 0 && !prefixContext.EndsWith("\n") && !prefixContext.EndsWith("\r"))
                    checkHeader = false;
                firstLine = false;
            }

            int headerEnd = -1;
            if (checkHeader)
            {
                int lastHeaderChar = -1;
                for (int c = 0; c < line.Length; c++)
                {
                    char ch = line[c];
                    if (char.IsWhiteSpace(ch) || ch == '\r' || ch == '\n') continue;
                    if ((ch >= 'A' && ch <= 'H') || ch == 'P')
                    {
                        lastHeaderChar = c;
                    }
                    else
                    {
                        break;
                    }
                }
                if (lastHeaderChar != -1)
                {
                    headerEnd = lastHeaderChar + 1;
                }
            }

            string header = "";
            string mmlPart = line;

            if (headerEnd > 0)
            {
                header = line.Substring(0, headerEnd);
                mmlPart = line.Substring(headerEnd);
            }

            output.Append(header);

            int lastPos = 0;
            var matches = regex.Matches(mmlPart);

            foreach (Match m in matches)
            {
                // Append non-matched text (spaces, other commands like volume/tempo)
                output.Append(mmlPart.Substring(lastPos, m.Index - lastPos));
                lastPos = m.Index + m.Length;

                string token = m.Value;

                // 1. Handle Length 'l' command
                if (token.StartsWith("l", StringComparison.OrdinalIgnoreCase))
                {
                    if (options.LengthMode == MmlEditorOptions.LengthEditMode.Unify && options.UseLCommandForUnify)
                        continue; // Strip it
                        
                    if (options.LengthMode == MmlEditorOptions.LengthEditMode.Expand || options.LengthMode == MmlEditorOptions.LengthEditMode.Optimize)
                    {
                        currentLengthText = token.Substring(1); // Extract "8", "16.", etc.
                        if (options.LengthMode == MmlEditorOptions.LengthEditMode.Expand)
                            continue; // Expand mode strips all 'l' commands so notes are explicit
                            
                        if (options.LengthMode == MmlEditorOptions.LengthEditMode.Optimize)
                            continue; // Optimize regenerates them strategically
                    }
                }

                // 2. Handle Octave Conversions
                if (token.StartsWith("o", StringComparison.OrdinalIgnoreCase) || token == "<" || token == ">")
                {
                    if (token.StartsWith("o", StringComparison.OrdinalIgnoreCase) && int.TryParse(token.Substring(1), out int parsedOct))
                    {
                        contextOctave = parsedOct;
                        if (options.OctaveMode == MmlEditorOptions.OctaveEditMode.ToRelative)
                        {
                            int diff = parsedOct - outputOctave;
                            if (diff > 0) output.Append(new String('>', diff));
                            else if (diff < 0) output.Append(new String('<', -diff));
                            outputOctave = parsedOct;
                            continue;
                        }
                        else if (options.OctaveMode == MmlEditorOptions.OctaveEditMode.ToAbsolute)
                        {
                            output.Append($"o{parsedOct}");
                            outputOctave = parsedOct;
                            continue;
                        }
                        else
                        {
                            output.Append($"o{parsedOct}");
                            outputOctave = parsedOct;
                            continue;
                        }
                    }
                    else if (token == "<")
                    {
                        contextOctave--;
                        if (options.OctaveMode == MmlEditorOptions.OctaveEditMode.ToAbsolute)
                        {
                            output.Append($"o{contextOctave}");
                            outputOctave = contextOctave;
                            continue;
                        }
                        else
                        {
                            output.Append("<");
                            outputOctave--;
                            continue;
                        }
                    }
                    else if (token == ">")
                    {
                        contextOctave++;
                        if (options.OctaveMode == MmlEditorOptions.OctaveEditMode.ToAbsolute)
                        {
                            output.Append($"o{contextOctave}");
                            outputOctave = contextOctave;
                            continue;
                        }
                        else
                        {
                            output.Append(">");
                            outputOctave++;
                            continue;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(m.Groups[8].Value))
                {
                    string tokenStr = m.Groups[8].Value; 
                    string tieNum = tokenStr.Substring(1); 
                    
                    if (options.LengthMode == MmlEditorOptions.LengthEditMode.RemoveAll) {
                        output.Append("^");
                    }
                    else if (options.LengthMode == MmlEditorOptions.LengthEditMode.Unify && !options.UseLCommandForUnify) {
                        output.Append("^" + options.UnifyLength.ToString());
                    }
                    else if (options.LengthMode == MmlEditorOptions.LengthEditMode.Unify && options.UseLCommandForUnify) {
                        output.Append("^");
                    }
                    else if (options.LengthMode == MmlEditorOptions.LengthEditMode.Increment && options.IncrementLengthValue != 0) {
                        int currentNoteLen = string.IsNullOrEmpty(tieNum) ? contextLength : ParseBaseLength(tieNum);
                        int currentTicks = LengthToTicks(string.IsNullOrEmpty(tieNum) ? currentNoteLen.ToString() : tieNum, contextLength);
                        int modifierTicks = LengthToTicks(Math.Abs(options.IncrementLengthValue).ToString(), contextLength);
                        if (options.IncrementLengthValue > 0) currentTicks -= modifierTicks; else currentTicks += modifierTicks;
                        if (currentTicks <= 0) currentTicks = 3; 
                        output.Append("^" + TicksToLengthString(currentTicks));
                    }
                    else if (options.LengthMode == MmlEditorOptions.LengthEditMode.Expand) {
                        string intendedLength = string.IsNullOrEmpty(tieNum) ? currentLengthText : tieNum;
                        output.Append("^" + intendedLength);
                    }
                    else if (options.LengthMode == MmlEditorOptions.LengthEditMode.Optimize) {
                        string intendedLength = string.IsNullOrEmpty(tieNum) ? currentLengthText : tieNum;
                        if (intendedLength != activeLCmdLength) {
                            output.Append("^" + intendedLength);
                        } else {
                            output.Append("^");
                        }
                    }
                    else {
                        output.Append(tokenStr);
                    }
                    continue;
                }

                // 3. Handle Notes & Transpose
                if (!string.IsNullOrEmpty(m.Groups[1].Value)) 
                {
                    string originalNoteCase = m.Groups[1].Value;
                    string noteName = originalNoteCase.ToLowerInvariant();
                    string lengthStr = m.Groups[2].Value; // e.g., "16", "4.", ""

                    // Note Replacement
                    if (options.NoteReplacements.TryGetValue(noteName, out string replacementNote))
                    {
                        bool isUpper = char.IsUpper(originalNoteCase[0]);
                        noteName = replacementNote.ToLowerInvariant();
                        originalNoteCase = isUpper ? replacementNote.ToUpperInvariant() : noteName;
                    }

                    // Transpose Math (Skip for rests)
                    if (options.TransposeAmount != 0 && noteName != "r")
                    {
                        int noteIndex = NoteToIndex(noteName);
                        if (noteIndex >= 0)
                        {
                            int absolutePitch = (contextOctave * 12) + noteIndex + options.TransposeAmount;
                            int newOctave = absolutePitch / 12;
                            int newNoteIndex = absolutePitch % 12;
                            if (newNoteIndex < 0) { newNoteIndex += 12; newOctave--; }

                            // Adjust output octave if it crossed a boundary during transposition
                            if (newOctave != outputOctave)
                            {
                                int diff = newOctave - outputOctave;
                                if (options.OctaveMode == MmlEditorOptions.OctaveEditMode.ToRelative)
                                {
                                    if (diff > 0) output.Append(new String('>', diff));
                                    else if (diff < 0) output.Append(new String('<', -diff));
                                }
                                else if (options.OctaveMode == MmlEditorOptions.OctaveEditMode.ToAbsolute)
                                {
                                    output.Append($"o{newOctave}");
                                }
                                else
                                {
                                    if (Math.Abs(diff) <= 2) {
                                        if (diff > 0) output.Append(new String('>', diff));
                                        else if (diff < 0) output.Append(new String('<', -diff));
                                    } else {
                                        output.Append($"o{newOctave}");
                                    }
                                }
                                outputOctave = newOctave;
                            }

                            bool preferFlat = noteName.Contains("-") || options.TransposeAmount < 0;
                            // Re-apply original case
                            string transposedNote = preferFlat ? NoteNamesDim[newNoteIndex] : NoteNames[newNoteIndex];
                            noteName = char.IsUpper(originalNoteCase[0]) ? transposedNote.ToUpperInvariant() : transposedNote;
                        }
                    }
                    else
                    {
                        noteName = originalNoteCase; // Restore precise original case if untouched
                    }

                    output.Append(noteName);

                    // Insert 'l' command at the very first note if configured
                    if (options.LengthMode == MmlEditorOptions.LengthEditMode.Unify && options.UseLCommandForUnify && !lCommandInserted)
                    {
                        string lCmd = $"l{options.UnifyLength} ";
                        output.Insert(output.Length - noteName.Length, lCmd);
                        lCommandInserted = true;
                    }

                    // Length Math
                    if (options.LengthMode == MmlEditorOptions.LengthEditMode.RemoveAll)
                    {
                        // Do nothing, effectively dropping lengthStr
                    }
                    else if (options.LengthMode == MmlEditorOptions.LengthEditMode.Unify)
                    {
                        if (!options.UseLCommandForUnify)
                        {
                            output.Append(options.UnifyLength.ToString());
                        }
                    }
                    else if (options.LengthMode == MmlEditorOptions.LengthEditMode.Increment && options.IncrementLengthValue != 0)
                    {
                        int currentNoteLen = string.IsNullOrEmpty(lengthStr) ? contextLength : ParseBaseLength(lengthStr);
                        int currentTicks = LengthToTicks(string.IsNullOrEmpty(lengthStr) ? currentNoteLen.ToString() : lengthStr, contextLength);
                        
                        int modifierTicks = LengthToTicks(Math.Abs(options.IncrementLengthValue).ToString(), contextLength);
                        
                        if (options.IncrementLengthValue > 0) currentTicks -= modifierTicks; 
                        else currentTicks += modifierTicks;

                        if (currentTicks <= 0) 
                        {
                            currentTicks = 3; 
                        }

                        string newLength = TicksToLengthString(currentTicks);
                        output.Append(newLength);
                    }
                    else if (options.LengthMode == MmlEditorOptions.LengthEditMode.Expand)
                    {
                        string intendedLength = string.IsNullOrEmpty(lengthStr) ? currentLengthText : lengthStr;
                        output.Append(intendedLength);
                    }
                    else if (options.LengthMode == MmlEditorOptions.LengthEditMode.Optimize)
                    {
                        string intendedLength = string.IsNullOrEmpty(lengthStr) ? currentLengthText : lengthStr;
                        if (intendedLength != activeLCmdLength)
                        {
                            string lCmd = $"l{intendedLength} ";
                            output.Insert(output.Length - noteName.Length, lCmd);
                            activeLCmdLength = intendedLength;
                        }
                    }
                    else
                    {
                        // None (keep original)
                        output.Append(lengthStr);
                    }
                    continue;
                }

                output.Append(token);
            }

            output.Append(mmlPart.Substring(lastPos));
        }

        if (options.LengthMode == MmlEditorOptions.LengthEditMode.Unify && options.UseLCommandForUnify && !lCommandInserted)
        {
            output.Insert(0, $"l{options.UnifyLength} ");
        }

        return output.ToString();
    }

    private static int NoteToIndex(string note)
    {
        note = note.ToLowerInvariant();
        char baseNote = note[0];
        int val = baseNote switch { 'c' => 0, 'd' => 2, 'e' => 4, 'f' => 5, 'g' => 7, 'a' => 9, 'b' => 11, _ => -1 };
        if (val == -1) return -1;
        if (note.Contains("+") || note.Contains("#")) val++;
        if (note.Contains("-")) val--;
        return (val + 12) % 12;
    }

    private static int ParseBaseLength(string len)
    {
        var match = Regex.Match(len, "^[0-9]+");
        if (match.Success && int.TryParse(match.Value, out int l)) return l;
        return 4; // fallback
    }

    private static int LengthToTicks(string lengthStr, int defaultLength)
    {
        int l = defaultLength;
        var match = Regex.Match(lengthStr, "^[0-9]+");
        if (match.Success) int.TryParse(match.Value, out l);
        
        if (l <= 0) return 0;

        int ticks = 192 / l;
        
        int dots = lengthStr.Length - (match.Success ? match.Length : 0);
        int add = ticks / 2;
        for (int i = 0; i < dots; i++) { ticks += add; add /= 2; }

        return ticks;
    }

    private static string TicksToLengthString(int ticks)
    {
        // Reverse mapping ticks back to MML notation (rough approximation for standard lengths)
        // 192=1, 96=2, 48=4, 72=4., 24=8, 36=8., 12=16, 18=16.
        if (ticks >= 192) return "1";
        if (ticks == 144) return "2.";
        if (ticks >= 96) return "2";
        if (ticks == 72) return "4.";
        if (ticks >= 48) return "4";
        if (ticks == 36) return "8.";
        if (ticks >= 24) return "8";
        if (ticks == 18) return "16.";
        if (ticks >= 12) return "16";
        if (ticks >= 6) return "32";
        return "64";
    }
}
