using System;
using System.Collections.Generic;
using System.Text;

namespace Mz1500SoundPlayer.Sound;

public static class BytecodeDumper
{
    public static string Dump(byte[] data)
    {
        var sb = new StringBuilder();
        int i = 0;
        
        while (i < data.Length)
        {
            sb.Append($"{i:X4} : ");
            byte cmd = data[i++];

            if (cmd <= 0x5F)
            {
                sb.AppendLine($"NoteON   (Note={cmd})");
            }
            else if (cmd == (byte)Z80SequenceCommand.Rest)
            {
                sb.AppendLine("Rest");
            }
            else if (cmd >= (byte)Z80SequenceCommand.ShortLengthBase && cmd <= 0x8F)
            {
                int len = (cmd - (byte)Z80SequenceCommand.ShortLengthBase) + 1;
                sb.AppendLine($"ShortLen (Len={len})");
            }
            else if (cmd == (byte)Z80SequenceCommand.LongLength)
            {
                if (i + 1 < data.Length)
                {
                    int len = data[i] | (data[i + 1] << 8);
                    sb.AppendLine($"LongLen  (Len={len})");
                    i += 2;
                }
                else
                {
                    sb.AppendLine("LongLen  (Truncated)");
                }
            }
            else if (cmd == (byte)Z80SequenceCommand.SetVoice)
            {
                if (i < data.Length)
                {
                    sb.AppendLine($"SetVoice (Voice={data[i]})");
                    i++;
                }
                else
                {
                    sb.AppendLine("SetVoice (Truncated)");
                }
            }
            else if (cmd == (byte)Z80SequenceCommand.SetVolume)
            {
                if (i < data.Length)
                {
                    sb.AppendLine($"SetVol   (Vol={data[i]})");
                    i++;
                }
                else
                {
                    sb.AppendLine("SetVol   (Truncated)");
                }
            }
            else if (cmd == (byte)Z80SequenceCommand.LoopMarker)
            {
                sb.AppendLine("LoopMarker");
            }
            else if (cmd == (byte)Z80SequenceCommand.TrackEnd)
            {
                sb.AppendLine("TrackEnd");
                break;
            }
            else
            {
                sb.AppendLine($"Unknown  ({cmd:X2})");
            }
        }

        return sb.ToString();
    }
}
