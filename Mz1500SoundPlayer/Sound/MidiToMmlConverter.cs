using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NAudio.Midi;

namespace Mz1500SoundPlayer.Sound
{
    public static class MmlLengthConverter
    {
        /// <summary>
        /// MIDIのTick数を、MMLの音符長文字列（例: "4", "8.", "16^32"）に変換するユーティリティ。
        /// 微小なTickのズレを吸収し、最適な付点やタイ（^）を用いてMMLを生成します。
        /// </summary>
        public static string ConvertTicksToMmlLength(int targetTicks, int ticksPerQuarterNote)
        {
            if (targetTicks <= 0) return "0";

            // 全音符のTick数（四分音符の4倍）
            int wholeNoteTicks = ticksPerQuarterNote * 4;

            // 手弾きMIDIなどの微妙なズレを吸収する許容誤差（例：32分音符の半分まではスナップさせる）
            int tolerance = wholeNoteTicks / 64 / 2;

            int remainingTicks = targetTicks;
            List<string> mmlLengths = new List<string>();

            // 標準的な音符と付点音符の定義（長い順に判定してタイを減らす）
            var standardLengths = new[]
            {
                new { Name = "1.", Ticks = wholeNoteTicks * 3 / 2 },
                new { Name = "1",  Ticks = wholeNoteTicks },
                new { Name = "2.", Ticks = wholeNoteTicks / 2 * 3 / 2 },
                new { Name = "2",  Ticks = wholeNoteTicks / 2 },
                new { Name = "4.", Ticks = wholeNoteTicks / 4 * 3 / 2 },
                new { Name = "4",  Ticks = wholeNoteTicks / 4 },
                new { Name = "8.", Ticks = wholeNoteTicks / 8 * 3 / 2 },
                new { Name = "8",  Ticks = wholeNoteTicks / 8 },
                new { Name = "16.",Ticks = wholeNoteTicks / 16 * 3 / 2 },
                new { Name = "16", Ticks = wholeNoteTicks / 16 },
                new { Name = "32", Ticks = wholeNoteTicks / 32 },
                new { Name = "64", Ticks = wholeNoteTicks / 64 }
            };

            // Tickを消費しきるまで、入る最大の音符を切り出していく（グリーディ法）
            while (remainingTicks > tolerance)
            {
                // 許容誤差を含めて、現在残っているTickに収まる最大の音符を探す
                var bestFit = standardLengths.FirstOrDefault(l => l.Ticks <= remainingTicks + tolerance);

                if (bestFit != null)
                {
                    mmlLengths.Add(bestFit.Name);
                    // 実際に減らすのは基準Tick数（誤差は無視して正規化）
                    remainingTicks -= bestFit.Ticks; 
                }
                else
                {
                    // 64分音符より細かいゴミTickは切り捨ててループを抜ける
                    break;
                }
            }

            // 極端に短いノートだった場合のフォールバック
            if (mmlLengths.Count == 0) return "64";

            // 複数の音符に分割された場合はタイ(^)で繋いで返す (例: "4^16")
            return string.Join("^", mmlLengths);
        }
    }

    public class MidiToMmlConverter
    {
        private static readonly string[] AvailableChannels = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" };

        public string Convert(string midiFilePath)
        {
            var midiFile = new MidiFile(midiFilePath, false);
            var sb = new StringBuilder();

            int ticksPerQuarterNote = midiFile.DeltaTicksPerQuarterNote;
            int tempoBpm = 120;

            // Tempo Event 検索
            foreach (var track in midiFile.Events)
            {
                foreach (var ev in track)
                {
                    if (ev is TempoEvent tempoEvent)
                    {
                        tempoBpm = (int)tempoEvent.Tempo;
                        break;
                    }
                }
                if (tempoBpm != 120) break;
            }

            var rawTracks = new List<string>();
            foreach (var track in midiFile.Events)
            {
                var rawMml = ConvertTrack(track, ticksPerQuarterNote);
                if (!string.IsNullOrWhiteSpace(rawMml))
                {
                    rawTracks.Add(rawMml);
                }
            }

            if (rawTracks.Count > AvailableChannels.Length)
            {
                rawTracks = rawTracks.Take(AvailableChannels.Length).ToList();
            }

            if (rawTracks.Count == 0) return "";

            string usedChannelsString = string.Join("", AvailableChannels.Take(rawTracks.Count));
            sb.AppendLine($"{usedChannelsString} t{tempoBpm}");
            sb.AppendLine();

            for (int i = 0; i < rawTracks.Count; i++)
            {
                string channelPrefix = AvailableChannels[i];
                string formattedTrack = FormatWithLineBreaks(rawTracks[i], channelPrefix);
                sb.AppendLine(formattedTrack);
            }

            return sb.ToString();
        }

        private string FormatWithLineBreaks(string rawMml, string channelPrefix)
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

        private string ConvertTrack(IList<MidiEvent> trackEvents, int ticksPerQuarterNote)
        {
            // NoteOn / NoteOff を解析してモノフォニックなMMLイベントとして抽出
            var activeNotes = new List<NoteOnEvent>();
            var mmlEvents = new List<MmlSegment>();
            long currentTick = 0;

            foreach (var ev in trackEvents.OrderBy(e => e.AbsoluteTime))
            {
                // Handle rest before this event
                if (ev is NoteOnEvent evNoteOn && evNoteOn.Velocity > 0)
                {
                    if (ev.AbsoluteTime > currentTick && activeNotes.Count == 0)
                    {
                        // Record rest
                        int restTicks = (int)(ev.AbsoluteTime - currentTick);
                        var lengthStr = MmlLengthConverter.ConvertTicksToMmlLength(restTicks, ticksPerQuarterNote);
                        if (lengthStr != "0")
                        {
                            mmlEvents.Add(new MmlSegment { Text = $"r{lengthStr}" });
                        }
                        currentTick = ev.AbsoluteTime;
                    }

                    // For monophonic, if a note is already playing, we ignore new notes or cut the previous one.
                    // We'll just cut the previous note and start a new one if it's higher (simple precedence)
                    if (activeNotes.Count == 0 || activeNotes.Last().NoteNumber < evNoteOn.NoteNumber)
                    {
                        if (activeNotes.Count > 0)
                        {
                            // Finish the previous note early
                            var prevNote = activeNotes.Last();
                            int noteTicks = (int)(ev.AbsoluteTime - prevNote.AbsoluteTime);
                            AddNoteToMml(mmlEvents, prevNote, noteTicks, ticksPerQuarterNote);
                            activeNotes.Remove(prevNote);
                        }
                        activeNotes.Add(evNoteOn);
                    }
                    else
                    {
                        // Add but it doesn't take precedence
                        activeNotes.Add(evNoteOn);
                    }
                    
                    if (currentTick < ev.AbsoluteTime)
                    {
                        currentTick = ev.AbsoluteTime;
                    }
                }
                else if (ev is NAudio.Midi.NoteEvent evNoteOff && ev.CommandCode == MidiCommandCode.NoteOff)
                {
                    // Or NoteOn with Velocity 0
                    var match = activeNotes.FirstOrDefault(n => n.NoteNumber == evNoteOff.NoteNumber);
                    if (match != null)
                    {
                        if (match == activeNotes.Last())
                        {
                            // This was the currently sounding note
                            int noteTicks = (int)(ev.AbsoluteTime - match.AbsoluteTime);
                            AddNoteToMml(mmlEvents, match, noteTicks, ticksPerQuarterNote);
                            currentTick = ev.AbsoluteTime;
                        }
                        activeNotes.Remove(match);
                        
                        // If there is another held note, we could resume it, but for simple monophonic MML, returning to a rest is safer/easier
                        if (activeNotes.Count > 0 && match == activeNotes.Last())
                        {
                            // Shift current tick to where the newly top note supposedly started sounding...
                            // For simplicity, we just leave currentTick here so the next note handles rests correctly.
                        }
                    }
                }
                else if (ev is NoteOnEvent evNoteOffVel0 && evNoteOffVel0.Velocity == 0) // NoteOff as NoteOn Vel 0
                {
                    var match = activeNotes.FirstOrDefault(n => n.NoteNumber == evNoteOffVel0.NoteNumber);
                    if (match != null)
                    {
                        if (match == activeNotes.Last())
                        {
                            int noteTicks = (int)(ev.AbsoluteTime - match.AbsoluteTime);
                            AddNoteToMml(mmlEvents, match, noteTicks, ticksPerQuarterNote);
                            currentTick = ev.AbsoluteTime;
                        }
                        activeNotes.Remove(match);
                    }
                }
            }

            if (mmlEvents.Count == 0) return string.Empty;

            // Build string
            var sb = new StringBuilder();
            int currentOctave = 4; // Check first note instead?
            
            // Try to find first note to set initial octave
            var firstNoteSeg = mmlEvents.FirstOrDefault(x => x.NoteNumber.HasValue);
            if (firstNoteSeg != null)
            {
                int initOctave = (firstNoteSeg.NoteNumber.Value / 12) - 1;
                if (initOctave < 1) initOctave = 1;
                if (initOctave > 8) initOctave = 8;
                sb.Append($"o{initOctave} ");
                currentOctave = initOctave;
            }

            foreach (var seg in mmlEvents)
            {
                string textToAppend = seg.Text;

                if (seg.NoteNumber.HasValue)
                {
                    // Determine octave shifts
                    int targetOctave = (seg.NoteNumber.Value / 12) - 1;
                    if (targetOctave < 1) targetOctave = 1;
                    if (targetOctave > 8) targetOctave = 8;

                    string prefix = "";
                    if (targetOctave != currentOctave)
                    {
                        if (targetOctave == currentOctave + 1)
                        {
                            prefix = ">";
                        }
                        else if (targetOctave == currentOctave - 1)
                        {
                            prefix = "<";
                        }
                        else
                        {
                            prefix = $"o{targetOctave}";
                        }
                        currentOctave = targetOctave;
                    }
                    
                    textToAppend = prefix + textToAppend;
                }

                sb.Append(textToAppend);
                sb.Append(" ");
            }

            return sb.ToString().TrimEnd();
        }

        private void AddNoteToMml(List<MmlSegment> mmlEvents, NoteOnEvent noteOn, int noteTicks, int ticksPerQuarterNote)
        {
            if (noteTicks <= 0) return;

            string[] noteNames = { "c", "c+", "d", "d+", "e", "f", "f+", "g", "g+", "a", "a+", "b" };
            int noteIndex = noteOn.NoteNumber % 12;
            string noteName = noteNames[noteIndex];

            string lenStr = MmlLengthConverter.ConvertTicksToMmlLength(noteTicks, ticksPerQuarterNote);
            if (lenStr == "0") return;

            // Handle tied notes from converter (e.g. 4^16 -> c4^c16 OR c4^16 if parser supports it, MML parsers usually need tie on the note or just ^ length?
            // Actually our ast parser: 'c4^8' => note C with length 4 linked to length 8?
            // In typical MML, "c4^8" means c4 tied with 8. Or "c4^" means tie with next note.
            // Let's output "c4^8" - wait, standard is NoteName + lengths joined by ^?
            // Let's do `c4^8`.
            string mmlNote = noteName + lenStr;

            mmlEvents.Add(new MmlSegment { Text = mmlNote, NoteNumber = noteOn.NoteNumber });
        }

        private class MmlSegment
        {
            public string Text { get; set; } = "";
            public int? NoteNumber { get; set; }
        }
    }
}
