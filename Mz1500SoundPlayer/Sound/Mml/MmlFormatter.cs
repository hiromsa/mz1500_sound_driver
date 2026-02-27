using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Mz1500SoundPlayer.Sound.Mml
{
    public static class MmlFormatter
    {
        public static string Format(string inputMml, int timeSigNumerator, int timeSigDenominator, double upbeatDurationInWholeNotes, int barsPerLine, bool insertSpace)
        {
            if (string.IsNullOrWhiteSpace(inputMml)) return inputMml;

            var sb = new StringBuilder();
            var lines = inputMml.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            double barLengthInWholeNotes = (double)timeSigNumerator / timeSigDenominator;
            double wrapLength = barLengthInWholeNotes * barsPerLine;

            string lastTrackPrefix = null;
            double currentTime = 0;
            if (upbeatDurationInWholeNotes > 0)
            {
                currentTime = wrapLength - upbeatDurationInWholeNotes;
            }
            double defaultLength = 1.0 / 4.0;
            bool isFirstTokenOnLine = true;
            bool spaceBeforeNext = false;

            // Tokenizer regex. Order is important! 
            // Longest/most specific matches first.
            string pattern = 
                @"(@[a-zA-Z]+\d*(?:,\d+)?(?:x\d+)?)|" + // @commands like @v1, @q8, @EP0
                @"([eE][pP]\d+|@[eE][pP]\d+)|" + // EP0, @EP0
                @"([qQ]\d+|[kK]\-?\d+|D\-?\d+)|" + // q, K, D detune (uppercase D)
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
                    currentTime = upbeatDurationInWholeNotes > 0 ? wrapLength - upbeatDurationInWholeNotes : 0;
                    defaultLength = 1.0 / 4.0;
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
                    currentTime = upbeatDurationInWholeNotes > 0 ? wrapLength - upbeatDurationInWholeNotes : 0;
                    defaultLength = 1.0 / 4.0;
                    
                    sb.Append(trackPrefix);
                    isFirstTokenOnLine = true;
                    spaceBeforeNext = false;
                }
                else if (trackPrefix == "" && lastTrackPrefix == null)
                {
                    // No prefix, no previous state
                    lastTrackPrefix = "";
                    currentTime = upbeatDurationInWholeNotes > 0 ? wrapLength - upbeatDurationInWholeNotes : 0;
                    defaultLength = 1.0 / 4.0;
                    isFirstTokenOnLine = true;
                    spaceBeforeNext = false;
                }

                var matches = regex.Matches(trimmed);
                foreach (Match m in matches)
                {
                    var token = m.Value;
                    if (string.IsNullOrWhiteSpace(token)) continue;

                    if (currentTime >= wrapLength - 0.0001 && wrapLength > 0)
                    {
                        currentTime -= wrapLength;
                        sb.AppendLine();
                        sb.Append(lastTrackPrefix);
                        isFirstTokenOnLine = true;
                        spaceBeforeNext = false;
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

                    double durationToAdd = 0.0;
                    char firstChar = char.ToLower(token[0]);
                    
                    if ((firstChar >= 'a' && firstChar <= 'g') || firstChar == 'r' || firstChar == '^')
                    {
                        durationToAdd = ParseDuration(token, defaultLength);
                    }
                    else if (firstChar == 'l' && token != "L")
                    {
                        defaultLength = ParseDuration(token, defaultLength);
                    }

                    currentTime += durationToAdd;
                }
            }

            return sb.ToString().TrimEnd('\t', ' ', '\r', '\n');
        }

        private static double ParseDuration(string token, double defaultLength)
        {
            var match = Regex.Match(token, @"\d+");
            double val = defaultLength;
            if (match.Success)
            {
                int len = int.Parse(match.Value);
                if (len > 0) val = 1.0 / len;
            }
            else if (token.StartsWith("^") && token.Length == 1)
            {
                val = defaultLength;
            }
            
            int dots = token.Length - token.Replace(".", "").Length;
            double total = val;
            double currentAddition = val;
            for (int i = 0; i < dots; i++)
            {
                currentAddition /= 2;
                total += currentAddition;
            }
            return total;
        }
    }
}
