import sys
import re

file_path = r'c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\Sound\MmlToZ80Compiler.cs'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Add constants
consts = '''    public const byte CMD_WAIT = 0x20;
    public const byte CMD_YM2151_REG_WRITE = 0x21;'''

if 'CMD_WAIT = 0x20' not in content:
    content = content.replace('public const byte CMD_END  = 0xFF;', 'public const byte CMD_END  = 0xFF;\n' + consts)

# Add CompileFmTrack
compile_fm_code = '''
    public byte[] CompileFmTrack(List<NoteEvent> events, byte fmChannel, MmlData mmlData)
    {
        var output = new List<byte>();
        
        double currentTimeMs = 0;
        int currentFrame = 0;
        int currentVoiceId = -1;
        int currentPan = -1;
        
        Action<byte, byte> emitReg = (reg, val) => 
        {
            output.Add(CMD_YM2151_REG_WRITE);
            output.Add(reg);
            output.Add(val);
        };
        
        Action<int> emitWait = (frames) =>
        {
            while (frames > 0)
            {
                int waitFrames = Math.Min(frames, 65535);
                ushort fUnits = (ushort)(waitFrames - 1);
                output.Add(CMD_WAIT);
                output.Add((byte)(fUnits & 0xFF));
                output.Add((byte)((fUnits >> 8) & 0xFF));
                frames -= waitFrames;
            }
        };
        
        foreach (var ev in events)
        {
            if (ev.IsLoopPoint)
            {
                output.Add(CMD_LOOP_MARKER);
            }
            
            if (ev.RegisterWrites != null)
            {
                foreach (var rw in ev.RegisterWrites)
                {
                    emitReg((byte)rw.Register, (byte)rw.Value);
                }
            }
            
            if (ev.VoiceId >= 0 && ev.VoiceId != currentVoiceId && mmlData.FmVoiceEnvelopes.TryGetValue(ev.VoiceId, out var toneData))
            {
                currentVoiceId = ev.VoiceId;
                int[] p = toneData.Parameters;
                if (p.Length >= 38)
                {
                    byte panFlCon = (byte)(((ev.Pan & 3) << 6) | ((p[1] & 7) << 3) | (p[0] & 7));
                    emitReg((byte)(0x20 + fmChannel), panFlCon);
                    
                    for (int op = 0; op < 4; op++)
                    {
                        int opOffset = op * 8;
                        int pd = 2 + (op * 9); 
                        emitReg((byte)(0x40 + opOffset + fmChannel), (byte)(((p[pd+8]&7)<<4) | (p[pd+7]&15)));
                        emitReg((byte)(0x60 + opOffset + fmChannel), (byte)(p[pd+5] & 127));
                        emitReg((byte)(0x80 + opOffset + fmChannel), (byte)(((p[pd+6]&3)<<6) | (p[pd+0]&31)));
                        emitReg((byte)(0xA0 + opOffset + fmChannel), (byte)(((p[pd+10]&1)<<7) | (p[pd+1]&31)));
                        emitReg((byte)(0xC0 + opOffset + fmChannel), (byte)(((p[pd+9]&3)<<6) | (p[pd+2]&31)));
                        emitReg((byte)(0xE0 + opOffset + fmChannel), (byte)(((p[pd+4]&15)<<4) | (p[pd+3]&15)));
                    }
                }
            }
            
            if (ev.Pan != currentPan)
            {
                currentPan = ev.Pan;
                // Simplified pan update without caching AL/FB
                // emitReg((byte)(0x20 + fmChannel), ...);
            }

            double nextTimeMs = currentTimeMs + ev.DurationMs;
            int nextFrame = (int)Math.Round(nextTimeMs * 60.0 / 1000.0);
            int totalFrames = nextFrame - currentFrame;
            if (totalFrames < 1) totalFrames = 1;

            double gateEndTimeMs = currentTimeMs + ev.GateTimeMs;
            int gateEndFrame = (int)Math.Round(gateEndTimeMs * 60.0 / 1000.0);
            int gateFrames = gateEndFrame - currentFrame;
            if (gateFrames > totalFrames) gateFrames = totalFrames;
            if (gateFrames < 1 && ev.Frequency > 0) gateFrames = 1;
            
            if (ev.Frequency > 0 && gateFrames > 0)
            {
                Ym2151Helper.GetKcKf(ev.Frequency, out byte kc, out byte kf);
                emitReg((byte)(0x28 + fmChannel), kc);
                emitReg((byte)(0x30 + fmChannel), kf);
                
                emitReg(0x08, (byte)(0x78 | fmChannel));
                emitWait(gateFrames);
                emitReg(0x08, (byte)(0x00 | fmChannel));
                emitWait(totalFrames - gateFrames);
            }
            else
            {
                emitWait(totalFrames);
            }

            currentTimeMs = nextTimeMs;
            currentFrame = nextFrame;
        }
        
        output.Add(CMD_END);
        return output.ToArray();
    }
'''

if 'CompileFmTrack' not in content:
    content = content.replace('return output.ToArray();\n    }', 'return output.ToArray();\n    }\n' + compile_fm_code)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)
print("Done modifying MmlToZ80Compiler")