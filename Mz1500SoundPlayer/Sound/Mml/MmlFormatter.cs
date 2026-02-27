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
            double lineLengthInWholeNotes = barLengthInWholeNotes * barsPerLine;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";")) 
                {
                    sb.AppendLine(line); // Preserve comments and empty lines
                    continue;
                }

                // Extract track prefix (e.g., "A ", "DCSG1 ")
                string trackPrefix = "";
                var matchTrack = Regex.Match(trimmed, @"^([A-HP0-9,]+)\s+");
                if (matchTrack.Success)
                {
                    trackPrefix = matchTrack.Groups[1].Value + " ";
                    trimmed = trimmed.Substring(matchTrack.Length);
                }
                else
                {
                    // For single character channel like 'A' without space initially
                    var matchTrack2 = Regex.Match(trimmed, @"^([A-HP])(?=([^a-z]|$))");
                    if (matchTrack2.Success)
                    {
                        trackPrefix = matchTrack2.Value + " ";
                        trimmed = trimmed.Substring(matchTrack2.Length).TrimStart();
                    }
                }

                sb.Append(FormatLine(trimmed, trackPrefix, lineLengthInWholeNotes, upbeatDurationInWholeNotes, insertSpace));
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd('\r', '\n');
        }

        private static string FormatLine(string mml, string trackPrefix, double wrapLength, double upbeatDurationInWholeNotes, bool insertSpace)
        {
            var sb = new StringBuilder();
            
            // Handle upbeat: If there is an upbeat offset, we start currentTime not at 0,
            // but at a position that will cause the first wrap to happen earlier.
            double currentTime = 0;
            if (upbeatDurationInWholeNotes > 0)
            {
                currentTime = wrapLength - upbeatDurationInWholeNotes;
            }

            double defaultLength = 1.0 / 4.0; // Default to quarter note (l4)
            
            // Tokenizer regex targeting meaningful MML atoms
            var regex = new Regex(@"(@[a-zA-Z]+\d*(?:,\d+)?(?:x\d+)?)|([a-gA-G][#\+\-]?\d*\.*)|([rR]\d*\.*)|([lL]\d*\.*)|(\^\d*\.*)|([oO]\d+|[<>])|([vV]\d+)|([tT]\d+)|([qQ]\d+|[kK]\-?\d+|[dD]\-?\d+|EP\d+|@EP\d+)|(\[|\]\d*)|(L)|(\{|\})|([^a-zA-Z0-9\s]+)|([a-zA-Z0-9]+)");
            var matches = regex.Matches(mml);

            sb.Append(trackPrefix);
            
            bool isFirstTokenOnLine = true;
            bool spaceBeforeNext = false;

            foreach (Match m in matches)
            {
                var token = m.Value;
                if (string.IsNullOrWhiteSpace(token)) continue;

                if (currentTime >= wrapLength - 0.0001 && wrapLength > 0)
                {
                    currentTime -= wrapLength;
                    sb.AppendLine();
                    sb.Append(trackPrefix);
                    isFirstTokenOnLine = true;
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
                else if (firstChar == 'l')
                {
                    // Update default length but it does not consume time right now
                    defaultLength = ParseDuration(token, defaultLength);
                }

                currentTime += durationToAdd;
            }
            
            return sb.ToString();
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
