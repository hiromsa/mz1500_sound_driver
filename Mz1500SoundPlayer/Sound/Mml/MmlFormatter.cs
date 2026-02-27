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

        public static string Format(string text, int timeSignatureNumerator, int timeSignatureDenominator, double upbeatDurationInWholeNotes, int barsPerLine, bool insertSpace, bool insertMeasureSpace = true)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            
            // Ticks defined as: 192 = Whole note (l1)
            int TicksPerWholeNote = 192;
            int TicksPerQuarterNote = 48; // 192 / 4

            var sb = new StringBuilder();
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            int barLengthInTicks = (TicksPerWholeNote * timeSignatureNumerator) / timeSignatureDenominator;
            int wrapLengthInTicks = barLengthInTicks * barsPerLine;
            bool pendingWrap = false;
            
            // For inserting measure spaces, we need to track cumulative ticks since the last measure boundary.
            int cumulativeMeasureTicks = 0;

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
                    sb.Append(lastTrackPrefix);
                    isFirstTokenOnLine = true;
                    spaceBeforeNext = false;
                    pendingWrap = false;
                    currentTime = upbeatTicks > 0 ? wrapLengthInTicks - upbeatTicks : 0;
                    cumulativeMeasureTicks = currentTime % barLengthInTicks;
                    defaultLengthTicks = TicksPerQuarterNote;
                }
                else if (string.IsNullOrWhiteSpace(lastTrackPrefix)) // This condition covers both trackPrefix == "" and lastTrackPrefix == null
                {
                    lastTrackPrefix = trackPrefix; // If trackPrefix is also "", it becomes ""
                    sb.Append(lastTrackPrefix);
                    isFirstTokenOnLine = true;
                    spaceBeforeNext = false;
                    pendingWrap = false;
                    currentTime = upbeatTicks > 0 ? wrapLengthInTicks - upbeatTicks : 0;
                    cumulativeMeasureTicks = currentTime % barLengthInTicks;
                    defaultLengthTicks = TicksPerQuarterNote;
                }

                var matches = regex.Matches(trimmed);
                foreach (Match m in matches)
                {
                    var token = m.Value;
                    if (string.IsNullOrWhiteSpace(token)) continue;

                    int durationToAdd = 0;
                    char firstChar = char.ToLower(token[0]);
                    
                    bool isNote = (firstChar >= 'a' && firstChar <= 'g') || firstChar == 'r';
                    bool isTie = firstChar == '^';
                    
                    if (isNote)
                    {
                        string lowerToken = token.ToLowerInvariant();
                        if (lowerToken.StartsWith("ep") || lowerToken.StartsWith("@ep"))
                        {
                            isNote = false;
                        }
                    }

                    if (isNote || isTie)
                    {
                        durationToAdd = ParseDurationTicks(token, defaultLengthTicks);
                    }
                    else if (firstChar == 'l' && token.ToUpperInvariant() != "L")
                    {
                        defaultLengthTicks = ParseDurationTicks(token, defaultLengthTicks);
                    }

                    if (pendingWrap && !isTie)
                    {
                        sb.AppendLine();
                        sb.Append(lastTrackPrefix);
                        isFirstTokenOnLine = true;
                        spaceBeforeNext = false;
                        pendingWrap = false;
                        cumulativeMeasureTicks = 0;
                    }

                    // Insert measure space if a measure boundary was hit by the PREVIOUS note
                    if (insertMeasureSpace && !isFirstTokenOnLine && cumulativeMeasureTicks == 0 && barLengthInTicks > 0)
                    {
                        if (!isTie) 
                        {
                            sb.Append(" ");
                        }
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

                    if (durationToAdd > 0)
                    {
                        cumulativeMeasureTicks = (cumulativeMeasureTicks + durationToAdd) % barLengthInTicks;

                        if (currentTime < wrapLengthInTicks && currentTime + durationToAdd >= wrapLengthInTicks)
                        {
                            currentTime = (currentTime + durationToAdd) - wrapLengthInTicks;
                            pendingWrap = true;
                        }
                        else
                        {
                            currentTime += durationToAdd;
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
