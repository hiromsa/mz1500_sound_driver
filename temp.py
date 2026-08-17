import re

with open('c:/tools/mz1500_sound_driver/Mz1500SoundPlayer/Sound/BytecodeDumper.cs', 'r', encoding='utf-8') as f:
    content = f.read()

new_dump_track = '''
    public static string DumpTrack(byte[] bytecode)
    {
        var sb = new StringBuilder();
        int offset = 0;
        int currentLength = -1;

        while (offset < bytecode.Length)
        {
            byte cmd = bytecode[offset];
            sb.Append($\"{offset:X4}: {cmd:X2} \");
            
            if (cmd >= (byte)Z80SequenceCommand.ToneBase && cmd <= (byte)Z80SequenceCommand.ToneBase + 95)
            {
                int note = cmd - (byte)Z80SequenceCommand.ToneBase;
                sb.AppendLine($\"TONE {note} (length: {currentLength})\");
                offset++;
            }
            else if (cmd >= (byte)Z80SequenceCommand.ShortLengthBase && cmd <= (byte)Z80SequenceCommand.ShortLengthBase + 15)
            {
                currentLength = (cmd - (byte)Z80SequenceCommand.ShortLengthBase) + 1;
                sb.AppendLine($\"SHORT_LENGTH {currentLength}\");
                offset++;
            }
            else
            {
                switch ((Z80SequenceCommand)cmd)
                {
                    case Z80SequenceCommand.LongLength:
                        if (offset + 2 < bytecode.Length)
                        {
                            currentLength = bytecode[offset + 1] | (bytecode[offset + 2] << 8);
                            sb.AppendLine($\"{bytecode[offset+1]:X2} {bytecode[offset+2]:X2} LONG_LENGTH {currentLength}\");
                            offset += 3;
                        }
                        else { sb.AppendLine(\"LONG_LENGTH (EOF)\"); offset++; }
                        break;
                    case Z80SequenceCommand.Rest:
                        sb.AppendLine($\"REST (length: {currentLength})\");
                        offset++;
                        break;
                    case Z80SequenceCommand.NoiseBase:
                        sb.AppendLine($\"NOISE (length: {currentLength})\");
                        offset++;
                        break;
                    case Z80SequenceCommand.SyncNoiseBase:
                        sb.AppendLine($\"SYNC_NOISE (length: {currentLength})\");
                        offset++;
                        break;
                    case Z80SequenceCommand.SetVolume:
                        if (offset + 1 < bytecode.Length)
                        {
                            sb.AppendLine($\"{bytecode[offset+1]:X2} VOL {bytecode[offset+1]}\");
                            offset += 2;
                        }
                        else { sb.AppendLine(\"VOL (EOF)\"); offset++; }
                        break;
                    case Z80SequenceCommand.SetVoice:
                        if (offset + 1 < bytecode.Length)
                        {
                            sb.AppendLine($\"{bytecode[offset+1]:X2} VOICE {bytecode[offset+1]}\");
                            offset += 2;
                        }
                        else { sb.AppendLine(\"VOICE (EOF)\"); offset++; }
                        break;
                    case Z80SequenceCommand.SetHardwareEnvelope:
                        if (offset + 2 < bytecode.Length)
                        {
                            sb.AppendLine($\"{bytecode[offset+1]:X2} {bytecode[offset+2]:X2} HENV {bytecode[offset+1]} {bytecode[offset+2]}\");
                            offset += 3;
                        }
                        else { sb.AppendLine(\"HENV (EOF)\"); offset++; }
                        break;
                    case Z80SequenceCommand.LoopMarker:
                        sb.AppendLine(\"LOOP_MARKER\");
                        offset++;
                        break;
                    case Z80SequenceCommand.TrackEnd:
                        sb.AppendLine(\"TRACK_END\");
                        offset++;
                        break;
                    default:
                        sb.AppendLine($\"UNKNOWN {cmd:X2}\");
                        offset++;
                        break;
                }
            }
        }
        return sb.ToString();
    }
'''

content = re.sub(r'public static string DumpTrack\(byte\[\] bytecode\)[\s\S]*?return sb\.ToString\(\);\s*\}', new_dump_track.strip(), content)

with open('c:/tools/mz1500_sound_driver/Mz1500SoundPlayer/Sound/BytecodeDumper.cs', 'w', encoding='utf-8') as f:
    f.write(content)
