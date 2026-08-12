import sys
import re

file_path = r'c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\Sound\MmlPlayerModel.cs'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Add a debug log for the first 20 bytes of F1 bytecode
find = '''        await PlayBytecodeDictAsync(trackBinaries, metronomeTimings, mmlData.VolumeEnvelopes, compiler.HwPitchEnvelopes, maxMs, hasInfiniteLoop);'''
repl = '''        if (trackBinaries.ContainsKey(""F1""))
        {
            var b = trackBinaries[""F1""];
            var bStr = string.Join("" "", System.Linq.Enumerable.Take(b, 30).Select(x => x.ToString(""X2"")));
            log.AppendLine($""[DEBUG] F1 Bytecode (first 30): {bStr}"");
        }
        await PlayBytecodeDictAsync(trackBinaries, metronomeTimings, mmlData.VolumeEnvelopes, compiler.HwPitchEnvelopes, maxMs, hasInfiniteLoop);'''

if ""[DEBUG] F1 Bytecode"" not in content:
    content = content.replace(find, repl)
    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)
