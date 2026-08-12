file_path = r'c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\Sound\TrackEventExpander.cs'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Add fields
fields_find = '''        int currentTranspose = 0; // Transpose (semitones)

        // ループ処理用スタック等 (今回は簡易的にフラット展開する)'''
fields_repl = '''        int currentTranspose = 0; // Transpose (semitones)
        
        int currentFmVoiceId = 0;
        int currentFmPan = 3;
        int currentFmVolume = 127;
        var currentRegisterWrites = new List<Ym2151RegisterCommand>();

        // ループ処理用スタック等 (今回は簡易的にフラット展開する)'''
content = content.replace(fields_find, fields_repl)

# Add parsing
parse_find = '''            else if (cmd is TransposeCommand tr) { currentTranspose = tr.Transpose; }'''
parse_repl = '''            else if (cmd is TransposeCommand tr) { currentTranspose = tr.Transpose; }
            else if (cmd is VoiceCommand vcmd) { currentFmVoiceId = vcmd.VoiceId; }
            else if (cmd is PanCommand pcmd) { currentFmPan = pcmd.Pan; }
            else if (cmd is FmVolumeCommand fvcmd) { currentFmVolume = fvcmd.Volume; }
            else if (cmd is Ym2151RegisterCommand ycmd) { currentRegisterWrites.Add(ycmd); }'''
content = content.replace(parse_find, parse_repl)

# Inner parsing (inside tuplet)
inner_parse_find = '''                        else if (inner is TransposeCommand trci) { currentTranspose = trci.Transpose; }'''
inner_parse_repl = '''                        else if (inner is TransposeCommand trci) { currentTranspose = trci.Transpose; }
                        else if (inner is VoiceCommand vcmd2) { currentFmVoiceId = vcmd2.VoiceId; }
                        else if (inner is PanCommand pcmd2) { currentFmPan = pcmd2.Pan; }
                        else if (inner is FmVolumeCommand fvcmd2) { currentFmVolume = fvcmd2.Volume; }
                        else if (inner is Ym2151RegisterCommand ycmd2) { currentRegisterWrites.Add(ycmd2); }'''
content = content.replace(inner_parse_find, inner_parse_repl)

# Update NoteEvent creation to pass these fields.
import re

# We need to replace all events.Add(new NoteEvent(...))
# Fortunately NoteEvent constructor arguments map exactly.
# NoteEvent(freq, durationMs, vol, gateMs, currentEnvelopeId, currentPitchEnvelopeId, currentNoiseWaveMode, currentIntegrateNoiseMode, nextIsLoopPoint, textStart, textLen, currentDetune, currentSweep)
# To: NoteEvent(..., VoiceId: currentFmVoiceId, Pan: currentFmPan, FmVolume: currentFmVolume, RegisterWrites: writes)

# Instead of complex regex, let's just do text replacements for the specific NoteEvent instantiations.

def replace_note_event(match):
    # match.group(0) is the entire events.Add(new NoteEvent(...));
    inner = match.group(1) # inside NoteEvent(
    return f'''var writes = currentRegisterWrites.Count > 0 ? new List<Ym2151RegisterCommand>(currentRegisterWrites) : null;
                                currentRegisterWrites.Clear();
                                events.Add(new NoteEvent({inner}, VoiceId: currentFmVoiceId, Pan: currentFmPan, FmVolume: currentFmVolume, RegisterWrites: writes));'''

content = re.sub(r'events\.Add\(new NoteEvent\((.*?)\)\);', replace_note_event, content)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)