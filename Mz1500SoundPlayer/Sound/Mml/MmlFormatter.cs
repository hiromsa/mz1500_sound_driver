using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Mz1500SoundPlayer.Sound.Mml
{
    public static class MmlFormatter
    {
        // 1 quarter note (l4) = 48 Ticks
        // 1 whole note (l1) = 192 Ticks
        private const int TicksPerQuarterNote = 48;
        private const int TicksPerWholeNote = TicksPerQuarterNote * 4;

        public static string Format(string inputMml, int timeSigNumerator, int timeSigDenominator, double upbeatDurationInWholeNotes, int barsPerLine, bool insertSpace)
        {
            if (string.IsNullOrWhiteSpace(inputMml)) return inputMml;

            var sb = new StringBuilder();
            var lines = inputMml.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            int barLengthInTicks = (TicksPerWholeNote * timeSigNumerator) / timeSigDenominator;
            int wrapLengthInTicks = barLengthInTicks * barsPerLine;

            string lastTrackPrefix = null;
            int currentTime = 0;
            int upbeatTicks = (int)Math.Round(upbeatDurationInWholeNotes * TicksPerWholeNote);

            if (upbeatTicks > 0)
            {
                currentTime = wrapLengthInTicks - upbeatTicks;
            }
            
            int defaultLengthTicks = TicksPerQuarterNote;
            bool isFirstTokenOnLine = true;
            bool spaceBeforeNext = false;

            // Tokenizer regex. Order is important! 
            // Longest/most specific matches first.
            string pattern = 
                @"(@[a-zA-Z]+\d*(?:,\d+)?(?:x\d+)?)|" + // @commands like @v1, @q8, @EP0
                @"([eE][pP]\d+|@[eE][pP]\d+)|" + // EP0, @EP0
                @"([qQ]\d+|[kK]\-?\d+|[dD]\-?\d+)|" + // q, K, D detune (uppercase D)
                @"([vV]\d+)|" + // v
                @"([tT]\d+)|" + // t
                @"([oO]\d+|[<>])|" + // o, <, >
                @"([lL]\d+\.*)|" + // l (must have digits)
                @"([rR]\d*\.*)|" + // r
                @"([a-gA-G][#\+\-]?\d*\.*)|" + // notes
                @"(\^\d*\.*)|" + // tie
                @"(\[|\]\d*)|" + // loops
                @"(L)|" + // loop marker
                @"(\{|\})|" + // tuplet
                @"([^a-zA-Z0-9\s]+)|" + // other symbols
                @"([a-zA-Z0-9]+)"; // identifiers
            var regex = new Regex(pattern);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();
                
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("/")) 
                {
                    if (lastTrackPrefix != null && !isFirstTokenOnLine)
                    {
                        sb.AppendLine();
                    }
                    sb.AppendLine(line);
                    
                    lastTrackPrefix = null;
                    currentTime = upbeatTicks > 0 ? wrapLengthInTicks - upbeatTicks : 0;
                    defaultLengthTicks = TicksPerQuarterNote;
                    isFirstTokenOnLine = true;
                    spaceBeforeNext = false;
                    continue;
                }

                // Extract track prefix
                string trackPrefix = "";
                var matchTrack = Regex.Match(trimmed, @"^([A-HP0-9,a-h]+)\s+"); 
                if (matchTrack.Success)
                {
                    trackPrefix = matchTrack.Groups[1].Value + " ";
                    trimmed = trimmed.Substring(matchTrack.Length);
                }
                else
                {
                    var matchTrack2 = Regex.Match(trimmed, @"^([A-HP])(?=([^a-zA-Z]|$))");
                    if (matchTrack2.Success)
                    {
                        trackPrefix = matchTrack2.Value + " ";
                        trimmed = trimmed.Substring(matchTrack2.Length).TrimStart();
                    }
                }
                
                // Track transition logic
                if (trackPrefix != "" && trackPrefix != lastTrackPrefix)
                {
                    if (lastTrackPrefix != null && !isFirstTokenOnLine)
                    {
                        sb.AppendLine();
                    }
                    
                    lastTrackPrefix = trackPrefix;
                    currentTime = upbeatTicks > 0 ? wrapLengthInTicks - upbeatTicks : 0;
                    defaultLengthTicks = TicksPerQuarterNote;
                    
                    sb.Append(trackPrefix);
                    isFirstTokenOnLine = true;
                    spaceBeforeNext = false;
                }
                else if (trackPrefix == "" && lastTrackPrefix == null)
                {
                    // No prefix, no previous state
                    lastTrackPrefix = "";
                    currentTime = upbeatTicks > 0 ? wrapLengthInTicks - upbeatTicks : 0;
                    defaultLengthTicks = TicksPerQuarterNote;
                    isFirstTokenOnLine = true;
                    spaceBeforeNext = false;
                }

                var matches = regex.Matches(trimmed);
                foreach (Match m in matches)
                {
                    var token = m.Value;
                    if (string.IsNullOrWhiteSpace(token)) continue;

                    int durationToAdd = 0;
                    char firstChar = char.ToLower(token[0]);
                    
                    if ((firstChar >= 'a' && firstChar <= 'g') || firstChar == 'r' || firstChar == '^')
                    {
                        durationToAdd = ParseDurationTicks(token, defaultLengthTicks);
                    }
                    else if (firstChar == 'l' && token != "L")
                    {
                        defaultLengthTicks = ParseDurationTicks(token, defaultLengthTicks);
                    }

                    // If this token pushes us PAST the wrap boundary, we wrap FIRST
                    if (currentTime + durationToAdd > wrapLengthInTicks && wrapLengthInTicks > 0)
                    {
                        sb.AppendLine();
                        sb.Append(lastTrackPrefix);
                        isFirstTokenOnLine = true;
                        spaceBeforeNext = false;
                        currentTime = 0;
                    }

                    if (insertSpace && !isFirstTokenOnLine)
                    {
                        if (!token.StartsWith("^") && !spaceBeforeNext)
                        {
                            sb.Append(" ");
                        }
                    }
                    
                    sb.Append(token);
                    isFirstTokenOnLine = false;
                    spaceBeforeNext = token.StartsWith("^"); 

                    currentTime += durationToAdd;

                    // If this token perfectly hits the wrap boundary, we wrap AFTER it
                    if (currentTime >= wrapLengthInTicks && wrapLengthInTicks > 0)
                    {
                        if (currentTime == wrapLengthInTicks)
                        {
                            sb.AppendLine();
                            sb.Append(lastTrackPrefix);
                            isFirstTokenOnLine = true;
                            spaceBeforeNext = false;
                            currentTime = 0;
                        }
                    }
                }
            }

            return sb.ToString().TrimEnd('\t', ' ', '\r', '\n');
        }

        private static int ParseDurationTicks(string token, int defaultLengthTicks)
        {
            var match = Regex.Match(token, @"\d+");
            int val = defaultLengthTicks;
            if (match.Success)
            {
                int len = int.Parse(match.Value);
                if (len > 0) 
                {
                    val = TicksPerWholeNote / len;
                }
            }
            else if (token.StartsWith("^") && token.Length == 1)
            {
                val = defaultLengthTicks;
            }
            
            int dots = token.Length - token.Replace(".", "").Length;
            int total = val;
            int currentAddition = val;
            for (int i = 0; i < dots; i++)
            {
                currentAddition /= 2;
                total += currentAddition;
            }
            return total;
        }
    }
}
