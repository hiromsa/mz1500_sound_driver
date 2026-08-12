file_path = r'c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\Sound\MmlToZ80Compiler.cs'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

find = '''                if (p.Length >= 38)
                {
                    byte panFlCon = (byte)(((ev.Pan & 3) << 6) | ((p[1] & 7) << 3) | (p[0] & 7));
                    emitReg((byte)(0x20 + fmChannel), panFlCon);
                    
                    for (int op = 0; op < 4; op++)
                    {
                        int opOffset = op * 8;
                        int pd = 2 + (op * 9);'''

repl = '''                if (p.Length >= 46)
                {
                    byte panFlCon = (byte)(((ev.Pan & 3) << 6) | ((p[1] & 7) << 3) | (p[0] & 7));
                    emitReg((byte)(0x20 + fmChannel), panFlCon);
                    
                    for (int op = 0; op < 4; op++)
                    {
                        int opOffset = op * 8;
                        int pd = 2 + (op * 11);'''

content = content.replace(find, repl)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)