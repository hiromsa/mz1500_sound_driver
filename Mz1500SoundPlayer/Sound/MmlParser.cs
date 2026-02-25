using System;
using System.Collections.Generic;

namespace Mz1500SoundPlayer.Sound;

public record NoteEvent(double Frequency, double DurationMs, double Volume, double GateTimeMs, int EnvelopeId = -1, int PitchEnvelopeId = -1, int NoiseWaveMode = 1, int IntegrateNoiseMode = 0, bool IsLoopPoint = false, int TextStartIndex = -1, int TextLength = 0);

public class MmlParser
{
    private int _tempo = 120;
    private int _defaultLength = 4;
    private int _octave = 4;
    private int _volume = 15; // 0-15
    private int _quantize = 8; // 1-8

    public List<NoteEvent> Parse(string mml)
    {
        var events = new List<NoteEvent>();
        mml = mml.ToLowerInvariant().Replace(" ", "").Replace("\n", "").Replace("\r", "");
        
        int i = 0;
        while (i < mml.Length)
        {
            char c = mml[i++];
            switch (c)
            {
                case 't': _tempo = ReadInt(mml, ref i, 120); break;
                case 'o': _octave = ReadInt(mml, ref i, 4); break;
                case 'l': _defaultLength = ReadInt(mml, ref i, 4); break;
                case 'v': _volume = ReadInt(mml, ref i, 15); break;
                case 'q': _quantize = ReadInt(mml, ref i, 8); break;
                case '>': _octave++; break;
                case '<': _octave--; break;
                case 'a': case 'b': case 'c': case 'd': case 'e': case 'f': case 'g':
                case 'r':
                    ParseNote(c, mml, ref i, events);
                    break;
            }
        }
        return events;
    }

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
        if (i < mml.Length && char.IsDigit(mml[i]))
        {
            length = ReadInt(mml, ref i, _defaultLength);
        }

        bool dotted = false;
        if (i < mml.Length && mml[i] == '.')
        {
            dotted = true;
            i++;
        }

        // Calculate duration in ms
        // tempo = quarter notes per minute. 1 quarter note = 60000 / tempo ms
        double quarterNoteMs = 60000.0 / _tempo;
        double durationMs = (quarterNoteMs * 4.0) / length;
        if (dotted) durationMs *= 1.5;

        double gateTimeMs = durationMs * (_quantize / 8.0);
        
        if (noteChar == 'r')
        {
            events.Add(new NoteEvent(0, durationMs, 0, 0));
        }
        else
        {
            double freq = GetFrequency(noteChar, semiToneOffset, _octave);
            double vol = _volume / 15.0 * 0.2; // 0.2 is max volume to avoid clipping
            events.Add(new NoteEvent(freq, durationMs, vol, gateTimeMs));
        }
    }

    private int ReadInt(string mml, ref int i, int defaultValue)
    {
        int start = i;
        while (i < mml.Length && char.IsDigit(mml[i]))
        {
            i++;
        }
        if (start < i && int.TryParse(mml.Substring(start, i - start), out int val))
        {
            return val;
        }
        return defaultValue;
    }

    private double GetFrequency(char note, int semiToneOffset, int octave)
    {
        int noteIndex = note switch
        {
            'c' => 0, 'd' => 2, 'e' => 4, 'f' => 5, 'g' => 7, 'a' => 9, 'b' => 11,
            _ => 0
        };
        noteIndex += semiToneOffset;

        // A4 = 440Hz -> (Octave 4, 'a' which is index 9)
        int semitonesFromA4 = noteIndex - 9 + (octave - 4) * 12;
        return 440.0 * Math.Pow(2.0, semitonesFromA4 / 12.0);
    }
}
