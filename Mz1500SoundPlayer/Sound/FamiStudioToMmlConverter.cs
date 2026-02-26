using System;
using System.Text;
using System.Collections.Generic;
using FamiStudio;

namespace Mz1500SoundPlayer.Sound
{
    public class FamiStudioToMmlConverter
    {
        public static string Convert(Project project, int songIndex = 0)
        {
            if (project == null || project.Songs.Count <= songIndex) return "";
            var song = project.Songs[songIndex];

            StringBuilder mml = new StringBuilder();
            mml.AppendLine($"; Imported FMS: {project.Name}");
            mml.AppendLine($"; Song: {song.Name}");
            mml.AppendLine($"; Tempo: {song.FamitrackerTempo} BPM");
            mml.AppendLine();

            // Dump instruments as @v envelopes
            Dictionary<int, int> instrumentIdToEnvId = new Dictionary<int, int>();
            int envIndex = 0;
            foreach (var inst in project.Instruments)
            {
                if (inst.VolumeEnvelope != null && inst.VolumeEnvelope.Length > 0)
                {
                    mml.Append($"@v{envIndex} = {{ ");
                    
                    var values = new List<string>();
                    for (int i = 0; i < inst.VolumeEnvelope.Length; i++)
                    {
                        values.Add(inst.VolumeEnvelope.Values[i].ToString());
                    }
                    mml.Append(string.Join(", ", values));
                    mml.AppendLine(" }");

                    instrumentIdToEnvId[inst.Id] = envIndex;
                    envIndex++;
                }
            }
            mml.AppendLine();

            // Mapping NES channels to MZ-1500
            // ChannelType.Square1 -> A
            // ChannelType.Square2 -> B
            // ChannelType.Triangle -> C
            // ChannelType.Noise -> D
            var channelMap = new Dictionary<int, string>()
            {
                { ChannelType.Square1, "A" },
                { ChannelType.Square2, "B" },
                { ChannelType.Triangle, "C" },
            };

            var rawTracks = new List<string>();
            var usedChannelPrefixes = new List<string>();

            foreach (var kvp in channelMap)
            {
                int famiChannelType = kvp.Key;
                string mmlTrackPrefix = kvp.Value;

                var channel = song.Channels[famiChannelType];
                if (channel == null) continue;

                // 状態変数の一元管理
                int currentMmlOctave = -1;
                int currentEnvId = -1;
                string noteStringPending = null;
                var trackTokens = new List<string>();
                int noteRowsPending = 0;
                int activeNoteDuration = 0;
                
                string GenerateMml(string nstr, int rows)
                {
                    if (rows <= 0) return "";
                    List<string> parts = new List<string>();
                    int remaining = rows;
                    
                    int quarterNoteRows = project.UsesFamiStudioTempo ? song.BeatLength : 4;
                    int wholeNoteRows = quarterNoteRows * 4;

                    while (remaining > 0)
                    {
                        if (remaining >= wholeNoteRows)            { parts.Add("1"); remaining -= wholeNoteRows; }
                        else if (remaining >= quarterNoteRows * 2) { parts.Add("2"); remaining -= quarterNoteRows * 2; }
                        else if (remaining >= quarterNoteRows)     { parts.Add("4"); remaining -= quarterNoteRows; }
                        else if (remaining >= quarterNoteRows / 2) { parts.Add("8"); remaining -= quarterNoteRows / 2; }
                        else if (remaining >= quarterNoteRows / 4) { parts.Add("16"); remaining -= quarterNoteRows / 4; }
                        else if (remaining >= quarterNoteRows / 8) { parts.Add("32"); remaining -= quarterNoteRows / 8; }
                        else if (remaining >= quarterNoteRows / 16) { parts.Add("64"); remaining -= quarterNoteRows / 16; }
                        else { parts.Add("64"); remaining -= 1; }
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

                Action FlushPending = () => {
                    if (noteRowsPending > 0) {
                        string generated = GenerateMml(noteStringPending, noteRowsPending);
                        if (!string.IsNullOrEmpty(generated))
                        {
                            trackTokens.AddRange(generated.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                        }
                        noteRowsPending = 0;
                    }
                };

                for (int p = 0; p < song.Length; p++)
                {
                    int patternLen = song.GetPatternLength(p);
                    for (int r = 0; r < patternLen; r++)
                    {
                        var note = channel.GetNoteAt(new NoteLocation(p, r));
                        
                        if (note != null && note.IsMusical)
                        {
                            FlushPending();
                            
                            int octave = (note.Value - 1) / 12;
                            int noteIdx = (note.Value - 1) % 12;
                            string[] mmlNotes = { "c", "c+", "d", "d+", "e", "f", "f+", "g", "g+", "a", "a+", "b" };
                            
                            string prefixMods = "";
                            
                            if (currentMmlOctave != octave)
                            {
                                if (currentMmlOctave != -1 && octave == currentMmlOctave + 1)
                                {
                                    prefixMods += ">";
                                }
                                else if (currentMmlOctave != -1 && octave == currentMmlOctave - 1)
                                {
                                    prefixMods += "<";
                                }
                                else
                                {
                                    prefixMods += $"o{octave}";
                                }
                                currentMmlOctave = octave;
                            }

                            if (note.Instrument != null && instrumentIdToEnvId.TryGetValue(note.Instrument.Id, out int envId))
                            {
                                if (currentEnvId != envId)
                                {
                                    prefixMods += $"@v{envId}";
                                    currentEnvId = envId;
                                }
                            }

                            noteStringPending = prefixMods + mmlNotes[noteIdx];
                            noteRowsPending = 1;

                            if (project.UsesFamiStudioTempo)
                            {
                                activeNoteDuration = note.Duration - 1;
                            }
                            else
                            {
                                activeNoteDuration = 999999;
                            }
                        }
                        else if (note != null && (note.IsStop || note.IsRelease))
                        {
                            FlushPending();
                            noteStringPending = "r";
                            noteRowsPending = 1;
                            activeNoteDuration = 0;
                        }
                        else
                        {
                            if (project.UsesFamiStudioTempo && noteStringPending != "r" && noteStringPending != null && activeNoteDuration <= 0)
                            {
                                FlushPending();
                                noteStringPending = "r";
                                noteRowsPending = 1;
                            }
                            else
                            {
                                if (noteStringPending == null) noteStringPending = "r";
                                noteRowsPending++;
                                if (project.UsesFamiStudioTempo && noteStringPending != "r")
                                {
                                    activeNoteDuration--;
                                }
                            }
                        }
                    }
                }
                FlushPending();

                if (trackTokens.Count > 0)
                {
                    rawTracks.Add(string.Join(" ", trackTokens));
                    usedChannelPrefixes.Add(mmlTrackPrefix);
                }
            }

            if (rawTracks.Count > 0)
            {
                // Write Tempo
                int tempoBpm = (int)Math.Round((float)song.BPM);
                string usedChannelsString = string.Join("", usedChannelPrefixes);
                mml.AppendLine($"{usedChannelsString} t{tempoBpm}");
                mml.AppendLine();

                // Format each track with line breaks
                for (int i = 0; i < rawTracks.Count; i++)
                {
                    string channelPrefix = usedChannelPrefixes[i];
                    string formattedTrack = FormatWithLineBreaks(rawTracks[i], channelPrefix);
                    mml.AppendLine(formattedTrack);
                    mml.AppendLine();
                }
            }

            return mml.ToString();
        }

        private static string FormatWithLineBreaks(string rawMml, string channelPrefix)
        {
            var sb = new StringBuilder();
            var tokens = rawMml.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string prefix = channelPrefix + " ";
            int lineLen = 0;
            
            sb.Append(prefix);
            lineLen += prefix.Length;

            foreach (var token in tokens)
            {
                if (lineLen + token.Length > 100 && lineLen > prefix.Length)
                {
                    sb.AppendLine();
                    sb.Append(prefix);
                    lineLen = prefix.Length;
                }
                sb.Append(token);
                sb.Append(" ");
                lineLen += token.Length + 1;
            }
            return sb.ToString().TrimEnd();
        }
    }
}
