file_path = r'c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\Sound\MmlToZ80Compiler.cs'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

find = '''                emitReg(0x08, (byte)(0x78 | fmChannel));
                emitWait(gateFrames);
                emitReg(0x08, (byte)(0x00 | fmChannel));
                emitWait(totalFrames - gateFrames);'''
repl = '''                emitReg(0x08, (byte)(0x78 | fmChannel));
                
                int actualGate = gateFrames;
                int restWait = totalFrames - gateFrames;
                
                // Hardware envelope needs at least 1 frame of KEYOFF to re-trigger properly
                if (restWait == 0)
                {
                    actualGate = Math.Max(1, gateFrames - 1);
                    restWait = totalFrames - actualGate;
                }

                emitWait(actualGate);
                emitReg(0x08, (byte)(0x00 | fmChannel));
                emitWait(restWait);'''

if "actualGate" not in content:
    content = content.replace(find, repl)
    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)