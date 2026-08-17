with open('Mz1500SoundPlayer/Sound/Z80SequenceCompiler.cs', 'r', encoding='utf-8') as f:
    code = f.read()

code = code.replace('public const byte CMD_NOISE= 0x06;', 'public const byte CMD_NOISE = 0xA6;')
code = code.replace('public const byte CMD_SYNC_NOISE = 0x07;', 'public const byte CMD_SYNC_NOISE = 0xA7;')

with open('Mz1500SoundPlayer/Sound/Z80SequenceCompiler.cs', 'w', encoding='utf-8') as f:
    f.write(code)

with open('Mz1500SoundPlayer/Sound/Z80/Z80DriverGenerator.cs', 'r', encoding='utf-8') as f:
    code = f.read()

code = code.replace('asm.CP(asm.Value((byte)0x06));', 'asm.CP(asm.Value((byte)0xA6));')
code = code.replace('asm.CP(asm.Value((byte)0x07));', 'asm.CP(asm.Value((byte)0xA7));')
code = code.replace('asm.LD(asm.A, asm.B);\n        asm.ADD(asm.A, asm.A); // A = Note * 2', 'asm.ADD(asm.A, asm.A); // A = Note * 2')

with open('Mz1500SoundPlayer/Sound/Z80/Z80DriverGenerator.cs', 'w', encoding='utf-8') as f:
    f.write(code)
