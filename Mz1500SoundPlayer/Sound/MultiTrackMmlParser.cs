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

            var pEnvMatch = Regex.Match(trimmed, @"^(?:@EP|@ep|@p)(\d+)\s*=\s*\{(.*?)\}");
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

            var commandsLine = new List<MmlCommand>();
            ParseLineChunk(mmlData, lineStartIndex + dataOffsetInLine, result, commandsLine);

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
        string pattern = allowNegative ? @"-?\d+(?:\s*[xX]\s*\d+)?|\||>" : @"\d+(?:\s*[xX]\s*\d+)?|\||>";
        var matches = Regex.Matches(innerText, pattern);
        
        var envData = new EnvelopeData();
        bool isReleasePhase = false;

        foreach (Match m in matches)
        {
            string v = m.Value.Trim();
            if (v == "|")
            {
                envData.LoopIndex = envData.Values.Count;
            }
            else if (v == ">")
            {
                isReleasePhase = true;
            }
            else
            {
                int repeatCount = 1;
                string valStr = v;
                int xIdx = v.IndexOf('x', StringComparison.OrdinalIgnoreCase);
                
                if (xIdx > 0)
                {
                    valStr = v.Substring(0, xIdx).Trim();
                    string countStr = v.Substring(xIdx + 1).Trim();
                    bool countParseError;
                    if (int.TryParse(countStr, out int parsedCount))
                    {
                        repeatCount = Math.Max(1, parsedCount);
                    }
                }

                bool valParseError;
                if (int.TryParse(valStr, out int val))
                {
                    for (int n = 0; n < repeatCount; n++)
                    {
                        if (isReleasePhase)
                        {
                            envData.ReleaseValues.Add(val);
                        }
                        else
                        {
                            envData.Values.Add(val);
                        }
                    }
                }
            }
        }
        
        if (envData.LoopIndex < 0 && envData.Values.Count > 0)
        {
            envData.LoopIndex = envData.Values.Count - 1;
        }
        return envData;
    }

    private void ParseLineChunk(string data, int absoluteDataOffset, MmlData mmlData, List<MmlCommand> cmds)
    {
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
            
            // Skip comments internally here? Usually comments are stripped before ParseLine, but just in case:
            if (originalC == ';' || originalC == '/')
            {
                // Line comment
                break;
            }
            
            if ((originalC == 'l' || originalC == 'L') && i < data.Length && (data[i] == 'l' || data[i] == 'L'))
            {
                i++; // Handle typo LL -> L
            }

            MmlCommand createdCmd = null;
            bool parseError = false;

            if (originalC == 'D')
            {
                createdCmd = new DetuneCommand { Detune = ReadSignedInt(data, ref i, 0, out parseError) };
                if (parseError) mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, i - cmdStartIdx, $"無効なデチューン値です。"));
            }
            else
            {
                char c = char.ToLowerInvariant(originalC);
                switch (c)
                {
                    case 'k':
                        if (originalC == 'K')
                        {
                            createdCmd = new TransposeCommand { Transpose = ReadSignedInt(data, ref i, 0, out parseError) };
                            if (parseError) mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, i - cmdStartIdx, $"無効なトランスポーズ値です。"));
                        }
                        else
                        {
                            mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, 1, $"大文字の 'K' を使用してください。小文字の 'k' は使用できません。"));
                        }
                        break;
                    case '@':
                        createdCmd = ParseAtCommand(data, ref i, mmlData, absoluteDataOffset);
                        break;
                    case 'e':
                        if (i < data.Length && char.ToLowerInvariant(data[i]) == 'p')
                        {
                            i++;
                            createdCmd = new PitchEnvelopeCommand { EnvelopeId = ReadInt(data, ref i, 0, out parseError) };
                            if (parseError) mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, i - cmdStartIdx, $"無効なピッチエンベロープIDです。"));
                        }
                        else if (originalC == 'e')
                        {
                            createdCmd = ParseNoteOrRest(c, data, ref i, mmlData, absoluteDataOffset);
                        }
                        break;
                    case 't':
                        createdCmd = new TempoCommand { Tempo = ReadInt(data, ref i, 120, out parseError) };
                        if (parseError) mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, i - cmdStartIdx, $"無効なテンポ値です。"));
                        break;
                    case 'o':
                        createdCmd = new OctaveCommand { Octave = ReadInt(data, ref i, 4, out parseError) };
                        if (parseError) mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, i - cmdStartIdx, $"無効なオクターブ値です。"));
                        break;
                    case '>': createdCmd = new RelativeOctaveCommand { Offset = 1 }; break;
                    case '<': createdCmd = new RelativeOctaveCommand { Offset = -1 }; break;
                    case 'l':
                    case 'L': 
                        if (i < data.Length && char.IsDigit(data[i]))
                        {
                            var dCmd = new DefaultLengthCommand { Length = ReadInt(data, ref i, 4, out parseError) };
                            if (parseError) mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, i - cmdStartIdx, $"無効な音長値です。"));
                            while (i < data.Length && data[i] == '.')
                            {
                                dCmd.Dots++;
                                i++;
                            }
                            createdCmd = dCmd;
                        }
                        else
                            createdCmd = new InfiniteLoopPointCommand();
                        break;
                    case 'v':
                        createdCmd = new VolumeCommand { Volume = ReadInt(data, ref i, 15, out parseError) };
                        if (parseError) mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, i - cmdStartIdx, $"無効なボリューム値です。"));
                        break;
                    case 'q':
                        createdCmd = new QuantizeCommand { Quantize = ReadInt(data, ref i, 8, out parseError) };
                        if (parseError) mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, i - cmdStartIdx, $"無効なクオンタイズ値です。"));
                        break;
                    case '{':
                        int blockEnd = data.IndexOf('}', i);
                        if (blockEnd == -1)
                        {
                            mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, data.Length - cmdStartIdx, $"連符の閉じカッコ '}}' が見つかりません。"));
                            i = data.Length; // Skip to end
                        }
                        else
                        {
                            string innerMml = data.Substring(i, blockEnd - i);
                            i = blockEnd + 1; // Move past '}'
                            
                            var tupletCmd = new TupletCommand();
                            int innerOffset = absoluteDataOffset + cmdStartIdx + 1;
                            
                            var tempInnerCmds = new List<MmlCommand>();
                            ParseLineChunk(innerMml, innerOffset, mmlData, tempInnerCmds);
                            tupletCmd.InnerCommands = tempInnerCmds;

                            tupletCmd.Length = ReadInt(data, ref i, 0, out parseError); // Default to 0 (uses track default) if none specified
                            if (parseError) mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, i - cmdStartIdx, $"無効な連符音長です。"));
                            
                            tupletCmd.Dots = 0;
                            while (i < data.Length && data[i] == '.')
                            {
                                tupletCmd.Dots++;
                                i++;
                            }

                            createdCmd = tupletCmd;
                        }
                        break;
                    case '[':
                        createdCmd = new LoopBeginCommand(); break;
                    case ']':
                        createdCmd = new LoopEndCommand { Count = ReadInt(data, ref i, 2, out parseError) };
                        if (parseError) mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, i - cmdStartIdx, $"無効なループ回数です。"));
                        break;
                    case '^':
                        createdCmd = ParseTie(data, ref i, mmlData, absoluteDataOffset); break;
                    case 'a': case 'b': case 'c': case 'd': case 'f': case 'g': case 'r':
                        if (originalC == c)
                        {
                            createdCmd = ParseNoteOrRest(c, data, ref i, mmlData, absoluteDataOffset);
                        }
                        else
                        {
                            mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, 1, $"大文字の '{originalC}' は使用できません。音符は小文字を使用してください。"));
                        }
                        break;
                    default:
                        mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, 1, $"未知の文字またはコマンド '{originalC}' です。"));
                        break;
                }
            }

            if (createdCmd != null)
            {
                createdCmd.TextStartIndex = absoluteDataOffset + cmdStartIdx;
                createdCmd.TextLength = i - cmdStartIdx;
                cmds.Add(createdCmd);
            }
            else if (!parseError && originalC != ';' && originalC != '/' && (mmlData.Errors.Count == 0 || mmlData.Errors[^1].TextStartIndex != absoluteDataOffset + cmdStartIdx))
            {
                // In case a command function returns null because of syntax error, log an error if not already handled
                // This catch-all is for cases where a specific error wasn't added by the parsing function itself.
                mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, 1, $"未知の文字またはコマンド '{originalC}' です。"));
            }
        }
    }

    private MmlCommand ParseAtCommand(string data, ref int i, MmlData mmlData, int absoluteDataOffset)
    {
        int cmdStartIdx = i - 1;
        if (i >= data.Length) 
        {
            mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, 1, "'@' の後にコマンドがありません。"));
            return null;
        }
        char c = char.ToLowerInvariant(data[i++]);
        bool parseError = false;
        
        switch (c)
        {
            case 't': 
                int len = ReadInt(data, ref i, 4, out parseError);
                if (parseError) mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, i - cmdStartIdx, $"無効な音長値です。"));
                if (i < data.Length && data[i] == ',') i++;
                int frames = ReadInt(data, ref i, 30, out parseError);
                if (parseError) mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, i - cmdStartIdx, $"無効なフレーム数です。"));
                return new FrameTempoCommand { Length = len, FrameCount = frames };
            case 'v': 
                return new EnvelopeCommand { EnvelopeId = ReadInt(data, ref i, 0, out parseError) };
            case 'p': 
                return new PitchEnvelopeCommand { EnvelopeId = ReadInt(data, ref i, 0, out parseError) };
            // 'p' or 'e' both handled above if they map to envelope/pitch, 
            // but if there was a duplicate case 'e': here, it's removed.
            case 'q': 
                return new FrameQuantizeCommand { Frames = ReadInt(data, ref i, 0, out parseError) };
            case 'w': 
                if (i < data.Length && char.ToLowerInvariant(data[i]) == 'n')
                {
                    i++;
                    return new NoiseWaveCommand { WaveType = ReadInt(data, ref i, 1, out parseError) };
                }
                else
                {
                    mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, 2, $"未知の @ コマンド '@{c}' です。'@wn' が期待されます。"));
                    return null;
                }
            case 'i':
                if (i < data.Length && char.ToLowerInvariant(data[i]) == 'n')
                {
                    i++;
                    return new IntegrateNoiseCommand { IntegrateMode = ReadInt(data, ref i, 0, out parseError) };
                }
                else
                {
                    mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, 2, $"未知の @ コマンド '@{c}' です。'@in' が期待されます。"));
                    return null;
                }
            default:
                if (char.IsDigit(c))
                {
                    i--;
                    return new VoiceCommand { VoiceId = ReadInt(data, ref i, 0, out parseError) };
                }
                break;
        }
        return null;
    }

    private MmlCommand ParseNoteOrRest(char note, string data, ref int i, MmlData mmlData, int absoluteDataOffset)
    {
        int cmdStartIdx = i - 1;
        int semiToneOffset = 0;
        bool parseError = false;

        // '+', '#', '-' での半音調整
        while (i < data.Length)
        {
            if (data[i] == '+' || data[i] == '#') { semiToneOffset++; i++; }
            else if (data[i] == '-') { semiToneOffset--; i++; }
            else break;
        }

        int len = 0;
        if (i < data.Length && char.IsDigit(data[i]))
        {
            len = ReadInt(data, ref i, 0, out parseError);
            if (parseError) mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, i - cmdStartIdx, $"無効な音符の長さです。"));
        }

        int dots = 0;
        while (i < data.Length && data[i] == '.')
        {
            dots++; i++;
        }

        return new NoteCommand
        {
            Note = note,
            SemiToneOffset = semiToneOffset,
            Length = len,
            Dots = dots
        };
    }

    private MmlCommand ParseTie(string data, ref int i, MmlData mmlData, int absoluteDataOffset)
    {
        int cmdStartIdx = i - 1;
        int len = 0;
        bool parseError = false;

        if (i < data.Length && char.IsDigit(data[i]))
        {
            len = ReadInt(data, ref i, 0, out parseError);
            if (parseError) mmlData.Errors.Add(new MmlError(absoluteDataOffset + cmdStartIdx, i - cmdStartIdx, $"無効なタイの長さです。"));
        }

        int dots = 0;
        while (i < data.Length && data[i] == '.')
        {
            dots++; i++;
        }

        return new TieCommand { Length = len, Dots = dots };
    }

    private int ReadInt(string data, ref int i, int defaultValue, out bool hasError)
    {
        hasError = false;
        int start = i;
        while (i < data.Length && char.IsDigit(data[i]))
        {
            i++;
        }
        if (start == i) return defaultValue;
        if (int.TryParse(data.Substring(start, i - start), out int result))
        {
            return result;
        }
        hasError = true;
        return defaultValue;
    }

    private int ReadSignedInt(string data, ref int i, int defaultValue, out bool hasError)
    {
        hasError = false;
        int start = i;
        if (i < data.Length && (data[i] == '+' || data[i] == '-'))
        {
            i++;
        }
        while (i < data.Length && char.IsDigit(data[i]))
        {
            i++;
        }
        if (start == i || (i - start == 1 && (data[start] == '+' || data[start] == '-'))) 
        {
            hasError = true;
            return defaultValue;
        }

        if (int.TryParse(data.Substring(start, i - start), out int result))
        {
            return result;
        }
        hasError = true;
        return defaultValue;
    }
}
