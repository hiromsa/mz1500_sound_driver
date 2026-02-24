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
        
        // ; または / 以降の行末までをコメントとして削除
        var lines = mmlText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(line => Regex.Replace(line, @"[;/].*$", ""))
                           .Where(line => !string.IsNullOrWhiteSpace(line))
                           .ToList();

        string currentTrackNames = "A"; // デフォルトの操作対象トラック

        foreach (var originalLine in lines)
        {
            var line = originalLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // ボリュームエンベロープマクロの定義のパース (例: @v0 = {15,14,13} or @v0 = {15, 14, |, 13})
            var envMatch = Regex.Match(line, @"^@v(\d+)\s*=\s*\{(.*?)\}");
            if (envMatch.Success)
            {
                int envId = int.Parse(envMatch.Groups[1].Value);
                var envData = ParseEnvelopeData(envMatch.Groups[2].Value);
                result.VolumeEnvelopes[envId] = envData;
                continue;
            }

            // ピッチエンベロープマクロの定義のパース (例: @EP1 = {0, 10, 20} または @p1 = {0, 10, 20})
            var pEnvMatch = Regex.Match(line, @"^(?:@EP|@ep|EP|ep|@p)(\d+)\s*=\s*\{(.*?)\}");
            if (pEnvMatch.Success)
            {
                int envId = int.Parse(pEnvMatch.Groups[1].Value);
                // Pitch envelopes can have negative values, so Regex must include '-' sign
                var envData = ParseEnvelopeData(pEnvMatch.Groups[2].Value, allowNegative: true);
                result.PitchEnvelopes[envId] = envData;
                continue;
            }

            // 行頭がアルファベットの連続で始まり、その後に空白が続く場合はトラックヘッダとみなす (ex: "ABC @t1,86")
            var match = Regex.Match(line, @"^([A-Za-z]+)\s+(.*)");
            string mmlData = line;

            if (match.Success)
            {
                currentTrackNames = match.Groups[1].Value.ToUpperInvariant();
                mmlData = match.Groups[2].Value;
            }

            // スペースはパースの邪魔なので消すが、すでにトークン化する際に見るなら残してもよい
            // ここでは1文字ずつ舐める単純な字句解析を行う
            mmlData = mmlData.ToLowerInvariant().Replace(" ", "").Replace("\t", "");
            mmlData = mmlData.Replace("ll", "l"); // LLのTypoをLとして扱う対応
            
            var commandsLine = ParseLine(mmlData);

            // 宣言された全トラックへ追記する
            foreach (char tName in currentTrackNames)
            {
                string tKey = tName.ToString();
                if (!tracks.ContainsKey(tKey))
                {
                    tracks[tKey] = new TrackData { Name = tKey };
                }
                tracks[tKey].Commands.AddRange(commandsLine);
            }
        }

        return result;
    }

    private EnvelopeData ParseEnvelopeData(string innerText, bool allowNegative = false)
    {
        // '-' を許容するかどうかで正規表現を切り替える
        string pattern = allowNegative ? @"-?\d+|\|" : @"\d+|\|";
        var matches = Regex.Matches(innerText, pattern);
        
        var envData = new EnvelopeData();
        foreach (Match m in matches)
        {
            string v = m.Value;
            if (v == "|")
            {
                envData.LoopIndex = envData.Values.Count;
            }
            else if (int.TryParse(v, out int val))
            {
                envData.Values.Add(val);
            }
        }
        
        // If loop index is not specified, default to the last valid value.
        if (envData.LoopIndex < 0 && envData.Values.Count > 0)
        {
            envData.LoopIndex = envData.Values.Count - 1;
        }
        return envData;
    }

    private List<MmlCommand> ParseLine(string data)
    {
        var cmds = new List<MmlCommand>();
        int i = 0;
        
        while (i < data.Length)
        {
            char c = data[i++];
            switch (c)
            {
                case '@':
                    ParseAtCommand(data, ref i, cmds);
                    break;
                case 'e':
                    if (i < data.Length && data[i] == 'p')
                    {
                        i++;
                        cmds.Add(new PitchEnvelopeCommand { EnvelopeId = ReadInt(data, ref i, 0) });
                    }
                    else
                    {
                        ParseNoteOrRest(c, data, ref i, cmds);
                    }
                    break;
                case 't':
                    cmds.Add(new TempoCommand { Tempo = ReadInt(data, ref i, 120) });
                    break;
                case 'o':
                    cmds.Add(new OctaveCommand { Octave = ReadInt(data, ref i, 4) });
                    break;
                case '>': cmds.Add(new RelativeOctaveCommand { Offset = 1 }); break;
                case '<': cmds.Add(new RelativeOctaveCommand { Offset = -1 }); break;
                case 'l':
                case 'L':
                    if (i < data.Length && char.IsDigit(data[i]))
                    {
                        cmds.Add(new DefaultLengthCommand { Length = ReadInt(data, ref i, 4) });
                    }
                    else
                    {
                        cmds.Add(new InfiniteLoopPointCommand());
                    }
                    break;
                case 'v':
                    cmds.Add(new VolumeCommand { Volume = ReadInt(data, ref i, 15) });
                    break;
                case 'q':
                    cmds.Add(new QuantizeCommand { Quantize = ReadInt(data, ref i, 8) });
                    break;
                case '[':
                    cmds.Add(new LoopBeginCommand());
                    break;
                case ']':
                    cmds.Add(new LoopEndCommand { Count = ReadInt(data, ref i, 2) });
                    break;
                case '^':
                    ParseTie(data, ref i, cmds);
                    break;
                case 'a': case 'b': case 'c': case 'd': case 'f': case 'g':
                case 'r':
                    ParseNoteOrRest(c, data, ref i, cmds);
                    break;
            }
        }
        return cmds;
    }

    private void ParseAtCommand(string data, ref int i, List<MmlCommand> cmds)
    {
        if (i >= data.Length) return;
        char c = data[i++];
        
        switch (c)
        {
            case 't': // @t<len>,<num> : 指定音符が<num>フレームになるテンポ
                int len = ReadInt(data, ref i, 4);
                if (i < data.Length && data[i] == ',') i++;
                int frames = ReadInt(data, ref i, 30);
                cmds.Add(new FrameTempoCommand { Length = len, FrameCount = frames });
                break;
            case 'v': // @v<num> : ソフトウェアエンベロープ(現状はVolume扱いか無視)
                cmds.Add(new EnvelopeCommand { EnvelopeId = ReadInt(data, ref i, 0) });
                break;
            case 'p': // @p<num> : ピッチエンベロープ(EPと同等)
                cmds.Add(new PitchEnvelopeCommand { EnvelopeId = ReadInt(data, ref i, 0) });
                break;
            case 'q': // @q<num> : フレーム単位のクオンタイズ
                cmds.Add(new FrameQuantizeCommand { Frames = ReadInt(data, ref i, 0) });
                break;
            case 'w': // @wn<num> : ノイズジェネレータ波形設定
                if (i < data.Length && data[i] == 'n')
                {
                    i++;
                    cmds.Add(new NoiseWaveCommand { WaveType = ReadInt(data, ref i, 1) });
                }
                break;
            case 'i': // @in<num> : ノイズジェネレータ連携設定(0=Off, 1=Periodic, 2=White)
                if (i < data.Length && data[i] == 'n')
                {
                    i++;
                    cmds.Add(new IntegrateNoiseCommand { IntegrateMode = ReadInt(data, ref i, 0) });
                }
                break;
            default:
                if (char.IsDigit(c))
                {
                    // @1 などの音色指定。iを戻して数値を読む
                    i--;
                    cmds.Add(new VoiceCommand { VoiceId = ReadInt(data, ref i, 0) });
                }
                break;
        }
    }

    private void ParseNoteOrRest(char noteChar, string data, ref int i, List<MmlCommand> cmds)
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

        int length = 0; // 0 == use default length
        if (i < data.Length && char.IsDigit(data[i]))
        {
            length = ReadInt(data, ref i, 0);
        }

        int dots = 0;
        while (i < data.Length && data[i] == '.')
        {
            dots++; i++;
        }

        cmds.Add(new NoteCommand 
        { 
            Note = noteChar, 
            SemiToneOffset = offset, 
            Length = length, 
            Dots = dots 
        });
    }

    private void ParseTie(string data, ref int i, List<MmlCommand> cmds)
    {
        int length = 0; // 0 == use default length
        if (i < data.Length && char.IsDigit(data[i]))
        {
            length = ReadInt(data, ref i, 0);
        }

        int dots = 0;
        while (i < data.Length && data[i] == '.')
        {
            dots++; i++;
        }

        cmds.Add(new TieCommand { Length = length, Dots = dots });
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
}
