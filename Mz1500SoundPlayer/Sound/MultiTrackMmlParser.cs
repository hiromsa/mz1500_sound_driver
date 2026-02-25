using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Mz1500SoundPlayer.Sound;

// PPMCK/MCK互換のマルチトラックMMLパーサー
public class MultiTrackMmlParser
{
    public MmlData Parse(string mmlText)
    {
        var result = new MmlData();
        var tracks = result.Tracks;

        int absoluteIndex = 0;
        string currentTrackNames = "A";

        while (absoluteIndex < mmlText.Length)
        {
            int nextNewline = mmlText.IndexOf('\n', absoluteIndex);
            if (nextNewline == -1) nextNewline = mmlText.Length;

            int lineLength = nextNewline - absoluteIndex;
            string rawLine = mmlText.Substring(absoluteIndex, lineLength);
            int lineStartIndex = absoluteIndex;
            
            absoluteIndex = nextNewline + 1; // Advance loop to next line

            int commentIdx = rawLine.IndexOfAny(new[] { ';', '/' });
            string logicalLine = commentIdx >= 0 ? rawLine.Substring(0, commentIdx) : rawLine;
            
            if (string.IsNullOrWhiteSpace(logicalLine)) continue;

            string trimmed = logicalLine.Trim();

            // Check Envelope Definitions
            var envMatch = Regex.Match(trimmed, @"^@v(\d+)\s*=\s*\{(.*?)\}");
            if (envMatch.Success)
            {
                int envId = int.Parse(envMatch.Groups[1].Value);
                result.VolumeEnvelopes[envId] = ParseEnvelopeData(envMatch.Groups[2].Value);
                continue;
            }

            var pEnvMatch = Regex.Match(trimmed, @"^(?:@EP|@ep|EP|ep|@p)(\d+)\s*=\s*\{(.*?)\}");
            if (pEnvMatch.Success)
            {
                int envId = int.Parse(pEnvMatch.Groups[1].Value);
                result.PitchEnvelopes[envId] = ParseEnvelopeData(pEnvMatch.Groups[2].Value, allowNegative: true);
                continue;
            }

            // Check Track Definition (e.g. "ABC o4cde")
            var trackMatch = Regex.Match(logicalLine, @"^\s*([A-Za-z]+)\s+(.*)");
            string mmlData = logicalLine;
            int dataOffsetInLine = 0;

            if (trackMatch.Success)
            {
                currentTrackNames = trackMatch.Groups[1].Value.ToUpperInvariant();
                mmlData = trackMatch.Groups[2].Value;
                dataOffsetInLine = trackMatch.Groups[2].Index;
            }

            var commandsLine = ParseLine(mmlData, lineStartIndex + dataOffsetInLine);

            foreach (char tName in currentTrackNames)
            {
                string tKey = tName.ToString();
                if (!tracks.ContainsKey(tKey))
                {
                    tracks[tKey] = new TrackData { Name = tKey };
                }
                
                // Since MmlCommands are read-only state changes (or Note commands),
                // appending the same instance multiple times is fine.
                // If they mutate per track later, they should be cloned.
                tracks[tKey].Commands.AddRange(commandsLine);
            }
        }

        return result;
    }

    private EnvelopeData ParseEnvelopeData(string innerText, bool allowNegative = false)
    {
        string pattern = allowNegative ? @"-?\d+|\|" : @"\d+|\|";
        var matches = Regex.Matches(innerText, pattern);
        
        var envData = new EnvelopeData();
        foreach (Match m in matches)
        {
            string v = m.Value;
            if (v == "|")
                envData.LoopIndex = envData.Values.Count;
            else if (int.TryParse(v, out int val))
                envData.Values.Add(val);
        }
        
        if (envData.LoopIndex < 0 && envData.Values.Count > 0)
        {
            envData.LoopIndex = envData.Values.Count - 1;
        }
        return envData;
    }

    private List<MmlCommand> ParseLine(string data, int absoluteDataOffset)
    {
        var cmds = new List<MmlCommand>();
        int i = 0;
        
        while (i < data.Length)
        {
            if (char.IsWhiteSpace(data[i]))
            {
                i++;
                continue;
            }

            int cmdStartIdx = i;
            char originalC = data[i++];
            
            if ((originalC == 'l' || originalC == 'L') && i < data.Length && (data[i] == 'l' || data[i] == 'L'))
            {
                i++; // Handle typo LL -> L
            }

            MmlCommand createdCmd = null;

            if (originalC == 'D')
            {
                createdCmd = new DetuneCommand { Detune = ReadSignedInt(data, ref i, 0) };
            }
            else
            {
                char c = char.ToLowerInvariant(originalC);
                switch (c)
                {
                    case '@':
                        createdCmd = ParseAtCommand(data, ref i);
                        break;
                    case 'e':
                        if (i < data.Length && data[i] == 'p')
                        {
                            i++;
                            createdCmd = new PitchEnvelopeCommand { EnvelopeId = ReadInt(data, ref i, 0) };
                        }
                        else
                        {
                            createdCmd = ParseNoteOrRest(c, data, ref i);
                        }
                        break;
                    case 't':
                        createdCmd = new TempoCommand { Tempo = ReadInt(data, ref i, 120) };
                        break;
                    case 'o':
                        createdCmd = new OctaveCommand { Octave = ReadInt(data, ref i, 4) };
                        break;
                    case '>': createdCmd = new RelativeOctaveCommand { Offset = 1 }; break;
                    case '<': createdCmd = new RelativeOctaveCommand { Offset = -1 }; break;
                    case 'l':
                    case 'L': 
                        if (i < data.Length && char.IsDigit(data[i]))
                            createdCmd = new DefaultLengthCommand { Length = ReadInt(data, ref i, 4) };
                        else
                            createdCmd = new InfiniteLoopPointCommand();
                        break;
                    case 'v':
                        createdCmd = new VolumeCommand { Volume = ReadInt(data, ref i, 15) };
                        break;
                    case 'q':
                        createdCmd = new QuantizeCommand { Quantize = ReadInt(data, ref i, 8) };
                        break;
                    case '[':
                        createdCmd = new LoopBeginCommand(); break;
                    case ']':
                        createdCmd = new LoopEndCommand { Count = ReadInt(data, ref i, 2) }; break;
                    case '^':
                        createdCmd = ParseTie(data, ref i); break;
                    case 'a': case 'b': case 'c': case 'd': case 'f': case 'g': case 'r':
                        createdCmd = ParseNoteOrRest(c, data, ref i); break;
                }
            }

            if (createdCmd != null)
            {
                createdCmd.TextStartIndex = absoluteDataOffset + cmdStartIdx;
                createdCmd.TextLength = i - cmdStartIdx;
                cmds.Add(createdCmd);
            }
        }
        return cmds;
    }

    private MmlCommand ParseAtCommand(string data, ref int i)
    {
        if (i >= data.Length) return null;
        char c = data[i++];
        
        switch (c)
        {
            case 't': 
                int len = ReadInt(data, ref i, 4);
                if (i < data.Length && data[i] == ',') i++;
                int frames = ReadInt(data, ref i, 30);
                return new FrameTempoCommand { Length = len, FrameCount = frames };
            case 'v': 
                return new EnvelopeCommand { EnvelopeId = ReadInt(data, ref i, 0) };
            case 'p': 
                return new PitchEnvelopeCommand { EnvelopeId = ReadInt(data, ref i, 0) };
            case 'q': 
                return new FrameQuantizeCommand { Frames = ReadInt(data, ref i, 0) };
            case 'w': 
                if (i < data.Length && data[i] == 'n')
                {
                    i++;
                    return new NoiseWaveCommand { WaveType = ReadInt(data, ref i, 1) };
                }
                break;
            case 'i': 
                if (i < data.Length && data[i] == 'n')
                {
                    i++;
                    return new IntegrateNoiseCommand { IntegrateMode = ReadInt(data, ref i, 0) };
                }
                break;
            default:
                if (char.IsDigit(c))
                {
                    i--;
                    return new VoiceCommand { VoiceId = ReadInt(data, ref i, 0) };
                }
                break;
        }
        return null;
    }

    private MmlCommand ParseNoteOrRest(char noteChar, string data, ref int i)
    {
        int offset = 0;
        if (i < data.Length && (data[i] == '+' || data[i] == '#'))
        {
            offset = 1; i++;
        }
        else if (i < data.Length && data[i] == '-')
        {
            offset = -1; i++;
        }

        int length = 0; 
        if (i < data.Length && char.IsDigit(data[i]))
        {
            length = ReadInt(data, ref i, 0);
        }

        int dots = 0;
        while (i < data.Length && data[i] == '.')
        {
            dots++; i++;
        }

        return new NoteCommand { Note = noteChar, SemiToneOffset = offset, Length = length, Dots = dots };
    }

    private MmlCommand ParseTie(string data, ref int i)
    {
        int length = 0;
        if (i < data.Length && char.IsDigit(data[i]))
        {
            length = ReadInt(data, ref i, 0);
        }

        int dots = 0;
        while (i < data.Length && data[i] == '.')
        {
            dots++; i++;
        }

        return new TieCommand { Length = length, Dots = dots };
    }

    private int ReadInt(string str, ref int i, int defaultValue)
    {
        int start = i;
        while (i < str.Length && char.IsDigit(str[i])) i++;
        if (start < i && int.TryParse(str.Substring(start, i - start), out int val))
        {
            return val;
        }
        return defaultValue;
    }

    private int ReadSignedInt(string str, ref int i, int defaultValue)
    {
        int start = i;
        if (i < str.Length && (str[i] == '-' || str[i] == '+'))
        {
            i++;
        }
        bool hasDigits = false;
        while (i < str.Length && char.IsDigit(str[i])) 
        {
            hasDigits = true;
            i++;
        }
        if (hasDigits && int.TryParse(str.Substring(start, i - start), out int val))
        {
            return val;
        }
        i = start; // Rollback
        return defaultValue;
    }
}
