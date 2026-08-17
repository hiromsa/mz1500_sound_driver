import re

with open('c:/tools/mz1500_sound_driver/Mz1500SoundPlayer/Sound/MmlParser.cs', 'r', encoding='utf-8') as f:
    content = f.read()

content = content.replace(
    'events.Add(new NoteEvent(freq, durationMs, vol, gateTimeMs));',
    'events.Add(new NoteEvent(freq, durationMs, vol, gateTimeMs, NoteNumber: _octave * 12 + noteIndex));'
)

# wait, noteIndex is local to GetFrequency. I should change ParseNote.
new_parse_note = '''
    private void ParseNote(char noteChar, string mml, ref int i, List<NoteEvent> events)
    {
        int semiToneOffset = 0;
        if (i < mml.Length && (mml[i] == '+' || mml[i] == '#'))
        {
            semiToneOffset = 1;
            i++;
        }
        else if (i < mml.Length && mml[i] == '-')
        {
            semiToneOffset = -1;
            i++;
        }

        int length = _defaultLength;
        int dots = _defaultDots;
        if (i < mml.Length && char.IsDigit(mml[i]))
        {
            length = ReadInt(mml, ref i, _defaultLength);
            dots = 0;
        }

        while (i < mml.Length && mml[i] == '.')
        {
            dots++;
            i++;
        }

        double quarterNoteMs = 60000.0 / _tempo;
        double durationMs = (quarterNoteMs * 4.0) / length;
        if (dots > 0)
        {
            double add = durationMs / 2.0;
            for (int d = 0; d < dots; d++) { durationMs += add; add /= 2.0; }
        }

        double gateTimeMs = durationMs * (_quantize / 8.0);
        
        if (noteChar == 'r')
        {
            events.Add(new NoteEvent(0, durationMs, 0, 0, NoteNumber: 0));
        }
        else
        {
            int noteIndex = noteChar switch
            {
                'c' => 0, 'd' => 2, 'e' => 4, 'f' => 5, 'g' => 7, 'a' => 9, 'b' => 11,
                _ => 0
            };
            noteIndex += semiToneOffset;
            int noteNum = _octave * 12 + noteIndex;

            double freq = GetFrequency(noteChar, semiToneOffset, _octave);
            double vol = _volume / 15.0 * 0.2; 
            events.Add(new NoteEvent(freq, durationMs, vol, gateTimeMs, NoteNumber: noteNum));
        }
    }
'''

content = re.sub(r'private void ParseNote\(char noteChar, string mml, ref int i, List<NoteEvent> events\)[\s\S]*?\}[\s]*private int ReadInt', new_parse_note.strip() + r'\n\n    private int ReadInt', content)

with open('c:/tools/mz1500_sound_driver/Mz1500SoundPlayer/Sound/MmlParser.cs', 'w', encoding='utf-8') as f:
    f.write(content)
