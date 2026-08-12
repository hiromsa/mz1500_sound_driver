import sys
import re

file_path = r'c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\Sound\MmlPlayerModel.cs'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Update PlayAsync compilation
play_find = '''            bool isBeep = kvp.Key.ToUpperInvariant() == "P";
            byte[] seqBin = compiler.CompileTrack(events, psgChannel, isBeep);'''

play_repl = '''            bool isBeep = kvp.Key.ToUpperInvariant() == "P";
            bool isFm = kvp.Key.ToUpperInvariant().StartsWith("F") && kvp.Key.Length == 2;
            byte[] seqBin;
            if (isFm)
            {
                byte fmChannel = (byte)(int.Parse(kvp.Key.Substring(1)) - 1);
                seqBin = compiler.CompileFmTrack(events, fmChannel, mmlData);
            }
            else
            {
                seqBin = compiler.CompileTrack(events, psgChannel, isBeep);
            }'''
content = content.replace(play_find, play_repl)

# Update ExportQdc compilation
export_find = '''            bool isBeep = kvp.Key.ToUpperInvariant() == "P";
            byte[] seqBin = compiler.CompileTrack(events, psgChannel, isBeep);
            
            musicAssembler.AppendChannel(new Z80.Channel("track_" + kvp.Key, ioPort, seqBin));'''

export_repl = '''            bool isBeep = kvp.Key.ToUpperInvariant() == "P";
            bool isFm = kvp.Key.ToUpperInvariant().StartsWith("F") && kvp.Key.Length == 2;
            byte[] seqBin;
            if (isFm)
            {
                byte fmChannel = (byte)(int.Parse(kvp.Key.Substring(1)) - 1);
                seqBin = compiler.CompileFmTrack(events, fmChannel, mmlData);
                musicAssembler.AppendChannel(new Z80.Channel("track_" + kvp.Key, 0x08, seqBin)); // 0x08 is placeholder for FM
            }
            else
            {
                seqBin = compiler.CompileTrack(events, psgChannel, isBeep);
                musicAssembler.AppendChannel(new Z80.Channel("track_" + kvp.Key, ioPort, seqBin));
            }'''
content = content.replace(export_find, export_repl)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)

file_path_sp = r'c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\Sound\MultiTrackSequenceProvider.cs'
with open(file_path_sp, 'r', encoding='utf-8') as f:
    content_sp = f.read()

init_find = '''                bool isBeep = kvp.Key.ToUpperInvariant() == "P";
                Console.WriteLine($"[MultiTrackSequenceProvider] Track {kvp.Key} has {kvp.Value.Length} bytes.");
                _trackProviders.Add((kvp.Key.ToUpperInvariant(), new MmlSequenceProvider(kvp.Value, envelopes, hwPitchEnvelopes, sampleRate, isBeep)));'''

init_repl = '''                bool isBeep = kvp.Key.ToUpperInvariant() == "P";
                bool isFm = kvp.Key.ToUpperInvariant().StartsWith("F") && kvp.Key.Length == 2;
                Console.WriteLine($"[MultiTrackSequenceProvider] Track {kvp.Key} has {kvp.Value.Length} bytes.");
                
                if (isFm)
                {
                    _trackProviders.Add((kvp.Key.ToUpperInvariant(), new Ym2151SequenceProvider(kvp.Value, YM2151, sampleRate)));
                }
                else
                {
                    _trackProviders.Add((kvp.Key.ToUpperInvariant(), new MmlSequenceProvider(kvp.Value, envelopes, hwPitchEnvelopes, sampleRate, isBeep)));
                }'''
content_sp = content_sp.replace(init_find, init_repl)

# Also need to change _trackProviders type from MmlSequenceProvider to ISampleProvider
content_sp = content_sp.replace('List<(string TrackName, MmlSequenceProvider Provider)>', 'List<(string TrackName, ISampleProvider Provider)>')
# But wait! Ym2151SequenceProvider needs an IsMuted property! And MmlSequenceProvider has IsMuted.
# Let's check if ISampleProvider has IsMuted. It doesn't. We should use a common interface or dynamic.
# Or just reflect / cast. Let's cast:
# if (provider is MmlSequenceProvider m) m.IsMuted = ...
# if (provider is Ym2151SequenceProvider y) y.IsMuted = ...

read_find = '''            var provider = item.Provider;
            provider.IsMuted = !ActiveChannels.Contains(item.TrackName);

            // MmlSequenceProvider が YM2151 を制御できるようにする（後で Ym2151SequenceProvider へ移行）
            int read = provider.Read(_tempBuffer, 0, count);'''

read_repl = '''            var provider = item.Provider;
            bool isMuted = !ActiveChannels.Contains(item.TrackName);
            if (provider is MmlSequenceProvider m) m.IsMuted = isMuted;
            if (provider is Ym2151SequenceProvider y) y.IsMuted = isMuted;

            int read = provider.Read(_tempBuffer, 0, count);'''
content_sp = content_sp.replace(read_find, read_repl)

with open(file_path_sp, 'w', encoding='utf-8') as f:
    f.write(content_sp)

print("Modified both files")