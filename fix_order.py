import re

with open('Mz1500SoundPlayer/Sound/Z80SequenceCompiler.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# Fix NoteON (Tones)
code = re.sub(r'(output\.Add\(\(byte\)noteNum\);\s*// 長さ出力\s*ushort durationUnits = \(ushort\)\(gateFrames - 1\);\s*emitLength\(durationUnits \+ 1\);)',
              r'// 長さ出力\n                    ushort durationUnits = (ushort)(gateFrames - 1);\n                    emitLength(durationUnits + 1);\n                    output.Add((byte)noteNum);',
              code)

# Fix Beep
code = re.sub(r'(output\.Add\(toneCmd2\);\s*// Beepは長さ出力\s*ushort durationUnitsBp = \(ushort\)\(gateFrames - 1\);\s*emitLength\(durationUnitsBp \+ 1\);)',
              r'// Beepは長さ出力\n                    ushort durationUnitsBp = (ushort)(gateFrames - 1);\n                    emitLength(durationUnitsBp + 1);\n                    output.Add(toneCmd2);',
              code)

# Fix Noise
code = re.sub(r'(output\.Add\(noiseCmd\);\s*// 長さ出力\s*ushort durationUnitsNoise = \(ushort\)\(gateFrames - 1\);\s*emitLength\(durationUnitsNoise \+ 1\);)',
              r'// 長さ出力\n                    ushort durationUnitsNoise = (ushort)(gateFrames - 1);\n                    emitLength(durationUnitsNoise + 1);\n                    output.Add(noiseCmd);',
              code)

# Fix Rest 1
code = re.sub(r'(output\.Add\(\(byte\)Z80SequenceCommand\.Rest\);\s*emitLength\(durationUnits \+ 1\);)',
              r'emitLength(durationUnits + 1);\n                    output.Add((byte)Z80SequenceCommand.Rest);',
              code)

# Fix Rest 2
code = re.sub(r'(output\.Add\(\(byte\)Z80SequenceCommand\.Rest\);\s*emitLength\(restUnits \+ 1\);)',
              r'emitLength(restUnits + 1);\n                        output.Add((byte)Z80SequenceCommand.Rest);',
              code)

with open('Mz1500SoundPlayer/Sound/Z80SequenceCompiler.cs', 'w', encoding='utf-8') as f:
    f.write(code)
